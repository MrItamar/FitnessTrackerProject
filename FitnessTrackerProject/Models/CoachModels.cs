using System.Windows.Media;

namespace FitnessTrackerProject.Models
{
    public class CoachTraineeItem
    {
        public int TraineeId { get; set; }
        public string Name { get; set; }
        public string Initial { get; set; }
        public string Goal { get; set; }
        public string Details { get; set; } // e.g., "Age: 24 · 72kg · 178cm"
        public string PicturePath { get; set; }
        public int TotalWorkouts { get; set; }
    }

    public class TraineeWorkoutActivity
    {
        public string ExerciseName { get; set; }
        public string ResultDisplay { get; set; }  // e.g., "20 reps" or "45s"
        public string DateText { get; set; }       // e.g., "Tue, 14 Oct 16:30"
        public string Note { get; set; }
        public bool NoteIsGood { get; set; }
        public int TrackingQuality { get; set; }
    }
}