using FitnessTrackerProject.Models;
using FitnessTrackerProject.Scripts;
using Microsoft.Win32;
using System;
using System.Data.OleDb;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace FitnessTrackerProject
{
    public partial class SignUpWindow : Window
    {
        private string profilePicturePath = "";

        public SignUpWindow()
        {
            InitializeComponent();
        }

        private void UploadPicture_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png|All Files|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                profilePicturePath = openFileDialog.FileName;

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(profilePicturePath);
                bitmap.EndInit();

                ProfileImage.Source = bitmap;
            }
        }

        //Shows the trainee fields or the coach fields, depending on the chosen role.
        private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // The panels don't exist yet while the window is still being built
            if (TraineePanel == null || CoachPanel == null) return;

            string role = (RoleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            CoachPanel.Visibility = role == "Coach" ? Visibility.Visible : Visibility.Collapsed;
            TraineePanel.Visibility = role == "Trainee" ? Visibility.Visible : Visibility.Collapsed;
        }

        //Protecting the password using hash
        private string HashPassword(string rawPassword)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawPassword));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        //saving profile 
        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text;
            string ageText = AgeTextBox.Text;
            string gender = GenderComboBox.Text;
            string role = RoleComboBox.Text;
            string password = PasswordInput.Password;
            //checking fields
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ageText) ||
                string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please fill out all fields, including the password.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BaseUser newUser;
            //checking role
            if (role == "Coach")
            {
                newUser = new Coach();
            }
            else
            {
                newUser = new Trainee();
            }

            //formating the user
            try
            {
                newUser.Username = name;
                newUser.Age = Convert.ToInt32(ageText);
                newUser.Gender = gender;
                newUser.ProfilePicPath = profilePicturePath;
            }
            catch (FormatException)
            {
                MessageBox.Show("Age must be a valid number.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Role specific fields
            double height = 0, weight = 0;
            string goal = "", specialty = "", bio = "";
            //update variable from UI fields
            if (role == "Coach")
            {
                specialty = SpecialtyTextBox.Text.Trim();
                bio = BioTextBox.Text.Trim();

                if (specialty == "")
                {
                    MessageBox.Show("Please enter your specialty.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                goal = (GoalComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

                if (!double.TryParse(HeightTextBox.Text, out height) || height < 50 || height > 250)
                {
                    MessageBox.Show("Height must be a number between 50 and 250 (cm).", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!double.TryParse(WeightTextBox.Text, out weight) || weight < 20 || weight > 400)
                {
                    MessageBox.Show("Weight must be a number between 20 and 400 (kg).", "Input Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (goal == "")
                {
                    MessageBox.Show("Please choose a goal.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                // Check if username exists using DatabaseHelper
                object countResult = DatabaseHelper.ExecuteScalar(
                    "SELECT COUNT(*) FROM Users WHERE Username = @Name",
                    paramsCol => paramsCol.AddWithValue("@Name", newUser.Username)
                );

                int userCount = Convert.ToInt32(countResult);
                if (userCount > 0)
                {
                    MessageBox.Show("A profile with this username already exists. Please choose a different name.", "User Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string securedPassword = HashPassword(password);

                // Saves the user and the trainee/coach row together
                int newUserId = SaveNewUser(newUser, securedPassword, role, height, weight, goal, specialty, bio);

                //profile successfully created
                MessageBox.Show($"{newUser.GetUserRole()} profile successfully created!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                MainWindow mainAppWindow = new MainWindow(newUserId);
                mainAppWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving to database: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Inserts the user and the matching Trainees/Trainers row and returns the new user's ID.
        private int SaveNewUser(BaseUser user, string passwordHash, string role,
                                double height, double weight, string goal, string specialty, string bio)
        {
            //opening connection
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                using (OleDbTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        //inserting the user info
                        DatabaseHelper.ExecuteNonQueryWithCurrentConnection(connection, transaction,
                            "INSERT INTO Users (Username, Age, Gender, UserRole, ProfilePicPath, [Password]) VALUES (?, ?, ?, ?, ?, ?)",
                            user.Username, user.Age, user.Gender, user.GetUserRole(), user.ProfilePicPath ?? "", passwordHash);

                        // The ID Access just created for this user
                        int userId;
                        using (var command = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                        {
                            userId = Convert.ToInt32(command.ExecuteScalar());
                        }

                        if (userId == 0)
                            throw new Exception("Could not get the new user ID.");
                        //Inserting specific fields in the correct user id
                        if (role == "Coach")
                        {
                            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(connection, transaction,
                                "INSERT INTO Trainers (UserID, Specialty, Bio) VALUES (?, ?, ?)",
                                userId, specialty, bio);
                        }
                        else
                        {
                            DatabaseHelper.ExecuteNonQueryWithCurrentConnection(connection, transaction,
                                "INSERT INTO Trainees (UserID, Height, Weight, FitnessGoal) VALUES (?, ?, ?, ?)",
                                userId, height, weight, goal);
                        }

                        transaction.Commit();
                        return userId;
                    }
                    catch
                    {
                        transaction.Rollback(); // something failed so undo everything
                        throw;                  // pass the error up so the screen can show it
                    }
                }
            }
        }

        //After signing up go to the log-in page
        private void GoToLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}