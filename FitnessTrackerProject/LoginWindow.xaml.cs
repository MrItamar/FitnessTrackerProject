using System;
using System.Windows;
using System.Data.OleDb;
using System.Security.Cryptography;
using System.Text;

namespace FitnessTrackerProject
{
    public partial class LoginWindow : Window
    {
        // IMPORTANT: Make sure this is the exact same path you used in Window1!
        string connectionString = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\Users\Itamar\source\repos\FitnessTrackerProject\FitnessTrackerProject\DataBase\FitnessTrackerDB1.accdb;";

        public LoginWindow()
        {
            InitializeComponent();
        }

        // We need the exact same hashing function so the passwords match
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
                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    connection.Open();

                    // Check if there is a row where BOTH the username and the hashed password match
                    string query = "SELECT COUNT(*) FROM Users WHERE Username = @Name AND [Password] = @Password";
                    using (OleDbCommand command = new OleDbCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", username);
                        command.Parameters.AddWithValue("@Password", securedPassword);

                        int matchCount = (int)command.ExecuteScalar();

                        if (matchCount > 0)
                        {
                            MessageBox.Show("Login Successful!", "Welcome", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Go to the AI Tracking window
                            MainWindow mainAppWindow = new MainWindow();
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
            // Takes the user back to the registration screen
            Window1 signUpWindow = new Window1();
            signUpWindow.Show();
            this.Close();
        }
    }
}