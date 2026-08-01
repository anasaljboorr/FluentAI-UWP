using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI.Core;
using Windows.ApplicationModel.DataTransfer;
using System.ComponentModel;
using Project_FluentAI.Models;
using Project_FluentAI.ViewModels;

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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Wire up PreviewKeyDown in code-behind to avoid the XAML compiler 
            // generating an incompatible cast to IUIElement7 on older Windows versions.
            // PreviewKeyDown is available since Build 16299 (1709).
            if (ApiInformation.IsEventPresent("Windows.UI.Xaml.UIElement", "PreviewKeyDown"))
            {
                InputBox.PreviewKeyDown += InputBox_PreviewKeyDown;
            }
            else
            {
                // Fallback for even older versions (original Windows 10 Mobile)
                InputBox.KeyDown += InputBox_PreviewKeyDown;
            }

            UpdateInputBarTheme();
            if (ApiInformation.IsEventPresent("Windows.UI.Xaml.FrameworkElement", "ActualThemeChanged"))
            {
                this.ActualThemeChanged += (s, ev) => UpdateInputBarTheme();
            }
        }

        private void UpdateInputBarTheme()
        {
            bool isDark = false;
            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.FrameworkElement", "ActualTheme"))
            {
                isDark = this.ActualTheme == ElementTheme.Dark || 
                         (this.ActualTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);
            }
            else
            {
                isDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
            }

            // Cortana-style bar background: Dark Gray in Dark mode, Light Gray in Light mode
            // Cortana-style bar background: Dark Gray in Dark mode, Very Light Gray/White in Light mode
            InputBarBackground.Background = isDark 
                ? new SolidColorBrush(Color.FromArgb(255, 38, 38, 38)) 
                : new SolidColorBrush(Color.FromArgb(255, 242, 242, 242));
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
                // A real conversation was selected — show the chat UI.
                _currentChat = chatItem;
                _currentChat.PropertyChanged += OnChatPropertyChanged;

                ChatTitle.Text = _currentChat.Title;
                MessageListView.ItemsSource = _currentChat.Messages;
                InputBox.Text = string.Empty;

                EmptyStatePanel.Visibility = Visibility.Collapsed;
                ChatPanel.Visibility = Visibility.Visible;
            }
            else
            {
                // No conversation selected — show the empty state.
                _currentChat = null;
                MessageListView.ItemsSource = null;

                ChatPanel.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
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

        private void InputBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var shiftState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift);
                bool isShiftDown = (shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

                if (!isShiftDown)
                {
                    // Mark as handled to prevent the TextBox from adding a newline
                    e.Handled = true;
                    SendMessage();
                }
            }
        }

        private void SendMessage()
        {
            if (ViewModel != null && _currentChat != null)
            {
                // If the textbox is empty or contains only whitespace, do nothing.
                if (string.IsNullOrWhiteSpace(InputBox.Text))
                {
                    return;
                }

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

        private void Message_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ShowMessageMenu(sender as FrameworkElement);
        }

        private void Message_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                ShowMessageMenu(sender as FrameworkElement);
                e.Handled = true;
            }
        }

        private void ShowMessageMenu(FrameworkElement target)
        {
            var message = target?.DataContext as Message;
            if (message == null) return;

            var menu = new MenuFlyout();
            var copy = new MenuFlyoutItem { Text = "Copy Message", DataContext = message };
            copy.Click += CopyMessage_Click;
            var delete = new MenuFlyoutItem { Text = "Delete Message", DataContext = message };
            delete.Click += DeleteMessage_Click;
            menu.Items.Add(copy);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(delete);
            menu.ShowAt(target);
        }

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
