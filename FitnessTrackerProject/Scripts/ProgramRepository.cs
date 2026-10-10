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
        public static int SaveProgram(int coachId, string name, string goal, string level, List<CoachDay> days)
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
    }
}