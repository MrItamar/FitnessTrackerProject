using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FitnessTrackerProject.Scripts
{
    public class TrainerItem
    {
        public int Id { get; set; }                 // = Users.ID of the trainer
        public string Name { get; set; }
        public string Initial { get; set; }
        public string Specialty { get; set; }
        public string Bio { get; set; }
        public string Stats { get; set; }
        public ImageSource Picture { get; set; }
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
        public ImageSource Picture { get; set; }    // the COACH's profile picture
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

    public static class TraineeRepository
    {
        private static bool _schemaChecked = false;

        private const string ProgramSelect =
            "SELECT ProgramsTable.ProgramID, ProgramsTable.ProgramName, ProgramsTable.Goal, " +
            "ProgramsTable.ProgramLevel, ProgramsTable.CoachID, Users.Username, Users.ProfilePicPath " +
            "FROM ProgramsTable INNER JOIN Users ON ProgramsTable.CoachID = Users.ID ";

        // ================================================================
        // Database upgrade (runs once per app start, does nothing if already done)
        // ================================================================
        private static void EnsureSchema()
        {
            if (_schemaChecked) return;

            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                // 1. Trainees.TrainerID column (the dedicated trainer)
                DataTable columns = connection.GetSchema("Columns", new string[] { null, null, "Trainees", null });
                bool hasTrainer = false;
                foreach (DataRow row in columns.Rows)
                {
                    if (string.Equals(row["COLUMN_NAME"].ToString(), "TrainerID", StringComparison.OrdinalIgnoreCase))
                        hasTrainer = true;
                }
                if (!hasTrainer) RunDdl(connection, "ALTER TABLE Trainees ADD COLUMN TrainerID LONG");

                // 2. TraineePrograms table (one row per program a trainee added)
                DataTable tables = connection.GetSchema("Tables", new string[] { null, null, "TraineePrograms", "TABLE" });
                if (tables.Rows.Count == 0)
                    RunDdl(connection, "CREATE TABLE TraineePrograms (TraineeID LONG, ProgramID LONG)");
            }

            _schemaChecked = true;
        }

        private static void RunDdl(OleDbConnection connection, string sql)
        {
            using (var command = new OleDbCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        // ================================================================
        // Trainee info
        // ================================================================

        public static bool IsTrainee(int userId)
        {
            int count = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM Trainees WHERE UserID = ?",
                p => p.AddWithValue("?", userId)));
            return count > 0;
        }

        /// <summary>The ID of the trainee's dedicated trainer, or null if there is none.</summary>
        public static int? GetTrainerId(int traineeId)
        {
            EnsureSchema();

            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                "SELECT TrainerID FROM Trainees WHERE UserID = ?",
                p => p.AddWithValue("?", traineeId));

            if (dt.Rows.Count == 0) return null;
            if (dt.Rows[0]["TrainerID"] == DBNull.Value) return null;
            return Convert.ToInt32(dt.Rows[0]["TrainerID"]);
        }

        /// <summary>The IDs of all programs the trainee added.</summary>
        public static HashSet<int> GetProgramIds(int traineeId)
        {
            EnsureSchema();

            HashSet<int> ids = new HashSet<int>();
            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                "SELECT ProgramID FROM TraineePrograms WHERE TraineeID = ?",
                p => p.AddWithValue("?", traineeId));

            foreach (DataRow row in dt.Rows)
            {
                ids.Add(Convert.ToInt32(row["ProgramID"]));
            }
            return ids;
        }

        // ================================================================
        // Trainers
        // ================================================================

        public static List<TrainerItem> GetTrainers(int? selectedTrainerId)
        {
            List<TrainerItem> list = new List<TrainerItem>();

            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                "SELECT Users.ID, Users.Username, Users.ProfilePicPath, Trainers.Specialty, Trainers.Bio " +
                "FROM Users INNER JOIN Trainers ON Users.ID = Trainers.UserID " +
                "ORDER BY Users.Username");

            foreach (DataRow row in dt.Rows)
            {
                int id = Convert.ToInt32(row["ID"]);
                string name = Text(row, "Username");

                int programs = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM ProgramsTable WHERE CoachID = ?",
                    p => p.AddWithValue("?", id)));

                TrainerItem item = new TrainerItem();
                item.Id = id;
                item.Name = name;
                item.Initial = FirstLetter(name);
                item.Specialty = Text(row, "Specialty");
                item.Bio = Text(row, "Bio");
                item.Stats = programs + (programs == 1 ? " program" : " programs");
                item.Picture = LoadPicture(Text(row, "ProfilePicPath"));
                item.IsSelected = selectedTrainerId.HasValue && selectedTrainerId.Value == id;
                list.Add(item);
            }

            return list;
        }

        // ================================================================
        // Programs
        // ================================================================

        /// <summary>All programs. The ones in selectedIds get IsSelected = true.</summary>
        public static List<ProgramChoiceItem> GetPrograms(HashSet<int> selectedIds)
        {
            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                ProgramSelect + "ORDER BY ProgramsTable.CreatedDate DESC");
            return BuildPrograms(dt, selectedIds);
        }

        /// <summary>All programs made by one coach (the trainee's dedicated trainer).</summary>
        public static List<ProgramChoiceItem> GetProgramsByCoach(int coachId)
        {
            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                ProgramSelect + "WHERE ProgramsTable.CoachID = ? ORDER BY ProgramsTable.CreatedDate DESC",
                p => p.AddWithValue("?", coachId));
            return BuildPrograms(dt, null);
        }

        /// <summary>The programs this trainee added.</summary>
        public static List<ProgramChoiceItem> GetChosenPrograms(int traineeId)
        {
            EnsureSchema();

            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                ProgramSelect +
                "WHERE ProgramsTable.ProgramID IN (SELECT ProgramID FROM TraineePrograms WHERE TraineeID = ?) " +
                "ORDER BY ProgramsTable.CreatedDate DESC",
                p => p.AddWithValue("?", traineeId));
            return BuildPrograms(dt, null);
        }

        /// <summary>One program, or null if it doesn't exist any more.</summary>
        public static ProgramChoiceItem GetProgramById(int programId)
        {
            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                ProgramSelect + "WHERE ProgramsTable.ProgramID = ?",
                p => p.AddWithValue("?", programId));

            List<ProgramChoiceItem> list = BuildPrograms(dt, null);
            if (list.Count == 0) return null;
            return list[0];
        }

        /// <summary>The days of a program, each with its exercises in order.</summary>
        public static List<ProgramDayItem> GetProgramDays(int programId)
        {
            List<ProgramDayItem> days = new List<ProgramDayItem>();

            DataTable dayTable = DatabaseHelper.ExecuteQueryTable(
                "SELECT DayID, DayNumber, DayFocus FROM ProgramDaysTable WHERE ProgramID = ? ORDER BY DayNumber",
                p => p.AddWithValue("?", programId));

            foreach (DataRow dayRow in dayTable.Rows)
            {
                int dayId = Convert.ToInt32(dayRow["DayID"]);
                string focus = Text(dayRow, "DayFocus");

                ProgramDayItem day = new ProgramDayItem();
                day.Title = "Day " + Convert.ToInt32(dayRow["DayNumber"]);
                if (focus != "") day.Title = day.Title + " \u00B7 " + focus;
                day.Exercises = new List<ProgramExerciseItem>();

                DataTable exerciseTable = DatabaseHelper.ExecuteQueryTable(
                    "SELECT ExercisesTable.ExerciseName, ProgramExercisesTable.Sets, " +
                    "ProgramExercisesTable.Reps, ProgramExercisesTable.RestTime " +
                    "FROM ProgramExercisesTable INNER JOIN ExercisesTable " +
                    "ON ProgramExercisesTable.ExerciseID = ExercisesTable.ExerciseID " +
                    "WHERE ProgramExercisesTable.DayID = ? ORDER BY ProgramExercisesTable.SortOrder",
                    p => p.AddWithValue("?", dayId));

                foreach (DataRow exerciseRow in exerciseTable.Rows)
                {
                    string details = "";
                    string sets = Text(exerciseRow, "Sets");
                    string reps = Text(exerciseRow, "Reps");
                    string rest = Text(exerciseRow, "RestTime");

                    if (sets != "") details = details + "Sets " + sets;
                    if (reps != "")
                    {
                        if (details != "") details = details + " \u00B7 ";
                        details = details + "Reps " + reps;
                    }
                    if (rest != "")
                    {
                        if (details != "") details = details + " \u00B7 ";
                        details = details + "Rest " + rest;
                    }

                    ProgramExerciseItem exercise = new ProgramExerciseItem();
                    exercise.Name = Text(exerciseRow, "ExerciseName");
                    exercise.Details = details;
                    day.Exercises.Add(exercise);
                }

                days.Add(day);
            }

            return days;
        }

        // Turns the rows of a program query into items for the screen
        private static List<ProgramChoiceItem> BuildPrograms(DataTable dt, HashSet<int> selectedIds)
        {
            List<ProgramChoiceItem> list = new List<ProgramChoiceItem>();

            foreach (DataRow row in dt.Rows)
            {
                int id = Convert.ToInt32(row["ProgramID"]);
                string coachName = Text(row, "Username");
                string goal = Text(row, "Goal");
                string level = Text(row, "ProgramLevel");

                int days = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM ProgramDaysTable WHERE ProgramID = ?",
                    p => p.AddWithValue("?", id)));

                string subtitle = goal;
                if (goal != "" && level != "") subtitle = goal + " \u00B7 " + level;
                else if (goal == "") subtitle = level;

                ProgramChoiceItem item = new ProgramChoiceItem();
                item.Id = id;
                item.Name = Text(row, "ProgramName");
                item.Subtitle = subtitle;
                item.Stats = days + (days == 1 ? " day" : " days");
                item.CoachId = Convert.ToInt32(row["CoachID"]);
                item.CoachName = coachName;
                item.Initial = FirstLetter(coachName);
                item.Picture = LoadPicture(Text(row, "ProfilePicPath"));
                item.IsSelected = selectedIds != null && selectedIds.Contains(id);
                list.Add(item);
            }

            return list;
        }

        // ================================================================
        // Saving
        // ================================================================

        public static void SetTrainer(int traineeId, int? trainerId)
        {
            EnsureSchema();

            DatabaseHelper.ExecuteNonQuery(
                "UPDATE Trainees SET TrainerID = ? WHERE UserID = ?",
                p =>
                {
                    p.AddWithValue("?", trainerId.HasValue ? (object)trainerId.Value : DBNull.Value);
                    p.AddWithValue("?", traineeId);
                });
        }

        public static void AddProgram(int traineeId, int programId)
        {
            EnsureSchema();

            // Don't add the same program twice
            HashSet<int> existing = GetProgramIds(traineeId);
            if (existing.Contains(programId)) return;

            DatabaseHelper.ExecuteNonQuery(
                "INSERT INTO TraineePrograms (TraineeID, ProgramID) VALUES (?, ?)",
                p =>
                {
                    p.AddWithValue("?", traineeId);
                    p.AddWithValue("?", programId);
                });
        }

        public static void RemoveProgram(int traineeId, int programId)
        {
            EnsureSchema();

            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM TraineePrograms WHERE TraineeID = ? AND ProgramID = ?",
                p =>
                {
                    p.AddWithValue("?", traineeId);
                    p.AddWithValue("?", programId);
                });
        }

        // ================================================================
        // Small helpers
        // ================================================================

        private static string Text(DataRow row, string column)
        {
            return row[column] == DBNull.Value ? "" : row[column].ToString().Trim();
        }

        private static string FirstLetter(string name)
        {
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper();
        }

        /// <summary>Loads a profile picture from its saved path. Returns null if there is none.</summary>
        public static ImageSource LoadPicture(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 160;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}