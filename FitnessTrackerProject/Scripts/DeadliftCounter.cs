using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>
    /// Counts barbell deadlifts (side view) using HIP HEIGHT relative to shin length:
    /// (ankle.Y - hip.Y) / (ankle.Y - knee.Y). Standing about 2.0, bar on the floor about 1.2-1.4.
    /// Only hips, knees and ankles are used, because shoulders and hands are often cut off
    /// or hidden behind the plates. Both sides are averaged so the value can't jump between sides.
    /// A rep is counted when the person goes DOWN and comes back UP to lockout.
    /// </summary>
    public class DeadliftCounter : IExerciseCounter
    {
        private const float MinVisibility = 0.25f;
        private const double Smoothing = 0.6;
        private const double MinUsefulRange = 0.3;        // ratio units; if the hips barely moved, use the defaults

        private double _upThreshold = 1.75;               // defaults, used if calibration finds too little data
        private double _downThreshold = 1.45;
        private double? _smoothedRatio;

        public int Count { get; private set; }
        public string State { get; private set; } = "UP";
        public string Name { get { return "Deadlift"; } }
        public string ResultText(double fps) { return "Deadlifts counted: " + Count; }
        public string OverlayText(double fps) { return "Deadlift - " + Count; }
        public string StatusText { get { return "State: " + State; } }
        public bool IsHorizontal { get { return false; } }

        public double? LastRatio { get; private set; }

        // A deadlift: hands stay at hip level or lower, and the hip height goes up and down
        public double Match(List<FrameResult> frames)
        {
            return PoseChecks.RepScore(frames, GetHipHeightRatio, HandsAtHipLevelOrBelow, MinUsefulRange);
        }

        public void Calibrate(List<FrameResult> frames)
        {
            var ratios = new List<double>();
            double? smoothed = null;

            foreach (FrameResult frame in frames)
            {
                if (frame.State == TrackState.Lost) continue;

                double? raw = GetHipHeightRatio(frame.Keypoints);
                if (!raw.HasValue) continue;

                smoothed = Smooth(raw.Value, smoothed);
                ratios.Add(smoothed.Value);
            }

            if (ratios.Count < 30) return;

            ratios.Sort();
            double low = Percentile(ratios, 0.05);
            double high = Percentile(ratios, 0.95);

            if (high - low < MinUsefulRange) return;

            _upThreshold = low + (high - low) * 0.70;
            _downThreshold = low + (high - low) * 0.30;
        }

        public void Update(List<Keypoint> keypoints)
        {
            double? raw = GetHipHeightRatio(keypoints);

            if (!raw.HasValue)
            {
                LastRatio = null;
                return;
            }

            _smoothedRatio = Smooth(raw.Value, _smoothedRatio);
            double ratio = _smoothedRatio.Value;
            LastRatio = ratio;

            if (ratio < _downThreshold && State == "UP")
            {
                State = "DOWN";
            }
            else if (ratio > _upThreshold && State == "DOWN")
            {
                State = "UP";
                Count++;                      // down and back up to lockout = one rep
            }
        }

        private static double Smooth(double raw, double? previous)
        {
            if (!previous.HasValue) return raw;
            return previous.Value + Smoothing * (raw - previous.Value);
        }

        private static double Percentile(List<double> sorted, double p)
        {
            double position = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }

        /// <summary>
        /// Averages hip, knee and ankle height over every side whose three joints are visible.
        /// Returns false if neither side is visible.
        /// </summary>
        private static bool TryGetLegs(List<Keypoint> kp, out double hipY, out double kneeY, out double ankleY)
        {
            hipY = kneeY = ankleY = 0;
            if (kp.Count <= Landmark.RightAnkle) return false;

            int sides = 0;
            int[][] legs =
            {
                new[] { Landmark.LeftHip, Landmark.LeftKnee, Landmark.LeftAnkle },
                new[] { Landmark.RightHip, Landmark.RightKnee, Landmark.RightAnkle }
            };

            foreach (int[] leg in legs)
            {
                Keypoint h = kp[leg[0]], k = kp[leg[1]], a = kp[leg[2]];
                if (h.Visibility < MinVisibility || k.Visibility < MinVisibility || a.Visibility < MinVisibility) continue;

                hipY += h.Y; kneeY += k.Y; ankleY += a.Y;
                sides++;
            }

            if (sides == 0) return false;
            hipY /= sides; kneeY /= sides; ankleY /= sides;
            return true;
        }

        /// <summary>Hip height divided by shin length, or null if the legs can't be seen properly.</summary>
        private static double? GetHipHeightRatio(List<Keypoint> kp)
        {
            double hipY, kneeY, ankleY;
            if (!TryGetLegs(kp, out hipY, out kneeY, out ankleY)) return null;

            double shin = ankleY - kneeY;
            if (shin <= 1) return null;

            double ratio = (ankleY - hipY) / shin;
            if (ratio < 0.3 || ratio > 4) return null;   // impossible value = tracking glitch, ignore

            return ratio;
        }

        /// <summary>
        /// True when the hands are at hip level or lower (Y grows downward).
        /// Separates a deadlift from a squat, where the hands are at the chest or in front.
        /// Uses only wrists, hips, knees and ankles (no shoulders, which are often cut off).
        /// </summary>
        public static bool HandsAtHipLevelOrBelow(List<Keypoint> kp)
        {
            double hipY, kneeY, ankleY;
            if (!TryGetLegs(kp, out hipY, out kneeY, out ankleY)) return false;
            if (kp.Count <= Landmark.RightWrist) return false;

            double wristY = 0;
            int count = 0;
            foreach (int w in new[] { Landmark.LeftWrist, Landmark.RightWrist })
            {
                if (kp[w].Visibility < MinVisibility) continue;
                wristY += kp[w].Y;
                count++;
            }
            if (count == 0) return false;
            wristY /= count;

            double shin = ankleY - kneeY;
            if (shin <= 1) return false;

            return wristY >= hipY - 0.3 * shin;   // tolerance for tracking noise
        }
    }
}