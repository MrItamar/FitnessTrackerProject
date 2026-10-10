using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Input;

namespace FitnessTrackerProject
{
    /// <summary>One exercise box. Later these will come from the database.</summary>
    public class ExerciseItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Glyph { get; set; }          // icon letter from the Segoe MDL2 Assets font
        public bool IsAnalyzable { get; set; }     // true = the app can count it from a video
    }

    public partial class AllExercisesWindow : Window
    {
        /// <summary>The exercise the user clicked, or null if the window was just closed.</summary>
        public ExerciseItem SelectedExercise { get; private set; }

        public AllExercisesWindow()
        {
            InitializeComponent();
            lstExercises.ItemsSource = LoadExercises();
        }

        /// <summary>
        /// a query on your Exercises table and build one ExerciseItem per row.
        /// </summary>
        private List<ExerciseItem> LoadExercises()
        {
            var exercises = new List<ExerciseItem>();

            try
            {
                // Initialize and pull all exercises from the centralized pool cache
                ExercisePool.Initialize();
                var poolModels = ExercisePool.GetAll();

                foreach (var model in poolModels)
                {
                    exercises.Add(new ExerciseItem
                    {
                        Id = model.Id,
                        Name = model.Name,
                        Description = model.Description,
                        Glyph = "\uE716", // Default glyph icon
                        IsAnalyzable = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading exercises from pool: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return exercises;
        }

        private void ExerciseCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Every box's DataContext is the ExerciseItem it was made from
            FrameworkElement box = (FrameworkElement)sender;
            SelectedExercise = box.DataContext as ExerciseItem;

            DialogResult = true;   // closes the window and tells the caller "something was picked"
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}