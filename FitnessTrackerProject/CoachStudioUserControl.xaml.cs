using System.Windows;
using System.Windows.Controls;

namespace FitnessTrackerProject
{
    /// <summary>Coach Studio: a tab bar and a frame that shows "My programs" or "Create program".</summary>
    public partial class CoachStudioUserControl : UserControl
    {
        // Each screen is created once, so a half-built program isn't lost when switching tabs
        private readonly CoachProgramsUserControl _programs;
        private readonly CreateProgramUserControl _create;

        public CoachStudioUserControl() : this(0) { }

        public CoachStudioUserControl(int coachId)
        {
            InitializeComponent();

            _programs = new CoachProgramsUserControl(coachId);
            _create = new CreateProgramUserControl(coachId);

            _programs.CreateRequested += (s, e) => ShowCreate();   // button shown when the list is empty
            _create.ProgramSaved += (s, e) => ShowPrograms();      // after saving, go to the list

            ShowPrograms();
        }

        private void ShowPrograms()
        {
            tabPrograms.IsChecked = true;
            _programs.Reload();                    // read the database again, so a new program appears
            if (coachFrame.Content != _programs) coachFrame.Navigate(_programs);
        }

        private void ShowCreate()
        {
            tabCreate.IsChecked = true;
            if (coachFrame.Content != _create) coachFrame.Navigate(_create);
        }

        private void TabPrograms_Click(object sender, RoutedEventArgs e) { ShowPrograms(); }
        private void TabCreate_Click(object sender, RoutedEventArgs e) { ShowCreate(); }
    }
}