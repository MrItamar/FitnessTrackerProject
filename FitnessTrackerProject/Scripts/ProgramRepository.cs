using System;
using System.Collections.Generic;
using System.Data.OleDb;

namespace FitnessTrackerProject.Scripts
{
    /// <summary>Saves a coach's program (program > days > exercises) to the Access database.</summary>
    public static class ProgramRepository
    {
        /// <summary>
        /// Saves everything in ONE transaction: either the whole program is saved, or nothing is.
        /// Returns the new ProgramID.
        /// </summary>
        public static int SaveProgram(int coachId, string name, string goal, string level, List<CoachDay> days, int? assignedTraineeId = null)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                using (OleDbTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. The program itself
                        int programId = InsertAndGetId(connection, transaction,
                                "INSERT INTO ProgramsTable (CoachID, ProgramName, Goal, ProgramLevel, CreatedDate, AssignedTraineeID) VALUES (?, ?, ?, ?, ?, ?)",
                                coachId, name, goal, level, DateTime.Now, assignedTraineeId.HasValue ? (object)assignedTraineeId.Value : DBNull.Value);

                        // 2. Its days
                        for (int d = 0; d < days.Count; d++)
                        {
                            CoachDay day = days[d];
                            string focus = string.IsNullOrWhiteSpace(day.Focus) ? null : day.Focus.Trim();

                            int dayId = InsertAndGetId(connection, transaction,
                                "INSERT INTO ProgramDaysTable (ProgramID, DayNumber, DayFocus) VALUES (?, ?, ?)",
                                programId, d + 1, focus);

                            // 3. The exercises of this day
                            for (int e = 0; e < day.Exercises.Count; e++)
                            {
                                CoachExercise ex = day.Exercises[e];

                                Run(connection, transaction,
                                    "INSERT INTO ProgramExercisesTable (DayID, ExerciseID, SortOrder, Sets, Reps, RestTime) VALUES (?, ?, ?, ?, ?, ?)",
                                    dayId, ex.ExerciseId, e + 1, ex.Sets.Trim(), ex.Reps.Trim(), ex.Rest.Trim());
                            }
                        }

                        transaction.Commit();   // everything worked: make it permanent
                        return programId;
                    }
                    catch
                    {
                        transaction.Rollback(); // something failed: undo everything
                        throw;                  // pass the error up so the screen can show it
                    }
                }
            }
        }
        public static void UpdateProgram(int programId, int coachId, string name, string goal, string level, List<CoachDay> days)
        {
            // 1. Update program details
            DatabaseHelper.ExecuteNonQuery(
                "UPDATE ProgramsTable SET ProgramName = ?, Goal = ?, ProgramLevel = ? WHERE ProgramID = ? AND CoachID = ?",
                p =>
                {
                    p.AddWithValue("?", name);
                    p.AddWithValue("?", goal);
                    p.AddWithValue("?", level);
                    p.AddWithValue("?", programId);
                    p.AddWithValue("?", coachId);
                });

            // 2. Remove old exercises and days
            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM ProgramExercisesTable WHERE DayID IN (SELECT DayID FROM ProgramDaysTable WHERE ProgramID = ?)",
                p => p.AddWithValue("?", programId));

            DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM ProgramDaysTable WHERE ProgramID = ?",
                p => p.AddWithValue("?", programId));

            // 3. Insert the updated days and exercises
            for (int d = 0; d < days.Count; d++)
            {
                CoachDay day = days[d];
                string focus = string.IsNullOrWhiteSpace(day.Focus) ? "" : day.Focus.Trim();

                DatabaseHelper.ExecuteNonQuery(
                    "INSERT INTO ProgramDaysTable (ProgramID, DayNumber, DayFocus) VALUES (?, ?, ?)",
                    p =>
                    {
                        p.AddWithValue("?", programId);
                        p.AddWithValue("?", d + 1);
                        p.AddWithValue("?", focus);
                    });

                // Get the ID of the day we just added
                int dayId = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                    "SELECT MAX(DayID) FROM ProgramDaysTable WHERE ProgramID = ?",
                    p => p.AddWithValue("?", programId)));

                for (int e = 0; e < day.Exercises.Count; e++)
                {
                    CoachExercise ex = day.Exercises[e];
                    DatabaseHelper.ExecuteNonQuery(
                        "INSERT INTO ProgramExercisesTable (DayID, ExerciseID, SortOrder, Sets, Reps, RestTime) VALUES (?, ?, ?, ?, ?, ?)",
                        p =>
                        {
                            p.AddWithValue("?", dayId);
                            p.AddWithValue("?", ex.ExerciseId);
                            p.AddWithValue("?", e + 1);
                            p.AddWithValue("?", ex.Sets.Trim());
                            p.AddWithValue("?", ex.Reps.Trim());
                            p.AddWithValue("?", ex.Rest.Trim());
                        });
                }
            }
        }
        /// <summary>Runs an INSERT and returns the AutoNumber ID that Access just created.</summary>
        private static int InsertAndGetId(OleDbConnection connection, OleDbTransaction transaction,
                                          string sql, params object[] values)
        {
            Run(connection, transaction, sql, values);

            // Must be the same connection as the INSERT, that's why we don't use DatabaseHelper's methods here
            using (var command = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        /// <summary>Runs a command. Access fills the ? marks in ORDER, so the values must be in the same order.</summary>
        private static void Run(OleDbConnection connection, OleDbTransaction transaction,
                                string sql, params object[] values)
        {
            using (var command = new OleDbCommand(sql, connection, transaction))
            {
                foreach (object value in values)
                {
                    if (value is DateTime)
                        command.Parameters.Add("?", OleDbType.Date).Value = value;
                    else
                        command.Parameters.AddWithValue("?", value ?? DBNull.Value);   // null -> empty cell in the database
                }
                command.ExecuteNonQuery();
            }
        }
        public static void AssignCustomProgramToTrainee(int originalProgramId, int coachId, int traineeId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Copy the main program and stamp it with the AssignedTraineeID
                        string insertProg = "INSERT INTO ProgramsTable (CoachID, ProgramName, Goal, ProgramLevel, CreatedDate, AssignedTraineeID) " +
                                            "SELECT CoachID, ProgramName & ' (Custom)', Goal, ProgramLevel, NOW(), ? " +
                                            "FROM ProgramsTable WHERE ProgramID = ?";

                        int newProgramId = 0;
                        using (var cmd = new System.Data.OleDb.OleDbCommand(insertProg, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("?", traineeId);
                            cmd.Parameters.AddWithValue("?", originalProgramId);
                            cmd.ExecuteNonQuery();

                            cmd.CommandText = "SELECT @@IDENTITY";
                            newProgramId = Convert.ToInt32(cmd.ExecuteScalar()); // FIXED: Safe conversion
                        }

                        // 2. Find all the days in the original program
                        var daysTable = new System.Data.DataTable();
                        using (var cmdDays = new System.Data.OleDb.OleDbCommand("SELECT DayID, DayNumber, DayFocus FROM ProgramDaysTable WHERE ProgramID = ?", connection, transaction))
                        {
                            cmdDays.Parameters.AddWithValue("?", originalProgramId);
                            using (var reader = cmdDays.ExecuteReader()) { daysTable.Load(reader); }
                        }

                        // 3. Duplicate each day AND its exercises
                        foreach (System.Data.DataRow dayRow in daysTable.Rows)
                        {
                            int oldDayId = Convert.ToInt32(dayRow["DayID"]);

                            // A. Copy the Day
                            string insertDay = "INSERT INTO ProgramDaysTable (ProgramID, DayNumber, DayFocus) VALUES (?, ?, ?)";
                            int newDayId = 0;
                            using (var cmdD = new System.Data.OleDb.OleDbCommand(insertDay, connection, transaction))
                            {
                                cmdD.Parameters.AddWithValue("?", newProgramId);
                                cmdD.Parameters.AddWithValue("?", dayRow["DayNumber"]);
                                cmdD.Parameters.AddWithValue("?", dayRow["DayFocus"]);
                                cmdD.ExecuteNonQuery();

                                cmdD.CommandText = "SELECT @@IDENTITY";
                                newDayId = Convert.ToInt32(cmdD.ExecuteScalar()); // FIXED: Safe conversion
                            }

                            // B. RESTORED: Copy the Exercises for this specific day
                            string insertEx = "INSERT INTO ProgramExercisesTable (DayID, ExerciseID, Sets, Reps, RestTime, SortOrder) " +
                                              "SELECT ?, ExerciseID, Sets, Reps, RestTime, SortOrder " +
                                              "FROM ProgramExercisesTable WHERE DayID = ?";
                            using (var cmdE = new System.Data.OleDb.OleDbCommand(insertEx, connection, transaction))
                            {
                                cmdE.Parameters.AddWithValue("?", newDayId);
                                cmdE.Parameters.AddWithValue("?", oldDayId);
                                cmdE.ExecuteNonQuery();
                            }
                        }

                        // 4. Add to the Trainee's dashboard list
                        using (var cmdT = new System.Data.OleDb.OleDbCommand("INSERT INTO TraineePrograms (TraineeID, ProgramID) VALUES (?, ?)", connection, transaction))
                        {
                            cmdT.Parameters.AddWithValue("?", traineeId);
                            cmdT.Parameters.AddWithValue("?", newProgramId);
                            cmdT.ExecuteNonQuery();
                        }

                        // 5. Remove the original public template from their list
                        using (var cmdDel = new System.Data.OleDb.OleDbCommand("DELETE FROM TraineePrograms WHERE TraineeID = ? AND ProgramID = ?", connection, transaction))
                        {
                            cmdDel.Parameters.AddWithValue("?", traineeId);
                            cmdDel.Parameters.AddWithValue("?", originalProgramId);
                            cmdDel.ExecuteNonQuery();
                        }

                        // FIXED: Commit MUST be outside the loop, at the very end
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(); } catch { }
                        throw new Exception("The REAL error is: " + ex.Message);
                    }
                }
            }
        }
    }
}