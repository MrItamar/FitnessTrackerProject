using FitnessTrackerProject.Scripts;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FitnessTrackerProject
{
    public partial class CoachTraineesUserControl : UserControl
    {
        private readonly int _coachId;

        public CoachTraineesUserControl() : this(0) { }

        public CoachTraineesUserControl(int coachId)
        {
            InitializeComponent();
            _coachId = coachId;
            Reload();
        }

        public void Reload()
        {
            List<CoachTraineeItem> trainees = CoachRepository.GetTraineesForCoach(_coachId);
            lstTrainees.ItemsSource = trainees;

            bool hasTrainees = trainees.Count > 0;
            txtNoTrainees.Visibility = hasTrainees ? Visibility.Collapsed : Visibility.Visible;

            if (hasTrainees)
            {
                lstTrainees.SelectedIndex = 0;
            }
            else
            {
                panelTraineeDetails.Visibility = Visibility.Collapsed;
                txtPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void LstTrainees_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstTrainees.SelectedItem is CoachTraineeItem trainee)
            {
                txtPlaceholder.Visibility = Visibility.Collapsed;
                panelTraineeDetails.Visibility = Visibility.Visible;

                txtSelectedTraineeName.Text = trainee.Name;
                txtSelectedTraineeMeta.Text = $"{trainee.Goal} · Total Workouts: {trainee.TotalWorkouts}";

                List<TraineeWorkoutActivity> workouts = CoachRepository.GetTraineeWorkoutHistory(trainee.TraineeId);
                lstWorkoutHistory.ItemsSource = workouts;
                txtNoWorkouts.Visibility = workouts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                panelTraineeDetails.Visibility = Visibility.Collapsed;
                txtPlaceholder.Visibility = Visibility.Visible;
            }
        }
    }
}