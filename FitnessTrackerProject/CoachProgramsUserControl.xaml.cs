using FitnessTrackerProject.Scripts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FitnessTrackerProject
{
    // Small classes that hold what the screen shows (properties, so WPF binding can read them)
    public class ProgramItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Subtitle { get; set; }   // "Build strength · Beginner"
        public string Stats { get; set; }      // "3 days · 8 exercises"
        public string Created { get; set; }
    }

    public class ProgramDayView
    {
        public string Header { get; set; }                       // "Day 1 · Legs"
        public List<ProgramLineView> Lines { get; set; } = new List<ProgramLineView>();
    }

    public class ProgramLineView
    {
        public string Name { get; set; }
        public string Detail { get; set; }                       // "3 x 10 · rest 60s"
    }

    public partial class CoachProgramsUserControl : UserControl
    {
        private readonly int _coachId;

        public event EventHandler CreateRequested;   // the host switches to the "Create program" tab

        public CoachProgramsUserControl() : this(0) { }

        public CoachProgramsUserControl(int coachId)
        {
            InitializeComponent();
            _coachId = coachId;
        }
        /// <summary>Reads this coach's programs from the database and shows them.</summary>
        public void Reload()
        {
            int selectedId = (lstPrograms.SelectedItem as ProgramItem)?.Id ?? -1;
            var items = new List<ProgramItem>();

            try
            {
                // 1. The programs themselves (a simple query)
                DataTable programs = DatabaseHelper.ExecuteQueryTable(
                    "SELECT ProgramID, ProgramName, Goal, ProgramLevel, CreatedDate " +
                    "FROM ProgramsTable WHERE CoachID = ? ORDER BY CreatedDate DESC",
                    p => p.AddWithValue("?", _coachId));

                foreach (DataRow row in programs.Rows)
                {
                    int id = Convert.ToInt32(row["ProgramID"]);
                    string goal = row["Goal"] == DBNull.Value ? "" : row["Goal"].ToString();
                    string level = row["ProgramLevel"] == DBNull.Value ? "" : row["ProgramLevel"].ToString();

                    // Two small questions to the database, once per program
                    int days = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                        "SELECT COUNT(*) FROM ProgramDaysTable WHERE ProgramID = ?",
                        p => p.AddWithValue("?", id)));

                    int exercises = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                        "SELECT COUNT(*) FROM ProgramExercisesTable AS e INNER JOIN ProgramDaysTable AS d ON e.DayID = d.DayID WHERE d.ProgramID = ?",
                        p => p.AddWithValue("?", id)));

                    items.Add(new ProgramItem
                    {
                        Id = id,
                        Name = row["ProgramName"].ToString(),
                        Subtitle = string.Join(" · ", new[] { goal, level }.Where(s => s != "")),
                        Stats = days + (days == 1 ? " day" : " days") + " · " +
                                exercises + (exercises == 1 ? " exercise" : " exercises"),
                        Created = row["CreatedDate"] == DBNull.Value
                            ? ""
                            : "Created " + Convert.ToDateTime(row["CreatedDate"]).ToString("d MMM yyyy")
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load your programs: " + ex.Message, "Coach Studio");
            }

            lstPrograms.ItemsSource = items;
            panelEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Keep the same program selected after a reload (if it still exists)
            lstPrograms.SelectedItem = items.FirstOrDefault(i => i.Id == selectedId);
            ShowDetails();
        }

        private void Programs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowDetails();
        }

        /// <summary>Shows the days and exercises of the selected program.</summary>
        private void ShowDetails()
        {
            var program = lstPrograms.SelectedItem as ProgramItem;

            if (program == null)
            {
                panelDetails.Visibility = Visibility.Collapsed;
                txtNoSelection.Visibility = Visibility.Visible;
                return;
            }

            txtProgramName.Text = program.Name;
            txtProgramInfo.Text = program.Subtitle == "" ? program.Stats : program.Subtitle + " · " + program.Stats;

            var days = new List<ProgramDayView>();

            try
            {
                // LEFT JOIN: keeps days that have no exercises (rest days)
                string sql = @"
                    SELECT d.DayID, d.DayNumber, d.DayFocus,
                           e.SortOrder, x.ExerciseName, e.Sets, e.Reps, e.RestTime
                    FROM (ProgramDaysTable AS d
                    LEFT JOIN ProgramExercisesTable AS e ON e.DayID = d.DayID)
                    LEFT JOIN ExercisesTable AS x ON x.ExerciseID = e.ExerciseID
                    WHERE d.ProgramID = ?
                    ORDER BY d.DayNumber, e.SortOrder";

                DataTable dt = DatabaseHelper.ExecuteQueryTable(sql, p => p.AddWithValue("?", program.Id));

                ProgramDayView current = null;
                int currentDayId = -1;

                foreach (DataRow row in dt.Rows)
                {
                    int dayId = Convert.ToInt32(row["DayID"]);

                    // A new DayID means a new day card (the rows are sorted by day)
                    if (current == null || dayId != currentDayId)
                    {
                        string focus = row["DayFocus"] == DBNull.Value ? "" : row["DayFocus"].ToString().Trim();
                        current = new ProgramDayView
                        {
                            Header = "Day " + row["DayNumber"] + (focus == "" ? "" : " · " + focus)
                        };
                        days.Add(current);
                        currentDayId = dayId;
                    }

                    // On a rest day the exercise columns are empty (NULL)
                    if (row["SortOrder"] != DBNull.Value)
                    {
                        current.Lines.Add(new ProgramLineView
                        {
                            Name = row["ExerciseName"] == DBNull.Value ? "?" : row["ExerciseName"].ToString(),
                            Detail = row["Sets"] + " x " + row["Reps"] + " · rest " + row["RestTime"]
                        });
                    }
                }

                // A day without exercises is a rest day
                foreach (ProgramDayView day in days)
                {
                    if (day.Lines.Count == 0)
                        day.Lines.Add(new ProgramLineView { Name = "Rest day", Detail = "" });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load this program: " + ex.Message, "Coach Studio");
            }

            lstDays.ItemsSource = days;
            panelDetails.Visibility = Visibility.Visible;
            txtNoSelection.Visibility = Visibility.Collapsed;
        }

        private void DeleteProgram_Click(object sender, RoutedEventArgs e)
        {
            var program = lstPrograms.SelectedItem as ProgramItem;
            if (program == null) return;

            var answer = MessageBox.Show(
                "Delete \"" + program.Name + "\"? Its days and exercises will be deleted too.",
                "Coach Studio", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                // Also checks CoachID, so a coach can only delete their own programs.
                // The days and exercises are removed by the cascade delete in the relationships.
                DatabaseHelper.ExecuteNonQuery(
                    "DELETE FROM ProgramsTable WHERE ProgramID = ? AND CoachID = ?",
                    p =>
                    {
                        p.AddWithValue("?", program.Id);
                        p.AddWithValue("?", _coachId);
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not delete the program: " + ex.Message, "Coach Studio");
                return;
            }

            Reload();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            CreateRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}