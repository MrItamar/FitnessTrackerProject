using FitnessTrackerProject.Models;
using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace FitnessTrackerProject
{
    public partial class CoachTraineesUserControl : UserControl
    {
        private readonly int _coachId;
        private int _activeTraineeId = 0;

        // Fixed: Swapped to TraineeWorkoutActivity to match your CoachRepository
        private List<TraineeWorkoutActivity> _allTraineeWorkouts = new List<TraineeWorkoutActivity>();
        private int _loadedWorkoutsCount = 0;
        private const int WorkoutsPerPage = 10;

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

                // Trigger the pagination logic instead of loading everything at once
                LoadTraineeWorkouts(trainee.TraineeId);
            }
            else
            {
                panelTraineeDetails.Visibility = Visibility.Collapsed;
                txtPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void OpenChat_Click(object sender, RoutedEventArgs e)
        {
            // Fixed: Cast to CoachTraineeItem, not TrainerItem
            if (lstTrainees.SelectedItem is CoachTraineeItem selectedTrainee)
            {
                // Fixed: Passed _coachId instead of currentUserId
                ChatWindow chatWin = new ChatWindow(_coachId, selectedTrainee.TraineeId, selectedTrainee.Name);
                chatWin.Show();
            }
        }

        private void LoadTraineeWorkouts(int traineeId)
        {
            // Fixed: Uses your actual CoachRepository method
            _allTraineeWorkouts = CoachRepository.GetTraineeWorkoutHistory(traineeId);
            _loadedWorkoutsCount = WorkoutsPerPage;

            RefreshWorkoutListDisplay();
        }
        private void OpenAssignProgram_Click(object sender, RoutedEventArgs e)
        {
            if (lstTrainees.SelectedItem is CoachTraineeItem selectedTrainee)
            {
                _activeTraineeId = selectedTrainee.TraineeId;

                // Load the coach's public templates
                var templates = TraineeRepository.GetProgramsByCoach(_coachId);
                lstProgramTemplates.ItemsSource = templates;

                AssignProgramOverlay.Visibility = Visibility.Visible;
            }
        }

        private void CancelAssign_Click(object sender, RoutedEventArgs e)
        {
            AssignProgramOverlay.Visibility = Visibility.Collapsed;
        }

        private void ConfirmAssign_Click(object sender, RoutedEventArgs e)
        {
            if (lstProgramTemplates.SelectedItem is ProgramChoiceItem selectedProgram)
            {
                try
                {
                    // 1. Check if they already have this specific custom program
                    string expectedCustomName = selectedProgram.Name + " (Custom)";

                    int alreadyExists = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                        "SELECT COUNT(*) FROM ProgramsTable WHERE AssignedTraineeID = ? AND ProgramName = ?",
                        p =>
                        {
                            p.AddWithValue("?", _activeTraineeId);
                            p.AddWithValue("?", expectedCustomName);
                        }));

                    // 2. Stop and warn the coach if it's already assigned
                    if (alreadyExists > 0)
                    {
                        MessageBox.Show($"{expectedCustomName} is already assigned to this trainee.", "Already Assigned");
                        return;
                    }

                    // 3. Otherwise, go ahead and duplicate it
                    ProgramRepository.AssignCustomProgramToTrainee(selectedProgram.Id, _coachId, _activeTraineeId);

                    MessageBox.Show($"'{selectedProgram.Name}' has been duplicated and assigned to the trainee!", "Success");
                    AssignProgramOverlay.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not assign program: " + ex.Message, "Error");
                }
            }
            else
            {
                MessageBox.Show("Please select a program template to assign.", "Notice");
            }
        }
        private void RefreshWorkoutListDisplay()
        {
            lstWorkoutHistory.ItemsSource = _allTraineeWorkouts.Take(_loadedWorkoutsCount).ToList();

            // Show "Load More" only if there are more workouts hiding
            if (_loadedWorkoutsCount < _allTraineeWorkouts.Count)
            {
                btnLoadMore.Visibility = Visibility.Visible;
            }
            else
            {
                btnLoadMore.Visibility = Visibility.Collapsed;
            }

            // Show "Show Less" only if we have expanded past the default first page
            if (_loadedWorkoutsCount > WorkoutsPerPage)
            {
                btnShowLess.Visibility = Visibility.Visible;
            }
            else
            {
                btnShowLess.Visibility = Visibility.Collapsed;
            }

            txtNoWorkouts.Visibility = _allTraineeWorkouts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadMoreWorkouts_Click(object sender, RoutedEventArgs e)
        {
            _loadedWorkoutsCount += WorkoutsPerPage;
            RefreshWorkoutListDisplay();
        }
        private void ShowLessWorkouts_Click(object sender, RoutedEventArgs e)
        {
            // Subtract 10, but never let it go below the default starting amount
            _loadedWorkoutsCount = Math.Max(WorkoutsPerPage, _loadedWorkoutsCount - WorkoutsPerPage);
            RefreshWorkoutListDisplay();
        }
    }
}