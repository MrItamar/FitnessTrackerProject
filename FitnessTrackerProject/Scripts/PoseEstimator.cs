using FitnessTrackerProject.Scripts;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>
    /// Everything about the AI model: takes a video frame + a box, returns the body keypoints.
    /// Steps: crop a square -> resize to 256x256 -> run model -> convert back to frame pixels -> smooth.
    /// </summary>
    public class PoseEstimator
    {
        private const int InputSize = 256;               // the model wants 256x256 images
        private const int RotationProbeFrames = 8;       // first 8 frames choose the rotation

        // --- smoothing ---
        private const float LowConfidence = 0.3f;        // below this visibility we keep the old position
        private const float StillSmoothing = 0.15f;      // smoothing for a joint that barely moves
        private const float SpeedGain = 8.0f;            // faster joint -> less smoothing (less lag)
        private const float MaxSmoothing = 0.85f;
        private const float VisibilitySmoothing = 0.3f;

        private enum Rotation { None, Ccw, Cw }

        private class RawPose
        {
            public List<Keypoint> Keypoints;
            public int CropSide;        // size of the square that was cropped (frame pixels)
            public float Confidence;    // the model's own "a person is here" score, -1 if it has none
            public float Score;         // upright visibility score used to pick rotation
        }

        private readonly InferenceSession _session;
        private readonly float[] _inputBuffer = new float[InputSize * InputSize * 3];
        private List<Keypoint> _previous;                // keypoints from the last frame

        private Rotation _rotation = Rotation.None;
        private bool _probing;
        private int _probeFrames;
        private double _scoreNone, _scoreCcw, _scoreCw;

        public float Confidence { get; private set; } = -1f;

        /// <summary>True during the first 8 frames while choosing rotation.</summary>
        public bool IsProbing { get { return _probing; } }

        /// <summary>True if the chosen rotation is horizontal (Ccw or Cw).</summary>
        public bool IsHorizontal { get { return _rotation != Rotation.None; } }

        public PoseEstimator(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        /// <summary>Call before every new video.</summary>
        public void StartVideo(bool plankMode = false)
        {
            _previous = null;
            _probeFrames = 0;
            _scoreNone = 0;
            _scoreCcw = 0;
            _scoreCw = 0;

            // Probe the first 8 frames on the full frame to auto-detect upright vs horizontal
            _probing = true;
            _rotation = plankMode ? Rotation.Ccw : Rotation.None;
        }

        /// <summary>Forget the last frame's keypoints (used when the tracker starts searching again).</summary>
        public void ResetSmoothing()
        {
            _previous = null;
        }

        /// <summary>Main method: frame + box in, smoothed keypoints (in frame pixels) out.</summary>
        public List<Keypoint> Estimate(Mat frame, BoundingBox box)
        {
            RawPose pose;

            if (_probing)
            {
                RawPose none = Run(frame, box, Rotation.None);
                RawPose ccw = Run(frame, box, Rotation.Ccw);
                RawPose cw = Run(frame, box, Rotation.Cw);

                if (none != null) _scoreNone += none.Score;
                if (ccw != null) _scoreCcw += ccw.Score;
                if (cw != null) _scoreCw += cw.Score;

                pose = none;
                if (ccw != null && (pose == null || ccw.Score > pose.Score)) pose = ccw;
                if (cw != null && (pose == null || cw.Score > pose.Score)) pose = cw;

                _probeFrames++;
                if (_probeFrames >= RotationProbeFrames)
                {
                    if (_scoreNone >= _scoreCcw && _scoreNone >= _scoreCw)
                        _rotation = Rotation.None;
                    else if (_scoreCcw >= _scoreCw)
                        _rotation = Rotation.Ccw;
                    else
                        _rotation = Rotation.Cw;

                    _probing = false;
                    _previous = null; // start smoothing fresh once rotation is locked
                }
            }
            else
            {
                pose = Run(frame, box, _rotation);
            }

            if (pose == null)
            {
                Confidence = -1f;
                return _previous != null ? new List<Keypoint>(_previous) : new List<Keypoint>();
            }

            Confidence = pose.Confidence;
            return Smooth(pose.Keypoints, pose.CropSide);
        }

        private RawPose Run(Mat frame, BoundingBox box, Rotation rotation)
        {
            int cropX, cropY, side;
            using (Mat input = PrepareInput(frame, box, rotation, out cropX, out cropY, out side))
            {
                if (input == null) return null;
                return RunModel(input, cropX, cropY, side, rotation);
            }
        }

        private Mat PrepareInput(Mat frame, BoundingBox box, Rotation rotation,
                                 out int cropX, out int cropY, out int side)
        {
            side = Math.Max(2, (int)Math.Round(Math.Max(box.Width, box.Height)));
            cropX = (int)Math.Round(box.X + box.Width / 2f - side / 2f);
            cropY = (int)Math.Round(box.Y + box.Height / 2f - side / 2f);

            int left = Math.Max(0, cropX);
            int top = Math.Max(0, cropY);
            int right = Math.Min(frame.Cols, cropX + side);
            int bottom = Math.Min(frame.Rows, cropY + side);

            if (right - left <= 0 || bottom - top <= 0) return null;

            using (var inside = new Mat(frame, new Rect(left, top, right - left, bottom - top)))
            using (var square = new Mat())
            using (var resized = new Mat())
            using (var rgb = new Mat())
            {
                Cv2.CopyMakeBorder(inside, square,
                    top - cropY,
                    (cropY + side) - bottom,
                    left - cropX,
                    (cropX + side) - right,
                    BorderTypes.Constant, Scalar.Black);

                Cv2.Resize(square, resized, new Size(InputSize, InputSize));
                Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

                var result = new Mat();
                switch (rotation)
                {
                    case Rotation.Ccw: Cv2.Rotate(rgb, result, RotateFlags.Rotate90Counterclockwise); break;
                    case Rotation.Cw: Cv2.Rotate(rgb, result, RotateFlags.Rotate90Clockwise); break;
                    default: rgb.CopyTo(result); break;
                }
                return result;
            }
        }

        private RawPose RunModel(Mat input, int cropX, int cropY, int side, Rotation rotation)
        {
            using (var floatImage = new Mat())
            {
                input.ConvertTo(floatImage, MatType.CV_32FC3, 1.0 / 255.0);
                Marshal.Copy(floatImage.Data, _inputBuffer, 0, _inputBuffer.Length);
            }
            var tensor = new DenseTensor<float>(_inputBuffer, new[] { 1, InputSize, InputSize, 3 });

            string inputName = _session.InputMetadata.Keys.First();
            string outputName = _session.OutputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

            using (var results = _session.Run(inputs))
            {
                float[] values = results.First(r => r.Name == outputName).AsTensor<float>().ToArray();
                float confidence = FindConfidence(results, outputName);

                // Check if the body is upright in this rotation's 256x256 input (shoulders above hips)
                bool isUprightInModel = false;
                if (values.Length > Landmark.RightHip * 5 + 1)
                {
                    float rawShoulderX = (values[Landmark.LeftShoulder * 5] + values[Landmark.RightShoulder * 5]) * 0.5f;
                    float rawShoulderY = (values[Landmark.LeftShoulder * 5 + 1] + values[Landmark.RightShoulder * 5 + 1]) * 0.5f;
                    float rawHipX = (values[Landmark.LeftHip * 5] + values[Landmark.RightHip * 5]) * 0.5f;
                    float rawHipY = (values[Landmark.LeftHip * 5 + 1] + values[Landmark.RightHip * 5 + 1]) * 0.5f;

                    float rawDx = Math.Abs(rawHipX - rawShoulderX);
                    float rawDy = rawHipY - rawShoulderY; // positive when shoulders are above hips
                    isUprightInModel = rawDy > rawDx && rawDy > 15f;
                }

                var keypoints = new List<Keypoint>();
                float scale = side / (float)InputSize;

                for (int i = 0; i + 4 < values.Length; i += 5)
                {
                    float x = values[i];
                    float y = values[i + 1];
                    float visibility = 1f / (1f + (float)Math.Exp(-values[i + 3]));

                    if (rotation == Rotation.Ccw)
                    {
                        float newX = InputSize - y;
                        float newY = x;
                        x = newX;
                        y = newY;
                    }
                    else if (rotation == Rotation.Cw)
                    {
                        float newX = y;
                        float newY = InputSize - x;
                        x = newX;
                        y = newY;
                    }

                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
                    {
                        x = 0; y = 0; visibility = 0;
                    }

                    keypoints.Add(new Keypoint(cropX + x * scale, cropY + y * scale, visibility));
                }

                int n = Math.Min(keypoints.Count, Landmark.BodyCount);
                float avgVis = n > 0 ? keypoints.Take(n).Average(k => k.Visibility) : 0f;

                // Only the rotation where the person is upright in the model gets full score
                float score = isUprightInModel ? avgVis : avgVis * 0.1f;

                return new RawPose { Keypoints = keypoints, CropSide = side, Confidence = confidence, Score = score };
            }
        }

        private static float FindConfidence(IEnumerable<DisposableNamedOnnxValue> results, string landmarkOutputName)
        {
            foreach (var output in results)
            {
                if (output.Name == landmarkOutputName) continue;
                try
                {
                    Tensor<float> t = output.AsTensor<float>();
                    if (t.Length == 1)
                    {
                        float value = t.GetValue(0);
                        if (value < 0f || value > 1f) value = 1f / (1f + (float)Math.Exp(-value));
                        return value;
                    }
                }
                catch
                {
                }
            }
            return -1f;
        }

        private List<Keypoint> Smooth(List<Keypoint> current, int cropSide)
        {
            if (_previous == null || _previous.Count != current.Count)
            {
                _previous = current;
                return current;
            }

            var smoothed = new List<Keypoint>();
            float side = Math.Max(1, cropSide);

            for (int i = 0; i < current.Count; i++)
            {
                Keypoint now = current[i];
                Keypoint before = _previous[i];

                if (now.Visibility < LowConfidence)
                {
                    smoothed.Add(new Keypoint(before.X, before.Y, before.Visibility * 0.9f));
                    continue;
                }

                float dx = now.X - before.X;
                float dy = now.Y - before.Y;
                float speed = (float)Math.Sqrt(dx * dx + dy * dy) / side;

                float alpha = Math.Min(MaxSmoothing, StillSmoothing + SpeedGain * speed);
                if (now.Visibility <= 0.5f) alpha *= 0.5f;

                smoothed.Add(new Keypoint(
                    before.X + alpha * dx,
                    before.Y + alpha * dy,
                    before.Visibility + VisibilitySmoothing * (now.Visibility - before.Visibility)));
            }

            _previous = smoothed;
            return smoothed;
        }
    }
}