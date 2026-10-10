using FitnessTrackerProject.Scripts;
using Microsoft.Win32;
using OpenCvSharp;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

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
        private const string LiveWindowName = "Live Camera";
        private const double CalibrationSeconds = 8.0;
        DistanceStatus distance;
        private int currentUserId;
        private bool IsCoach;
        public MainWindow() { }
        public MainWindow(int userId)
        {
            InitializeComponent();
            currentUserId = userId;
            IsCoach = CheckIsCoach();
            if (IsCoach) CoachStudioBtn.Visibility = Visibility.Visible;

            MainFrame.Navigate(new WorkoutHubUserControl(currentUserId));
            LoadModel();
        }
        private bool CheckIsCoach()
        {
            try
            {
                object role = DatabaseHelper.ExecuteScalar(
                    "SELECT UserRole FROM Users WHERE ID = ?",
                    p => p.AddWithValue("?", currentUserId));

                // role is null if there is no such user, and DBNull if the cell is empty
                return role != null && role != DBNull.Value &&
                       string.Equals(role.ToString().Trim(), "Coach", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not check your role: " + ex.Message);
                return false;   // if we can't tell, don't let them in
            }
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
        public void ProcessVideo(string video, ExerciseModel exercise)
        {
            if (_estimator == null)
            {
                MessageBox.Show("Cannot process video. AI model failed to load.");
                return;
            }

            // Plank and push-ups are horizontal exercises: the image is rotated before the AI sees it

            bool isHorizontal = exercise.IsHorizontal;

            // PASS 1: run the AI on every frame and remember the results (ESC cancels)
            List<FrameResult> frames = AnalyzeVideo(video, isHorizontal);
            if (frames == null) return;   // cancelled or could not open the video

            if (frames.Count == 0)
            {
                MessageBox.Show("Could not read any frames from this video.");
                return;
            }

            // Calibrate on this person's own movement
            var squats = new SquatCounter();
            var plank = new PlankChecker();
            var pushUps = new PushUpCounter();
            var pullUps = new PullUpCounter();

            if (exercise.Name == "Squat") squats.Calibrate(frames);
            if (exercise.Name == "Pushup") pushUps.Calibrate(frames);

            if (ShowPlayback && video != null)
                PlayBack(video, frames, exercise, squats, plank, pushUps,pullUps);
            else
                ShowSummary(frames, exercise, squats, plank, pushUps, pullUps);
        }

        /// <summary>No playback: let the counter do its work on the saved frames and show the result.</summary>
        private void ShowSummary(List<FrameResult> frames, ExerciseModel exercise,
                                 SquatCounter squats, PlankChecker plank, PushUpCounter pushUps, PullUpCounter pullUps)
        {
            foreach (FrameResult result in frames)
            {
                if (result.State == TrackState.Lost || result.Distance == DistanceStatus.TooClose) continue;   // don't count frames we can't trust
                switch (exercise.Name)
                {
                    case "Plank": plank.Update(result.Keypoints); break;
                    case "Pushup": pushUps.Update(result.Keypoints); break;
                    case "Pullup": pullUps.Update(result.Keypoints); break;
                    default: squats.Update(result.Keypoints); break;
                }
            }

            string message;
            switch (exercise.Name)
            {
                case "Plank": message = $"Plank hold time: {plank.HoldSeconds(_videoFps)} seconds"; break;
                case "Pushup": message = $"Push-ups counted: {pushUps.Count}"; break;
                case "Pullup": message = $"Pull-ups counted: {pullUps.Count}"; break;
                default: message = $"Squats counted: {squats.Count}"; break;
            }

            MessageBox.Show(message, "Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>Pass 2: show the video with the skeleton and the exercise info.</summary>
        private void PlayBack(string video, List<FrameResult> frames, ExerciseModel exercise,
                              SquatCounter squats, PlankChecker plank, PushUpCounter pushUps, PullUpCounter pullUps)
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
                        else if (exercise.Name == "Plank")
                        {
                            ShowPlank(frame, result.Keypoints, plank, fps);
                        }
                        else if (exercise.Name == "Pushup")
                        {
                            ShowPushUps(frame, result.Keypoints, pushUps);
                        }
                        else if (exercise.Name == "Pullup")
                        {
                            ShowPullUps(frame, result.Keypoints, pullUps);
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

        private void ShowPushUps(Mat frame, List<Keypoint> keypoints, PushUpCounter pushUps)
        {
            pushUps.Update(keypoints);

            string angleText = pushUps.LastAngle.HasValue
                ? $"Elbow angle: {Math.Round(pushUps.LastAngle.Value)}"
                : "Get into push-up position";

            PoseDrawer.PutText(frame, $"Push-ups: {pushUps.Count}", 50, 1.5, Scalar.Yellow, 3);
            PoseDrawer.PutText(frame, angleText, 100, 1, Scalar.LimeGreen, 2);
            PoseDrawer.PutText(frame, $"State: {pushUps.State}", 140, 1, Scalar.Cyan, 2);
        }

        private void ShowPullUps(Mat frame, List<Keypoint> keypoints, PullUpCounter pullUps)
        {
            pullUps.Update(keypoints);

            string angleText = pullUps.LastAngle.HasValue
                ? $"Elbow angle: {Math.Round(pullUps.LastAngle.Value)}"
                : "Get into push-up position";

            PoseDrawer.PutText(frame, $"Push-ups: {pullUps.Count}", 50, 1.5, Scalar.Yellow, 3);
            PoseDrawer.PutText(frame, angleText, 100, 1, Scalar.LimeGreen, 2);
            PoseDrawer.PutText(frame, $"State: {pullUps.State}", 140, 1, Scalar.Cyan, 2);
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

        /// <summary>Turns the counters into one result (not used yet, ready for when you save workouts).</summary>
        /// <summary>
        /// Pass 1: runs the AI on every frame and remembers the results.
        /// video = path to a file, or null to use the live camera (ESC = finish).
        /// </summary>
        private List<FrameResult> AnalyzeVideo(string video, bool isPlankMode)
        {
            var results = new List<FrameResult>();
            bool cancelled = false;
            bool isLive = (video == null);

            _estimator.StartVideo(isPlankMode);

            using (var capture = isLive ? new VideoCapture(0, VideoCaptureAPIs.DSHOW) : new VideoCapture(video))
            {
                if (!capture.IsOpened())
                {
                    if (isLive) MessageBox.Show("Could not open the camera.");
                    return null;
                }

                // A camera has no real frame count, so 0 = "unknown" (the preview handles that)
                int totalFrames = isLive ? 0 : capture.FrameCount;

                // A file knows its own fps. A camera doesn't, so it is measured after the loop.
                if (!isLive) _videoFps = capture.Fps > 1 ? capture.Fps : 30.0;

                BoxTracker tracker = null;
                Stopwatch clock = Stopwatch.StartNew();

                Cv2.NamedWindow(AnalysisWindowName, WindowFlags.Normal);
                Cv2.ResizeWindow(AnalysisWindowName, 640, 360);

                using (var frame = new Mat())
                {
                    int frameNumber = 0;
                    DistanceChecker.Reset();

                    while (capture.Read(frame) && !frame.Empty())
                    {
                        frameNumber++;

                        // Mirror the camera image, like looking in a mirror
                        if (isLive) Cv2.Flip(frame, frame, FlipMode.Y);

                        // The tracker needs the frame size, so we create it on the first frame.
                        if (tracker == null) tracker = new BoxTracker(frame.Cols, frame.Rows);

                        BoundingBox boxUsed = tracker.Box;

                        List<Keypoint> keypoints = _estimator.Estimate(frame, boxUsed);

                        // If the tracker gave up and starts searching again, old keypoints must not influence the smoothing.
                        if (tracker.ReacquiredThisFrame) _estimator.ResetSmoothing();

                        // Distance check on EVERY frame (it needs consecutive frames to measure jitter)
                        DistanceStatus distance = DistanceChecker.Update(keypoints, _estimator.Confidence, frame.Rows);

                        if (isPlankMode)
                        {
                            results.Add(new FrameResult { Keypoints = keypoints, Box = BoundingBox.FullFrame(frame.Cols, frame.Rows), State = TrackState.Tracking, Distance = distance });
                        }
                        else
                        {
                            TrackState state = tracker.Update(keypoints, _estimator.Confidence);
                            results.Add(new FrameResult { Keypoints = keypoints, Box = boxUsed, State = state, Distance = distance });
                        }

                        // Live preview (every 2nd frame is enough).
                        // ESC on a file = cancel everything. ESC on the camera = "I'm done", keep what was recorded.
                        if (frameNumber % 2 == 0 && !ShowAnalysisPreview(frame, keypoints, distance, frameNumber, totalFrames))
                        {
                            if (!isLive) cancelled = true;
                            break;
                        }
                    }

                    // Camera: fps = frames processed / seconds passed (used by the plank timer)
                    if (isLive && frameNumber > 0)
                        _videoFps = frameNumber / Math.Max(0.001, clock.Elapsed.TotalSeconds);
                }
            }

            Cv2.DestroyWindow(AnalysisWindowName);
            return cancelled ? null : results;
        }

        private bool ShowAnalysisPreview(Mat frame, List<Keypoint> keypoints, DistanceStatus status,
                                         int frameNumber, int totalFrames)
        {
            using (var preview = frame.Clone())
            {
                PoseDrawer.DrawSkeleton(preview, keypoints);

                using (Mat small = PoseDrawer.ResizeForDisplay(preview, 640, 360))
                {
                    string guidance;
                    Scalar color;
                    if (status == DistanceStatus.TooClose)
                    {
                        guidance = "Too close - step back";
                        color = Scalar.Red;
                    }
                    else
                    {
                        guidance = "Good tracking!";
                        color = Scalar.LimeGreen;
                    }

                    string progress = totalFrames > 0
                        ? $"Analyzing {frameNumber}/{totalFrames}  (ESC = cancel)"
                        : $"Analyzing {frameNumber}  (ESC = cancel)";

                    Cv2.PutText(small, progress, new OpenCvSharp.Point(10, 25),
                        HersheyFonts.HersheySimplex, 0.5, Scalar.White, 1);
                    Cv2.PutText(small, guidance, new OpenCvSharp.Point(10, 60),
                        HersheyFonts.HersheySimplex, 0.8, color, 2);

                    // Temporary, for tuning thresholds. Remove when done.
                    Cv2.PutText(small, DistanceChecker.DebugInfo, new OpenCvSharp.Point(10, 90),
                        HersheyFonts.HersheySimplex, 0.45, Scalar.Yellow, 1);

                    Cv2.ImShow(AnalysisWindowName, small);
                    return Cv2.WaitKey(1) != 27;
                }
            }
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
            Button btn = sender as Button;
            int screenIndex = Convert.ToInt32(btn.Tag);

            switch (screenIndex)
            {
                case 0:
                    MainFrame.Navigate(new WorkoutHubUserControl(currentUserId));
                    break;

                case 1:
                    // CoachStudio
                    MainFrame.Navigate(new CoachStudioUserControl(currentUserId));
                    break;

                case 2:
                    // Profile
                    MainFrame.Navigate(new ProfileUserControl(currentUserId));
                    break;
            }
        }
    }
}