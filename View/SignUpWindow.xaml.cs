using Microsoft.Win32;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FitnessTrackerProject.Models;
using ViewModel.Scripts;

namespace View
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
                // Hash the password
                string securedPassword = HashPassword(password);

                // Save the user using DatabaseHelper
                int newUserId = UserRepository.SignUp(
                    newUser,
                    securedPassword,
                    role,
                    height,
                    weight,
                    goal,
                    specialty,
                    bio
                );

                // Check if username already exists
                if (newUserId == -1)
                {
                    MessageBox.Show(
                        "A profile with this username already exists. Please choose a different name.",
                        "User Exists",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                // Profile successfully created
                MessageBox.Show(
                    $"{newUser.GetUserRole()} profile successfully created!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                MainWindow mainAppWindow = new MainWindow(newUserId);
                mainAppWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving to database: {ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
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