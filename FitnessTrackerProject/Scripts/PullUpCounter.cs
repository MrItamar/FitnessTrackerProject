using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>
    /// Counts pull-ups (side view) using the ELBOW ANGLE: shoulder -> elbow -> wrist.
    /// Hanging with straight arms (bottom) = big angle (about 160+), 
    /// Top of the pull-up (chin to bar) = small angle (about 70-90).
    /// A rep is counted when the person pulls UP to the top and lowers back down to a hang.
    /// </summary>
    public class PullUpCounter
    {
        private const float MinVisibility = 0.2f;
        private const double MaxTorsoDeviationDegrees = 30; // torso must be roughly vertical (hanging)
        private const double AngleSmoothing = 0.7;          // smooths the angle so it doesn't jitter
        private const double MinUsefulRange = 40;           // minimum range of motion to calibrate

        private double _hangThreshold = 150;                // default angle for arms straight (hanging)
        private double _pullThreshold = 100;                // default angle for arms bent (top of pull-up)
        private double? _smoothedAngle;

        public int Count { get; private set; }
        public string State { get; private set; } = "BOTTOM";

        /// <summary>The (smoothed) elbow angle of the latest frame, or null when not in a pull-up position.</summary>
        public double? LastAngle { get; private set; }

        /// <summary>Look at all frames once and set the thresholds from this person's own movement.</summary>
        public void Calibrate(List<FrameResult> frames)
        {
            var angles = new List<double>();
            double? smoothed = null;

            foreach (FrameResult frame in frames)
            {
                if (frame.State == TrackState.Lost) continue;   // don't learn from bad frames

                double? raw = GetElbowAngle(frame.Keypoints);
                if (!raw.HasValue) continue;

                smoothed = Smooth(raw.Value, smoothed);
                angles.Add(smoothed.Value);
            }

            if (angles.Count < 30) return;   // too little data: keep defaults

            angles.Sort();
            double low = Percentile(angles, 0.05);   // smallest angle (top of pull-up)
            double high = Percentile(angles, 0.95);  // largest angle (hanging bottom)

            if (high - low < MinUsefulRange) return;   // no real pull-up movement: keep defaults

            _pullThreshold = low + (high - low) * 0.30;
            _hangThreshold = low + (high - low) * 0.70;
        }

        /// <summary>Call once per frame.</summary>
        public void Update(List<Keypoint> keypoints)
        {
            double? raw = GetElbowAngle(keypoints);

            // Not in a pull-up position (or arm not visible): change nothing.
            if (!raw.HasValue)
            {
                LastAngle = null;
                return;
            }

            _smoothedAngle = Smooth(raw.Value, _smoothedAngle);
            double angle = _smoothedAngle.Value;
            LastAngle = angle;

            if (angle < _pullThreshold && State == "BOTTOM")
            {
                State = "TOP";              // the person pulled up to the bar
            }
            else if (angle > _hangThreshold && State == "TOP")
            {
                State = "BOTTOM";           // lowered back down to hang: one full rep
                Count++;
            }
        }

        private static double Smooth(double raw, double? previous)
        {
            if (!previous.HasValue) return raw;
            return previous.Value + AngleSmoothing * (raw - previous.Value);
        }

        /// <summary>Value below which p (0..1) of the numbers lie. The list must be sorted.</summary>
        private static double Percentile(List<double> sorted, double p)
        {
            double position = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }

        /// <summary>
        /// Angle at the elbow in degrees, using whichever arm the camera sees better.
        /// Returns null if the arm isn't visible or the torso isn't roughly vertical.
        /// </summary>
        private static double? GetElbowAngle(List<Keypoint> kp)
        {
            if (kp.Count <= Landmark.RightWrist) return null;

            float leftVisibility = Math.Min(kp[Landmark.LeftShoulder].Visibility,
                                   Math.Min(kp[Landmark.LeftElbow].Visibility, kp[Landmark.LeftWrist].Visibility));
            float rightVisibility = Math.Min(kp[Landmark.RightShoulder].Visibility,
                                    Math.Min(kp[Landmark.RightElbow].Visibility, kp[Landmark.RightWrist].Visibility));

            bool useLeft = leftVisibility >= rightVisibility;
            if (Math.Max(leftVisibility, rightVisibility) < MinVisibility) return null;

            Keypoint shoulder = kp[useLeft ? Landmark.LeftShoulder : Landmark.RightShoulder];
            Keypoint elbow = kp[useLeft ? Landmark.LeftElbow : Landmark.RightElbow];
            Keypoint wrist = kp[useLeft ? Landmark.LeftWrist : Landmark.RightWrist];
            Keypoint hip = kp[useLeft ? Landmark.LeftHip : Landmark.RightHip];

            // Is the torso (shoulder -> hip) roughly vertical? Otherwise this is not a pull-up.
            if (hip.Visibility < MinVisibility) return null;

            // Calculate torso angle relative to the vertical axis (0 degrees = perfectly upright/hanging)
            double torsoDeviation = Math.Atan2(Math.Abs(hip.X - shoulder.X), Math.Abs(hip.Y - shoulder.Y)) * 180.0 / Math.PI;
            if (torsoDeviation > MaxTorsoDeviationDegrees) return null;

            // Angle between the two arm bones, measured at the elbow
            double upperX = shoulder.X - elbow.X, upperY = shoulder.Y - elbow.Y;
            double lowerX = wrist.X - elbow.X, lowerY = wrist.Y - elbow.Y;

            double upperLength = Math.Sqrt(upperX * upperX + upperY * upperY);
            double lowerLength = Math.Sqrt(lowerX * lowerX + lowerY * lowerY);
            if (upperLength < 1 || lowerLength < 1) return null;

            double cosine = (upperX * lowerX + upperY * lowerY) / (upperLength * lowerLength);
            cosine = Math.Max(-1, Math.Min(1, cosine));

            return Math.Acos(cosine) * 180.0 / Math.PI;
        }
    }
}