using FitnessTrackerProject.Scripts;
using System;
using System.Data.OleDb;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace FitnessTrackerProject
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
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

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = LoginUsernameBox.Text;
            string password = LoginPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both username and password.", "Missing Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string securedPassword = HashPassword(password);

            try
            {
                // Uses DatabaseHelper to get the connection automatically
                using (OleDbConnection connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();

                    string query = "SELECT ID FROM Users WHERE Username = @Name AND [Password] = @Password";
                    using (OleDbCommand command = new OleDbCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", username);
                        command.Parameters.AddWithValue("@Password", securedPassword);

                        object result = command.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            int foundUserId = Convert.ToInt32(result);

                            MessageBox.Show("Login Successful!", "Welcome", MessageBoxButton.OK, MessageBoxImage.Information);

                            MainWindow mainAppWindow = new MainWindow(foundUserId);
                            mainAppWindow.Show();
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("Invalid username or password. Please try again.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoToSignUp_Click(object sender, RoutedEventArgs e)
        {
            SignUpWindow signUpWindow = new SignUpWindow();
            signUpWindow.Show();
            this.Close();
        }
    }
}