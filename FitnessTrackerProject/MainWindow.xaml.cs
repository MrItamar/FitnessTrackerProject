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

        private List<(float X, float Y, float Visibility)> _previousKeypoints = null;
        private BoundingBox _previousBox = null;
        private readonly float SmoothingFactor = 0.15f;

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

        public void ProcessVideo(string video)
        {
            //TODO: understand

            _previousBox = null;
            _previousKeypoints = null;

            using (var capture = new VideoCapture(video))
            {
                if (!capture.IsOpened()) return;

                using (var frame = new Mat())
                {
                    while (capture.Read(frame) && !frame.Empty())
                    {
                        using (var processedFrame = frame.Clone())
                        {
                            BoundingBox currentBox;
                            int maxFrameDim = Math.Max(frame.Cols, frame.Rows);

                            if (_previousBox == null)
                            {
                                currentBox = new BoundingBox
                                {
                                    X = (frame.Cols - maxFrameDim) / 2.0f,
                                    Y = (frame.Rows - maxFrameDim) / 2.0f,
                                    Width = maxFrameDim,
                                    Height = maxFrameDim
                                };
                                _previousKeypoints = null;
                            }
                            else
                            {
                                currentBox = _previousBox;
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
                                _previousBox = null;
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

                                // Check if the user ticked the box on the UI
                                bool isPlankMode = false;
                                Dispatcher.Invoke(() => { isPlankMode = PlankModeCheckBox.IsChecked == true; });

                                if (isPlankMode)
                                {
                                    Cv2.Rotate(resized, resized, RotateFlags.Rotate90Counterclockwise);
                                }

                                var keypoints = RunInference(resized, currentBox, isPlankMode);

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
                                        _previousBox = new BoundingBox { X = targetX, Y = targetY, Width = targetBoxSize, Height = targetBoxSize };
                                    }
                                    else
                                    {
                                        float cameraSmooth = 0.2f;
                                        _previousBox = new BoundingBox
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
                                    _previousBox = null;
                                }

                                DrawPoseKeypoints(processedFrame, keypoints);

                                Cv2.ImShow("Pose Estimation", processedFrame);
                                if (Cv2.WaitKey(1) == 27) break;
                            }
                        }
                    }
                }
            }
            Cv2.DestroyAllWindows();
        }

        public List<(float X, float Y, float Visibility)> RunInference(Mat inputMat, BoundingBox box, bool isPlankMode)
        {
            //creating a massive 3D grid
            var tensor = new DenseTensor<float>(new[] { 1, 256, 256, 3 });
            //going through the pixels and translating it to between 0-1 for ai to understand it better
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

            //getting results from ai
            //TODO: understand

            string inputName = _session.InputMetadata.Keys.First();
            string outputName = _session.OutputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };


            using (var results = _session.Run(inputs))
            {
                //TODO: understand
                var outputTensor = results.First(r => r.Name == outputName).AsTensor<float>();

                List<float> outputValues = new List<float>(outputTensor);
                var currentKeypoints = new List<(float X, float Y, float Visibility)>();

                //every fifth number represent a joint: n1- x,n2-y,n3-z,n4-Visibility,n5-Presence(bool)
                // saving those number into currentKeypoints
                for (int i = 0; i < outputValues.Count; i += 5)
                {
                    if (i + 4 < outputValues.Count)
                    {
                        float rawX = outputValues[i];
                        float rawY = outputValues[i + 1];
                        float visibility = 1.0f / (1.0f + (float)Math.Exp(-outputValues[i + 3]));

                        if (isPlankMode)
                        {
                            //switching the x and y to rotate the video if its horizontal
                            float tempX = rawX;
                            rawX = 256.0f - rawY;
                            rawY = tempX;
                        }
                        //getting the x and y on the screen
                        float x = box.X + (rawX / 256.0f) * box.Width;
                        float y = box.Y + (rawY / 256.0f) * box.Height;

                        currentKeypoints.Add((x, y, visibility));
                    }
                }
                //TODO: understand
                if (_previousKeypoints == null || _previousKeypoints.Count != currentKeypoints.Count)
                {
                    _previousKeypoints = currentKeypoints;
                    return currentKeypoints;
                }

                //checking the currentKeypoints against the _previousKeypoints making sure it doesnt move all the way and twitch
                var smoothedKeypoints = new List<(float X, float Y, float Visibility)>();
                for (int i = 0; i < currentKeypoints.Count; i++)
                {
                    var curr = currentKeypoints[i];
                    var prev = _previousKeypoints[i];

                    float dynamicSmooth = curr.Visibility > 0.5f ? SmoothingFactor : 0.05f;

                    float smoothedX = prev.X + dynamicSmooth * (curr.X - prev.X);
                    float smoothedY = prev.Y + dynamicSmooth * (curr.Y - prev.Y);
                    float smoothedVis = prev.Visibility + SmoothingFactor * (curr.Visibility - prev.Visibility);

                    smoothedKeypoints.Add((smoothedX, smoothedY, smoothedVis));
                }

                _previousKeypoints = smoothedKeypoints;
                return smoothedKeypoints;
            }
        }

        private void DrawPoseKeypoints(Mat frame, List<(float X, float Y, float Visibility)> keypoints)
        {
            float threshold = 0.35f;

            //the defult id for the joints: 11- elbow
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
                //checking if there is enough keypoints
                if (conn.p1 < keypoints.Count && conn.p2 < keypoints.Count)
                {
                    //getting the keypoint using the id
                    var kp1 = keypoints[conn.p1];
                    var kp2 = keypoints[conn.p2];

                    //checking if thev isibility is higher that the threshold
                    if (kp1.Visibility > threshold && kp2.Visibility > threshold)
                    {
                        //getting the points and drawing a line between them
                        var p1 = new OpenCvSharp.Point(kp1.X, kp1.Y);
                        var p2 = new OpenCvSharp.Point(kp2.X, kp2.Y);
                        Cv2.Line(frame, p1, p2, Scalar.LimeGreen, 2, LineTypes.AntiAlias);
                    }
                }
            }
            //drawing the points of joints
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