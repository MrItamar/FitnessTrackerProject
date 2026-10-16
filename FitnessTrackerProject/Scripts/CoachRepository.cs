using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Media;

namespace FitnessTrackerProject.Scripts
{
    public class CoachTraineeItem
    {
        public int TraineeId { get; set; }
        public string Name { get; set; }
        public string Initial { get; set; }
        public string Goal { get; set; }
        public string Details { get; set; }        // e.g., "Age: 24 · 72kg · 178cm"
        public ImageSource Picture { get; set; }
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

    public static class CoachRepository
    {
        /// <summary>
        /// Retrieves all trainees who assigned this coach as their TrainerID.
        /// </summary>
        public static List<CoachTraineeItem> GetTraineesForCoach(int coachId)
        {
            var list = new List<CoachTraineeItem>();

            string query = @"
                SELECT u.ID, u.Username, u.Age, u.ProfilePicPath, 
                       t.FitnessGoal, t.Height, t.Weight
                FROM Users AS u
                INNER JOIN Trainees AS t ON u.ID = t.UserID
                WHERE t.TrainerID = ?
                ORDER BY u.Username ASC";

            DataTable dt = DatabaseHelper.ExecuteQueryTable(query, p => p.AddWithValue("?", coachId));

            foreach (DataRow row in dt.Rows)
            {
                int traineeId = Convert.ToInt32(row["ID"]);
                string name = row["Username"]?.ToString() ?? "Unknown";
                string age = row["Age"] != DBNull.Value ? row["Age"].ToString() : "-";
                string height = row["Height"] != DBNull.Value ? $"{row["Height"]}cm" : "";
                string weight = row["Weight"] != DBNull.Value ? $"{row["Weight"]}kg" : "";

                var parts = new List<string>();
                if (age != "-") parts.Add($"Age {age}");
                if (!string.IsNullOrEmpty(height)) parts.Add(height);
                if (!string.IsNullOrEmpty(weight)) parts.Add(weight);

                int workoutCount = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM Workouts WHERE UserID = ?",
                    p => p.AddWithValue("?", traineeId)));

                list.Add(new CoachTraineeItem
                {
                    TraineeId = traineeId,
                    Name = name,
                    Initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper(),
                    Goal = row["FitnessGoal"] != DBNull.Value ? row["FitnessGoal"].ToString() : "General Fitness",
                    Details = parts.Count > 0 ? string.Join(" · ", parts) : "No metrics logged",
                    Picture = TraineeRepository.LoadPicture(row["ProfilePicPath"]?.ToString()),
                    TotalWorkouts = workoutCount
                });
            }

            return list;
        }

        /// <summary>
        /// Retrieves the recent workout sessions performed by a specific trainee.
        /// </summary>
        public static List<TraineeWorkoutActivity> GetTraineeWorkoutHistory(int traineeId)
        {
            var list = new List<TraineeWorkoutActivity>();

            string query = @"
                SELECT w.ResultValue, w.ResultNote, w.NoteIsGood, 
                       w.TrackingQuality, w.WorkoutDate, e.ExerciseName
                FROM Workouts AS w
                INNER JOIN ExercisesTable AS e ON w.ExerciseID = e.ExerciseID
                WHERE w.UserID = ?
                ORDER BY w.WorkoutDate DESC, w.WorkoutID DESC";

            DataTable dt = DatabaseHelper.ExecuteQueryTable(query, p => p.AddWithValue("?", traineeId));

            foreach (DataRow row in dt.Rows)
            {
                string exName = row["ExerciseName"].ToString();
                int val = Convert.ToInt32(row["ResultValue"]);
                DateTime dtWorkout = Convert.ToDateTime(row["WorkoutDate"]);

                list.Add(new TraineeWorkoutActivity
                {
                    ExerciseName = exName,
                    ResultDisplay = exName.Equals("Plank", StringComparison.OrdinalIgnoreCase)
                                    ? $"{val}s"
                                    : $"{val} reps",
                    DateText = dtWorkout.ToString("ddd, d MMM · HH:mm"),
                    Note = row["ResultNote"]?.ToString() ?? "",
                    NoteIsGood = Convert.ToBoolean(row["NoteIsGood"]),
                    TrackingQuality = Convert.ToInt32(row["TrackingQuality"])
                });
            }

            return list;
        }
    }
}