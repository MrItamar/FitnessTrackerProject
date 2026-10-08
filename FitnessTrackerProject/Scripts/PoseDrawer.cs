using FitnessTrackerProject.Scripts;
using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>Everything that draws on the video: skeleton, box, text, and resizing for the window.</summary>
    public static class PoseDrawer
    {
        private const float DrawThreshold = 0.55f;   // only draw points the model is fairly sure about

        // Which points are connected by a line
        private static readonly int[][] Bones =
        {
            new[] { Landmark.LeftShoulder, Landmark.RightShoulder },   // torso
            new[] { Landmark.LeftShoulder, Landmark.LeftHip },
            new[] { Landmark.RightShoulder, Landmark.RightHip },
            new[] { Landmark.LeftHip, Landmark.RightHip },

            new[] { Landmark.LeftShoulder, Landmark.LeftElbow },       // left arm
            new[] { Landmark.LeftElbow, Landmark.LeftWrist },
            new[] { Landmark.RightShoulder, Landmark.RightElbow },     // right arm
            new[] { Landmark.RightElbow, Landmark.RightWrist },

            new[] { Landmark.LeftHip, Landmark.LeftKnee },             // left leg
            new[] { Landmark.LeftKnee, Landmark.LeftAnkle },
            new[] { Landmark.LeftAnkle, Landmark.LeftHeel },
            new[] { Landmark.LeftHeel, Landmark.LeftFootIndex },

            new[] { Landmark.RightHip, Landmark.RightKnee },           // right leg
            new[] { Landmark.RightKnee, Landmark.RightAnkle },
            new[] { Landmark.RightAnkle, Landmark.RightHeel },
            new[] { Landmark.RightHeel, Landmark.RightFootIndex }
        };

        public static void DrawSkeleton(Mat frame, List<Keypoint> keypoints)
        {
            foreach (int[] bone in Bones)
            {
                if (bone[0] >= keypoints.Count || bone[1] >= keypoints.Count) continue;

                Keypoint a = keypoints[bone[0]];
                Keypoint b = keypoints[bone[1]];

                if (a.Visibility > DrawThreshold && b.Visibility > DrawThreshold)
                {
                    Cv2.Line(frame, new Point(a.X, a.Y), new Point(b.X, b.Y), Scalar.LimeGreen, 2, LineTypes.AntiAlias);
                }
            }

            foreach (Keypoint k in keypoints)
            {
                if (k.Visibility > DrawThreshold)
                {
                    Cv2.Circle(frame, new Point(k.X, k.Y), 3, Scalar.Red, -1, LineTypes.AntiAlias);
                }
            }
        }

        /// <summary>Draws the tracking box (useful to see what the tracker is doing).</summary>
        public static void DrawBox(Mat frame, BoundingBox box, Scalar color)
        {
            var rect = new Rect(
                (int)Math.Round(box.X), (int)Math.Round(box.Y),
                (int)Math.Round(box.Width), (int)Math.Round(box.Height));
            Cv2.Rectangle(frame, rect, color, 2);
        }

        /// <summary>Writes text at the left side of the frame, y pixels from the top.</summary>
        public static void PutText(Mat frame, string text, int y, double scale, Scalar color, int thickness)
        {
            Cv2.PutText(frame, text, new Point(20, y), HersheyFonts.HersheySimplex, scale, color, thickness);
        }

        /// <summary>
        /// Shrinks the frame to fit the window and adds black bars so the shape is not distorted.
        /// Only for display: the keypoints are NOT affected.
        /// </summary>
        public static Mat ResizeForDisplay(Mat source, int maxWidth, int maxHeight)
        {
            double scale = Math.Min((double)maxWidth / source.Cols, (double)maxHeight / source.Rows);
            scale = Math.Min(scale, 1.0);   // never enlarge

            int newWidth = Math.Max(1, (int)(source.Cols * scale));
            int newHeight = Math.Max(1, (int)(source.Rows * scale));

            using (var resized = new Mat())
            {
                Cv2.Resize(source, resized, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);

                int padTop = (maxHeight - newHeight) / 2;
                int padBottom = maxHeight - newHeight - padTop;
                int padLeft = (maxWidth - newWidth) / 2;
                int padRight = maxWidth - newWidth - padLeft;

                var padded = new Mat();
                Cv2.CopyMakeBorder(resized, padded, padTop, padBottom, padLeft, padRight, BorderTypes.Constant, Scalar.Black);
                return padded;
            }
        }
    }
}