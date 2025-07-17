using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace SCE2
{
    public sealed partial class SettingsWindow : Window
    {
        private MainWindow parentWindow;

        public SettingsWindow(MainWindow parent)
        {
            this.InitializeComponent();
            this.parentWindow = parent;

            LoadCurrentSettings();

            SettingsNavigation.SelectedItem = SettingsNavigation.MenuItems[0];
        }

        private void LoadCurrentSettings()
        {
            var currentSettings = parentWindow.GetCurrentSettings();

            TabSizeNumberBox.Value = currentSettings.tabSize;
            AutoIndentToggle.IsOn = currentSettings.autoIndent;
            AutoCompletionToggle.IsOn = currentSettings.autoCompletion;
            AutoBraceClosingToggle.IsOn = currentSettings.autoBraceClosing;
            LineNumbersToggle.IsOn = currentSettings.lineNumbers;
            WordWrapToggle.IsOn = currentSettings.wordWrap;
        }

        private void SettingsNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString();

                GeneralPanel.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Collapsed;
                ThemePanel.Visibility = Visibility.Collapsed;
                AboutPanel.Visibility = Visibility.Collapsed;

                switch (tag)
                {
                    case "General":
                        GeneralPanel.Visibility = Visibility.Visible;
                        break;
                    case "Editor":
                        EditorPanel.Visibility = Visibility.Visible;
                        break;
                    case "Theme":
                        ThemePanel.Visibility = Visibility.Visible;
                        break;
                    case "About":
                        AboutPanel.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TabSizeNumberBox.Value < 1 || TabSizeNumberBox.Value > 8)
                {
                    ShowErrorDialog("Tab size must be between 1 and 8.");
                    return;
                }

                parentWindow.UpdateSettings(
                    (short)TabSizeNumberBox.Value,
                    AutoIndentToggle.IsOn,
                    AutoCompletionToggle.IsOn,
                    AutoBraceClosingToggle.IsOn,
                    LineNumbersToggle.IsOn,
                    WordWrapToggle.IsOn
                );

                ShowSuccessDialog("Settings applied successfully!");
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Error applying settings: {ex.Message}");
            }
        }

        private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
        {
            TabSizeNumberBox.Value = 4;
            AutoIndentToggle.IsOn = true;
            AutoCompletionToggle.IsOn = true;
            AutoBraceClosingToggle.IsOn = true;
            LineNumbersToggle.IsOn = true;
            WordWrapToggle.IsOn = false;

            AutoSaveToggle.IsOn = true;
            RestoreSessionToggle.IsOn = true;
        }

        private async void ShowErrorDialog(string message)
        {
            ContentDialog dialog = new ContentDialog()
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async void ShowSuccessDialog(string message)
        {
            ContentDialog dialog = new ContentDialog()
            {
                Title = "Success",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}