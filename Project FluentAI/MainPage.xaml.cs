using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Input;
using Windows.UI.Core;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Media;
using Project_FluentAI.ViewModels;
using Project_FluentAI.Views;
using Project_FluentAI.Models;
using Windows.Foundation;
using Windows.UI;

namespace Project_FluentAI
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; set; }

        private const double PhoneWidth = 640;
        private bool _isNavigating;
        private bool _isPhoneLayout;

        public MainPage()
        {
            InitializeComponent();

            ViewModel = new MainViewModel();
            ContentFrame.Navigate(typeof(ChatPage));
            Loaded += MainPage_Loaded;
            SizeChanged += MainPage_SizeChanged;
            SystemNavigationManager.GetForCurrentView().BackRequested += BackRequested;

            UpdateSidebarBackground();
            if (ApiInformation.IsEventPresent("Windows.UI.Xaml.FrameworkElement", "ActualThemeChanged"))
            {
                this.ActualThemeChanged += (s, e) => UpdateSidebarBackground();
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateShellLayout(ActualWidth);
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateShellLayout(e.NewSize.Width);
        }

        private void UpdateShellLayout(double width)
        {
            _isPhoneLayout = width <= PhoneWidth;
            ShellSplitView.DisplayMode = _isPhoneLayout ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline;
            ShellSplitView.IsPaneOpen = _isPhoneLayout ? !HasDetailOpen() : true;
            
            // On mobile, sidebar should cover the whole screen
            ShellSplitView.OpenPaneLength = _isPhoneLayout ? width : 300;

            BackButton.Visibility = _isPhoneLayout && HasDetailOpen() ? Visibility.Visible : Visibility.Collapsed;
            
            // Custom title bar is only for Desktop.
            AppTitleBar.Visibility = _isPhoneLayout ? Visibility.Collapsed : Visibility.Visible;

            // In compact/phone mode, hide some sidebar elements
            HamburgerButton.Visibility = _isPhoneLayout ? Visibility.Visible : Visibility.Visible; // Always show for now
            
            // Update settings indicator
            SettingsAccentIndicator.Visibility = ContentFrame.Content is SettingsPage ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateSidebarBackground()
        {
            bool isDark = false;
            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.FrameworkElement", "ActualTheme"))
            {
                isDark = this.ActualTheme == ElementTheme.Dark || 
                         (this.ActualTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);
            }
            else
            {
                // Fallback for older Windows 10 versions (including Mobile)
                isDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
            }

            Color backgroundColor = isDark ? Color.FromArgb(255, 30, 30, 30) : Color.FromArgb(255, 255, 255, 255);
            Color tintColor = isDark ? Color.FromArgb(255, 10, 10, 10) : Color.FromArgb(255, 255, 255, 255);
            
            // Apply Solid fallback first
            SidebarRoot.Background = new SolidColorBrush(backgroundColor);

            // Apply Acrylic if supported and not on mobile (HostBackdrop is Desktop-only)
            if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Media.AcrylicBrush") && 
                Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Desktop")
            {
                SidebarRoot.Background = new AcrylicBrush
                {
                    BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                    TintColor = tintColor,
                    TintOpacity = isDark ? 0.8 : 0.6,
                    FallbackColor = backgroundColor
                };
            }
        }

        private bool HasDetailOpen()
        {
            return ContentFrame.Content is SettingsPage || ViewModel.SelectedChat != null;
        }

        private void ShowSidebar()
        {
            if (!_isPhoneLayout) return;
            ChatListView.SelectedItem = null;
            ViewModel.SelectedChat = null;
            ShellSplitView.IsPaneOpen = true;
            BackButton.Visibility = Visibility.Collapsed;

            // Ensure sidebar background is maintained
            UpdateSidebarBackground();
        }

        private void OpenDetail(Type page, object parameter = null)
        {
            _isNavigating = true;
            ContentFrame.Navigate(page, parameter);
            _isNavigating = false;
            
            // Update settings indicator
            SettingsAccentIndicator.Visibility = page == typeof(SettingsPage) ? Visibility.Visible : Visibility.Collapsed;

            // Ensure sidebar background is maintained during navigation
            UpdateSidebarBackground();

            if (_isPhoneLayout)
            {
                ShellSplitView.IsPaneOpen = false;
                BackButton.Visibility = Visibility.Visible;
            }
        }

        private void BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (_isPhoneLayout && !ShellSplitView.IsPaneOpen)
            {
                e.Handled = true;
                ShowSidebar();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => ShowSidebar();

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ChatListView.SelectedItem = null;
            OpenDetail(typeof(SettingsPage));
        }

        private void ChatListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isNavigating && ChatListView.SelectedItem is ChatItem chat)
            {
                ViewModel.SelectedChat = chat;
                OpenDetail(typeof(ChatPage), chat);
            }
        }

        private void NewChatButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ViewModel.CreateNewChat();
            if (ViewModel.SelectedChat != null)
            {
                OpenDetail(typeof(ChatPage), ViewModel.SelectedChat);
            }
        }

        private void ChatSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            ViewModel.SearchText = sender.Text;
        }

        private void ChatItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ShowChatMenu(sender as FrameworkElement);
        }

        private void ChatItem_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                ShowChatMenu(sender as FrameworkElement);
                e.Handled = true;
            }
        }

        private void ShowChatMenu(FrameworkElement target)
        {
            var chat = target?.DataContext as ChatItem;
            if (chat == null) return;

            var menu = new MenuFlyout();
            var rename = new MenuFlyoutItem { Text = "Rename", DataContext = chat };
            rename.Click += RenameChat_Click;
            var pin = new MenuFlyoutItem { Text = chat.PinLabel, DataContext = chat };
            pin.Click += PinChat_Click;
            var delete = new MenuFlyoutItem { Text = "Delete", DataContext = chat };
            delete.Click += DeleteChat_Click;

            menu.Items.Add(rename);
            menu.Items.Add(pin);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(delete);
            menu.ShowAt(target);
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
                ShowSidebar();
            }
        }
    }
}
