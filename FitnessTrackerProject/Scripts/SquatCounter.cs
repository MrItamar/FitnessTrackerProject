using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>
    /// Counts squats using the "leg ratio" = thigh height / calf height (vertical distances).
    /// Standing the ratio is about 1, at the bottom of a squat it is close to 0.
    /// Because every person is different, Calibrate() first looks at the whole video to find
    /// this person's highest and lowest ratio, then sets the UP and DOWN thresholds between them.
    /// </summary>
    public class SquatCounter
    {
        private const double RatioSmoothing = 0.4;       // smooths the ratio so it doesn't jitter around a threshold
        private const double MinUsefulRange = 0.25;      // if the person barely moved, use the default thresholds

        private double _upThreshold = 0.76;              // defaults, used if calibration finds too little data
        private double _downThreshold = 0.44;
        private double? _smoothedRatio;

        public int Count { get; private set; }
        public string State { get; private set; } = "UP";

        /// <summary>The (smoothed) ratio of the latest frame, or null when the legs were not visible.</summary>
        public double? LastRatio { get; private set; }

        /// <summary>
        /// Look at all frames once and set the thresholds from this person's own movement.
        /// We use the 5th and 95th percentile instead of the minimum and maximum, so a single
        /// tracking glitch can't ruin the thresholds.
        /// </summary>
        public void Calibrate(List<FrameResult> frames)
        {
            var ratios = new List<double>();
            double? smoothed = null;

            foreach (FrameResult frame in frames)
            {
                if (frame.State == TrackState.Lost) continue;   // don't learn from bad frames

                double? raw = GetRatio(frame.Keypoints);
                if (!raw.HasValue) continue;

                smoothed = Smooth(raw.Value, smoothed);
                ratios.Add(smoothed.Value);
            }

            if (ratios.Count < 30) return;   // too little data: keep the defaults

            ratios.Sort();
            double low = Percentile(ratios, 0.05);
            double high = Percentile(ratios, 0.95);

            if (high - low < MinUsefulRange) return;   // no real squatting movement: keep the defaults

            _upThreshold = low + (high - low) * 0.70;
            _downThreshold = low + (high - low) * 0.30;
        }

        /// <summary>Call once per frame during playback.</summary>
        public void Update(List<Keypoint> keypoints)
        {
            double? raw = GetRatio(keypoints);

            // Legs not visible: change nothing (otherwise a short dropout could count a fake rep).
            if (!raw.HasValue)
            {
                LastRatio = null;
                return;
            }

            _smoothedRatio = Smooth(raw.Value, _smoothedRatio);
            double ratio = _smoothedRatio.Value;
            LastRatio = ratio;

            if (ratio > _upThreshold)
            {
                State = "UP";
            }
            else if (ratio < _downThreshold && State == "UP")
            {
                State = "DOWN";
                Count++;                         // one full trip down = one rep
            }
        }

        private static double Smooth(double raw, double? previous)
        {
            if (!previous.HasValue) return raw;
            return previous.Value + RatioSmoothing * (raw - previous.Value);
        }

        /// <summary>Value below which p (0..1) of the numbers lie. The list must be sorted.</summary>
        private static double Percentile(List<double> sorted, double p)
        {
            double position = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }

        /// <summary>Ratio of the more-bent leg, or null if the legs can't be seen properly.</summary>
        private static double? GetRatio(List<Keypoint> keypoints)
        {
            double? left = GetLegRatio(keypoints, Landmark.LeftHip, Landmark.LeftKnee, Landmark.LeftAnkle);
            double? right = GetLegRatio(keypoints, Landmark.RightHip, Landmark.RightKnee, Landmark.RightAnkle);

            if (left.HasValue && right.HasValue) return Math.Min(left.Value, right.Value);
            return left ?? right;
        }

        private static double? GetLegRatio(List<Keypoint> keypoints, int hipIndex, int kneeIndex, int ankleIndex)
        {
            if (keypoints.Count <= ankleIndex) return null;

            Keypoint hip = keypoints[hipIndex];
            Keypoint knee = keypoints[kneeIndex];
            Keypoint ankle = keypoints[ankleIndex];

            if (hip.Visibility < 0.35f || knee.Visibility < 0.35f || ankle.Visibility < 0.35f) return null;

            float thigh = knee.Y - hip.Y;
            float calf = ankle.Y - knee.Y;
            if (calf <= 1f) return null;

            double ratio = thigh / calf;
            if (ratio < -0.5 || ratio > 1.5) return null;   // impossible value = tracking glitch, ignore

            return ratio;
        }
    }
}