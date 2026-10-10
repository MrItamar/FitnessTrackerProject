using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>
    /// Moves the crop box so it follows the person. It starts as the whole frame, then
    /// shrinks around the body. Safety rules stop one bad frame from ruining the box ("collapse").
    /// </summary>
    public class BoxTracker
    {
        // --- your safety rules ---
        private const float MinBoxHeightFraction = 0.5f;   // box side is at least half the frame height
        private const float MaxJumpFraction = 0.15f;       // moves bigger than 15% of the frame are ignored
        private const int MinVisibleKeypoints = 8;         // fewer visible points -> don't touch the box
        private const float MinVisibility = 0.3f;

        // --- box shape and movement ---
        private const float Padding = 1.1f;               // a bit of space around the body
        private const float MoveSmoothing = 0.2f;          // the box center moves 20% of the way each frame
        private const float GrowSmoothing = 0.5f;          // the box may GROW quickly...
        private const float ShrinkSmoothing = 0.05f;       // ...but shrinks slowly, so it can't collapse

        // --- using the model's own "person present" score ---
        private const bool UsePoseConfidence = true;       // set to false if good frames are marked "Lost"
        private const float PoseConfidenceThreshold = 0.5f;
        private const int ReacquireAfterLostFrames = 30;   // lost this long -> search the whole frame again (0 = never)

        private readonly int _frameWidth;
        private readonly int _frameHeight;
        private bool _hasLocked;                           // false until we find the person
        private int _lostFrames;

        /// <summary>The box to use for the NEXT frame.</summary>
        public BoundingBox Box { get; private set; }

        /// <summary>True if the tracker just gave up and went back to searching the whole frame.</summary>
        public bool ReacquiredThisFrame { get; private set; }

        public BoxTracker(int frameWidth, int frameHeight)
        {
            _frameWidth = frameWidth;
            _frameHeight = frameHeight;
            Box = BoundingBox.FullFrame(frameWidth, frameHeight);
        }

        /// <summary>
        /// Call once per frame with the keypoints the model just found and the model's confidence
        /// (-1 if the model has none). Returns what we think about THIS frame.
        /// </summary>
        public TrackState Update(List<Keypoint> keypoints, float poseConfidence)
        {
            ReacquiredThisFrame = false;

            bool modelSaysNoPerson = UsePoseConfidence &&
                                     poseConfidence >= 0f &&
                                     poseConfidence < PoseConfidenceThreshold;

            float centerX, centerY, bodySize;
            bool bodyFound = MeasureBody(keypoints, out centerX, out centerY, out bodySize);

            // 1. Still searching: wait for a body, then lock on to it.
            if (!_hasLocked)
            {
                if (bodyFound)
                {
                    Box = MakeBox(centerX, centerY, bodySize);
                    _hasLocked = true;
                    _lostFrames = 0;
                }
                return TrackState.Searching;
            }

            // 2. Bad frame: the box stays EXACTLY where it is.
            if (modelSaysNoPerson || !bodyFound)
            {
                _lostFrames++;

                if (ReacquireAfterLostFrames > 0 && _lostFrames >= ReacquireAfterLostFrames)
                {
                    Box = BoundingBox.FullFrame(_frameWidth, _frameHeight);
                    _hasLocked = false;
                    _lostFrames = 0;
                    ReacquiredThisFrame = true;
                }
                return TrackState.Lost;
            }

            // 3. Good frame: move the box toward the body.
            _lostFrames = 0;
            MoveBox(centerX, centerY, bodySize);
            return TrackState.Tracking;
        }

        /// <summary>
        /// Finds where the body is and how big it is.
        /// Center = the middle of the hips (stays put even if the feet disappear), otherwise the middle of all points.
        /// Size = twice the distance to the farthest visible point.
        /// Only the 33 body points count, and only if they are visible and roughly inside the frame.
        /// </summary>
        private bool MeasureBody(List<Keypoint> keypoints, out float centerX, out float centerY, out float bodySize)
        {
            centerX = 0;
            centerY = 0;
            bodySize = 0;

            float marginX = _frameWidth * 0.25f;
            float marginY = _frameHeight * 0.25f;

            var visible = keypoints
                .Take(Landmark.BodyCount)
                .Where(k => k.Visibility > MinVisibility &&
                            k.X > -marginX && k.X < _frameWidth + marginX &&
                            k.Y > -marginY && k.Y < _frameHeight + marginY)
                .ToList();

            // RULE: too few points -> we can't measure -> caller keeps the old box.
            if (visible.Count < MinVisibleKeypoints) return false;

            if (keypoints.Count > Landmark.RightHip &&
                keypoints[Landmark.LeftHip].Visibility > 0.5f &&
                keypoints[Landmark.RightHip].Visibility > 0.5f)
            {
                centerX = (keypoints[Landmark.LeftHip].X + keypoints[Landmark.RightHip].X) / 2f;
                centerY = (keypoints[Landmark.LeftHip].Y + keypoints[Landmark.RightHip].Y) / 2f;
            }
            else
            {
                centerX = (visible.Min(k => k.X) + visible.Max(k => k.X)) / 2f;
                centerY = (visible.Min(k => k.Y) + visible.Max(k => k.Y)) / 2f;
            }

            float farthest = 0;
            foreach (Keypoint k in visible)
            {
                float distance = Math.Max(Math.Abs(k.X - centerX), Math.Abs(k.Y - centerY));
                farthest = Math.Max(farthest, distance);
            }

            bodySize = farthest * 2f;
            return true;
        }

        /// <summary>The box for the first lock: goes straight to the body.</summary>
        private BoundingBox MakeBox(float centerX, float centerY, float bodySize)
        {
            float size = LimitSize(bodySize * Padding);
            return new BoundingBox
            {
                X = KeepInside(centerX - size / 2f, size, _frameWidth),
                Y = KeepInside(centerY - size / 2f, size, _frameHeight),
                Width = size,
                Height = size
            };
        }

        /// <summary>Normal update: grow fast, shrink slowly, ignore huge jumps, move smoothly.</summary>
        private void MoveBox(float centerX, float centerY, float bodySize)
        {
            float currentSize = Math.Max(Box.Width, Box.Height);
            float wantedSize = LimitSize(bodySize * Padding);

            float sizeSpeed = wantedSize > currentSize ? GrowSmoothing : ShrinkSmoothing;
            float newSize = LimitSize(currentSize + sizeSpeed * (wantedSize - currentSize));

            // Where the top-left corner would be if the box were exactly on the body
            float targetX = KeepInside(centerX - newSize / 2f, newSize, _frameWidth);
            float targetY = KeepInside(centerY - newSize / 2f, newSize, _frameHeight);

            // RULE: a huge jump is almost certainly a bad prediction -> ignore it, keep the old box.
            float dx = targetX - Box.X;
            float dy = targetY - Box.Y;
            float maxJump = MaxJumpFraction * Math.Max(_frameWidth, _frameHeight);
            if (Math.Sqrt(dx * dx + dy * dy) > maxJump) return;

            // Glide the CENTER toward the body (so a size change doesn't push the box sideways).
            float oldCenterX = Box.X + Box.Width / 2f;
            float oldCenterY = Box.Y + Box.Height / 2f;
            float newCenterX = oldCenterX + MoveSmoothing * (centerX - oldCenterX);
            float newCenterY = oldCenterY + MoveSmoothing * (centerY - oldCenterY);

            Box = new BoundingBox
            {
                X = KeepInside(newCenterX - newSize / 2f, newSize, _frameWidth),
                Y = KeepInside(newCenterY - newSize / 2f, newSize, _frameHeight),
                Width = newSize,
                Height = newSize
            };
        }

        /// <summary>RULE: at least half the frame height. Also never bigger than the longest frame side.</summary>
        private float LimitSize(float size)
        {
            size = Math.Max(size, _frameHeight * MinBoxHeightFraction);
            return Math.Min(size, Math.Max(_frameWidth, _frameHeight));
        }

        /// <summary>Keeps the box inside the frame. If it is bigger than the frame (long plank), center it.</summary>
        private static float KeepInside(float position, float size, float frameLength)
        {
            if (size >= frameLength) return (frameLength - size) / 2f;
            return Math.Max(0f, Math.Min(position, frameLength - size));
        }
    }
}