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
            string query = @"SELECT Users.Username, Users.Age, Users.Gender, Users.ProfilePicPath, 
                             Trainees.Height, Trainees.Weight, Trainees.FitnessGoal 
                             FROM Users LEFT JOIN Trainees ON Users.ID = Trainees.UserID 
                             WHERE Users.ID = ?";

            using (OleDbConnection conn = DatabaseHelper.GetConnection())
            {
                using (OleDbCommand cmd = new OleDbCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("?", currentUserId);
                    conn.Open();
                    using (OleDbDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtUsername.Text = reader["Username"]?.ToString();
                            txtAge.Text = reader["Age"]?.ToString();
                            txtHeight.Text = reader["Height"]?.ToString();
                            txtWeight.Text = reader["Weight"]?.ToString();

                            // Dropdowns: select the saved value (if it isn't in the list, it is added so nothing is lost)
                            SelectOrAdd(cmbGender, reader["Gender"]?.ToString());
                            cmbFitnessGoal.Text = reader["FitnessGoal"]?.ToString();

                            string picPath = reader["ProfilePicPath"]?.ToString();
                            if (!string.IsNullOrEmpty(picPath) && File.Exists(picPath))
                            {
                                imgProfile.Source = new BitmapImage(new Uri(picPath));
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
            // ---- 1. Check the input BEFORE touching the database ----
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

            // ---- 2. Save ----
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

        // =====================================================================
        //  Input restrictions (typing letters in a number box is blocked)
        // =====================================================================

        /// <summary>Age box: digits only.</summary>
        private void Digits_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        /// <summary>Height / weight boxes: up to 3 digits, optionally a dot and up to 2 decimals.</summary>
        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox box = (TextBox)sender;

            // What the text would look like AFTER this keystroke
            string proposed = box.Text
                .Remove(box.SelectionStart, box.SelectionLength)
                .Insert(box.SelectionStart, e.Text);

            e.Handled = !Regex.IsMatch(proposed, @"^\d{0,3}([.,]\d{0,2})?$");
        }

        // =====================================================================
        //  Small helpers
        // =====================================================================

        /// <summary>Empty text counts as 0. Accepts both "70.5" and "70,5".</summary>
        private static bool TryParseNumber(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return true;

            return double.TryParse(text.Trim().Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>The visible text of a dropdown item.</summary>
        private static string ItemText(object item)
        {
            ComboBoxItem comboItem = item as ComboBoxItem;
            object content = comboItem != null ? comboItem.Content : item;
            return content?.ToString();
        }

        /// <summary>Selects the item with this text. If the list doesn't have it, adds it first.</summary>
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
    }
}