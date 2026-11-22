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
                        "INSERT INTO ProgramsTable (CoachID, ProgramName, Goal, ProgramLevel, CreatedDate) VALUES (?, ?, ?, ?, ?)",
                        coachId, name, goal, level, DateTime.Now);

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

                                DatabaseHelper.ExecuteNonQueryWithCurrentConnection(connection, transaction,
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
        private static int InsertAndGetId(
            OleDbConnection connection,
            OleDbTransaction transaction,
            string sql,
            params object[] values)
        {
            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                connection, transaction, sql, values);

            using (var command = new OleDbCommand(
                "SELECT @@IDENTITY", connection, transaction))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
        public static void AssignCustomProgramToTrainee(
            int originalProgramId,
            int coachId,
            int traineeId)
        {
            using (var connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string customName = "";

                        // 1. Get the original template name
                        using (var cmd = new OleDbCommand(
                            "SELECT ProgramName FROM ProgramsTable WHERE ProgramID = ? AND CoachID = ?",
                            connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("?", originalProgramId);
                            cmd.Parameters.AddWithValue("?", coachId);

                            object result = cmd.ExecuteScalar();

                            if (result == null || result == DBNull.Value)
                                throw new Exception("Original program template was not found.");

                            customName = result.ToString() + " (Custom)";
                        }

                        int customProgramId = 0;

                        // 2. Check whether this coach already has this custom program
                        using (var cmd = new OleDbCommand(
                            "SELECT ProgramID FROM ProgramsTable WHERE CoachID = ? AND ProgramName = ?",
                            connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("?", coachId);
                            cmd.Parameters.AddWithValue("?", customName);

                            object result = cmd.ExecuteScalar();

                            if (result != null && result != DBNull.Value)
                                customProgramId = Convert.ToInt32(result);
                        }

                        // 3. If it doesn't exist, create the custom program
                        if (customProgramId == 0)
                        {
                            using (var cmd = new OleDbCommand(
                                @"INSERT INTO ProgramsTable
                          (CoachID, ProgramName, Goal, ProgramLevel, CreatedDate)
                          SELECT CoachID, ?, Goal, ProgramLevel, ?
                          FROM ProgramsTable
                          WHERE ProgramID = ? AND CoachID = ?",
                                connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("?", customName);
                                cmd.Parameters.AddWithValue("?", DateTime.Now);
                                cmd.Parameters.AddWithValue("?", originalProgramId);
                                cmd.Parameters.AddWithValue("?", coachId);

                                int inserted = cmd.ExecuteNonQuery();

                                if (inserted == 0)
                                    throw new Exception("Could not create the custom program.");
                            }

                            using (var cmd = new OleDbCommand(
                                "SELECT @@IDENTITY", connection, transaction))
                            {
                                customProgramId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 4. Copy the program days and exercises
                            var daysTable = new System.Data.DataTable();

                            using (var cmd = new OleDbCommand(
                                "SELECT DayID, DayNumber, DayFocus FROM ProgramDaysTable WHERE ProgramID = ?",
                                connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("?", originalProgramId);

                                using (var reader = cmd.ExecuteReader())
                                {
                                    daysTable.Load(reader);
                                }
                            }

                            foreach (System.Data.DataRow dayRow in daysTable.Rows)
                            {
                                int oldDayId = Convert.ToInt32(dayRow["DayID"]);

                                using (var cmd = new OleDbCommand(
                                    @"INSERT INTO ProgramDaysTable
                              (ProgramID, DayNumber, DayFocus)
                              VALUES (?, ?, ?)",
                                    connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("?", customProgramId);
                                    cmd.Parameters.AddWithValue("?", dayRow["DayNumber"]);
                                    cmd.Parameters.AddWithValue("?", dayRow["DayFocus"]);

                                    cmd.ExecuteNonQuery();
                                }

                                int newDayId;

                                using (var cmd = new OleDbCommand(
                                    "SELECT @@IDENTITY", connection, transaction))
                                {
                                    newDayId = Convert.ToInt32(cmd.ExecuteScalar());
                                }

                                using (var cmd = new OleDbCommand(
                                    @"INSERT INTO ProgramExercisesTable
                              (DayID, ExerciseID, Sets, Reps, RestTime, SortOrder)
                              SELECT ?, ExerciseID, Sets, Reps, RestTime, SortOrder
                              FROM ProgramExercisesTable
                              WHERE DayID = ?",
                                    connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("?", newDayId);
                                    cmd.Parameters.AddWithValue("?", oldDayId);

                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // 5. Check whether this trainee is already assigned
                        using (var cmd = new OleDbCommand(
                            @"SELECT COUNT(*)
                      FROM TraineePrograms
                      WHERE TraineeID = ? AND ProgramID = ?",
                            connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("?", traineeId);
                            cmd.Parameters.AddWithValue("?", customProgramId);

                            int exists = Convert.ToInt32(cmd.ExecuteScalar());

                            if (exists > 0)
                                throw new Exception("This trainee is already assigned to this program.");
                        }

                        // 6. Connect the trainee to the custom program
                        using (var cmd = new OleDbCommand(
                            @"INSERT INTO TraineePrograms
                      (TraineeID, ProgramID)
                      VALUES (?, ?)",
                            connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("?", traineeId);
                            cmd.Parameters.AddWithValue("?", customProgramId);

                            cmd.ExecuteNonQuery();
                        }

                        // 7. Save everything
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}