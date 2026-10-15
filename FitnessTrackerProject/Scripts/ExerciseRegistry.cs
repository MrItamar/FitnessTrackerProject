using System;
using System.Collections.Generic;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>
    /// The list of all exercises the app can count.
    /// To add an exercise: write a class that implements IExerciseCounter and add ONE line below.
    /// </summary>
    public static class ExerciseRegistry
    {
        // Below this score we say "could not tell which exercise this is"
        private const double MinScore = 0.25;

        // Each line creates a NEW counter (a counter remembers its count, so every video needs its own)
        private static readonly List<Func<IExerciseCounter>> Factories = new List<Func<IExerciseCounter>>
        {
            () => new SquatCounter(),
            () => new PullUpCounter(),
            () => new PushUpCounter(),
            () => new PlankChecker()
        };

        /// <summary>The scores of the last Detect, like "Squat 0.12   Pullup 0.00 ...". Handy while tuning.</summary>
        public static string LastScores { get; private set; } = "";

        /// <summary>A new counter for this exercise name, or null if there is none (yet).</summary>
        public static IExerciseCounter Create(string name)
        {
            foreach (Func<IExerciseCounter> factory in Factories)
            {
                IExerciseCounter counter = factory();
                if (string.Equals(counter.Name, name, StringComparison.OrdinalIgnoreCase)) return counter;
            }
            return null;
        }

        /// <summary>Asks every exercise "does this video look like you?" and returns the best one (or null).</summary>
        public static IExerciseCounter Detect(List<FrameResult> frames, out double bestScore)
        {
            IExerciseCounter best = null;
            bestScore = 0;
            string report = "";

            foreach (Func<IExerciseCounter> factory in Factories)
            {
                IExerciseCounter counter = factory();
                double score = counter.Match(frames);

                report += counter.Name + " " + score.ToString("0.00") + "   ";

                if (score > bestScore)
                {
                    best = counter;
                    bestScore = score;
                }
            }

            LastScores = report;

            if (bestScore < MinScore) return null;
            return best;
        }
    }
}