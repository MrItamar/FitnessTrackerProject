using FitnessTrackerProject.Scripts;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FitnessTrackerProject
{
    public partial class WorkoutHubUserControl : UserControl
    {
        private int currentUserId;

        private string _selectedExercise;
        private string _cardAExercise;
        private string _cardBExercise;

        public ExerciseModel SelectedExerciseType
        {
            get
            {
                return ExercisePool.GetByName(_selectedExercise);
            }
        }
        public WorkoutHubUserControl()
        {
            InitializeComponent();
            ExercisePool.Initialize();
            LoadCatalogFromPool();
        }

        public WorkoutHubUserControl(int userId)
        {
            InitializeComponent();
            currentUserId = userId;
            ExercisePool.Initialize();
            LoadCatalogFromPool();
        }

        // ================================================================
        // Load exercises from the pool
        // ================================================================

        private void LoadCatalogFromPool()
        {
            List<ExerciseModel> exercises = ExercisePool.GetAll();
            if (exercises == null || exercises.Count == 0) return;

            // Assign default cards from the pool if available
            _cardAExercise = exercises[0].Name;
            _cardBExercise = exercises.Count > 1 ? exercises[1].Name : exercises[0].Name;
            _selectedExercise = _cardAExercise;

            FillCard(cardA, iconA, nameA, subA, _cardAExercise);
            FillCard(cardB, iconB, nameB, subB, _cardBExercise);

            ExerciseModel currentModel = ExercisePool.GetByName(_selectedExercise);
            if (currentModel != null)
            {
                txtHint.Text = currentModel.IsHorizontal ? "Side view, whole body in frame" : "Side view, full body visible";
            }
        }

        // ================================================================
        // Selecting exercises
        // ================================================================

        private void ExerciseCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border card)) return;

            // Check if tag is ExerciseID (int) or exercise name (string)
            if (card.Tag is int exerciseId)
            {
                ExerciseModel model = ExercisePool.GetAll().FirstOrDefault(x => x.Id == exerciseId);
                if (model != null)
                {
                    SelectExercise(model.Name);
                }
            }
            else if (card.Tag is string exerciseName)
            {
                SelectExercise(exerciseName);
            }
        }

        private void SelectExercise(string exercise)
        {
            ExerciseModel model = ExercisePool.GetByName(exercise);
            if (model == null) return;

            // An exercise selected from All Exercises replaces the card that is not currently selected.
            if (!string.Equals(exercise, _cardAExercise, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(exercise, _cardBExercise, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(_selectedExercise, _cardAExercise, StringComparison.OrdinalIgnoreCase))
                {
                    _cardBExercise = exercise;
                }
                else
                {
                    _cardAExercise = exercise;
                }
            }

            _selectedExercise = exercise;

            FillCard(cardA, iconA, nameA, subA, _cardAExercise);
            FillCard(cardB, iconB, nameB, subB, _cardBExercise);

            txtHint.Text = model.Hint;
        }

        private void FillCard(Border card, TextBlock icon, TextBlock name, TextBlock subtitle, string exercise)
        {
            ExerciseModel model = ExercisePool.GetByName(exercise);
            if (model == null)
            {
                card.Visibility = Visibility.Collapsed;
                card.Tag = null;
                return;
            }

            card.Visibility = Visibility.Visible;
            card.Tag = model.Id; // Tagged with the database ExerciseID

            name.Text = model.Name;
            subtitle.Text = $"Target: {model.TargetMuscle}";

            // Assign icon glyph based on exercise name or type
            icon.Text = model.Name.IndexOf("plank", StringComparison.OrdinalIgnoreCase) >= 0 ? "\uE121" : "\uE716";

            bool selected = string.Equals(exercise, _selectedExercise, StringComparison.OrdinalIgnoreCase);

            card.BorderBrush = Hex(selected ? "#3B82F6" : "#E5E7EB");
            card.Background = Hex(selected ? "#EFF6FF" : "#FFFFFF");
        }

        private static Brush Hex(string color)
        {
            return (Brush)new BrushConverter().ConvertFromString(color);
        }

        // ================================================================
        // All exercises window
        // ================================================================

        private void AllExercises_Click(object sender, RoutedEventArgs e)
        {
            var window = new AllExercisesWindow
            {
                Owner = Window.GetWindow(this)
            };

            if (window.ShowDialog() == true && window.SelectedExercise != null)
            {
                string name = window.SelectedExercise.Name;
                ExerciseModel model = ExercisePool.GetByName(name);

                if (model != null)
                {
                    SelectExercise(name);
                }
                else
                {
                    MessageBox.Show(name + " can't be counted from a video yet.", "Unsupported Exercise", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // ================================================================
        // Video upload & Live camera execution
        // ================================================================

        private void SelectVideoButton_Click(object sender, RoutedEventArgs e)
        {
            ExerciseModel model = SelectedExerciseType;
            if (model == null)
            {
                MessageBox.Show("There are no exercises available.", "Workout Tracker", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov|All Files|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ProcessVideo(dialog.FileName, model);
            }
        }

        private void LiveCameraButton_Click(object sender, RoutedEventArgs e)
        {
            ExerciseModel model = SelectedExerciseType;
            if (model == null)
            {
                MessageBox.Show("There are no exercises available.", "Workout Tracker", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.ProcessVideo(null, model);
            }
        }
    }
}