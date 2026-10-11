using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FitnessTrackerProject.Scripts
{
    // Small classes that hold what the Explore screen shows (properties, so WPF binding can read them)
    public class TrainerItem
    {
        public int Id { get; set; }                 // = Users.ID of the trainer
        public string Name { get; set; }
        public string Initial { get; set; }         // shown in the circle when there is no picture
        public string Specialty { get; set; }
        public string Bio { get; set; }
        public string Stats { get; set; }           // "3 programs"
        public ImageSource Picture { get; set; }    // null = no picture
        public bool IsSelected { get; set; }
        public string ButtonText { get { return IsSelected ? "Selected \u2713" : "Choose trainer"; } }
    }

    public class ProgramChoiceItem
    {
        public int Id { get; set; }                 // = ProgramID
        public string Name { get; set; }
        public string Subtitle { get; set; }        // "Build strength · Beginner"
        public string Stats { get; set; }           // "3 days"
        public int CoachId { get; set; }
        public string CoachName { get; set; }
        public string Initial { get; set; }         // first letter of the coach's name
        public ImageSource Picture { get; set; }    // the COACH's profile picture
        public bool IsSelected { get; set; }
        public string ButtonText { get { return IsSelected ? "Selected \u2713" : "Choose program"; } }
    }

    /// <summary>
    /// What a trainee has chosen: a dedicated trainer and/or a program.
    /// Two new columns on the Trainees table keep the choice:
    ///   TrainerID        = Users.ID of the chosen trainer (empty = none)
    ///   ActiveProgramID  = ProgramID of the chosen program (empty = none)
    /// The columns are added automatically the first time they are needed.
    /// </summary>
    public static class TraineeRepository
    {
        private static bool _schemaChecked = false;

        // ================================================================
        // Database upgrade (runs once per app start, does nothing if the columns exist)
        // ================================================================
        private static void EnsureSchema()
        {
            if (_schemaChecked) return;

            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                DataTable columns = connection.GetSchema("Columns", new string[] { null, null, "Trainees", null });
                bool hasTrainer = false;
                bool hasProgram = false;

                foreach (DataRow row in columns.Rows)
                {
                    string name = row["COLUMN_NAME"].ToString();
                    if (string.Equals(name, "TrainerID", StringComparison.OrdinalIgnoreCase)) hasTrainer = true;
                    if (string.Equals(name, "ActiveProgramID", StringComparison.OrdinalIgnoreCase)) hasProgram = true;
                }

                if (!hasTrainer) RunDdl(connection, "ALTER TABLE Trainees ADD COLUMN TrainerID LONG");
                if (!hasProgram) RunDdl(connection, "ALTER TABLE Trainees ADD COLUMN ActiveProgramID LONG");
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
        // Reading
        // ================================================================

        /// <summary>The trainee's current choices (null = nothing chosen).</summary>
        public static void GetSelection(int traineeId, out int? trainerId, out int? programId)
        {
            EnsureSchema();

            trainerId = null;
            programId = null;

            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                "SELECT TrainerID, ActiveProgramID FROM Trainees WHERE UserID = ?",
                p => p.AddWithValue("?", traineeId));

            if (dt.Rows.Count == 0) return;

            DataRow row = dt.Rows[0];
            if (row["TrainerID"] != DBNull.Value) trainerId = Convert.ToInt32(row["TrainerID"]);
            if (row["ActiveProgramID"] != DBNull.Value) programId = Convert.ToInt32(row["ActiveProgramID"]);
        }

        /// <summary>All trainers (coaches), with their profile picture.</summary>
        public static List<TrainerItem> GetTrainers(int? selectedTrainerId)
        {
            var list = new List<TrainerItem>();

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

                list.Add(new TrainerItem
                {
                    Id = id,
                    Name = name,
                    Initial = FirstLetter(name),
                    Specialty = Text(row, "Specialty"),
                    Bio = Text(row, "Bio"),
                    Stats = programs + (programs == 1 ? " program" : " programs"),
                    Picture = LoadPicture(Text(row, "ProfilePicPath")),
                    IsSelected = selectedTrainerId.HasValue && selectedTrainerId.Value == id
                });
            }

            return list;
        }

        /// <summary>All programs made by coaches, each with its coach's name and profile picture.</summary>
        public static List<ProgramChoiceItem> GetPrograms(int? selectedProgramId)
        {
            var list = new List<ProgramChoiceItem>();

            DataTable dt = DatabaseHelper.ExecuteQueryTable(
                "SELECT ProgramsTable.ProgramID, ProgramsTable.ProgramName, ProgramsTable.Goal, " +
                "ProgramsTable.ProgramLevel, ProgramsTable.CoachID, Users.Username, Users.ProfilePicPath " +
                "FROM ProgramsTable INNER JOIN Users ON ProgramsTable.CoachID = Users.ID " +
                "ORDER BY ProgramsTable.CreatedDate DESC");

            foreach (DataRow row in dt.Rows)
            {
                int id = Convert.ToInt32(row["ProgramID"]);
                string coachName = Text(row, "Username");
                string goal = Text(row, "Goal");
                string level = Text(row, "ProgramLevel");

                int days = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM ProgramDaysTable WHERE ProgramID = ?",
                    p => p.AddWithValue("?", id)));

                list.Add(new ProgramChoiceItem
                {
                    Id = id,
                    Name = Text(row, "ProgramName"),
                    Subtitle = string.Join(" \u00B7 ", new List<string> { goal, level }.FindAll(s => s != "")),
                    Stats = days + (days == 1 ? " day" : " days"),
                    CoachId = Convert.ToInt32(row["CoachID"]),
                    CoachName = coachName,
                    Initial = FirstLetter(coachName),
                    Picture = LoadPicture(Text(row, "ProfilePicPath")),
                    IsSelected = selectedProgramId.HasValue && selectedProgramId.Value == id
                });
            }

            return list;
        }

        // ================================================================
        // Saving (pass null to clear the choice)
        // ================================================================

        public static void SetTrainer(int traineeId, int? trainerId)
        {
            EnsureSchema();

            DatabaseHelper.ExecuteNonQuery(
                "UPDATE Trainees SET TrainerID = ? WHERE UserID = ?",
                p =>
                {
                    // Access fills the ? marks in ORDER
                    p.AddWithValue("?", trainerId.HasValue ? (object)trainerId.Value : DBNull.Value);
                    p.AddWithValue("?", traineeId);
                });
        }

        public static void SetProgram(int traineeId, int? programId)
        {
            EnsureSchema();

            DatabaseHelper.ExecuteNonQuery(
                "UPDATE Trainees SET ActiveProgramID = ? WHERE UserID = ?",
                p =>
                {
                    p.AddWithValue("?", programId.HasValue ? (object)programId.Value : DBNull.Value);
                    p.AddWithValue("?", traineeId);
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

        /// <summary>
        /// Loads a profile picture from the path saved in Users.ProfilePicPath.
        /// Returns null if there is no picture or the file is gone, so the screen shows the initial instead.
        /// </summary>
        public static ImageSource LoadPicture(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;   // reads the file now, so it isn't locked afterwards
                bitmap.DecodePixelWidth = 160;                   // small copy in memory, the avatars are tiny
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