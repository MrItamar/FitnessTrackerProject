using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    public enum PlankStatus
    {
        NotPlank,      // the body is not horizontal
        Good,          // straight line from shoulder to ankle
        HipsTooLow,    // sagging
        HipsTooHigh    // piked
    }

    /// <summary>
    /// Checks if the person is in a good plank (side view) and counts how long they hold it.
    /// Good plank = the line shoulder -> ankle is nearly horizontal AND the hip is close to that line.
    /// Everything is measured relative to body length, so it works at any video resolution.
    /// </summary>
    public class PlankChecker
    {
        private const float MinVisibility = 0.35f;
        private const double MaxBodyAngleDegrees = 30;   // more tilted than this = not a plank
        private const float HipTolerance = 0.08f;        // hip may be 8% of body length above/below the line

        public int HoldFrames { get; private set; }       // frames with good form
        public int HipsLowFrames { get; private set; }    // frames in a plank with the hips too low
        public int HipsHighFrames { get; private set; }   // frames in a plank with the hips too high
        public PlankStatus Status { get; private set; }

        public bool IsGoodForm
        {
            get { return Status == PlankStatus.Good; }
        }

        /// <summary>Call once per frame.</summary>
        public void Update(List<Keypoint> keypoints)
        {
            Status = CheckForm(keypoints);

            if (Status == PlankStatus.Good) HoldFrames++;
            else if (Status == PlankStatus.HipsTooLow) HipsLowFrames++;
            else if (Status == PlankStatus.HipsTooHigh) HipsHighFrames++;
        }

        public int HoldSeconds(double fps)
        {
            return (int)(HoldFrames / fps);
        }

        private static PlankStatus CheckForm(List<Keypoint> kp)
        {
            if (kp.Count <= Landmark.RightAnkle) return PlankStatus.NotPlank;

            // Use whichever side of the body the camera sees better.
            float leftVisibility = Math.Min(kp[Landmark.LeftShoulder].Visibility,
                                   Math.Min(kp[Landmark.LeftHip].Visibility, kp[Landmark.LeftAnkle].Visibility));
            float rightVisibility = Math.Min(kp[Landmark.RightShoulder].Visibility,
                                    Math.Min(kp[Landmark.RightHip].Visibility, kp[Landmark.RightAnkle].Visibility));

            if (Math.Max(leftVisibility, rightVisibility) < MinVisibility) return PlankStatus.NotPlank;

            bool useLeft = leftVisibility >= rightVisibility;
            Keypoint shoulder = kp[useLeft ? Landmark.LeftShoulder : Landmark.RightShoulder];
            Keypoint hip = kp[useLeft ? Landmark.LeftHip : Landmark.RightHip];
            Keypoint ankle = kp[useLeft ? Landmark.LeftAnkle : Landmark.RightAnkle];

            // 1. Is the body (shoulder -> ankle) roughly horizontal?
            float bodyX = ankle.X - shoulder.X;
            float bodyY = ankle.Y - shoulder.Y;
            float bodyLength = (float)Math.Sqrt(bodyX * bodyX + bodyY * bodyY);
            if (bodyLength < 1f) return PlankStatus.NotPlank;

            double angle = Math.Atan2(Math.Abs(bodyY), Math.Abs(bodyX)) * 180.0 / Math.PI;
            if (angle > MaxBodyAngleDegrees) return PlankStatus.NotPlank;

            // 2. Where is the hip compared to the straight line between shoulder and ankle?
            float howFarAlong = (hip.X - shoulder.X) / bodyX;
            float lineY = shoulder.Y + howFarAlong * bodyY;      // height of the line at the hip's X
            float hipOffset = (hip.Y - lineY) / bodyLength;      // positive = hip is lower on the screen

            if (hipOffset > HipTolerance) return PlankStatus.HipsTooLow;
            if (hipOffset < -HipTolerance) return PlankStatus.HipsTooHigh;
            return PlankStatus.Good;
        }
    }
}