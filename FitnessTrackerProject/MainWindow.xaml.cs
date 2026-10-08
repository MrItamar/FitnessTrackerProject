using FitnessTrackerProject.Scripts;
using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Windows;

namespace FitnessTrackerProject
{
    /// <summary>
    /// The window only connects the pieces together:
    ///   PoseEstimator = the AI model        BoxTracker   = follows the person
    ///   SquatCounter  = counts squats       PlankChecker = checks plank form and time
    ///   PoseDrawer    = draws on the video
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private const string ModelPath = "pose_estimation_mediapipe_2023mar.onnx";
        private const string AnalysisWindowName = "Analyzing...";
        private const string PlaybackWindowName = "Pose Estimation (Calibrated)";

        private const bool ShowTrackingBox = true;   // only used by the playback: cyan box = tracking, red = lost frame

        // The second "review" playback (skeleton + counters + box). Off for now.
        // It is a normal field (not const) so it can be connected to a checkbox or setting later.
        private bool ShowPlayback = false;

        private PoseEstimator _estimator;
        private double _videoFps = 30.0;             // remembered from pass 1, used for the plank timer

        public MainWindow()
        {
            InitializeComponent();
            LoadModel();
        }

        private void LoadModel()
        {
            try
            {
                _estimator = new PoseEstimator(ModelPath);
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
            if (_estimator == null)
            {
                MessageBox.Show("Cannot process video. AI model failed to load.");
                return;
            }

            bool isPlankMode = false;
            Dispatcher.Invoke(() => { isPlankMode = PlankModeCheckBox.IsChecked == true; });

            // PASS 1: run the AI on every frame and remember the results (ESC cancels)
            List<FrameResult> frames = AnalyzeVideo(video, isPlankMode);
            if (frames == null) return;   // cancelled or could not open the video

            if (frames.Count == 0)
            {
                MessageBox.Show("Could not read any frames from this video.");
                return;
            }

            // Calibrate squat thresholds on this person's own movement
            var squats = new SquatCounter();
            squats.Calibrate(frames);

            var plank = new PlankChecker();

            // PASS 2 (optional): play the video back with the skeleton and the counters.
            // Otherwise we just count silently and show the result.
            if (ShowPlayback)
                PlayBack(video, frames, isPlankMode, squats, plank);
            else
                ShowSummary(frames, isPlankMode, squats, plank);
        }

        /// <summary>Pass 1: frame -> keypoints, and the box follows the person.</summary>
        private List<FrameResult> AnalyzeVideo(string video, bool isPlankMode)
        {
            var results = new List<FrameResult>();
            bool cancelled = false;

            _estimator.StartVideo(isPlankMode);

            using (var capture = new VideoCapture(video))
            {
                if (!capture.IsOpened()) return null;

                int totalFrames = capture.FrameCount;
                _videoFps = capture.Fps > 1 ? capture.Fps : 30.0;
                BoxTracker tracker = null;

                Cv2.NamedWindow(AnalysisWindowName, WindowFlags.Normal);
                Cv2.ResizeWindow(AnalysisWindowName, 640, 360);

                using (var frame = new Mat())
                {
                    int frameNumber = 0;
                    while (capture.Read(frame) && !frame.Empty())
                    {
                        frameNumber++;

                        // The tracker needs the frame size, so we create it on the first frame.
                        if (tracker == null) tracker = new BoxTracker(frame.Cols, frame.Rows);

                        BoundingBox boxUsed = tracker.Box;

                        List<Keypoint> keypoints = _estimator.Estimate(frame, boxUsed);
                        TrackState state = tracker.Update(keypoints, _estimator.Confidence);

                        // If the tracker gave up and starts searching again, old keypoints must not influence the smoothing.
                        if (tracker.ReacquiredThisFrame) _estimator.ResetSmoothing();

                        results.Add(new FrameResult { Keypoints = keypoints, Box = boxUsed, State = state });

                        // Live preview (every 2nd frame is enough). ESC = cancel.
                        if (frameNumber % 2 == 0 && !ShowAnalysisPreview(frame, keypoints, frameNumber, totalFrames))
                        {
                            cancelled = true;
                            break;
                        }
                    }
                }
            }

            Cv2.DestroyWindow(AnalysisWindowName);
            return cancelled ? null : results;
        }

        /// <summary>Shows progress while pass 1 runs. Returns false if the user pressed ESC.</summary>
        private bool ShowAnalysisPreview(Mat frame, List<Keypoint> keypoints, int frameNumber, int totalFrames)
        {
            using (var preview = frame.Clone())
            {
                PoseDrawer.DrawSkeleton(preview, keypoints);

                using (Mat small = PoseDrawer.ResizeForDisplay(preview, 640, 360))
                {
                    string progress = totalFrames > 0
                        ? $"Analyzing {frameNumber}/{totalFrames}  (ESC = cancel)"
                        : $"Analyzing {frameNumber}  (ESC = cancel)";

                    Cv2.PutText(small, progress, new OpenCvSharp.Point(10, 25), HersheyFonts.HersheySimplex, 0.7, Scalar.Yellow, 2);
                    Cv2.ImShow(AnalysisWindowName, small);
                }
            }

            return Cv2.WaitKey(1) != 27;
        }

        /// <summary>
        /// No playback: go through the saved frames once, let the counters do their work
        /// (same logic as the playback, but nothing is drawn) and show the final result.
        /// </summary>
        private void ShowSummary(List<FrameResult> frames, bool isPlankMode,
                                 SquatCounter squats, PlankChecker plank)
        {
            foreach (FrameResult result in frames)
            {
                if (result.State == TrackState.Lost) continue;   // don't count on frames the tracker doesn't trust

                if (isPlankMode)
                    plank.Update(result.Keypoints);
                else
                    squats.Update(result.Keypoints);
            }

            string message = isPlankMode
                ? $"Plank hold time: {plank.HoldSeconds(_videoFps)} seconds"
                : $"Squats counted: {squats.Count}";

            MessageBox.Show(message, "Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>Pass 2: show the video with the skeleton and the squat / plank info.</summary>
        private void PlayBack(string video, List<FrameResult> frames, bool isPlankMode,
                              SquatCounter squats, PlankChecker plank)
        {
            using (var capture = new VideoCapture(video))
            {
                if (!capture.IsOpened()) return;

                double fps = capture.Fps > 1 ? capture.Fps : 30.0;
                int delay = Math.Max(1, (int)(1000.0 / fps));

                Cv2.NamedWindow(PlaybackWindowName, WindowFlags.Normal);
                Cv2.ResizeWindow(PlaybackWindowName, 960, 540);

                using (var frame = new Mat())
                {
                    int frameIndex = 0;
                    while (capture.Read(frame) && !frame.Empty() && frameIndex < frames.Count)
                    {
                        FrameResult result = frames[frameIndex];

                        PoseDrawer.DrawSkeleton(frame, result.Keypoints);

                        if (ShowTrackingBox && result.State != TrackState.Searching)
                        {
                            Scalar boxColor = result.State == TrackState.Tracking ? Scalar.Cyan : Scalar.Red;
                            PoseDrawer.DrawBox(frame, result.Box, boxColor);
                        }

                        if (result.State == TrackState.Lost)
                        {
                            // The tracker doesn't trust this frame, so we don't count anything on it.
                            PoseDrawer.PutText(frame, "Tracking lost...", 50, 1.2, Scalar.Red, 3);
                        }
                        else if (isPlankMode)
                        {
                            ShowPlank(frame, result.Keypoints, plank, fps);
                        }
                        else
                        {
                            ShowSquats(frame, result.Keypoints, squats);
                        }

                        using (Mat display = PoseDrawer.ResizeForDisplay(frame, 960, 540))
                        {
                            Cv2.ImShow(PlaybackWindowName, display);
                        }

                        if (Cv2.WaitKey(delay) == 27) break;   // ESC = stop
                        frameIndex++;
                    }
                }
            }
            Cv2.DestroyAllWindows();
        }

        private void ShowSquats(Mat frame, List<Keypoint> keypoints, SquatCounter squats)
        {
            squats.Update(keypoints);

            string ratioText = squats.LastRatio.HasValue
                ? $"Dynamic Ratio: {Math.Round(squats.LastRatio.Value, 2)}"
                : "Legs not visible";

            PoseDrawer.PutText(frame, $"Squats: {squats.Count}", 50, 1.5, Scalar.Yellow, 3);
            PoseDrawer.PutText(frame, ratioText, 100, 1, Scalar.LimeGreen, 2);
            PoseDrawer.PutText(frame, $"State: {squats.State}", 140, 1, Scalar.Cyan, 2);
        }

        private void ShowPlank(Mat frame, List<Keypoint> keypoints, PlankChecker plank, double fps)
        {
            plank.Update(keypoints);

            string message;
            switch (plank.Status)
            {
                case PlankStatus.Good: message = "Good Form!"; break;
                case PlankStatus.HipsTooLow: message = "Hips too low!"; break;
                case PlankStatus.HipsTooHigh: message = "Hips too high!"; break;
                default: message = "Get into plank position"; break;
            }

            Scalar color = plank.IsGoodForm ? Scalar.LimeGreen : Scalar.Red;

            PoseDrawer.PutText(frame, $"Plank Time: {plank.HoldSeconds(fps)}s", 50, 1.5, Scalar.Yellow, 3);
            PoseDrawer.PutText(frame, message, 100, 1, color, 2);
        }

        private void OpenSidebar(object sender, RoutedEventArgs e)
        {
            if(Sidebar.Visibility == Visibility.Visible)
            {
                Sidebar.Visibility = Visibility.Hidden;
                MainContentTransform.X = 0;
            }
            else
            {
                Sidebar.Visibility = Visibility.Visible;
                MainContentTransform.X = 200;
            }
        }
        private void SwitchScreen(object sender, RoutedEventArgs e)
        {
            //
        }
    }
}