
using System;
using System.Data;
using System.Data.OleDb;
using FitnessTrackerProject.Models;

namespace ViewModel.Scripts
{
    // Holds all the information needed to display a user profile.
    public class UserProfileData
    {
        public string Username { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; }
        public string ProfilePicPath { get; set; }
        public string UserRole { get; set; }

        // Coach information
        public string Specialty { get; set; }
        public string Bio { get; set; }

        // Trainee information
        public double Height { get; set; }
        public double Weight { get; set; }
        public string FitnessGoal { get; set; }
    }

    public static class UserRepository
    {
        // LOGIN

        public static int LogIn(string username, string passwordHash)
        {
            object result = DatabaseHelper.ExecuteScalar(
                "SELECT ID FROM Users WHERE Username = ? AND [Password] = ?",
                p =>
                {
                    p.AddWithValue("?", username);
                    p.AddWithValue("?", passwordHash);
                });

            if (result == null || result == DBNull.Value)
                return -1;

            return Convert.ToInt32(result);
        }

        // SIGNUP

        public static int SignUp(BaseUser user, string passwordHash, string role,
                                 double height, double weight, string goal,
                                 string specialty, string bio)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                using (OleDbTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Check whether username already exists
                        using (OleDbCommand checkCommand = new OleDbCommand(
                            "SELECT COUNT(*) FROM Users WHERE Username = ?",
                            connection, transaction))
                        {
                            checkCommand.Parameters.AddWithValue("?", user.Username);

                            int count = Convert.ToInt32(checkCommand.ExecuteScalar());

                            if (count > 0)
                            {
                                transaction.Rollback();
                                return -1;
                            }
                        }

                        // Insert general user information
                        DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                            connection, transaction,
                            "INSERT INTO Users (Username, Age, Gender, UserRole, ProfilePicPath, [Password]) VALUES (?, ?, ?, ?, ?, ?)",
                            user.Username,
                            user.Age,
                            user.Gender,
                            user.GetUserRole(),
                            user.ProfilePicPath ?? "",
                            passwordHash);

                        // Get the generated user ID
                        int userId;

                        using (OleDbCommand command = new OleDbCommand(
                            "SELECT @@IDENTITY", connection, transaction))
                        {
                            userId = Convert.ToInt32(command.ExecuteScalar());
                        }

                        if (userId <= 0)
                            throw new Exception("Could not get the new user ID.");

                        // Insert role-specific information
                        if (role == "Coach")
                        {
                            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                                connection, transaction,
                                "INSERT INTO Trainers (UserID, Specialty, Bio) VALUES (?, ?, ?)",
                                userId, specialty, bio);
                        }
                        else
                        {
                            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                                connection, transaction,
                                "INSERT INTO Trainees (UserID, Height, Weight, FitnessGoal) VALUES (?, ?, ?, ?)",
                                userId, height, weight, goal);
                        }

                        transaction.Commit();
                        return userId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // GET PROFILE

        public static UserProfileData GetProfile(int userId)
        {
            DataTable userTable = DatabaseHelper.ExecuteQueryTable(
                "SELECT Username, Age, Gender, ProfilePicPath, UserRole FROM Users WHERE ID = ?",
                p => p.AddWithValue("?", userId));

            if (userTable.Rows.Count == 0)
                return null;

            DataRow row = userTable.Rows[0];

            UserProfileData profile = new UserProfileData
            {
                Username = Text(row, "Username"),
                Age = row["Age"] == DBNull.Value ? 0 : Convert.ToInt32(row["Age"]),
                Gender = Text(row, "Gender"),
                ProfilePicPath = Text(row, "ProfilePicPath"),
                UserRole = Text(row, "UserRole")
            };

            // Get role-specific information
            if (string.Equals(profile.UserRole, "Coach",
                StringComparison.OrdinalIgnoreCase))
            {
                DataTable coachTable = DatabaseHelper.ExecuteQueryTable(
                    "SELECT Specialty, Bio FROM Trainers WHERE UserID = ?",
                    p => p.AddWithValue("?", userId));

                if (coachTable.Rows.Count > 0)
                {
                    DataRow coach = coachTable.Rows[0];

                    profile.Specialty = Text(coach, "Specialty");
                    profile.Bio = Text(coach, "Bio");
                }
            }
            else
            {
                DataTable traineeTable = DatabaseHelper.ExecuteQueryTable(
                    "SELECT Height, Weight, FitnessGoal FROM Trainees WHERE UserID = ?",
                    p => p.AddWithValue("?", userId));

                if (traineeTable.Rows.Count > 0)
                {
                    DataRow trainee = traineeTable.Rows[0];

                    profile.Height = trainee["Height"] == DBNull.Value
                        ? 0 : Convert.ToDouble(trainee["Height"]);

                    profile.Weight = trainee["Weight"] == DBNull.Value
                        ? 0 : Convert.ToDouble(trainee["Weight"]);

                    profile.FitnessGoal = Text(trainee, "FitnessGoal");
                }
            }

            return profile;
        }

        // UPDATE PROFILE

        public static void UpdateProfile(int userId, string username, int age,
                                         string gender, string profilePicPath,
                                         string role, double height, double weight,
                                         string fitnessGoal, string specialty, string bio)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                using (OleDbTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                            connection, transaction,
                            "UPDATE Users SET Username = ?, Age = ?, Gender = ? WHERE ID = ?",
                            username, age, gender, userId);

                        if (!string.IsNullOrEmpty(profilePicPath))
                        {
                            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                                connection, transaction,
                                "UPDATE Users SET ProfilePicPath = ? WHERE ID = ?",
                                profilePicPath, userId);
                        }

                        if (string.Equals(role, "Coach",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                                connection, transaction,
                                "UPDATE Trainers SET Specialty = ?, Bio = ? WHERE UserID = ?",
                                specialty, bio, userId);
                        }
                        else
                        {
                            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                                connection, transaction,
                                "UPDATE Trainees SET Height = ?, Weight = ?, FitnessGoal = ? WHERE UserID = ?",
                                height, weight, fitnessGoal, userId);
                        }

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

        // DELETE ACCOUNT

        public static void DeleteAccount(int userId)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                using (OleDbTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                            connection, transaction,
                            "DELETE FROM Trainees WHERE UserID = ?",
                            userId);

                        DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                            connection, transaction,
                            "DELETE FROM Trainers WHERE UserID = ?",
                            userId);

                        int rowsAffected =
                            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(
                                connection, transaction,
                                "DELETE FROM Users WHERE ID = ?",
                                userId);

                        if (rowsAffected == 0)
                            throw new Exception("The user account could not be found.");

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
        
        // HELPER

        private static string Text(DataRow row, string column)
        {
            return row[column] == DBNull.Value
                ? ""
                : row[column].ToString().Trim();
        }
        public static bool IsCoach(int userId)
        {
            object role = DatabaseHelper.ExecuteScalar(
                "SELECT UserRole FROM Users WHERE ID = ?",
                p => p.AddWithValue("?", userId));

            return role != null && role != DBNull.Value &&
                   string.Equals(
                       role.ToString().Trim(),
                       "Coach",
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
