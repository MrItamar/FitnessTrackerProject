using System.Collections.Generic;

namespace FitnessTrackerProject.Models
{
    /// <summary>One exercise inside a program day.</summary>
    public class CoachExercise
    {
        public int ExerciseId { get; set; }
        public string Name { get; set; }
        public string Muscle { get; set; }
        public string Sets { get; set; } = "3";
        public string Reps { get; set; } = "10";
        public string Rest { get; set; } = "60s";
    }

    /// <summary>One day of the program (Day 1, Day 2, ...).</summary>
    public class CoachDay
    {
        public string Title { get; set; }
        public string Focus { get; set; } = "";
        public bool IsRest { get { return Exercises.Count == 0; } }
        public string Label { get { return IsRest ? Title + " · Rest" : Title; } }
        public List<CoachExercise> Exercises { get; set; } = new List<CoachExercise>();
    }

    /// <summary>An exercise definition loaded from ExercisesTable.</summary>
    public class ExerciseModel
    {
        public int Id;
        public string Name;
        public string TargetMuscle;
        public string Description;
        public string Hint;
        public bool IsHorizontal;
    }
}