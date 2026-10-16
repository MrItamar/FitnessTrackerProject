using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;

namespace FitnessTrackerProject.Scripts
{
    public class WorkoutResult
    {
        public string ExerciseName { get; set; }
        public int Value { get; set; }
        public bool IsSeconds { get; set; }
        public string Note { get; set; }
        public bool NoteIsGood { get; set; }
        public int TrackingQuality { get; set; }
        public DateTime Time { get; set; }
    }

    public class WorkoutHistoryItem
    {
        public string ExerciseName { get; set; }
        public int ResultValue { get; set; }
        public string ResultDisplay { get; set; }
        public string ResultNote { get; set; }
        public bool NoteIsGood { get; set; }
        public int TrackingQuality { get; set; }
        public DateTime WorkoutDate { get; set; }
        public string DateText { get { return WorkoutDate.ToString("d MMM, HH:mm"); } }
    }

    public static class WorkoutRepository
    {
        public static void SaveWorkout(int userId, int exerciseId, int resultValue,
                                      string resultNote, bool noteIsGood, int trackingQuality)
        {
            string sql = @"INSERT INTO Workouts 
                           (UserID, ExerciseID, WorkoutDate, ResultValue, ResultNote, NoteIsGood, TrackingQuality) 
                           VALUES (?, ?, ?, ?, ?, ?, ?)";

            DatabaseHelper.ExecuteNonQuery(sql, p =>
            {
                p.AddWithValue("?", userId);
                p.AddWithValue("?", exerciseId);
                p.Add("?", OleDbType.Date).Value = DateTime.Now;
                p.AddWithValue("?", resultValue);
                p.AddWithValue("?", string.IsNullOrWhiteSpace(resultNote) ? "" : resultNote);
                p.AddWithValue("?", noteIsGood);
                p.AddWithValue("?", trackingQuality);
            });
        }

        public static WorkoutHistoryItem GetLatestWorkout(int userId)
        {
            string sql = @"SELECT TOP 1 w.ResultValue, w.ResultNote, w.NoteIsGood, 
                                        w.TrackingQuality, w.WorkoutDate, e.ExerciseName
                           FROM Workouts AS w
                           INNER JOIN ExercisesTable AS e ON w.ExerciseID = e.ExerciseID
                           WHERE w.UserID = ?
                           ORDER BY w.WorkoutDate DESC, w.WorkoutID DESC";

            DataTable dt = DatabaseHelper.ExecuteQueryTable(sql, p => p.AddWithValue("?", userId));
            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            string exName = row["ExerciseName"].ToString();
            int val = Convert.ToInt32(row["ResultValue"]);

            return new WorkoutHistoryItem
            {
                ExerciseName = exName,
                ResultValue = val,
                ResultDisplay = exName.Equals("Plank", StringComparison.OrdinalIgnoreCase)
                                ? $"{val}s"
                                : $"{val} reps",
                ResultNote = row["ResultNote"]?.ToString() ?? "",
                NoteIsGood = Convert.ToBoolean(row["NoteIsGood"]),
                TrackingQuality = Convert.ToInt32(row["TrackingQuality"]),
                WorkoutDate = Convert.ToDateTime(row["WorkoutDate"])
            };
        }

        public static List<WorkoutHistoryItem> GetRecentWorkouts(int userId)
        {
            var list = new List<WorkoutHistoryItem>();

            string sql = @"SELECT TOP 5 w.ResultValue, w.ResultNote, w.NoteIsGood, 
                                        w.TrackingQuality, w.WorkoutDate, e.ExerciseName
                           FROM Workouts AS w
                           INNER JOIN ExercisesTable AS e ON w.ExerciseID = e.ExerciseID
                           WHERE w.UserID = ?
                           ORDER BY w.WorkoutDate DESC, w.WorkoutID DESC";

            DataTable dt = DatabaseHelper.ExecuteQueryTable(sql, p => p.AddWithValue("?", userId));

            foreach (DataRow row in dt.Rows)
            {
                string exName = row["ExerciseName"].ToString();
                int val = Convert.ToInt32(row["ResultValue"]);

                list.Add(new WorkoutHistoryItem
                {
                    ExerciseName = exName,
                    ResultValue = val,
                    ResultDisplay = exName.Equals("Plank", StringComparison.OrdinalIgnoreCase)
                                    ? $"{val}s"
                                    : $"{val} reps",
                    ResultNote = row["ResultNote"]?.ToString() ?? "",
                    NoteIsGood = Convert.ToBoolean(row["NoteIsGood"]),
                    TrackingQuality = Convert.ToInt32(row["TrackingQuality"]),
                    WorkoutDate = Convert.ToDateTime(row["WorkoutDate"])
                });
            }

            return list;
        }
    }
}