using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FitnessTrackerProject
{
    /// <summary>One exercise inside a program day.</summary>
    public class CoachExercise
    {
        public int ExerciseId { get; set; }
        public string Name { get; set; }
        public string Muscle { get; set; }
        public string Sets { get; set; } = "3";
        public string Reps { get; set; } = "10";
        public string Rest { get; set; } = "60s";
    }

    /// <summary>One day of the program (Day 1, Day 2, ...).</summary>
    public class CoachDay
    {
        // These must be PROPERTIES (with get/set), because WPF binding can't read fields.
        public string Title { get; set; }
        public string Focus { get; set; } = "";
        public bool IsRest { get { return Exercises.Count == 0; } }
        public string Label { get { return IsRest ? Title + " · Rest" : Title; } }
        public List<CoachExercise> Exercises { get; set; } = new List<CoachExercise>();
    }

    public partial class CreateProgramUserControl : UserControl
    {
        private readonly List<CoachDay> _days = new List<CoachDay>();
        public CreateProgramUserControl() { }
        private int _coachId;
        public event EventHandler ProgramSaved;
        public CreateProgramUserControl(int coachId)
        {
            InitializeComponent();
            _coachId = coachId;
            LoadLibrary();
            lstDays.ItemsSource = _days;
            AddDay_Click(null, null);
        }

        private void LoadLibrary()
        {
            lstLibrary.ItemsSource = ExercisePool.GetAll()
                .Select(m => new CoachExercise
                {
                    ExerciseId = m.Id,
                    Name = m.Name,
                    Muscle = m.TargetMuscle
                })
                .ToList();

            // ExercisePool doesn't throw: it stores the error and returns an empty list
            if (ExercisePool.LastLoadError != null)
                MessageBox.Show("Could not load exercises: " + ExercisePool.LastLoadError);
        }

        // =====================================================================
        //  Days
        // =====================================================================

        private void AddDay_Click(object sender, RoutedEventArgs e)
        {
            var day = new CoachDay { Title = "Day " + (_days.Count + 1) };
            _days.Add(day);

            lstDays.Items.Refresh();        // a List doesn't announce changes, so we redraw the tabs
            lstDays.SelectedItem = day;     // this also shows the new (empty) day
            RefreshSummary();
        }

        private void RemoveDay_Click(object sender, RoutedEventArgs e)
        {
            var day = lstDays.SelectedItem as CoachDay;
            if (day == null) return;

            _days.Remove(day);

            // Renumber so there is no gap (Day 1, Day 2, ...)
            for (int i = 0; i < _days.Count; i++) _days[i].Title = "Day " + (i + 1);

            lstDays.Items.Refresh();
            if (_days.Count > 0) lstDays.SelectedIndex = 0;

            ShowDay();
            RefreshSummary();
        }

        private void Days_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowDay();
        }

        /// <summary>Shows the exercises of the selected day on the screen.</summary>
        private void ShowDay()
        {
            var day = lstDays.SelectedItem as CoachDay;

            // Setting the source to null and back makes the list redraw from scratch
            lstExercises.ItemsSource = null;
            lstExercises.ItemsSource = day?.Exercises;

            txtEmpty.Visibility = (day != null && day.Exercises.Count == 0)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // =====================================================================
        //  Exercises
        // =====================================================================

        private void AddFromLibrary_Click(object sender, RoutedEventArgs e)
        {
            var day = lstDays.SelectedItem as CoachDay;
            if (day == null)
            {
                MessageBox.Show("Add a day first.");
                return;
            }

            var source = (CoachExercise)((Button)sender).Tag;
            // Copy it, so editing sets/reps of one day doesn't change the library or other days
            day.Exercises.Add(new CoachExercise
            {
                ExerciseId = source.ExerciseId,
                Name = source.Name,
                Muscle = source.Muscle,
                Sets = source.Sets,
                Reps = source.Reps,
                Rest = source.Rest
            });

            ShowDay();
            RefreshTabs();
            RefreshSummary();
        }

        private void RemoveExercise_Click(object sender, RoutedEventArgs e)
        {
            var day = lstDays.SelectedItem as CoachDay;
            var exercise = (CoachExercise)((Button)sender).Tag;
            if (day == null) return;
            day.Exercises.Remove(exercise);

            ShowDay();
            RefreshTabs();
            RefreshSummary();
        }

        private void SaveProgram_Click(object sender, RoutedEventArgs e)
        {
            string name = txtProgramName.Text.Trim();

            if (name == "")
            {
                MessageBox.Show("Give the program a name.");
                return;
            }
            if (name.Length > 100)
            {
                MessageBox.Show("The program name is too long (max 100 characters).");
                return;
            }
            if (_coachId == 0)
            {
                MessageBox.Show("No coach is logged in, so the program can't be saved.");
                return;
            }
            if (_days.All(d => d.Exercises.Count == 0))
            {
                MessageBox.Show("Add at least one exercise. A program can't be only rest days.");
                return;
            }
            // Sets, reps and rest are required and limited to 20 characters in the table
            if (_days.Any(d => d.Exercises.Any(x =>
                    string.IsNullOrWhiteSpace(x.Sets) || string.IsNullOrWhiteSpace(x.Reps) || string.IsNullOrWhiteSpace(x.Rest) ||
                    x.Sets.Length > 20 || x.Reps.Length > 20 || x.Rest.Length > 20)))
            {
                MessageBox.Show("Every exercise needs sets, reps and rest (up to 20 characters each).");
                return;
            }

            string goal = ((ComboBoxItem)cmbGoal.SelectedItem).Content.ToString();
            string level = ((ComboBoxItem)cmbLevel.SelectedItem).Content.ToString();

            try
            {
                ProgramRepository.SaveProgram(_coachId, name, goal, level, _days);
                MessageBox.Show("Program saved!", "Coach Studio");
                ResetScreen();
                ProgramSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the program: " + ex.Message, "Coach Studio");
            }
        }

        /// <summary>Empty screen, ready for the next program (so pressing Save twice doesn't create a copy).</summary>
        private void ResetScreen()
        {
            txtProgramName.Text = "";
            _days.Clear();
            lstDays.Items.Refresh();
            AddDay_Click(null, null);   // start again with Day 1
        }

        private void RefreshSummary()
        {
            txtDaysCount.Text = _days.Count.ToString();
            txtExercisesCount.Text = _days.Sum(d => d.Exercises.Count).ToString();
        }
        private void RefreshTabs()
        {
            var selected = lstDays.SelectedItem;
            lstDays.Items.Refresh();
            lstDays.SelectedItem = selected;   // makes sure the selection doesn't jump
        }
    }
}