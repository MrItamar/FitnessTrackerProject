using FitnessTrackerProject.Scripts;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FitnessTrackerProject
{
    public partial class ExploreUserControl : UserControl
    {
        private int _traineeId;

        public ExploreUserControl(int traineeId)
        {
            InitializeComponent();
            _traineeId = traineeId;
            LoadLists();
        }

        // Reads the database and fills the screen
        private void LoadLists()
        {
            try
            {
                int? trainerId;
                int? programId;
                TraineeRepository.GetSelection(_traineeId, out trainerId, out programId);

                var trainers = TraineeRepository.GetTrainers(trainerId);
                var programs = TraineeRepository.GetPrograms(programId);

                lstTrainers.ItemsSource = trainers;
                lstPrograms.ItemsSource = programs;

                if (trainers.Count == 0) txtNoTrainers.Visibility = Visibility.Visible;
                else txtNoTrainers.Visibility = Visibility.Collapsed;

                if (programs.Count == 0) txtNoPrograms.Visibility = Visibility.Visible;
                else txtNoPrograms.Visibility = Visibility.Collapsed;

                // The box at the top: start with "nothing chosen"...
                txtCurrentTrainer.Text = "Your trainer: none chosen yet";
                btnClearTrainer.Visibility = Visibility.Collapsed;

                txtCurrentProgram.Text = "Your program: none chosen yet";
                btnClearProgram.Visibility = Visibility.Collapsed;

                // ...then fill in the chosen trainer and program, if there are any
                foreach (TrainerItem trainer in trainers)
                {
                    if (trainer.IsSelected)
                    {
                        txtCurrentTrainer.Text = "Your trainer: " + trainer.Name;
                        btnClearTrainer.Visibility = Visibility.Visible;
                    }
                }

                foreach (ProgramChoiceItem program in programs)
                {
                    if (program.IsSelected)
                    {
                        txtCurrentProgram.Text = "Your program: " + program.Name + " (by " + program.CoachName + ")";
                        btnClearProgram.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load the Explore screen: " + ex.Message);
            }
        }

        private void ChooseTrainer_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            int trainerId = Convert.ToInt32(button.Tag);

            try
            {
                TraineeRepository.SetTrainer(_traineeId, trainerId);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save your choice: " + ex.Message);
            }

            LoadLists();
        }

        private void ChooseProgram_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            int programId = Convert.ToInt32(button.Tag);

            try
            {
                TraineeRepository.SetProgram(_traineeId, programId);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save your choice: " + ex.Message);
            }

            LoadLists();
        }

        private void ClearTrainer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TraineeRepository.SetTrainer(_traineeId, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not remove your trainer: " + ex.Message);
            }

            LoadLists();
        }

        private void ClearProgram_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TraineeRepository.SetProgram(_traineeId, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not remove your program: " + ex.Message);
            }

            LoadLists();
        }
    }
}