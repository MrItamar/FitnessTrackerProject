using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FitnessTrackerProject.Scripts
{
    public class WorkoutResult
    {
        public int Value { get; set; }            // reps, or seconds for the plank
        public string Note { get; set; }          // short badge text, e.g. "Good form"
        public bool NoteIsGood { get; set; }      // green badge if true, amber if false
        public int TrackingQuality { get; set; }  // 0..100, share of frames the tracker trusted
    }
}
