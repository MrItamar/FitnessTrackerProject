using FitnessTrackerProject.Scripts;
using System.Windows;

namespace FitnessTrackerProject
{
    public partial class ChatWindow : Window
    {
        private int _currentUserId;
        private int _otherUserId;

        public ChatWindow(int currentUserId, int otherUserId, string otherUserName)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            _otherUserId = otherUserId;

            this.Title = $"Chat with {otherUserName}";
            LoadMessages();
        }

        private void LoadMessages()
        {
            var messages = MessageRepository.GetConversation(_currentUserId, _otherUserId);
            MessagesList.ItemsSource = messages;

            if (messages.Count > 0)
            {
                MessagesList.ScrollIntoView(messages[messages.Count - 1]);
            }
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            string text = MessageTextBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            MessageRepository.SendMessage(_currentUserId, _otherUserId, text);
            MessageTextBox.Clear();
            LoadMessages();
        }
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadMessages();
        }
    }
}