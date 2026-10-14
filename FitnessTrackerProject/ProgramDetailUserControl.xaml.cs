using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace FitnessTrackerProject
{
    public partial class ProgramDetailUserControl : UserControl
    {
        private int _userId;

        public ProgramDetailUserControl(int userId, int programId)
        {
            InitializeComponent();
            _userId = userId;
            LoadProgram(programId);
        }

        private void LoadProgram(int programId)
        {
            try
            {
                ProgramChoiceItem program = TraineeRepository.GetProgramById(programId);

                if (program == null)
                {
                    txtName.Text = "This program no longer exists";
                    return;
                }

                txtName.Text = program.Name;
                txtSubtitle.Text = program.Subtitle;
                txtCoach.Text = "by " + program.CoachName;
                txtInitial.Text = program.Initial;
                picBrush.ImageSource = program.Picture;

                List<ProgramDayItem> days = TraineeRepository.GetProgramDays(programId);
                lstDays.ItemsSource = days;

                if (days.Count == 0) txtNoDays.Visibility = Visibility.Visible;
                else txtNoDays.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load the program: " + ex.Message);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigationService navigation = NavigationService.GetNavigationService(this);
            if (navigation != null)
            {
                navigation.Navigate(new WorkoutHubUserControl(_userId));
            }
        }

        // "Start" opens the WorkoutHub with this exercise already selected
        private void StartExercise_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string exerciseName = button.Tag as string;

            ExercisePool.Initialize();
            if (ExercisePool.GetByName(exerciseName) == null)
            {
                MessageBox.Show(exerciseName + " can't be counted from a video yet.");
                return;
            }

            NavigationService navigation = NavigationService.GetNavigationService(this);
            if (navigation != null)
            {
                navigation.Navigate(new WorkoutHubUserControl(_userId, exerciseName));
            }
        }
    }
}