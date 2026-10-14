using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    public static class ExerciseDetector
    {
        private const float MinVisibility = 0.35f;

        public static string Detect(List<FrameResult> frames)
        {
            int verticalFrames = 0;
            int horizontalFrames = 0;
            int handsAboveShouldersFrames = 0;
            var elbowAngles = new List<double>();

            foreach (FrameResult frame in frames)
            {
                if (frame.State == TrackState.Lost || frame.Keypoints == null) continue;
                List<Keypoint> kp = frame.Keypoints;
                if (kp.Count <= Landmark.RightAnkle) continue;

                bool useLeft = kp[Landmark.LeftShoulder].Visibility >= kp[Landmark.RightShoulder].Visibility;

                Keypoint shoulder = kp[useLeft ? Landmark.LeftShoulder : Landmark.RightShoulder];
                Keypoint elbow = kp[useLeft ? Landmark.LeftElbow : Landmark.RightElbow];
                Keypoint wrist = kp[useLeft ? Landmark.LeftWrist : Landmark.RightWrist];
                Keypoint hip = kp[useLeft ? Landmark.LeftHip : Landmark.RightHip];
                Keypoint ankle = kp[useLeft ? Landmark.LeftAnkle : Landmark.RightAnkle];

                if (shoulder.Visibility < MinVisibility || hip.Visibility < MinVisibility) continue;

                Keypoint lowerPoint = ankle.Visibility >= MinVisibility ? ankle : hip;
                float dx = Math.Abs(lowerPoint.X - shoulder.X);
                float dy = Math.Abs(lowerPoint.Y - shoulder.Y);

                if (dy > dx)
                {
                    verticalFrames++;

                    // Y=0 is the top of the frame, so wrist.Y < shoulder.Y means hands are above shoulders
                    if (wrist.Visibility >= MinVisibility && wrist.Y < shoulder.Y)
                    {
                        handsAboveShouldersFrames++;
                    }
                }
                else
                {
                    horizontalFrames++;

                    if (elbow.Visibility >= MinVisibility && wrist.Visibility >= MinVisibility)
                    {
                        elbowAngles.Add(GetAngle(shoulder, elbow, wrist));
                    }
                }
            }

            // 1. Vertical Exercises: Squat vs Pullup
            if (verticalFrames >= horizontalFrames)
            {
                if (handsAboveShouldersFrames > verticalFrames * 0.35)
                    return "Pullup";

                return "Squat";
            }

            // 2. Horizontal Exercises: Pushup vs Plank
            if (elbowAngles.Count >= 15)
            {
                elbowAngles.Sort();
                double low = elbowAngles[(int)(elbowAngles.Count * 0.05)];
                double high = elbowAngles[(int)(elbowAngles.Count * 0.95)];

                if (high - low >= 35.0)
                    return "Pushup";
            }

            return "Plank";
        }

        private static double GetAngle(Keypoint a, Keypoint b, Keypoint c)
        {
            double upperX = a.X - b.X, upperY = a.Y - b.Y;
            double lowerX = c.X - b.X, lowerY = c.Y - b.Y;

            double upperLen = Math.Sqrt(upperX * upperX + upperY * upperY);
            double lowerLen = Math.Sqrt(lowerX * lowerX + lowerY * lowerY);
            if (upperLen < 1 || lowerLen < 1) return 180;

            double cosine = (upperX * lowerX + upperY * lowerY) / (upperLen * lowerLen);
            cosine = Math.Max(-1, Math.Min(1, cosine));

            return Math.Acos(cosine) * 180.0 / Math.PI;
        }
    }
}