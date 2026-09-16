using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FitnessTrackerProject
{
    public partial class MainWindow : System.Windows.Window
    {
        public InferenceSession _session;
        string modelPath = "pose_estimation_mediapipe_2023mar.onnx";

        private readonly float SmoothingFactor = 0.15f;

        private int _squatCount = 0;
        private string _squatState = "UP";

        public class BoundingBox
        {
            public float X, Y, Width, Height;
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadModel();
        }

        public void LoadModel()
        {
            try
            {
                _session = new InferenceSession(modelPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Model did not load correctly: {ex.Message}");
            }
        }

        private void SelectVideoButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov|All Files|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                ProcessVideo(openFileDialog.FileName);
            }
        }
        private int _plankSeconds = 0;
        private bool _isPlankActive = false;
        private bool IsPlankPosition(List<(float X, float Y, float Visibility)> keypoints)
        {
            // Check if key joints for shoulder (11/12), hip (23/24), and ankle (27/28) are visible
            if (keypoints.Count > 28 &&
                keypoints[11].Visibility > 0.35f &&
                keypoints[23].Visibility > 0.35f &&
                keypoints[27].Visibility > 0.35f)
            {
                // In a horizontal plank, the Y coordinates of the shoulder and hip should be very close 
                // (unlike standing upright where the shoulder Y is much smaller than the hip Y).
                float shoulderY = (keypoints[11].Y + keypoints[12].Y) / 2.0f;
                float hipY = (keypoints[23].Y + keypoints[24].Y) / 2.0f;
                float ankleY = (keypoints[27].Y + keypoints[28].Y) / 2.0f;

                // Difference between shoulder and hip height should be small
                float torsoHeightDifference = Math.Abs(shoulderY - hipY);

                // Also ensure they are lying down / horizontal (hip Y and ankle Y are close)
                float bodySlope = Math.Abs(hipY - ankleY);

                // If torso is relatively horizontal and body isn't upright, it's a valid plank form
                if (torsoHeightDifference < 60f) // Threshold for horizontal alignment (adjust as needed)
                {
                    return true;
                }
            }
            return false;
        }
        public void ProcessVideo(string video)
        {
            if (_session == null)
            {
                MessageBox.Show("Cannot process video. AI model failed to load.");
                return;
            }

            // --- PASS 1: SILENT CALIBRATION & CACHING ---
            // We process the video once in the background to record all keypoints and find body limits.
            var allFramesKeypoints = new List<List<(float X, float Y, float Visibility)>>();
            List<double> capturedRatios = new List<double>();

            MessageBox.Show("Analyzing video calibration... Please wait.", "Processing", MessageBoxButton.OK, MessageBoxImage.Information);

            using (var capture = new VideoCapture(video))
            {
                if (!capture.IsOpened()) return;

                BoundingBox previousBox = null;
                List<(float X, float Y, float Visibility)> previousKeypoints = null;

                using (var frame = new Mat())
                {
                    while (capture.Read(frame) && !frame.Empty())
                    {
                        BoundingBox currentBox;
                        int maxFrameDim = Math.Max(frame.Cols, frame.Rows);

                        if (previousBox == null)
                        {
                            currentBox = new BoundingBox
                            {
                                X = (frame.Cols - maxFrameDim) / 2.0f,
                                Y = (frame.Rows - maxFrameDim) / 2.0f,
                                Width = maxFrameDim,
                                Height = maxFrameDim
                            };
                            previousKeypoints = null;
                        }
                        else
                        {
                            currentBox = previousBox;
                        }

                        int x1 = (int)Math.Round(currentBox.X);
                        int y1 = (int)Math.Round(currentBox.Y);
                        int x2 = (int)Math.Round(currentBox.X + currentBox.Width);
                        int y2 = (int)Math.Round(currentBox.Y + currentBox.Height);

                        int safeX1 = Math.Max(0, x1);
                        int safeY1 = Math.Max(0, y1);
                        int safeX2 = Math.Min(frame.Cols, x2);
                        int safeY2 = Math.Min(frame.Rows, y2);

                        int safeW = safeX2 - safeX1;
                        int safeH = safeY2 - safeY1;

                        if (safeW <= 0 || safeH <= 0)
                        {
                            previousBox = null;
                            allFramesKeypoints.Add(new List<(float X, float Y, float Visibility)>());
                            continue;
                        }

                        using (var cropped = new Mat(frame, new OpenCvSharp.Rect(safeX1, safeY1, safeW, safeH)))
                        using (var paddedCrop = new Mat())
                        using (var resized = new Mat())
                        {
                            int padTop = safeY1 - y1;
                            int padBottom = y2 - safeY2;
                            int padLeft = safeX1 - x1;
                            int padRight = x2 - safeX2;

                            Cv2.CopyMakeBorder(cropped, paddedCrop, padTop, padBottom, padLeft, padRight, BorderTypes.Constant, Scalar.Black);
                            Cv2.Resize(paddedCrop, resized, new OpenCvSharp.Size(256, 256));
                            Cv2.CvtColor(resized, resized, ColorConversionCodes.BGR2RGB);

                            bool isPlankMode = false;
                            Dispatcher.Invoke(() => { isPlankMode = PlankModeCheckBox.IsChecked == true; });

                            if (isPlankMode)
                            {
                                Cv2.Rotate(resized, resized, RotateFlags.Rotate90Counterclockwise);
                            }

                            var keypoints = RunInference(resized, currentBox, isPlankMode, ref previousKeypoints);
                            allFramesKeypoints.Add(keypoints);

                            var validKp = keypoints.Where(k => k.Visibility > 0.15f).ToList();
                            if (validKp.Count > 5)
                            {
                                float minX = validKp.Min(k => k.X);
                                float maxX = validKp.Max(k => k.X);
                                float minY = validKp.Min(k => k.Y);
                                float maxY = validKp.Max(k => k.Y);

                                float cx = (minX + maxX) / 2.0f;
                                float cy = (minY + maxY) / 2.0f;
                                float w = maxX - minX;
                                float h = maxY - minY;

                                float maxDim = Math.Max(w, h);
                                maxDim = Math.Max(maxDim, 100f);

                                float targetBoxSize = maxDim * 1.25f;
                                float targetX = cx - targetBoxSize / 2.0f;
                                float targetY = cy - targetBoxSize / 2.0f;

                                if (currentBox.Width >= maxFrameDim - 1)
                                {
                                    previousBox = new BoundingBox { X = targetX, Y = targetY, Width = targetBoxSize, Height = targetBoxSize };
                                }
                                else
                                {
                                    float cameraSmooth = 0.2f;
                                    previousBox = new BoundingBox
                                    {
                                        X = currentBox.X + cameraSmooth * (targetX - currentBox.X),
                                        Y = currentBox.Y + cameraSmooth * (targetY - currentBox.Y),
                                        Width = currentBox.Width + cameraSmooth * (targetBoxSize - currentBox.Width),
                                        Height = currentBox.Height + cameraSmooth * (targetBoxSize - currentBox.Height)
                                    };
                                }
                            }
                            else
                            {
                                previousBox = null;
                            }

                            // Collect depth ratios during calibration pass to find user limits
                            double leftRatio = GetLegRatio(keypoints, true);
                            double rightRatio = GetLegRatio(keypoints, false);
                            double currentRatio = Math.Min(leftRatio, rightRatio);
                            if (currentRatio < 1.5) // Filter out tracking spikes
                            {
                                capturedRatios.Add(currentRatio);
                            }
                        }
                    }
                }
            }

            // Calculate dynamic thresholds based on the user's personal video data
            double userMaxRatio = capturedRatios.Count > 0 ? capturedRatios.Max() : 1.0;
            double userMinRatio = capturedRatios.Count > 0 ? capturedRatios.Min() : 0.2;

            // Dynamic triggers set precisely between their unique min and max height
            double upThreshold = userMinRatio + (userMaxRatio - userMinRatio) * 0.70;
            double downThreshold = userMinRatio + (userMaxRatio - userMinRatio) * 0.30;

            // --- PASS 2: VISUAL PLAYBACK & REP COUNTING ---
            // Now we play the video back smoothly using the pre-calculated custom thresholds.
            _squatCount = 0;
            _squatState = "UP";
            int plankFrames = 0; // Tracks frames holding a valid plank

            using (var capture = new VideoCapture(video))
            {
                if (!capture.IsOpened()) return;

                using (var frame = new Mat())
                {
                    int frameIndex = 0;
                    while (capture.Read(frame) && !frame.Empty())
                    {
                        using (var processedFrame = frame.Clone())
                        {
                            if (frameIndex < allFramesKeypoints.Count)
                            {
                                var keypoints = allFramesKeypoints[frameIndex];
                                DrawPoseKeypoints(processedFrame, keypoints);

                                // Check what mode the user selected on the UI checkbox
                                bool isPlankMode = false;
                                Dispatcher.Invoke(() => { isPlankMode = PlankModeCheckBox.IsChecked == true; });

                                if (isPlankMode)
                                {
                                    // --- PLANK MODE ---
                                    bool holdingPlank = IsPlankPosition(keypoints);
                                    if (holdingPlank)
                                    {
                                        plankFrames++;
                                    }

                                    // Convert frames to seconds (~30 frames per second)
                                    int plankSeconds = plankFrames / 30;

                                    Cv2.PutText(processedFrame, $"Plank Time: {plankSeconds}s", new OpenCvSharp.Point(20, 50), HersheyFonts.HersheySimplex, 1.5, Scalar.Yellow, 3);
                                    string formStatus = holdingPlank ? "Good Form!" : "Adjust Form!";
                                    Scalar formColor = holdingPlank ? Scalar.LimeGreen : Scalar.Red;
                                    Cv2.PutText(processedFrame, formStatus, new OpenCvSharp.Point(20, 100), HersheyFonts.HersheySimplex, 1, formColor, 2);
                                }
                                else
                                {
                                    // --- SQUAT MODE ---
                                    AnalyzeSquatDynamic(keypoints, processedFrame, upThreshold, downThreshold);
                                }
                            }

                            Cv2.ImShow("Pose Estimation (Calibrated)", processedFrame);
                            if (Cv2.WaitKey(30) == 27) break; // ~30ms playback delay for normal speed

                            frameIndex++;
                        }
                    }
                }
            }
            Cv2.DestroyAllWindows();
        }
        private double GetLegRatio(List<(float X, float Y, float Visibility)> keypoints, bool isLeft)
        {
            int hipIdx = isLeft ? 23 : 24;
            int kneeIdx = isLeft ? 25 : 26;
            int ankleIdx = isLeft ? 27 : 28;

            if (keypoints.Count > ankleIdx &&
                keypoints[hipIdx].Visibility > 0.35f &&
                keypoints[kneeIdx].Visibility > 0.35f &&
                keypoints[ankleIdx].Visibility > 0.35f)
            {
                float thighVertical = keypoints[kneeIdx].Y - keypoints[hipIdx].Y;
                float calfVertical = keypoints[ankleIdx].Y - keypoints[kneeIdx].Y;
                if (calfVertical > 0)
                {
                    return thighVertical / calfVertical;
                }
            }
            return 1.0;
        }

        public List<(float X, float Y, float Visibility)> RunInference(Mat inputMat, BoundingBox box, bool isPlankMode, ref List<(float X, float Y, float Visibility)> previousKeypointsRef)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 256, 256, 3 });
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    Vec3b pixel = inputMat.At<Vec3b>(y, x);
                    tensor[0, y, x, 0] = pixel.Item0 / 255.0f;
                    tensor[0, y, x, 1] = pixel.Item1 / 255.0f;
                    tensor[0, y, x, 2] = pixel.Item2 / 255.0f;
                }
            }

            string inputName = _session.InputMetadata.Keys.First();
            string outputName = _session.OutputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using (var results = _session.Run(inputs))
            {
                var outputTensor = results.First(r => r.Name == outputName).AsTensor<float>();
                List<float> outputValues = new List<float>(outputTensor);
                var currentKeypoints = new List<(float X, float Y, float Visibility)>();

                for (int i = 0; i < outputValues.Count; i += 5)
                {
                    if (i + 4 < outputValues.Count)
                    {
                        float rawX = outputValues[i];
                        float rawY = outputValues[i + 1];
                        float visibility = 1.0f / (1.0f + (float)Math.Exp(-outputValues[i + 3]));

                        if (isPlankMode)
                        {
                            float tempX = rawX;
                            rawX = 256.0f - rawY;
                            rawY = tempX;
                        }

                        float x = box.X + (rawX / 256.0f) * box.Width;
                        float y = box.Y + (rawY / 256.0f) * box.Height;

                        currentKeypoints.Add((x, y, visibility));
                    }
                }

                if (previousKeypointsRef == null || previousKeypointsRef.Count != currentKeypoints.Count)
                {
                    previousKeypointsRef = currentKeypoints;
                    return currentKeypoints;
                }

                var smoothedKeypoints = new List<(float X, float Y, float Visibility)>();
                for (int i = 0; i < currentKeypoints.Count; i++)
                {
                    var curr = currentKeypoints[i];
                    var prev = previousKeypointsRef[i];

                    float dynamicSmooth = curr.Visibility > 0.5f ? SmoothingFactor : 0.8f;
                    float smoothedX = prev.X + dynamicSmooth * (curr.X - prev.X);
                    float smoothedY = prev.Y + dynamicSmooth * (curr.Y - prev.Y);
                    float smoothedVis = prev.Visibility + SmoothingFactor * (curr.Visibility - prev.Visibility);

                    smoothedKeypoints.Add((smoothedX, smoothedY, smoothedVis));
                }

                previousKeypointsRef = smoothedKeypoints;
                return smoothedKeypoints;
            }
        }

        private void AnalyzeSquatDynamic(List<(float X, float Y, float Visibility)> keypoints, Mat processedFrame, double upThresh, double downThresh)
        {
            double leftDepthRatio = GetLegRatio(keypoints, true);
            double rightDepthRatio = GetLegRatio(keypoints, false);
            double trueDepthRatio = Math.Min(leftDepthRatio, rightDepthRatio);

            if (trueDepthRatio > upThresh)
            {
                _squatState = "UP";
            }
            if (trueDepthRatio < downThresh && _squatState == "UP")
            {
                _squatState = "DOWN";
                _squatCount++;
            }

            Cv2.PutText(processedFrame, $"Squats: {_squatCount}", new OpenCvSharp.Point(20, 50), HersheyFonts.HersheySimplex, 1.5, Scalar.Yellow, 3);
            Cv2.PutText(processedFrame, $"Dynamic Ratio: {Math.Round(trueDepthRatio, 2)}", new OpenCvSharp.Point(20, 100), HersheyFonts.HersheySimplex, 1, Scalar.LimeGreen, 2);
            Cv2.PutText(processedFrame, $"State: {_squatState}", new OpenCvSharp.Point(20, 140), HersheyFonts.HersheySimplex, 1, Scalar.Cyan, 2);
        }

        private void DrawPoseKeypoints(Mat frame, List<(float X, float Y, float Visibility)> keypoints)
        {
            float threshold = 0.55f;
            var connections = new List<(int p1, int p2)>
            {
                (11, 12), (11, 23), (12, 24), (23, 24), // Torso
                (11, 13), (13, 15),                     // Left Arm
                (12, 14), (14, 16),                     // Right Arm
                (23, 25), (25, 27), (27, 29), (29, 31), // Left Leg
                (24, 26), (26, 28), (28, 30), (30, 32)  // Right Leg
            };

            foreach (var conn in connections)
            {
                if (conn.p1 < keypoints.Count && conn.p2 < keypoints.Count)
                {
                    var kp1 = keypoints[conn.p1];
                    var kp2 = keypoints[conn.p2];

                    if (kp1.Visibility > threshold && kp2.Visibility > threshold)
                    {
                        var p1 = new OpenCvSharp.Point(kp1.X, kp1.Y);
                        var p2 = new OpenCvSharp.Point(kp2.X, kp2.Y);
                        Cv2.Line(frame, p1, p2, Scalar.LimeGreen, 2, LineTypes.AntiAlias);
                    }
                }
            }
            foreach (var kp in keypoints)
            {
                if (kp.Visibility > threshold)
                {
                    Cv2.Circle(frame, new OpenCvSharp.Point(kp.X, kp.Y), 3, Scalar.Red, -1, LineTypes.AntiAlias);
                }
            }
        }
    }
}