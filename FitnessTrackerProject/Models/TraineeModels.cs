using System.Collections.Generic;
using System.Windows.Media;

namespace FitnessTrackerProject.Models
{
    public class TrainerItem
    {
        public int Id { get; set; }                 // = Users.ID of the trainer
        public string Name { get; set; }
        public string Initial { get; set; }
        public string Specialty { get; set; }
        public string Bio { get; set; }
        public string Stats { get; set; }
        public string PicturePath { get; set; }
        public bool IsSelected { get; set; }
        public string ButtonText { get { return IsSelected ? "Selected \u2713" : "Choose trainer"; } }
    }

    public class ProgramChoiceItem
    {
        public int Id { get; set; }                 // = ProgramID
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public string Stats { get; set; }
        public int CoachId { get; set; }
        public string CoachName { get; set; }
        public string Initial { get; set; }
        public string PicturePath { get; set; }
        public bool IsSelected { get; set; }        // true = the trainee added this program
        public string ButtonText { get { return IsSelected ? "Remove" : "Add program"; } }
    }

    public class ProgramExerciseItem
    {
        public string Name { get; set; }
        public string Details { get; set; }         // "Sets 3 · Reps 10 · Rest 60"
    }

    public class ProgramDayItem
    {
        public string Title { get; set; }           // "Day 1 · Push"
        public List<ProgramExerciseItem> Exercises { get; set; }
    }
}