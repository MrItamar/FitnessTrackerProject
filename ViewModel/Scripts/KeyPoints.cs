using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>One body point found by the model, in VIDEO FRAME pixels.</summary>
    public class Keypoint
    {
        public float X { get; }
        public float Y { get; }
        public float Visibility { get; }   // 0..1, how sure the model is that the point is visible

        public Keypoint(float x, float y, float visibility)
        {
            X = x;
            Y = y;
            Visibility = visibility;
        }
    }

    /// <summary>Names for the MediaPipe landmark numbers, so the code reads "LeftHip" instead of 23.</summary>
    public static class Landmark
    {
        public const int BodyCount = 33;   // the model returns 33 body points (+ a few extra helper points)

        public const int LeftShoulder = 11;
        public const int RightShoulder = 12;
        public const int LeftElbow = 13;
        public const int RightElbow = 14;
        public const int LeftWrist = 15;
        public const int RightWrist = 16;
        public const int LeftHip = 23;
        public const int RightHip = 24;
        public const int LeftKnee = 25;
        public const int RightKnee = 26;
        public const int LeftAnkle = 27;
        public const int RightAnkle = 28;
        public const int LeftHeel = 29;
        public const int RightHeel = 30;
        public const int LeftFootIndex = 31;
        public const int RightFootIndex = 32;
    }

    /// <summary>The part of the frame we show to the model (it follows the person).</summary>
    public class BoundingBox
    {
        public float X, Y, Width, Height;

        public static BoundingBox FullFrame(int frameWidth, int frameHeight)
        {
            return new BoundingBox { X = 0, Y = 0, Width = frameWidth, Height = frameHeight };
        }
    }

    /// <summary>What the tracker thinks about one frame.</summary>
    public enum TrackState
    {
        Searching,   // still looking for the person (box = whole frame)
        Tracking,    // person found, box follows them
        Lost         // this frame is not trustworthy, box is frozen
    }

    /// <summary>Everything we remember about one frame after pass 1.</summary>
    public class FrameResult
    {
        public List<Keypoint> Keypoints;
        public BoundingBox Box;      // the box that was used to find these keypoints
        public TrackState State;
        public DistanceStatus Distance;
    }
}