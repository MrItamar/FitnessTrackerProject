using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
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
        private CoachStudioUserControl _parentStudio;
        private int? _editingProgramId = null;
        public CreateProgramUserControl(int coachId, CoachStudioUserControl parentStudio)
        {
            InitializeComponent();
            _coachId = coachId;
            _parentStudio = parentStudio;
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
            string goal = ((ComboBoxItem)cmbGoal.SelectedItem)?.Content?.ToString() ?? "";
            string level = ((ComboBoxItem)cmbLevel.SelectedItem)?.Content?.ToString() ?? "";
            try
            {
                if (_editingProgramId.HasValue)
                {
                    ProgramRepository.UpdateProgram(_editingProgramId.Value, _coachId, name, goal, level, _days);
                    MessageBox.Show("Program updated successfully!", "Coach Studio");
                }
                else
                {
                    ProgramRepository.SaveProgram(_coachId, name, goal, level, _days);
                    MessageBox.Show("Program saved!", "Coach Studio");
                }

                ResetScreen();
                if(_parentStudio!=null)
                    _parentStudio.OpenProgramsTab(); // Direct method call to switch tab!
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the program: " + ex.Message, "Coach Studio");
            }
        }
        public void LoadProgramForEdit(int programId)
        {
            _editingProgramId = programId;

            // 1. Load Program info
            DataTable dtProg = DatabaseHelper.ExecuteQueryTable(
                "SELECT ProgramName, Goal, ProgramLevel FROM ProgramsTable WHERE ProgramID = ?",
                p => p.AddWithValue("?", programId));

            if (dtProg.Rows.Count == 0) return;

            txtProgramName.Text = dtProg.Rows[0]["ProgramName"].ToString();
            string goal = dtProg.Rows[0]["Goal"]?.ToString();
            string level = dtProg.Rows[0]["ProgramLevel"]?.ToString();

            foreach (ComboBoxItem item in cmbGoal.Items)
                if (item.Content.ToString() == goal) cmbGoal.SelectedItem = item;

            foreach (ComboBoxItem item in cmbLevel.Items)
                if (item.Content.ToString() == level) cmbLevel.SelectedItem = item;

            // 2. Load Days & Exercises
            _days.Clear();

            DataTable dtDays = DatabaseHelper.ExecuteQueryTable(
                "SELECT DayID, DayNumber, DayFocus FROM ProgramDaysTable WHERE ProgramID = ? ORDER BY DayNumber",
                p => p.AddWithValue("?", programId));

            foreach (DataRow dayRow in dtDays.Rows)
            {
                int dayId = Convert.ToInt32(dayRow["DayID"]);
                CoachDay day = new CoachDay
                {
                    Title = "Day " + dayRow["DayNumber"],
                    Focus = dayRow["DayFocus"]?.ToString() ?? ""
                };

                DataTable dtEx = DatabaseHelper.ExecuteQueryTable(
                    "SELECT e.ExerciseID, x.ExerciseName, x.TargetMuscle, e.Sets, e.Reps, e.RestTime " +
                    "FROM ProgramExercisesTable AS e " +
                    "INNER JOIN ExercisesTable AS x ON e.ExerciseID = x.ExerciseID " +
                    "WHERE e.DayID = ? ORDER BY e.SortOrder",
                    p => p.AddWithValue("?", dayId));

                foreach (DataRow exRow in dtEx.Rows)
                {
                    day.Exercises.Add(new CoachExercise
                    {
                        ExerciseId = Convert.ToInt32(exRow["ExerciseID"]),
                        Name = exRow["ExerciseName"].ToString(),
                        Muscle = exRow["TargetMuscle"].ToString(),
                        Sets = exRow["Sets"].ToString(),
                        Reps = exRow["Reps"].ToString(),
                        Rest = exRow["RestTime"].ToString()
                    });
                }

                _days.Add(day);
            }

            lstDays.Items.Refresh();
            if (_days.Count > 0) lstDays.SelectedIndex = 0;
            RefreshSummary();
        }
        /// <summary>Empty screen, ready for the next program (so pressing Save twice doesn't create a copy).</summary>
        private void ResetScreen()
        {
            _editingProgramId = null;
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