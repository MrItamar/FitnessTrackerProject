using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace FitnessTrackerProject.Scripts
{
    public class ExerciseModel
    {
        public int Id;
        public string Name;
        public string TargetMuscle;
        public string Description;
        public string Hint;
        public bool IsHorizontal;
    }

    public static class ExercisePool
    {
        private static readonly List<ExerciseModel> _exercises =
            new List<ExerciseModel>();

        private static bool _isInitialized = false;

        public static string LastLoadError { get; private set; }

        /// <summary>
        /// Loads exercises from the database into memory.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            _exercises.Clear();
            LastLoadError = null;

            try
            {
                string query = @"
                    SELECT ExerciseID, ExerciseName, TargetMuscle,
                           Horizontal, Description, Hint
                    FROM ExercisesTable
                    ORDER BY ExerciseID ASC";

                DataTable dt = DatabaseHelper.ExecuteQueryTable(query);

                foreach (DataRow row in dt.Rows)
                {
                    _exercises.Add(new ExerciseModel
                    {
                        Id = Convert.ToInt32(row["ExerciseID"]),

                        Name = row["ExerciseName"] == DBNull.Value
                            ? string.Empty
                            : row["ExerciseName"].ToString(),

                        TargetMuscle = row["TargetMuscle"] == DBNull.Value
                            ? "General"
                            : row["TargetMuscle"].ToString(),

                        IsHorizontal = row["Horizontal"] != DBNull.Value
                            && Convert.ToBoolean(row["Horizontal"]),

                        Description = row["Description"] == DBNull.Value
                            ? string.Empty
                            : row["Description"].ToString(),

                        Hint = row["Hint"] == DBNull.Value
                            ? string.Empty
                            : row["Hint"].ToString()
                    });
                }

                // Ignore records without a usable exercise name.
                _exercises.RemoveAll(
                    exercise => string.IsNullOrWhiteSpace(exercise.Name));
            }
            catch (Exception ex)
            {
                _exercises.Clear();
                LastLoadError = ex.Message;
            }
            finally
            {
                _isInitialized = true;
            }
        }

        public static List<ExerciseModel> GetAll()
        {
            if (!_isInitialized)
                Initialize();

            // Return a copy so callers cannot modify the cached list.
            return _exercises.ToList();
        }

        public static ExerciseModel GetByName(string name)
        {
            if (!_isInitialized)
                Initialize();

            if (string.IsNullOrWhiteSpace(name))
                return null;

            return _exercises.FirstOrDefault(
                exercise => string.Equals(
                    exercise.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsHorizontal(string name)
        {
            var exercise = GetByName(name);

            return exercise != null && exercise.IsHorizontal;
        }

        public static void Refresh()
        {
            _isInitialized = false;
            Initialize();
        }
    }
}