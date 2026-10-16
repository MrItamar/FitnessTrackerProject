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
    public partial class MainWindow : System.Windows.Window
    {
        private const string ModelPath = "pose_estimation_mediapipe_2023mar.onnx";
        private const string AnalysisWindowName = "Analyzing...";

        private PoseEstimator _estimator;
        private double _videoFps = 30.0;
        private int currentUserId;
        private bool IsCoach;

        public MainWindow() { }

        public MainWindow(int userId)
        {
            InitializeComponent();
            currentUserId = userId;
            IsCoach = CheckIsCoach();
            if (IsCoach) CoachStudioBtn.Visibility = Visibility.Visible;
            else ExploreBtn.Visibility = Visibility.Visible;
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

                return role != null && role != DBNull.Value &&
                       string.Equals(role.ToString().Trim(), "Coach", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not check your role: " + ex.Message);
                return false;
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
            if (_estimator == null) return;
            this.Cursor = System.Windows.Input.Cursors.Wait;

            // 1. SNEAK PEEK: Read the first 2 seconds just to figure out what movement is happening
            bool initialOrientation = exercise != null && exercise.IsHorizontal;
            List<FrameResult> peekFrames = AnalyzeVideoQuick(video, initialOrientation);

            if (peekFrames == null || peekFrames.Count == 0)
            {
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                return;
            }

            // 2. DETECT: Let the AI figure out the real exercise from the sneak peek
            double detectScore;
            IExerciseCounter detected = ExerciseRegistry.Detect(peekFrames, out detectScore);

            string finalName = detected != null ? detected.Name : (exercise != null ? exercise.Name : "Squat");
            IExerciseCounter counter = ExerciseRegistry.Create(finalName) ?? new SquatCounter();

            // 3. THE FIX: Ask the counter itself what orientation it needs!
            bool correctOrientation = counter.IsHorizontal;

            // 4. FULL SILENT SCAN: Scan the whole video perfectly using the correct orientation
            List<FrameResult> fullFrames = AnalyzeVideo(video, correctOrientation);

            this.Cursor = System.Windows.Input.Cursors.Arrow;
            if (fullFrames == null || fullFrames.Count == 0) return;

            // 5. CALIBRATE: Learn the body's exact depth and angles for accurate reps
            if (counter is SquatCounter sq) sq.Calibrate(fullFrames);
            else if (counter is PushUpCounter pu) pu.Calibrate(fullFrames);
            else if (counter is PullUpCounter pl) pl.Calibrate(fullFrames);

            // 6. VISIBLE PLAYBACK: Open window and count live
            PlayVisiblePreview(video, fullFrames, counter);

            // 7. SAVE TO DB
            int finalValue = GetCounterValue(counter, _videoFps);
            string note = finalValue > 0 ? "Great workout!" : "Completed";
            bool noteIsGood = finalValue > 0;

            int trustedFrames = 0;
            foreach (var f in fullFrames)
            {
                if (f.State == TrackState.Tracking && f.Distance != DistanceStatus.TooClose)
                    trustedFrames++;
            }
            int quality = fullFrames.Count > 0 ? (int)((trustedFrames / (float)fullFrames.Count) * 100) : 100;

            if (currentUserId > 0)
            {
                try
                {
                    ExerciseModel matchedModel = ExercisePool.GetByName(counter.Name) ?? exercise;
                    int exerciseId = matchedModel != null ? matchedModel.Id : 1;

                    WorkoutRepository.SaveWorkout(currentUserId, exerciseId, finalValue, note, noteIsGood, quality);
                    MainFrame.Navigate(new WorkoutHubUserControl(currentUserId));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not save workout: " + ex.Message);
                }
            }
        }
        private List<FrameResult> AnalyzeVideo(string video, bool isPlankMode)
        {
            var results = new List<FrameResult>();
            bool isLive = string.IsNullOrEmpty(video);

            _estimator.StartVideo(isPlankMode);

            using (var capture = isLive ? new VideoCapture(0, VideoCaptureAPIs.DSHOW) : new VideoCapture(video))
            {
                if (!capture.IsOpened()) return null;

                if (!isLive) _videoFps = capture.Fps > 1 ? capture.Fps : 30.0;
                BoxTracker tracker = null;

                using (var frame = new Mat())
                {
                    while (capture.Read(frame) && !frame.Empty())
                    {
                        if (isLive) Cv2.Flip(frame, frame, FlipMode.Y);
                        if (tracker == null) tracker = new BoxTracker(frame.Cols, frame.Rows);

                        BoundingBox boxUsed = isPlankMode ? BoundingBox.FullFrame(frame.Cols, frame.Rows) : tracker.Box;
                        List<Keypoint> keypoints = _estimator.Estimate(frame, boxUsed);

                        if (tracker.ReacquiredThisFrame) _estimator.ResetSmoothing();

                        DistanceStatus distance = DistanceChecker.Update(keypoints, _estimator.Confidence, frame.Rows);
                        TrackState state = isPlankMode ? TrackState.Tracking : tracker.Update(keypoints, _estimator.Confidence);

                        results.Add(new FrameResult
                        {
                            Keypoints = keypoints,
                            Box = boxUsed,
                            State = state,
                            Distance = distance
                        });

                        // If testing a live camera, only sample 3 seconds to avoid freezing forever
                        if (isLive && results.Count > 90) break;
                    }
                }
            }
            return results;
        }
        private void PlayVisiblePreview(string video, List<FrameResult> frames, IExerciseCounter counter)
        {
            bool isLive = string.IsNullOrEmpty(video);

            using (var capture = isLive ? new VideoCapture(0, VideoCaptureAPIs.DSHOW) : new VideoCapture(video))
            {
                if (!capture.IsOpened()) return;

                // Play at the normal speed of the video
                int delay = isLive ? 1 : Math.Max(1, (int)(1000.0 / _videoFps));

                Cv2.NamedWindow(AnalysisWindowName, WindowFlags.Normal);
                Cv2.ResizeWindow(AnalysisWindowName, 640, 360);

                using (var frame = new Mat())
                {
                    int frameIndex = 0;
                    while (capture.Read(frame) && !frame.Empty() && frameIndex < frames.Count)
                    {
                        if (isLive) Cv2.Flip(frame, frame, FlipMode.Y);

                        FrameResult res = frames[frameIndex];
                        frameIndex++;

                        // Trigger the live counter!
                        if (res.State != TrackState.Lost && res.Distance != DistanceStatus.TooClose)
                        {
                            counter.Update(res.Keypoints);
                        }

                        int currentVal = GetCounterValue(counter, _videoFps);
                        string counterText = counter.Name == "Plank"
                            ? $"{counter.Name} - {currentVal}s"
                            : $"{counter.Name} - {currentVal}";

                        PoseDrawer.DrawSkeleton(frame, res.Keypoints);

                        if (res.Distance == DistanceStatus.TooClose)
                        {
                            PoseDrawer.PutText(frame, "Too close - step back", 95, 0.8, Scalar.Red, 2);
                        }

                        PoseDrawer.PutText(frame, counterText, 45, 1.3, Scalar.Yellow, 3);

                        using (Mat display = PoseDrawer.ResizeForDisplay(frame, 640, 360))
                        {
                            Cv2.ImShow(AnalysisWindowName, display);
                        }

                        if (Cv2.WaitKey(delay) == 27) break; // ESC stops playback
                    }
                }
            }
            Cv2.DestroyWindow(AnalysisWindowName);
        }
        private List<FrameResult> AnalyzeVideoQuick(string video, bool isPlankMode)
        {
            var results = new List<FrameResult>();
            bool isLive = (video == null);

            _estimator.StartVideo(isPlankMode);

            using (var capture = isLive ? new VideoCapture(0, VideoCaptureAPIs.DSHOW) : new VideoCapture(video))
            {
                if (!capture.IsOpened()) return null;

                BoxTracker tracker = null;
                using (var frame = new Mat())
                {
                    while (capture.Read(frame) && !frame.Empty())
                    {
                        if (isLive) Cv2.Flip(frame, frame, FlipMode.Y);
                        if (tracker == null) tracker = new BoxTracker(frame.Cols, frame.Rows);

                        BoundingBox boxUsed = isPlankMode ? BoundingBox.FullFrame(frame.Cols, frame.Rows) : tracker.Box;
                        List<Keypoint> keypoints = _estimator.Estimate(frame, boxUsed);

                        if (tracker.ReacquiredThisFrame) _estimator.ResetSmoothing();

                        DistanceStatus distance = DistanceChecker.Update(keypoints, _estimator.Confidence, frame.Rows);
                        TrackState state = isPlankMode ? TrackState.Tracking : tracker.Update(keypoints, _estimator.Confidence);

                        results.Add(new FrameResult
                        {
                            Keypoints = keypoints,
                            Box = boxUsed,
                            State = state,
                            Distance = distance
                        });

                        if (isLive && results.Count > 60) break; // For live camera, sample first 60 frames for detection
                    }
                }
            }
            return results;
        }
        private int GetCounterValue(IExerciseCounter counter, double fps)
        {
            if (counter is PlankChecker pk) return pk.HoldSeconds(fps);
            if (counter is SquatCounter sq) return sq.Count;
            if (counter is PushUpCounter pu) return pu.Count;
            if (counter is PullUpCounter pl) return pl.Count;
            return 0;
        }

        private void OpenSidebar(object sender, RoutedEventArgs e)
        {
            if (Sidebar.Visibility == Visibility.Visible)
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
                    MainFrame.Navigate(new CoachStudioUserControl(currentUserId));
                    break;
                case 2:
                    MainFrame.Navigate(new ProfileUserControl(currentUserId));
                    break;
                case 3:
                    MainFrame.Navigate(new ExploreUserControl(currentUserId));
                    break;
            }
        }
    }
}