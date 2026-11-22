using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FitnessTrackerProject.Models;
using ViewModel.Scripts;

namespace View
{
    public partial class MainWindow : Window
    {
        private readonly WorkoutAnalyzer _analyzer = new WorkoutAnalyzer();
        public static int currentUserId;
        private bool IsCoach;

        public MainWindow() { }

        public MainWindow(int userId)
        {
            InitializeComponent();
            currentUserId = userId;

            try
            {
                IsCoach = CoachRepository.IsCoach(currentUserId);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not check your role: " + ex.Message);
                IsCoach = false;
            }

            if (IsCoach) CoachStudioBtn.Visibility = Visibility.Visible;
            else ExploreBtn.Visibility = Visibility.Visible;

            MainFrame.Navigate(new WorkoutHubUserControl(currentUserId));

            try
            {
                _analyzer.LoadModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Model did not load correctly: {ex.Message}");
            }
        }

        public void ProcessVideo(string video, ExerciseModel exercise)
        {
            if (!_analyzer.IsModelLoaded) return;

            this.Cursor = Cursors.Wait;
            try
            {
                bool saved = _analyzer.ProcessAndSaveVideo(video, exercise, currentUserId);
                if (saved)
                {
                    MainFrame.Navigate(new WorkoutHubUserControl(currentUserId));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save workout: " + ex.Message);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void OpenSidebar(object sender, RoutedEventArgs e)
        {
            if (Sidebar.Visibility == Visibility.Visible)
            {
                Sidebar.Visibility = Visibility.Hidden;
                MainContentTransform.X = 0;
            }
            else
            {
                Sidebar.Visibility = Visibility.Visible;
                MainContentTransform.X = 200;
            }
        }

        private void SwitchScreen(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int screenIndex = Convert.ToInt32(btn.Tag);

            switch (screenIndex)
            {
                case 0:
                    MainFrame.Navigate(new WorkoutHubUserControl(currentUserId));
                    break;
                case 1:
                    MainFrame.Navigate(new CoachStudioUserControl(currentUserId));
                    break;
                case 2:
                    MainFrame.Navigate(new ProfileUserControl(currentUserId));
                    break;
                case 3:
                    MainFrame.Navigate(new ExploreUserControl(currentUserId));
                    break;
            }
        }
    }
}