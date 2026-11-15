using FitnessTrackerProject.Scripts;
using Microsoft.Win32;
using System;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace FitnessTrackerProject
{
    public partial class ProfileUserControl : UserControl
    {
        private int currentUserId;
        private string newPicPath = null;

        public ProfileUserControl(int userId)
        {
            InitializeComponent();
            currentUserId = userId;
            LoadUserProfile();
        }

        private void LoadUserProfile()
        {
            using (OleDbConnection conn = DatabaseHelper.GetConnection())
            {
                conn.Open();

                // Simple query for basic User info
                string userQuery = "SELECT Username, Age, Gender, ProfilePicPath, UserRole FROM Users WHERE ID = ?";
                string role = "";

                using (OleDbCommand cmd = new OleDbCommand(userQuery, conn))
                {
                    cmd.Parameters.AddWithValue("?", currentUserId);
                    using (OleDbDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtUsername.Text = reader["Username"]?.ToString();
                            txtAge.Text = reader["Age"]?.ToString();
                            SelectOrAdd(cmbGender, reader["Gender"]?.ToString());

                            string picPath = reader["ProfilePicPath"]?.ToString();
                            if (!string.IsNullOrEmpty(picPath) && File.Exists(picPath))
                            {
                                imgProfile.Source = new BitmapImage(new Uri(picPath));
                            }

                            role = reader["UserRole"]?.ToString().Trim();
                        }
                    }
                }

                // Simple separate queries based on the role
                bool isCoach = string.Equals(role, "Coach", StringComparison.OrdinalIgnoreCase);
                panelCoach.Visibility = isCoach ? Visibility.Visible : Visibility.Collapsed;
                panelTrainee.Visibility = isCoach ? Visibility.Collapsed : Visibility.Visible;

                if (isCoach)
                {
                    string coachQuery = "SELECT Specialty, Bio FROM Trainers WHERE UserID = ?";
                    using (OleDbCommand cmd = new OleDbCommand(coachQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("?", currentUserId);
                        using (OleDbDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtSpecialty.Text = reader["Specialty"]?.ToString();
                                txtBio.Text = reader["Bio"]?.ToString();
                            }
                        }
                    }
                }
                else
                {
                    //query for trainee stats
                    string traineeQuery = "SELECT Height, Weight, FitnessGoal FROM Trainees WHERE UserID = ?";
                    using (OleDbCommand cmd = new OleDbCommand(traineeQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("?", currentUserId);
                        using (OleDbDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtHeight.Text = reader["Height"]?.ToString();
                                txtWeight.Text = reader["Weight"]?.ToString();
                                cmbFitnessGoal.Text = reader["FitnessGoal"]?.ToString();
                            }
                        }
                    }
                }
            }
        }

        private void BtnChangePic_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Image Files(*.jpg; *.jpeg; *.png)|*.jpg; *.jpeg; *.png";
            if (ofd.ShowDialog() == true)
            {
                newPicPath = ofd.FileName;
                imgProfile.Source = new BitmapImage(new Uri(newPicPath));
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Check the input before touching the database
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                MessageBox.Show("Please enter a username.");
                return;
            }

            int age = 0;
            if (!string.IsNullOrWhiteSpace(txtAge.Text) && !int.TryParse(txtAge.Text, out age))
            {
                MessageBox.Show("Age must be a whole number.");
                return;
            }

            double height, weight;
            if (!TryParseNumber(txtHeight.Text, out height))
            {
                MessageBox.Show("Height must be a number, for example 175.5");
                return;
            }
            if (!TryParseNumber(txtWeight.Text, out weight))
            {
                MessageBox.Show("Weight must be a number, for example 70.5");
                return;
            }

            string gender = ItemText(cmbGender.SelectedItem) ?? "";
            string fitnessGoal = cmbFitnessGoal.Text;

            //  Save to db
            try
            {
                using (OleDbConnection conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    string updateUsers = "UPDATE Users SET Username = ?, Age = ?, Gender = ? WHERE ID = ?";
                    using (OleDbCommand cmdUsers = new OleDbCommand(updateUsers, conn))
                    {
                        cmdUsers.Parameters.AddWithValue("?", txtUsername.Text.Trim());
                        cmdUsers.Parameters.AddWithValue("?", age);
                        cmdUsers.Parameters.AddWithValue("?", gender);
                        cmdUsers.Parameters.AddWithValue("?", currentUserId);
                        cmdUsers.ExecuteNonQuery();
                    }

                    string updateTrainees = "UPDATE Trainees SET Height = ?, Weight = ?, FitnessGoal = ? WHERE UserID = ?";
                    using (OleDbCommand cmdTrainees = new OleDbCommand(updateTrainees, conn))
                    {
                        cmdTrainees.Parameters.AddWithValue("?", height);
                        cmdTrainees.Parameters.AddWithValue("?", weight);
                        cmdTrainees.Parameters.AddWithValue("?", fitnessGoal);
                        cmdTrainees.Parameters.AddWithValue("?", currentUserId);
                        cmdTrainees.ExecuteNonQuery();
                    }

                    if (!string.IsNullOrEmpty(newPicPath))
                    {
                        string updatePic = "UPDATE Users SET ProfilePicPath = ? WHERE ID = ?";
                        using (OleDbCommand cmdPic = new OleDbCommand(updatePic, conn))
                        {
                            cmdPic.Parameters.AddWithValue("?", newPicPath);
                            cmdPic.Parameters.AddWithValue("?", currentUserId);
                            cmdPic.ExecuteNonQuery();
                        }
                    }
                }

                MessageBox.Show("Profile updated successfully!");
            }
            catch (OleDbException ex)
            {
                MessageBox.Show("Could not save the profile: " + ex.Message);
            }
        }

        //  Input restrictions
        private void Digits_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        //weight boxes: up to 3 digits, optionally a dot and up to 2 decimals
        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox box = (TextBox)sender;

            // What the text would look like AFTER this keystroke
            string proposed = box.Text
                .Remove(box.SelectionStart, box.SelectionLength)
                .Insert(box.SelectionStart, e.Text);

            e.Handled = !Regex.IsMatch(proposed, @"^\d{0,3}([.,]\d{0,2})?$");
        }

        //Empty text counts as 0. Accepts both "70.5" and "70,5"
        private static bool TryParseNumber(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return true;

            return double.TryParse(text.Trim().Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // the visible text of a dropdown item.
        private static string ItemText(object item)
        {
            ComboBoxItem comboItem = item as ComboBoxItem;
            object content = comboItem != null ? comboItem.Content : item;
            return content?.ToString();
        }

        // Selects the item with this text. If the list doesn't have it, adds it first.
        private static void SelectOrAdd(ComboBox box, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            value = value.Trim();

            foreach (object item in box.Items)
            {
                if (string.Equals(ItemText(item), value, StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedItem = item;
                    return;
                }
            }

            ComboBoxItem extra = new ComboBoxItem { Content = value };
            box.Items.Add(extra);
            box.SelectedItem = extra;
        }

        //private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
        //{
        //    MessageBoxResult result = MessageBox.Show(
        //        "Are you sure you want to permanently delete your account?\n\n" +
        //        "This action cannot be undone.",
        //        "Delete Account",
        //        MessageBoxButton.YesNo,
        //        MessageBoxImage.Warning);

        //    if (result != MessageBoxResult.Yes)
        //        return;

        //    try
        //    {
        //        using (OleDbConnection conn = DatabaseHelper.GetConnection())
        //        {
        //            conn.Open();

        //            using (OleDbTransaction transaction = conn.BeginTransaction())
        //            {
        //                try
        //                {
        //                    // Delete the role-specific records first
        //                    using (OleDbCommand cmd = new OleDbCommand(
        //                        "DELETE FROM Trainees WHERE UserID = ?", conn, transaction))
        //                    {
        //                        cmd.Parameters.AddWithValue("?", currentUserId);
        //                        cmd.ExecuteNonQuery();
        //                    }

        //                    using (OleDbCommand cmd = new OleDbCommand(
        //                        "DELETE FROM Trainers WHERE UserID = ?", conn, transaction))
        //                    {
        //                        cmd.Parameters.AddWithValue("?", currentUserId);
        //                        cmd.ExecuteNonQuery();
        //                    }

        //                    // Delete the main user record
        //                    using (OleDbCommand cmd = new OleDbCommand(
        //                        "DELETE FROM Users WHERE ID = ?", conn, transaction))
        //                    {
        //                        cmd.Parameters.AddWithValue("?", currentUserId);

        //                        int rowsAffected = cmd.ExecuteNonQuery();

        //                        if (rowsAffected == 0)
        //                            throw new Exception("The user account could not be found.");
        //                    }

        //                    transaction.Commit();
        //                }
        //                catch
        //                {
        //                    transaction.Rollback();
        //                    throw;
        //                }
        //            }
        //        }

        //        MessageBox.Show(
        //            "Your account has been successfully deleted.",
        //            "Account Deleted",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Information);

        //        // Return to the login window
        //        LoginWindow loginWindow = new LoginWindow();
        //        loginWindow.Show();

        //        Window.GetWindow(this)?.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(
        //            "Could not delete your account: " + ex.Message,
        //            "Deletion Error",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Error);
        //    }
        //}
    }
}