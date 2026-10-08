using FitnessTrackerProject.Models;
using FitnessTrackerProject.Scripts;
using Microsoft.Win32;
using System;
using System.Data.OleDb;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
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

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text;
            string ageText = AgeTextBox.Text;
            string gender = GenderComboBox.Text;
            string role = RoleComboBox.Text;
            string password = PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ageText) ||
                string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please fill out all fields, including the password.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BaseUser newUser;

            if (role == "Coach")
            {
                newUser = new Coach();
            }
            else
            {
                newUser = new Trainee();
            }

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

            int newUserId = 0;
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

                // Insert into Users table using DatabaseHelper
                DatabaseHelper.ExecuteNonQuery(
                    "INSERT INTO Users (Username, Age, Gender, UserRole, ProfilePicPath, [Password]) VALUES (?, ?, ?, ?, ?, ?)",
                    paramsCol => {
                        paramsCol.AddWithValue("?", newUser.Username);
                        paramsCol.AddWithValue("?", newUser.Age);
                        paramsCol.AddWithValue("?", newUser.Gender);
                        paramsCol.AddWithValue("?", newUser.GetUserRole());
                        paramsCol.AddWithValue("?", newUser.ProfilePicPath ?? "");
                        paramsCol.AddWithValue("?", securedPassword);
                    }
                );

                // Get the generated user ID using DatabaseHelper
                object idResult = DatabaseHelper.ExecuteScalar("SELECT @@IDENTITY");
                newUserId = Convert.ToInt32(idResult);

                // Insert matching row into Trainees or Trainers table
                if (role == "Coach")
                {
                    DatabaseHelper.ExecuteNonQuery(
                        "INSERT INTO Trainers (UserID, Specialty, Bio) VALUES (?, '', '')",
                        paramsCol => paramsCol.AddWithValue("?", newUserId)
                    );
                }
                else
                {
                    DatabaseHelper.ExecuteNonQuery(
                        "INSERT INTO Trainees (UserID, Height, Weight, FitnessGoal) VALUES (?, 0, 0, '')",
                        paramsCol => paramsCol.AddWithValue("?", newUserId)
                    );
                }

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

        private void GoToLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}