using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using ViewModel.Scripts;
using FitnessTrackerProject.Models;

namespace View
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }
        //using hash in order to protect user password
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

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = LoginUsernameBox.Text;
            string password = LoginPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both username and password.", "Missing Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                string securedPassword = HashPassword(password);
                int UserId = UserRepository.LogIn(username, securedPassword);
                if (UserId != -1)
                {
                    MessageBox.Show("Login Successful!", "Welcome", MessageBoxButton.OK, MessageBoxImage.Information);

                    MainWindow mainAppWindow = new MainWindow(UserId);
                    mainAppWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Invalid username or password. Please try again.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}",
    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        //go to signUp if doesnt have a user already
        private void GoToSignUp_Click(object sender, RoutedEventArgs e)
        {
            SignUpWindow signUpWindow = new SignUpWindow();
            signUpWindow.Show();
            this.Close();
        }
    }
}