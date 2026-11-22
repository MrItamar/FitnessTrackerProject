using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    public static class PoseChecks
    {
        private const float MinVisibility = 0.35f;

        /// <summary>Frames the tracker trusts.</summary>
        public static int UsableFrames(List<FrameResult> frames)
        {
            int count = 0;
            foreach (FrameResult frame in frames)
            {
                if (frame.State != TrackState.Lost && frame.Keypoints != null) count++;
            }
            return count;
        }

        /// <summary>Is the body (shoulder to ankle) more vertical than horizontal?</summary>
        public static bool IsUpright(List<Keypoint> kp)
        {
            float dx, dy;
            if (!BodyDirection(kp, out dx, out dy)) return false;
            return dy > dx;
        }

        /// <summary>Is the body more horizontal than vertical?</summary>
        public static bool IsHorizontal(List<Keypoint> kp)
        {
            float dx, dy;
            if (!BodyDirection(kp, out dx, out dy)) return false;
            return dx >= dy;
        }

        /// <summary>Is at least one wrist above its shoulder? (Y = 0 is the top of the picture)</summary>
        public static bool HandsAboveShoulders(List<Keypoint> kp)
        {
            if (kp == null || kp.Count <= Landmark.RightWrist) return false;

            bool left = kp[Landmark.LeftWrist].Visibility >= MinVisibility
                        && kp[Landmark.LeftWrist].Y < kp[Landmark.LeftShoulder].Y;
            bool right = kp[Landmark.RightWrist].Visibility >= MinVisibility
                         && kp[Landmark.RightWrist].Y < kp[Landmark.RightShoulder].Y;

            return left || right;
        }

        /// <summary>The elbow angle in degrees on the better visible side, or null.</summary>
        public static double? ElbowAngle(List<Keypoint> kp)
        {
            if (kp == null || kp.Count <= Landmark.RightWrist) return null;

            bool useLeft = kp[Landmark.LeftShoulder].Visibility >= kp[Landmark.RightShoulder].Visibility;
            Keypoint shoulder = kp[useLeft ? Landmark.LeftShoulder : Landmark.RightShoulder];
            Keypoint elbow = kp[useLeft ? Landmark.LeftElbow : Landmark.RightElbow];
            Keypoint wrist = kp[useLeft ? Landmark.LeftWrist : Landmark.RightWrist];

            if (shoulder.Visibility < MinVisibility || elbow.Visibility < MinVisibility
                || wrist.Visibility < MinVisibility) return null;

            double upperX = shoulder.X - elbow.X, upperY = shoulder.Y - elbow.Y;
            double lowerX = wrist.X - elbow.X, lowerY = wrist.Y - elbow.Y;

            double upperLength = Math.Sqrt(upperX * upperX + upperY * upperY);
            double lowerLength = Math.Sqrt(lowerX * lowerX + lowerY * lowerY);
            if (upperLength < 1 || lowerLength < 1) return null;

            double cosine = (upperX * lowerX + upperY * lowerY) / (upperLength * lowerLength);
            cosine = Math.Max(-1, Math.Min(1, cosine));

            return Math.Acos(cosine) * 180.0 / Math.PI;
        }

        /// <summary>The elbow angle of every trusted frame where the arm is visible.</summary>
        public static List<double> ElbowAngles(List<FrameResult> frames)
        {
            List<double> angles = new List<double>();
            foreach (FrameResult frame in frames)
            {
                if (frame.State == TrackState.Lost || frame.Keypoints == null) continue;

                double? angle = ElbowAngle(frame.Keypoints);
                if (angle.HasValue) angles.Add(angle.Value);
            }
            return angles;
        }

        /// <summary>The share (0 to 1) of trusted frames where the check is true.</summary>
        public static double Fraction(List<FrameResult> frames, Func<List<Keypoint>, bool> check)
        {
            int usable = UsableFrames(frames);
            if (usable == 0) return 0;

            int matching = 0;
            foreach (FrameResult frame in frames)
            {
                if (frame.State == TrackState.Lost || frame.Keypoints == null) continue;
                if (check(frame.Keypoints)) matching++;
            }
            return (double)matching / usable;
        }

        /// <summary>
        /// Score for exercises with repetitions (squat, push-up, pull-up):
        ///   posture  = how often the body is in the right position
        ///   movement = does the measured value (angle, ratio) really go up and down?
        /// Standing still around gives movement = 0, so the score stays low.
        /// </summary>
        public static double RepScore(List<FrameResult> frames,
                                      Func<List<Keypoint>, double?> signal,
                                      Func<List<Keypoint>, bool> posture,
                                      double minRange)
        {
            int usable = UsableFrames(frames);
            List<double> values = new List<double>();

            foreach (FrameResult frame in frames)
            {
                if (frame.State == TrackState.Lost || frame.Keypoints == null) continue;
                if (!posture(frame.Keypoints)) continue;

                double? value = signal(frame.Keypoints);
                if (value.HasValue) values.Add(value.Value);
            }

            if (usable == 0 || values.Count < 30) return 0;

            double postureShare = (double)values.Count / usable;
            double movement = Math.Min(1.0, Range(values) / minRange);
            return postureShare * movement;
        }

        /// <summary>Biggest value minus smallest value (using the 5th and 95th percentile, so one glitch doesn't matter).</summary>
        public static double Range(List<double> values)
        {
            if (values.Count == 0) return 0;

            List<double> sorted = new List<double>(values);
            sorted.Sort();
            return Percentile(sorted, 0.95) - Percentile(sorted, 0.05);
        }

        private static double Percentile(List<double> sorted, double p)
        {
            double position = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }

        private static bool BodyDirection(List<Keypoint> kp, out float dx, out float dy)
        {
            dx = 0;
            dy = 0;
            if (kp == null || kp.Count <= Landmark.RightAnkle) return false;

            bool useLeft = kp[Landmark.LeftShoulder].Visibility >= kp[Landmark.RightShoulder].Visibility;
            Keypoint shoulder = kp[useLeft ? Landmark.LeftShoulder : Landmark.RightShoulder];
            Keypoint hip = kp[useLeft ? Landmark.LeftHip : Landmark.RightHip];
            Keypoint ankle = kp[useLeft ? Landmark.LeftAnkle : Landmark.RightAnkle];

            if (shoulder.Visibility < MinVisibility || hip.Visibility < MinVisibility) return false;

            Keypoint lower = ankle.Visibility >= MinVisibility ? ankle : hip;
            dx = Math.Abs(lower.X - shoulder.X);
            dy = Math.Abs(lower.Y - shoulder.Y);
            return true;
        }
    }
}