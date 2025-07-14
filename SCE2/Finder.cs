using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.System;
using Windows.Foundation;

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
        void CreateFindReplacePanel()
        {
            findReplacePanel = new Grid()
            {
                Height = 50,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0)
            };

            var findRow = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 8, 10, 4),
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            toggleReplaceButton = new Button()
            {
                Content = "▶",
                Width = 34,
                Height = 26,
                FontSize = 11,
                CornerRadius = new CornerRadius(0)
            };
            ToolTipService.SetToolTip(toggleReplaceButton, "Show Replace Options");

            findTextBox = new TextBox()
            {
                PlaceholderText = "Find",
                Width = 200,
                Height = 26,
                BorderThickness = new Thickness(0)
            };

            findTextBox.Resources["TextControlBorderBrushFocused"] = new SolidColorBrush(Colors.Transparent);
            findTextBox.Resources["TextControlBorderBrushPointerOver"] = new SolidColorBrush(Colors.Transparent);

            var findPrevButton = new Button()
            {
                Content = "▲",
                Width = 34,
                Height = 26,
                FontSize = 11,
                CornerRadius = new CornerRadius(0)
            };

            var findNextButton = new Button()
            {
                Content = "▼",
                Width = 34,
                Height = 26,
                FontSize = 11,
                CornerRadius = new CornerRadius(0)
            };

            matchCountText = new TextBlock()
            {
                Text = "",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
                UseSystemFocusVisuals = false
            };

            var closeButton = new Button()
            {
                Content = "✕",
                Width = 34,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontSize = 12,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(0)
            };

            CreateReplacePopup();

            findRow.Children.Add(toggleReplaceButton);
            findRow.Children.Add(findTextBox);
            findRow.Children.Add(findPrevButton);
            findRow.Children.Add(findNextButton);
            findRow.Children.Add(matchCountText);
            findRow.Children.Add(closeButton);

            findReplacePanel.Children.Add(findRow);

            findTextBox.TextChanged += FindTextBox_TextChanged;
            findPrevButton.Click += (s, e) => FindPrevious();
            findNextButton.Click += (s, e) => FindNext();
            toggleReplaceButton.Click += ToggleReplaceButton_Click;
            closeButton.Click += (s, e) => HideFindPanel();

            findTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    FindNext();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Escape)
                {
                    HideFindPanel();
                    e.Handled = true;
                }
            };
        }

        private void CreateReplacePopup()
        {
            replacePopup = new Popup()
            {
                IsLightDismissEnabled = false
            };

            var replaceContainer = new Border()
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(8)
            };

            var replaceRow = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            replaceTextBox = new TextBox()
            {
                PlaceholderText = "Replace",
                Width = 200,
                Height = 26,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Colors.White)
            };

            replaceTextBox.Resources["TextControlBorderBrushFocused"] = new SolidColorBrush(Colors.Transparent);
            replaceTextBox.Resources["TextControlBorderBrushPointerOver"] = new SolidColorBrush(Colors.Transparent);

            replaceButton = new Button()
            {
                Content = "Replace",
                Height = 28,
                MinWidth = 60,
                FontSize = 11,
                CornerRadius = new CornerRadius(0)
            };

            replaceAllButton = new Button()
            {
                Content = "Replace All",
                Height = 28,
                MinWidth = 80,
                FontSize = 11,
                CornerRadius = new CornerRadius(0)
            };

            replaceRow.Children.Add(replaceTextBox);
            replaceRow.Children.Add(replaceButton);
            replaceRow.Children.Add(replaceAllButton);

            replaceContainer.Child = replaceRow;
            replacePopup.Child = replaceContainer;

            replaceButton.Click += (s, e) => ReplaceNext();
            replaceAllButton.Click += (s, e) => ReplaceAll();

            replaceTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    ReplaceNext();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Escape)
                {
                    HideReplacePopup();
                    e.Handled = true;
                }
            };
        }

        private void ToggleReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (replacePopup.IsOpen)
            {
                HideReplacePopup();
            }
            else
            {
                ShowReplacePopup();
            }
        }

        private void ShowReplacePopup()
        {
            if (toggleReplaceButton != null)
            {
                replacePopup.XamlRoot = this.Content.XamlRoot;

                var transform = findTextBox.TransformToVisual(null);
                var position = transform.TransformPoint(new Point(0, 0));

                replacePopup.HorizontalOffset = position.X - 8;
                replacePopup.VerticalOffset = position.Y + toggleReplaceButton.ActualHeight + 5;
                replacePopup.IsOpen = true;

                toggleReplaceButton.Content = "▼";
                ToolTipService.SetToolTip(toggleReplaceButton, "Hide Replace Options");

                replaceTextBox.Focus(FocusState.Programmatic);
            }
        }

        private void HideReplacePopup()
        {
            replacePopup.IsOpen = false;
            toggleReplaceButton.Content = "▶";
            ToolTipService.SetToolTip(toggleReplaceButton, "Show Replace Options");

            findTextBox.Focus(FocusState.Programmatic);
        }

        private void ShowFindPanel()
        {
            if (findReplacePanel.Parent == null)
            {
                var mainGrid = (Grid)this.Content;
                Grid.SetRow(findReplacePanel, 1);
                Grid.SetColumn(findReplacePanel, 1);
                Grid.SetColumnSpan(findReplacePanel, 1);
                findReplacePanel.HorizontalAlignment = HorizontalAlignment.Right;
                findReplacePanel.Width = 420;
                mainGrid.Children.Add(findReplacePanel);
            }

            findReplacePanel.Visibility = Visibility.Visible;
            findTextBox.Focus(FocusState.Programmatic);

            var selection = CodeEditor.Document.Selection;
            if (selection.Length > 0)
            {
                string selectedText;
                selection.GetText(Microsoft.UI.Text.TextGetOptions.None, out selectedText);
                findTextBox.Text = selectedText;
                findTextBox.SelectAll();
            }
        }

        private void HideFindPanel()
        {
            findReplacePanel.Visibility = Visibility.Collapsed;
            HideReplacePopup();
            CodeEditor.Focus(FocusState.Programmatic);
        }


        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchTerm = findTextBox.Text;
            if (string.IsNullOrEmpty(searchTerm))
            {
                searchMatches.Clear();
                currentMatchIndex = -1;
                matchCountText.Text = "";
                findReplacePanel.Width = 420;
                return;
            }
            SearchText(searchTerm);
        }

        private void SearchText(string searchTerm)
        {
            searchMatches.Clear();
            currentMatchIndex = -1;

            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

            int index = 0;
            while ((index = text.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                searchMatches.Add(index);
                index += searchTerm.Length;
            }

            if (searchMatches.Count > 0)
            {
                matchCountText.Text = $"{searchMatches.Count} results";
                currentMatchIndex = 0;
                HighlightMatch();
                if (searchMatches.Count < 10)
                {
                    findReplacePanel.Width = 420;
                }
                else if (searchMatches.Count >= 10 && searchMatches.Count < 100)
                {
                    findReplacePanel.Width = 440;
                }
                else
                {
                    findReplacePanel.Width = 452;
                }
            }
            else
            {
                matchCountText.Text = "No results";
                findReplacePanel.Width = 460;
            }
        }



        private void ScrollToCurrentMatch()
        {
            if (currentMatchIndex < 0 || currentMatchIndex >= searchMatches.Count) return;

            int matchPos = searchMatches[currentMatchIndex];
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

            int lineNumber = 1;
            for (int i = 0; i < matchPos && i < text.Length; i++)
            {
                if (text[i] == '\r' || text[i] == '\n')
                {
                    lineNumber++;
                    if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                }
            }

            double estimatedLineHeight = GetLineHeight();
            double targetVerticalOffset = (lineNumber - 1) * estimatedLineHeight;

            double viewportHeight = EditorScrollViewer.ViewportHeight;
            double centeredOffset = Math.Max(0, targetVerticalOffset - (viewportHeight / 2));

            EditorScrollViewer.ChangeView(null, targetVerticalOffset, null, true);
        }

        private void FindNext()
        {
            if (searchMatches.Count == 0) return;
            currentMatchIndex = (currentMatchIndex + 1) % searchMatches.Count;
            HighlightMatch();
            ScrollToCurrentMatch();
        }

        private void FindPrevious()
        {
            if (searchMatches.Count == 0) return;
            currentMatchIndex = currentMatchIndex == 0 ? searchMatches.Count - 1 : currentMatchIndex - 1;
            HighlightMatch();
            ScrollToCurrentMatch();
        }

        private void HighlightMatch()
        {
            if (currentMatchIndex < 0 || currentMatchIndex >= searchMatches.Count) return;

            int matchPos = searchMatches[currentMatchIndex];
            string searchTerm = findTextBox.Text;

            CodeEditor.Document.Selection.SetRange(matchPos, matchPos + searchTerm.Length);
            matchCountText.Text = $"{currentMatchIndex + 1} of {searchMatches.Count}";
        }

        private void ReplaceNext()
        {
            if (currentMatchIndex < 0 || string.IsNullOrEmpty(replaceTextBox.Text)) return;

            var selection = CodeEditor.Document.Selection;
            selection.TypeText(replaceTextBox.Text);

            SearchText(findTextBox.Text);
        }

        private void ReplaceAll()
        {
            if (searchMatches.Count == 0 || string.IsNullOrEmpty(replaceTextBox.Text)) return;

            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

            string newText = text.Replace(findTextBox.Text, replaceTextBox.Text, StringComparison.OrdinalIgnoreCase);
            CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, newText);

            int replacements = searchMatches.Count;
            StatusBarText.Text = $"Replaced {replacements} occurrences";

            searchMatches.Clear();
            currentMatchIndex = -1;
            matchCountText.Text = "";
        }
    }
}
