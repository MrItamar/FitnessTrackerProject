using FitnessTrackerProject.Scripts;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FitnessTrackerProject
{
    /// <summary>
    /// Interaction logic for WorkoutHubUserControl.xaml
    /// </summary>
    public partial class WorkoutHubUserControl : UserControl
    {
        private int currentUserId;
        public bool IsPlankMode
        {
            get { return PlankModeCheckBox.IsChecked ?? false; }
        }
        public WorkoutHubUserControl()
        {
            InitializeComponent();
        }
        public WorkoutHubUserControl(int userId)
        {
            InitializeComponent();
            currentUserId = userId;
        }

        private void SelectVideoButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov|All Files|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                string videoPath = openFileDialog.FileName;
                bool isPlankMode = PlankModeCheckBox.IsChecked ?? false;

                // Find the parent MainWindow and trigger ProcessVideo
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.ProcessVideo(videoPath, isPlankMode);
                }
            }
        }
    }
}
