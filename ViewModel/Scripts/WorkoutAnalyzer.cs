using System;
using System.Collections.Generic;
using FitnessTrackerProject.Models;
using OpenCvSharp;

namespace ViewModel.Scripts
{
    public class WorkoutAnalyzer
    {
        private const string ModelPath = "pose_estimation_mediapipe_2023mar.onnx";
        private const string AnalysisWindowName = "Analyzing...";

        private PoseEstimator _estimator;
        private double _videoFps = 30.0;

        public bool IsModelLoaded => _estimator != null;

        public void LoadModel()
        {
            _estimator = new PoseEstimator(ModelPath);
        }

        /// <summary>
        /// Analyzes the video, plays the live OpenCV preview, and saves the result to the database.
        /// Returns true if a workout was saved.
        /// </summary>
        public bool ProcessAndSaveVideo(string video, ExerciseModel exercise, int userId)
        {
            if (_estimator == null) return false;

            // 1. Read the first 2 seconds to figure out what movement is happening
            bool initialOrientation = exercise != null && exercise.IsHorizontal;
            List<FrameResult> peekFrames = AnalyzeVideoQuick(video, initialOrientation);

            if (peekFrames == null || peekFrames.Count == 0) return false;

            // 2. Detect the exercise from the short clip
            double detectScore;
            IExerciseCounter detected = ExerciseRegistry.Detect(peekFrames, out detectScore);

            string finalName = detected != null ? detected.Name : (exercise != null ? exercise.Name : "Squat");
            IExerciseCounter counter = ExerciseRegistry.Create(finalName) ?? new SquatCounter();

            // 3. Scan the whole video with the correct orientation
            List<FrameResult> fullFrames = AnalyzeVideo(video, counter.IsHorizontal);
            if (fullFrames == null || fullFrames.Count == 0) return false;

            // 4. Calibrate for accurate reps
            if (counter is SquatCounter sq) sq.Calibrate(fullFrames);
            else if (counter is PushUpCounter pu) pu.Calibrate(fullFrames);
            else if (counter is PullUpCounter pl) pl.Calibrate(fullFrames);
            else if (counter is DeadliftCounter dl) dl.Calibrate(fullFrames);

            // 5. Open OpenCV window and play the live preview
            PlayVisiblePreview(video, fullFrames, counter);

            // 6. Calculate stats and save to database
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

            if (userId > 0)
            {
                ExerciseModel matchedModel = ExercisePool.GetByName(counter.Name) ?? exercise;
                int exerciseId = matchedModel != null ? matchedModel.Id : 1;

                WorkoutRepository.SaveWorkout(userId, exerciseId, finalValue, note, noteIsGood, quality);
                return true;
            }

            return false;
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

        private List<FrameResult> AnalyzeVideoQuick(string video, bool isPlankMode)
        {
            var results = new List<FrameResult>();
            bool isLive = string.IsNullOrEmpty(video);

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

                        if (isLive && results.Count > 60) break;
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

                int delay = isLive ? 1 : Math.Max(1, (int)(1000.0 / _videoFps));

                Cv2.NamedWindow(AnalysisWindowName, WindowFlags.Normal);
                Cv2.ResizeWindow(AnalysisWindowName, 640, 360);

                int badFormFrames = 0;

                using (var frame = new Mat())
                {
                    int frameIndex = 0;
                    while (capture.Read(frame) && !frame.Empty() && frameIndex < frames.Count)
                    {
                        if (isLive) Cv2.Flip(frame, frame, FlipMode.Y);

                        FrameResult res = frames[frameIndex];
                        frameIndex++;

                        if (res.State != TrackState.Lost && res.Distance != DistanceStatus.TooClose)
                        {
                            counter.Update(res.Keypoints);
                        }

                        int currentVal = GetCounterValue(counter, _videoFps);
                        string counterText = counter.Name == "Plank"
                            ? $"{counter.Name} - {currentVal}s"
                            : $"{counter.Name} - {currentVal}";

                        PoseDrawer.DrawSkeleton(frame, res.Keypoints);

                        string status = counter.StatusText ?? "";
                        bool isBadForm = status.IndexOf("too low", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         status.IndexOf("too high", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isBadForm)
                        {
                            badFormFrames++;
                            if (badFormFrames > 15)
                            {
                                PoseDrawer.PutText(frame, status.ToUpper() + "!", frame.Rows / 2, 2.0, Scalar.Red, 4);
                            }
                        }
                        else
                        {
                            badFormFrames = 0;
                        }

                        if (res.Distance == DistanceStatus.TooClose)
                        {
                            PoseDrawer.PutText(frame, "Too close - step back", 95, 0.8, Scalar.Red, 2);
                        }

                        PoseDrawer.PutText(frame, counterText, 45, 1.3, Scalar.Yellow, 3);
                        PoseDrawer.PutText(frame, status, 90, 0.7, Scalar.White, 2);

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

        private int GetCounterValue(IExerciseCounter counter, double fps)
        {
            if (counter is PlankChecker pk) return pk.HoldSeconds(fps);
            if (counter is SquatCounter sq) return sq.Count;
            if (counter is PushUpCounter pu) return pu.Count;
            if (counter is PullUpCounter pl) return pl.Count;
            if (counter is DeadliftCounter dl) return dl.Count;
            return 0;
        }
    }
}