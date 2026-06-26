using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Project_FluentAI.Models;
using Project_FluentAI.ViewModels;
using Windows.System;
using Windows.UI.Core;
using Windows.ApplicationModel.DataTransfer;
using System.ComponentModel;

namespace Project_FluentAI.Views
{
    public sealed partial class ChatPage : Page
    {
        private ChatItem _currentChat;

        public MainViewModel ViewModel =>
            (Window.Current.Content as Frame)?.Content is MainPage mainPage
                ? mainPage.ViewModel
                : null;

        public ChatPage()
        {
            this.InitializeComponent();
        }

        // ── Navigation ────────────────────────────────────────────────────────

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (_currentChat != null)
            {
                _currentChat.PropertyChanged -= OnChatPropertyChanged;
            }

            if (e.Parameter is ChatItem chatItem)
            {
                _currentChat = chatItem;
                _currentChat.PropertyChanged += OnChatPropertyChanged;

                ChatTitle.Text = _currentChat.Title;
                MessageListView.ItemsSource = _currentChat.Messages;
                InputBox.Text = string.Empty;
            }
            else
            {
                _currentChat = null;
                ChatTitle.Text = "Select a conversation";
                MessageListView.ItemsSource = null;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (_currentChat != null)
            {
                _currentChat.PropertyChanged -= OnChatPropertyChanged;
            }
        }

        private void OnChatPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatItem.Title) && _currentChat != null)
            {
                ChatTitle.Text = _currentChat.Title;
            }
        }

        // ── Send message ──────────────────────────────────────────────────────

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var shiftState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);
                bool isShiftDown = (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

                if (!isShiftDown)
                {
                    e.Handled = true;
                    SendMessage();
                }
            }
        }

        private void SendMessage()
        {
            if (ViewModel != null && _currentChat != null && !string.IsNullOrWhiteSpace(InputBox.Text))
            {
                // Ensure ViewModel's SelectedChat matches our current chat
                ViewModel.SelectedChat = _currentChat;

                ViewModel.InputMessage = InputBox.Text;
                ViewModel.SendMessage();
                InputBox.Text = string.Empty;

                // Scroll to bottom
                if (MessageListView.Items.Count > 0)
                {
                    MessageListView.ScrollIntoView(
                        MessageListView.Items[MessageListView.Items.Count - 1]);
                }
            }
        }

        // ── Message context-menu handlers ─────────────────────────────────────

        /// <summary>Copies the message text to the clipboard.</summary>
        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            var message = (sender as MenuFlyoutItem)?.DataContext as Message;
            if (message == null || string.IsNullOrEmpty(message.Content)) return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(message.Content);
            Clipboard.SetContent(dataPackage);
        }

        /// <summary>Removes the message from the current chat and persists the change.</summary>
        private void DeleteMessage_Click(object sender, RoutedEventArgs e)
        {
            var message = (sender as MenuFlyoutItem)?.DataContext as Message;
            if (message == null || _currentChat == null) return;

            _currentChat.Messages.Remove(message);

            // Persist the updated message list
            ViewModel?.SaveCurrentChat();
        }
    }
}
