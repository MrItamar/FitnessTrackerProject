using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>What every exercise (counter) must be able to do.</summary>
    public interface IExerciseCounter
    {
        /// <summary>Must be the same as ExerciseName in ExercisesTable.</summary>
        string Name { get; }

        /// <summary>0 to 1: how much does this video look like this exercise? (0 = not at all)</summary>
        double Match(List<FrameResult> frames);

        /// <summary>Look at the whole video once and adjust to this person (can do nothing).</summary>
        void Calibrate(List<FrameResult> frames);

        /// <summary>Call once per frame.</summary>
        void Update(List<Keypoint> keypoints);

        /// <summary>The message shown at the end, e.g. "Squats counted: 5".</summary>
        string ResultText(double fps);

        /// <summary>Big text on the playback video, e.g. "Squat - 5".</summary>
        string OverlayText(double fps);

        /// <summary>Small text under it, e.g. "State: UP".</summary>
        string StatusText { get; }
    }
}