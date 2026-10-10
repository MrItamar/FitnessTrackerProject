using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    public enum DistanceStatus { Good, TooClose }

    /// <summary>
    /// Warns the user to step back. When the person is too close, the model's points
    /// become unreliable and jump around. So we measure how much the points jump.
    /// </summary>
    public static class DistanceChecker
    {
        private const float JitterLimit = 0.012f;   // allowed jumpiness, as a fraction of frame height
        private const float MinConfidence = 0.5f;   // the model's own "I see a person" score
        private const int FramesToWarn = 8;         // bad frames in a row before we warn
        private const int FramesToClear = 5;        // good frames in a row before we stop warning

        private static List<Keypoint> _prev1;       // keypoints from 1 frame ago
        private static List<Keypoint> _prev2;       // keypoints from 2 frames ago
        private static int _badFrames;
        private static int _goodFrames;
        private static bool _warning;

        public static string DebugInfo { get; private set; } = "";

        /// <summary>Call once when a new video starts.</summary>
        public static void Reset()
        {
            _prev1 = null;
            _prev2 = null;
            _badFrames = 0;
            _goodFrames = 0;
            _warning = false;
        }

        /// <summary>Call once per frame.</summary>
        public static DistanceStatus Update(List<Keypoint> keypoints, float poseConfidence, int frameHeight)
        {
            float jitter = MeasureJitter(keypoints, frameHeight);

            // Remember this frame for next time
            _prev2 = _prev1;
            _prev1 = keypoints;

            bool lowConfidence = poseConfidence >= 0f && poseConfidence < MinConfidence;
            bool badFrame = jitter > JitterLimit || lowConfidence;

            DebugInfo = $"jitter={jitter:F4} confidence={poseConfidence:F2}";

            // Only warn after several bad frames in a row, so the message doesn't flicker
            if (badFrame)
            {
                _badFrames++;
                _goodFrames = 0;
                if (_badFrames >= FramesToWarn) _warning = true;
            }
            else
            {
                _goodFrames++;
                _badFrames = 0;
                if (_goodFrames >= FramesToClear) _warning = false;
            }

            return _warning ? DistanceStatus.TooClose : DistanceStatus.Good;
        }

        /// <summary>
        /// Average "jumpiness" of the visible points.
        /// A smoothly moving point changes by about the same amount each frame.
        /// A jittering point jumps forward and back, so that change is not steady.
        /// </summary>
        private static float MeasureJitter(List<Keypoint> now, int frameHeight)
        {
            if (_prev1 == null || _prev2 == null) return 0f;   // need 3 frames

            float total = 0f;
            int count = 0;

            for (int i = 0; i < Landmark.BodyCount && i < now.Count; i++)
            {
                if (now[i].Visibility < 0.3f) continue;

                // (now - prev1) is the last step, (prev1 - prev2) is the step before.
                // Their difference is how much the movement "changed direction or speed".
                float dx = (now[i].X - _prev1[i].X) - (_prev1[i].X - _prev2[i].X);
                float dy = (now[i].Y - _prev1[i].Y) - (_prev1[i].Y - _prev2[i].Y);

                total += (float)Math.Sqrt(dx * dx + dy * dy);
                count++;
            }

            if (count == 0) return 0f;
            return total / count / frameHeight;   // average, as a fraction of frame height
        }
    }
}