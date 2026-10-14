using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;
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
                int? trainerId = TraineeRepository.GetTrainerId(_traineeId);
                HashSet<int> programIds = TraineeRepository.GetProgramIds(_traineeId);

                List<TrainerItem> trainers = TraineeRepository.GetTrainers(trainerId);
                List<ProgramChoiceItem> programs = TraineeRepository.GetPrograms(programIds);

                lstTrainers.ItemsSource = trainers;
                lstPrograms.ItemsSource = programs;

                if (trainers.Count == 0) txtNoTrainers.Visibility = Visibility.Visible;
                else txtNoTrainers.Visibility = Visibility.Collapsed;

                if (programs.Count == 0) txtNoPrograms.Visibility = Visibility.Visible;
                else txtNoPrograms.Visibility = Visibility.Collapsed;

                // The "Your choices" box
                txtCurrentTrainer.Text = "Your trainer: none chosen yet";
                btnClearTrainer.Visibility = Visibility.Collapsed;

                foreach (TrainerItem trainer in trainers)
                {
                    if (trainer.IsSelected)
                    {
                        txtCurrentTrainer.Text = "Your trainer: " + trainer.Name;
                        btnClearTrainer.Visibility = Visibility.Visible;
                    }
                }

                int added = 0;
                foreach (ProgramChoiceItem program in programs)
                {
                    if (program.IsSelected) added++;
                }

                if (added == 0) txtCurrentProgram.Text = "Your programs: none added yet";
                else txtCurrentProgram.Text = "Your programs: " + added + " added";
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

        // One button does both: adds the program, or removes it if it was already added
        private void ChooseProgram_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            ProgramChoiceItem program = (ProgramChoiceItem)button.DataContext;

            try
            {
                if (program.IsSelected) TraineeRepository.RemoveProgram(_traineeId, program.Id);
                else TraineeRepository.AddProgram(_traineeId, program.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save your choice: " + ex.Message);
            }

            LoadLists();
        }
    }
}