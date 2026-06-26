using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Project_FluentAI.ViewModels;
using Project_FluentAI.Views;
using Project_FluentAI.Models;
using Windows.Foundation;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Project_FluentAI
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; set; }

        private bool _isNavigating;

        public MainPage()
        {
            InitializeComponent();

            // Set the custom drag region
            Window.Current.SetTitleBar(AppTitleBar);
            
            // Listen for layout changes to adjust padding if needed
            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += (s, e) => UpdateTitleBarLayout(s);

            ViewModel = new MainViewModel();

            ContentFrame.Navigate(typeof(ChatPage));
            
            // Initial theme setup for title bar buttons
            UpdateTitleBarButtonColors();
            this.ActualThemeChanged += (s, e) => UpdateTitleBarButtonColors();
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            // Adjust the padding columns to avoid overlapping with system buttons
            LeftPaddingColumn.Width = new GridLength(coreTitleBar.SystemOverlayLeftInset);
            RightPaddingColumn.Width = new GridLength(coreTitleBar.SystemOverlayRightInset);
            AppTitleBar.Height = coreTitleBar.Height;
        }

        private void UpdateTitleBarButtonColors()
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            var isDark = this.ActualTheme == ElementTheme.Dark || 
                        (this.ActualTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);

            if (isDark)
            {
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(25, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(51, 255, 255, 255);
                
                titleBar.ButtonInactiveForegroundColor = Colors.Gray;
            }
            else
            {
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(25, 0, 0, 0);
                titleBar.ButtonPressedForegroundColor = Colors.Black;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(51, 0, 0, 0);
                
                titleBar.ButtonInactiveForegroundColor = Colors.LightGray;
            }
        }

        private void NavView_ItemInvoked(
            NavigationView sender,
            NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                NavigateToSettings();
            }
        }

        private void NavigateToSettings()
        {
            if (_isNavigating)
                return;

            _isNavigating = true;

            ChatListView.SelectedItem = null;

            ContentFrame.Navigate(typeof(SettingsPage));

            _isNavigating = false;
        }

        private void ChatListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_isNavigating)
                return;

            if (ChatListView.SelectedItem != null)
            {
                _isNavigating = true;

                ContentFrame.Navigate(
                    typeof(ChatPage),
                    ChatListView.SelectedItem);

                _isNavigating = false;
            }
        }

        private void NavView_DisplayModeChanged(
            NavigationView sender,
            NavigationViewDisplayModeChangedEventArgs args)
        {
            bool compact =
                args.DisplayMode == NavigationViewDisplayMode.Compact ||
                args.DisplayMode == NavigationViewDisplayMode.Minimal;

            ChatSearchBox.Visibility =
                compact ? Visibility.Collapsed : Visibility.Visible;

            ChatsHeader.Visibility =
                compact ? Visibility.Collapsed : Visibility.Visible;

            ChatListView.Visibility =
                compact ? Visibility.Collapsed : Visibility.Visible;
        }

        private void NewChatButton_Tapped(
            object sender,
            TappedRoutedEventArgs e)
        {
            ViewModel.CreateNewChat();
            if (ViewModel.SelectedChat != null)
            {
                ContentFrame.Navigate(typeof(ChatPage), ViewModel.SelectedChat);
            }
        }

        private void ChatSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            ViewModel.SearchText = sender.Text;
        }

        // ── Context-menu handlers ─────────────────────────────────────────────

        private async void RenameChat_Click(object sender, RoutedEventArgs e)
        {
            var chat = (sender as MenuFlyoutItem)?.DataContext as ChatItem;
            if (chat == null) return;

            var input = new TextBox
            {
                Text = chat.Title,
                SelectionStart = 0,
                SelectionLength = chat.Title.Length
            };

            var dialog = new ContentDialog
            {
                Title = "Rename Conversation",
                Content = input,
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            {
                ViewModel.RenameChat(chat, input.Text);
            }
        }

        private void PinChat_Click(object sender, RoutedEventArgs e)
        {
            var chat = (sender as MenuFlyoutItem)?.DataContext as ChatItem;
            if (chat == null) return;

            ViewModel.TogglePinChat(chat);
        }

        private async void DeleteChat_Click(object sender, RoutedEventArgs e)
        {
            var chat = (sender as MenuFlyoutItem)?.DataContext as ChatItem;
            if (chat == null) return;

            var dialog = new ContentDialog
            {
                Title = "Delete Conversation",
                Content = $"Are you sure you want to delete \"{chat.Title}\"?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.DeleteChat(chat);
                ContentFrame.Navigate(typeof(ChatPage)); // Reset view
            }
        }
    }
}
