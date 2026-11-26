using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ViewModel.Scripts;
using FitnessTrackerProject.Models;

namespace View
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
            UserProfileData profile = UserRepository.GetProfile(currentUserId);

            if (profile == null)
            {
                MessageBox.Show("Could not find this user profile.");
                return;
            }

            // General user information
            txtUsername.Text = profile.Username;
            txtAge.Text = profile.Age.ToString();

            SelectOrAdd(cmbGender, profile.Gender);

            if (!string.IsNullOrEmpty(profile.ProfilePicPath) &&
                File.Exists(profile.ProfilePicPath))
            {
                imgProfile.Source = new BitmapImage(new Uri(profile.ProfilePicPath));
            }

            // Role-specific visibility
            bool isCoach = string.Equals(
                profile.UserRole, "Coach",
                StringComparison.OrdinalIgnoreCase);

            panelCoach.Visibility = isCoach ? Visibility.Visible : Visibility.Collapsed;
            panelTrainee.Visibility = isCoach ? Visibility.Collapsed : Visibility.Visible;

            // Role-specific information
            if (isCoach)
            {
                txtSpecialty.Text = profile.Specialty;
                txtBio.Text = profile.Bio;
            }
            else
            {
                txtHeight.Text = profile.Height.ToString();
                txtWeight.Text = profile.Weight.ToString();

                SelectOrAdd(cmbFitnessGoal, profile.FitnessGoal);
            }
        }

        //selecting a profile picture
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
            //fitnessGoal is free to edit
            string fitnessGoal = cmbFitnessGoal.Text;

            //  Save to db
            try
            {
                // Get the user's role
                UserProfileData profile = UserRepository.GetProfile(currentUserId);

                if (profile == null)
                {
                    MessageBox.Show("User profile not found.");
                    return;
                }

                string role = profile.UserRole;

                // Update profile through UserRepository
                UserRepository.UpdateProfile(
                    currentUserId,
                    txtUsername.Text.Trim(),
                    age,
                    gender,
                    newPicPath,
                    role,
                    height,
                    weight,
                    fitnessGoal,
                    txtSpecialty.Text.Trim(),
                    txtBio.Text.Trim()
                );

                MessageBox.Show("Profile updated successfully!");
            }
            catch (Exception ex)
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

            // What the text would look like typing this keystroke
            string proposed = box.Text
                .Remove(box.SelectionStart, box.SelectionLength)
                .Insert(box.SelectionStart, e.Text);
            //checking proposed for 3 digits: \d{0,3} and two decimals after a , or .: [.,]\d{0,2})?
            //if true this block the keypress
            e.Handled = !Regex.IsMatch(proposed, @"^\d{0,3}([.,]\d{0,2})?$");
        }

        //Empty text counts as 0, accepts both "70.5" and "70,5"
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

        // selects the item with this text, if the list doesn't have it, adds it first.
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

        //deleting account
        private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to permanently delete your account?\n\n" +
                "This action cannot be undone.",
                "Delete Account",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                UserRepository.DeleteAccount(currentUserId);

                MessageBox.Show(
                    "Your account has been successfully deleted.",
                    "Account Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Return to login window
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();

                Window.GetWindow(this)?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not delete your account: " + ex.Message,
                    "Deletion Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}