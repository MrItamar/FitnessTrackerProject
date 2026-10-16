using System.Windows;
using System.Windows.Controls;

namespace FitnessTrackerProject
{
    public partial class CoachStudioUserControl : UserControl
    {
        private readonly CoachProgramsUserControl _programs;
        private readonly CoachTraineesUserControl _trainees;
        private readonly CreateProgramUserControl _create;

        public CoachStudioUserControl() : this(0) { }

        public CoachStudioUserControl(int coachId)
        {
            InitializeComponent();

            _programs = new CoachProgramsUserControl(coachId, this);
            _trainees = new CoachTraineesUserControl(coachId);
            _create = new CreateProgramUserControl(coachId, this);

            OpenProgramsTab();
        }

        public void OpenProgramsTab()
        {
            tabPrograms.IsChecked = true;
            _programs.Reload();
            if (coachFrame.Content != _programs) coachFrame.Navigate(_programs);
        }

        public void OpenTraineesTab()
        {
            tabTrainees.IsChecked = true;
            _trainees.Reload();
            if (coachFrame.Content != _trainees) coachFrame.Navigate(_trainees);
        }

        public void OpenEditTab(int programId)
        {
            tabCreate.IsChecked = true;
            _create.LoadProgramForEdit(programId);
            if (coachFrame.Content != _create) coachFrame.Navigate(_create);
        }

        private void TabPrograms_Click(object sender, RoutedEventArgs e) { OpenProgramsTab(); }
        private void TabTrainees_Click(object sender, RoutedEventArgs e) { OpenTraineesTab(); }
        private void TabCreate_Click(object sender, RoutedEventArgs e)
        {
            tabCreate.IsChecked = true;
            if (coachFrame.Content != _create) coachFrame.Navigate(_create);
        }
    }
}