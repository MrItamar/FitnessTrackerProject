using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Data.OleDb;
using System.Security.Cryptography; // Added for password security
using System.Text;                  // Added for password security
using FitnessTrackerProject.Models;

namespace FitnessTrackerProject
{
    public partial class Window1 : Window
    {
        private string profilePicturePath = "";

        // Make sure this path points to your actual Access file!
        string connectionString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=..\..\DataBase\FitnessTrackerDB1.accdb;";

        public Window1()
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

        // --- NEW SECURITY FUNCTION ---
        // Scrambles the password so it is unreadable in your Access database
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
            // 1. Get UI Inputs
            string name = NameTextBox.Text;
            string ageText = AgeTextBox.Text;
            string gender = GenderComboBox.Text;
            string role = RoleComboBox.Text;
            string password = PasswordInput.Password; // Grab from the new PasswordBox

            // 2. Validate Inputs
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ageText) ||
                string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please fill out all fields, including the password.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. OOP Implementation (Polymorphism)
            BaseUser newUser;

            if (role == "Coach")
            {
                newUser = new Coach();
            }
            else
            {
                newUser = new Trainee();
            }

            // 4. OOP Implementation (Encapsulation)
            try
            {
                newUser.Username = name;
                newUser.Age = Convert.ToInt32(ageText); // Validates age 10-120
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

            // 5. Database Interaction
            try
            {
                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    connection.Open();

                    // Check if the username is already taken
                    string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Name";
                    using (OleDbCommand checkCommand = new OleDbCommand(checkQuery, connection))
                    {
                        // Parameterized query prevents SQL Injection here
                        checkCommand.Parameters.AddWithValue("@Name", newUser.Username);

                        int userCount = (int)checkCommand.ExecuteScalar();

                        if (userCount > 0)
                        {
                            MessageBox.Show("A profile with this username already exists. Please choose a different name.", "User Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return; // Stop the code here
                        }
                    }

                    // Scramble the password BEFORE saving it
                    string securedPassword = HashPassword(password);

                    // Notice the brackets [Password] - Access requires this because Password is a reserved word
                    string insertQuery = "INSERT INTO Users (Username, Age, Gender, UserRole, ProfilePicPath, [Password]) VALUES (@Name, @Age, @Gender, @Role, @PicPath, @Password)";

                    using (OleDbCommand insertCommand = new OleDbCommand(insertQuery, connection))
                    {
                        // Parameterized queries prevent SQL Injection here
                        insertCommand.Parameters.AddWithValue("@Name", newUser.Username);
                        insertCommand.Parameters.AddWithValue("@Age", newUser.Age);
                        insertCommand.Parameters.AddWithValue("@Gender", newUser.Gender);
                        insertCommand.Parameters.AddWithValue("@Role", newUser.GetUserRole());
                        insertCommand.Parameters.AddWithValue("@PicPath", newUser.ProfilePicPath);
                        insertCommand.Parameters.AddWithValue("@Password", securedPassword);

                        insertCommand.ExecuteNonQuery();
                    }
                }

                // 6. Show success and switch windows
                MessageBox.Show($"{newUser.GetUserRole()} profile successfully created!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                MainWindow mainAppWindow = new MainWindow();
                mainAppWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving to database: {ex.Message}\n\nMake sure your Access file is closed and has a Password column (Short Text)!", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void GoToLogin_Click(object sender, RoutedEventArgs e)
        {
            // Opens the login window and closes the sign-up window
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}