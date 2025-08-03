using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Claims;
using System.Text.RegularExpressions;
using TextControlBoxNS;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;

namespace SCE2
{
    public class TabInfo
    {
        public string TabId { get; set; }
        public string FilePath { get; set; }
        public int CursorLine { get; set; }
        public string TabText { get; set; }
        public ScrollBarPosition VerticalOffset { get; set; }
        public string Text { get; set; }
        public bool Saved { get; set; }

        public TabInfo(string tabId, string filePath, int cursorLine, string tabText, ScrollBarPosition verticalOffset = null)
        {
            TabId = tabId;
            FilePath = filePath;
            CursorLine = cursorLine;
            TabText = tabText;
            VerticalOffset = verticalOffset;
        }
    }

    public sealed partial class MainWindow : Window
    {
        private Button CreateTab(string tabText, string path = null, string tabId = null)
        {
            if (string.IsNullOrEmpty(tabId))
                tabId = Guid.NewGuid().ToString();

            var isDarkTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark;
            var oppositeColor = isDarkTheme ? Colors.White : Colors.Black;

            var tabButton = new Button
            {
                Name = tabText,
                Tag = tabId,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(1, 1, 1, 1),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Height = 26,
                MinWidth = 100,
                MaxWidth = 200,
                VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new CornerRadius(0)
            };

            tabButton.PointerEntered += (s, e) =>
            {
                if (activeTabId != tabId)
                {
                    tabButton.Background = new SolidColorBrush(isDarkTheme ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(40, 0, 0, 0));
                }
            };

            tabButton.PointerExited += (s, e) =>
            {
                if (activeTabId != tabId)
                {
                    tabButton.Background = new SolidColorBrush(Colors.Transparent);
                }
            };

            tabButton.Click += (s, e) =>
            {
                SwitchToTab(tabId);
            };

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };

            var textBlock = new TextBlock
            {
                Text = tabText,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 140
            };

            var closeButton = new Button
            {
                Content = "×",
                Width = 18,
                Height = 18,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(oppositeColor),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(0),
                Tag = tabId
            };

            closeButton.Click += (s, e) =>
            {
                DestroyTab(tabId);
            };

            contentPanel.Children.Add(textBlock);
            contentPanel.Children.Add(closeButton);

            tabButton.Content = contentPanel;

            TabContainer.Children.Add(tabButton);

            var tabInfo = new TabInfo(tabId, path ?? "", 0, tabText, new ScrollBarPosition(0, 0));
            openTabs.Add(tabInfo);

            if (activeTabId == null)
            {
                SetActiveTab(tabId);
            }

            SwitchToTab(tabId);

            return tabButton;
        }

        private bool DestroyTab(string tabId)
        {
            try
            {
                var tabButton = TabContainer.Children.Cast<Button>()
                    .FirstOrDefault(b => b.Tag.ToString() == tabId);

                if (tabButton != null)
                {
                    TabContainer.Children.Remove(tabButton);

                    var tabInfo = openTabs.FirstOrDefault(t => t.TabId == tabId);
                    if (tabInfo != null)
                    {
                        openTabs.Remove(tabInfo);
                    }

                    if (activeTabId == tabId)
                    {
                        activeTabId = null;

                        if (openTabs.Count > 0)
                        {
                            var nextTab = openTabs.LastOrDefault();
                            if (nextTab != null)
                            {
                                SwitchToTab(nextTab.TabId);
                            }
                        }
                        else
                        {
                            CodeEditor.SetText("");
                            currentFilePath = "";
                            UpdateCursorPosition();
                        }
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error destroying tab: {ex.Message}");
                return false;
            }
        }

        private void SwitchToTab(string tabId, bool path = false)
        {
            if (activeTabId != null)
            {
                var currentTab = openTabs.FirstOrDefault(t => t.TabId == activeTabId);
                if (currentTab != null)
                {
                    currentTab.Text = CodeEditor.Text;
                    SaveCurrentTabPosition();
                }
            }

            var tabInfo = openTabs.FirstOrDefault(t => t.TabId == tabId);
            if (tabInfo == null) return;

            try
            {
                if (!string.IsNullOrEmpty(tabInfo.FilePath))
                {
                    if (path) CodeEditor.LoadText(File.ReadAllText(tabInfo.FilePath));
                    else CodeEditor.LoadText(tabInfo.Text);
                    currentFilePath = tabInfo.FilePath;
                }

                if (tabInfo.CursorLine > 0)
                {
                    CodeEditor.SetCursorPosition(tabInfo.CursorLine, 0);
                }

                SetActiveTab(tabId);

                UpdateCursorPosition();

                if (tabInfo.VerticalOffset != null)
                {
                    CodeEditor.ScrollBarPosition = tabInfo.VerticalOffset;
                }
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Error switching to tab: {ex.Message}";
            }
        }

        private void SetActiveTab(string tabId)
        {
            var isDarkTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark;

            foreach (Button tabButton in TabContainer.Children.Cast<Button>())
            {
                tabButton.Background = new SolidColorBrush(Colors.Transparent);
                tabButton.BorderBrush = new SolidColorBrush(Colors.Gray);
            }

            var activeTabButton = TabContainer.Children.Cast<Button>()
                .FirstOrDefault(b => b.Tag.ToString() == tabId);

            if (activeTabButton != null)
            {
                activeTabButton.BorderBrush = new SolidColorBrush(isDarkTheme ? Colors.LightBlue : Colors.Blue);
            }

            activeTabId = tabId;
        }

        private void SaveCurrentTabPosition()
        {
            if (activeTabId == null) return;

            var tabInfo = openTabs.FirstOrDefault(t => t.TabId == activeTabId);
            if (tabInfo != null)
            {
                tabInfo.CursorLine = CodeEditor.CurrentLineIndex;
                tabInfo.VerticalOffset = CodeEditor.ScrollBarPosition;
            }
        }

        private string SerializeTabInfo(TabInfo tabInfo)
        {
            return $"{tabInfo.TabId}|{tabInfo.FilePath}|{tabInfo.CursorLine}|{tabInfo.TabText}|{tabInfo.VerticalOffset}";
        }

        private TabInfo DeserializeTabInfo(string serializedTab)
        {
            var parts = serializedTab.Split('|');
            if (parts.Length >= 4)
            {
                var verticalOffset = parts.Length > 4 && double.TryParse(parts[4], out double vOffset) ? vOffset : 0;
                var horizontalOffset = parts.Length > 5 && double.TryParse(parts[5], out double hOffset) ? hOffset : 0;

                return new TabInfo(
                    parts[0],
                    parts[1],
                    int.TryParse(parts[2], out int cursorLine) ? cursorLine : 0,
                    parts[3],
                    new ScrollBarPosition(verticalOffset, horizontalOffset)
                );
            }
            return null;
        }

        private void CleanupTabContent()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                var keysToRemove = localSettings.Values.Keys
                    .Where(key => key.StartsWith("TabContent_") || key.StartsWith("Tab_"))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    localSettings.Values.Remove(key);
                }

                localSettings.Values.Remove("OpenTabsCount");
                localSettings.Values.Remove("ActiveTabId");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up tab content: {ex.Message}");
            }
        }

        private void UpdateTabButtonText(string tabId, string newText)
        {
            var tabButton = TabContainer.Children.Cast<Button>()
                .FirstOrDefault(b => b.Tag.ToString() == tabId);
            if (tabButton != null && tabButton.Content is StackPanel panel && panel.Children[0] is TextBlock textBlock)
            {
                textBlock.Text = newText;
            }
        }
    }
}