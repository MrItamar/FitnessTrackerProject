using System.Windows;
using System.Windows.Controls;

namespace FitnessTrackerProject
{
    /// <summary>Coach Studio: a tab bar and a frame that shows "My programs" or "Create program".</summary>
    public partial class CoachStudioUserControl : UserControl
    {
        private readonly CoachProgramsUserControl _programs;
        private readonly CreateProgramUserControl _create;

        public CoachStudioUserControl() : this(0) { }

        public CoachStudioUserControl(int coachId)
        {
            InitializeComponent();

            _programs = new CoachProgramsUserControl(coachId, this);
            _create = new CreateProgramUserControl(coachId, this);

            OpenProgramsTab();
        }

        public void OpenProgramsTab()
        {
            tabPrograms.IsChecked = true;
            _programs.Reload();
            if (coachFrame.Content != _programs) coachFrame.Navigate(_programs);
        }

        public void OpenEditTab(int programId)
        {
            tabCreate.IsChecked = true;
            _create.LoadProgramForEdit(programId);
            if (coachFrame.Content != _create) coachFrame.Navigate(_create);
        }

        private void TabPrograms_Click(object sender, RoutedEventArgs e) { OpenProgramsTab(); }
        private void TabCreate_Click(object sender, RoutedEventArgs e)
        {
            tabCreate.IsChecked = true;
            if (coachFrame.Content != _create) coachFrame.Navigate(_create);
        }
    }
}