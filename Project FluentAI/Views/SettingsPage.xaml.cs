using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Project_FluentAI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isLoading = true;

        public SettingsPage()
        {
            this.InitializeComponent();
            LoadSettings();
            _isLoading = false;
        }

        private void LoadSettings()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            
            // Theme
            if (settings.Values.ContainsKey("AppTheme"))
            {
                string theme = (string)settings.Values["AppTheme"];
                int index = 2; // Default to "Use System Setting"
                if (theme == "Light") index = 0;
                else if (theme == "Dark") index = 1;
                
                ThemeSelector.SelectedIndex = index;
            }
            else
            {
                InitializeThemeSelector();
            }

            // Local Models
            if (settings.Values.ContainsKey("UseLocalModels"))
            {
                LocalModelsToggle.IsOn = (bool)settings.Values["UseLocalModels"];
            }
            else
            {
                LocalModelsToggle.IsOn = true;
                settings.Values["UseLocalModels"] = true;
            }

            // Ollama Model
            if (settings.Values.ContainsKey("OllamaModel"))
            {
                OllamaModelBox.Text = (string)settings.Values["OllamaModel"];
            }
            else
            {
                OllamaModelBox.Text = "qwen3:8b";
            }

            // API Key
            if (settings.Values.ContainsKey("OpenAIApiKey"))
            {
                ApiKeyBox.Password = (string)settings.Values["OpenAIApiKey"];
            }

            // Gemini
            if (settings.Values.ContainsKey("UseGemini"))
            {
                GeminiToggle.IsOn = (bool)settings.Values["UseGemini"];
            }
            if (settings.Values.ContainsKey("GeminiApiKey"))
            {
                GeminiApiKeyBox.Password = (string)settings.Values["GeminiApiKey"];
            }
        }

        private void InitializeThemeSelector()
        {
            if (Window.Current.Content is FrameworkElement rootElement)
            {
                switch (rootElement.RequestedTheme)
                {
                    case ElementTheme.Light: ThemeSelector.SelectedIndex = 0; break;
                    case ElementTheme.Dark: ThemeSelector.SelectedIndex = 1; break;
                    case ElementTheme.Default: ThemeSelector.SelectedIndex = 2; break;
                }
            }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                string theme = item.Content.ToString();
                ApplyTheme(theme);
                
                var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
                settings.Values["AppTheme"] = theme;
            }
        }

        private void ApplyTheme(string theme)
        {
            ElementTheme requestedTheme = ElementTheme.Default;
            switch (theme)
            {
                case "Light": requestedTheme = ElementTheme.Light; break;
                case "Dark": requestedTheme = ElementTheme.Dark; break;
                case "Use System Setting": requestedTheme = ElementTheme.Default; break;
            }

            if (Window.Current.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = requestedTheme;
            }
        }

        private void LocalModelsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["UseLocalModels"] = LocalModelsToggle.IsOn;
        }

        private void OllamaModelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["OllamaModel"] = OllamaModelBox.Text;
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["OpenAIApiKey"] = ApiKeyBox.Password;
        }

        private void GeminiToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["UseGemini"] = GeminiToggle.IsOn;
        }

        private void GeminiApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values["GeminiApiKey"] = GeminiApiKeyBox.Password;
        }
    }
}
