using System;
using System.Collections.Generic;
using System.Data;
using FitnessTrackerProject.Models;

namespace ViewModel.Scripts
{
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

                int workoutCount = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM Workouts WHERE UserID = ?",
                    p => p.AddWithValue("?", traineeId)));

                list.Add(new CoachTraineeItem
                {
                    TraineeId = traineeId,
                    Name = name,
                    Initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper(),
                    Goal = row["FitnessGoal"] != DBNull.Value ? row["FitnessGoal"].ToString() : "General Fitness",
                    Details = BuildDetailsString(row),
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

        /// <summary>
        /// Retrieves a single trainee's profile summary by their UserID.
        /// </summary>
        public static CoachTraineeItem GetTraineeById(int traineeId)
        {
            string sql = @"
                SELECT Users.ID, Users.Username, Users.Age, 
                       Trainees.FitnessGoal, Trainees.Height, Trainees.Weight
                FROM Users 
                INNER JOIN Trainees ON Users.ID = Trainees.UserID
                WHERE Users.ID = ?";

            var dt = DatabaseHelper.ExecuteQueryTable(sql, p => p.AddWithValue("?", traineeId));

            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];

                return new CoachTraineeItem
                {
                    TraineeId = Convert.ToInt32(row["ID"]),
                    Name = row["Username"].ToString(),
                    Goal = row["FitnessGoal"] == DBNull.Value ? "General Fitness" : row["FitnessGoal"].ToString(),
                    Details = BuildDetailsString(row)
                };
            }

            return null;
        }

        private static string BuildDetailsString(DataRow row)
        {
            string age = row["Age"] != DBNull.Value ? row["Age"].ToString() : "-";
            string height = row["Height"] != DBNull.Value ? $"{row["Height"]}cm" : "";
            string weight = row["Weight"] != DBNull.Value ? $"{row["Weight"]}kg" : "";

            var parts = new List<string>();
            if (age != "-") parts.Add($"Age {age}");
            if (!string.IsNullOrEmpty(height)) parts.Add(height);
            if (!string.IsNullOrEmpty(weight)) parts.Add(weight);

            return parts.Count > 0 ? string.Join(" · ", parts) : "No metrics logged";
        }
        public static bool IsCoach(int userId)
        {
            object role = DatabaseHelper.ExecuteScalar(
                "SELECT UserRole FROM Users WHERE ID = ?",
                p => p.AddWithValue("?", userId));

            return role != null && role != DBNull.Value &&
                   string.Equals(role.ToString().Trim(), "Coach", StringComparison.OrdinalIgnoreCase);
        }
    }
}