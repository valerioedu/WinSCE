using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI.Text;
using Windows.UI;
using Windows.System;

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
        private string currentFilePath = "";
        private string currentLanguage = "c";
        private bool isApplyingSyntaxHighlighting = false;
        private DispatcherTimer syntaxHighlightingTimer;
        private string lastHighlightedText = "";
        private readonly int MAX_HIGHLIGHT_LENGTH = 10000; // Skip highlighting for very large files
        private short tabs = 0;
        private short tabsize = 4;

        private Grid findReplacePanel;
        private TextBox findTextBox;
        private TextBox replaceTextBox;
        private TextBlock matchCountText;
        private Button replaceButton;
        private Button replaceAllButton;
        private List<int> searchMatches = new List<int>();
        private int currentMatchIndex = -1;


        // Define colors for syntax highlighting
        private readonly SolidColorBrush KeywordBrush = new SolidColorBrush(Color.FromArgb(255, 86, 156, 214)); // Blue - Types, declarations
        private readonly SolidColorBrush ControlFlowBrush = new SolidColorBrush(Color.FromArgb(255, 216, 160, 223)); // Purple - Control flow
        private readonly SolidColorBrush StringBrush = new SolidColorBrush(Color.FromArgb(255, 206, 145, 120)); // Orange
        private readonly SolidColorBrush CommentBrush = new SolidColorBrush(Color.FromArgb(255, 106, 153, 85)); // Green
        private readonly SolidColorBrush NumberBrush = new SolidColorBrush(Color.FromArgb(255, 181, 206, 168)); // Light green
        private readonly SolidColorBrush PreprocessorBrush = new SolidColorBrush(Color.FromArgb(255, 155, 155, 155)); // Gray
        private readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromArgb(255, 220, 220, 220)); // Light gray

        public MainWindow()
        {
            this.InitializeComponent();

            // Extend content into the title bar
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(
                Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                    WinRT.Interop.WindowNative.GetWindowHandle(this)
                )
            );
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0); // Transparent buttons

            // Set your custom title bar UIElement
            this.SetTitleBar(CustomTitleBar);

            CodeEditor.SelectionChanged += (s, e) =>
            {
                UpdateCursorPosition();
            };

            CodeEditor.TextChanged += (s, e) =>
            {
                UpdateLineNumbers();
                UpdateCursorPosition();
                ScheduleSyntaxHighlighting();
            };

            EditorScrollViewer.ViewChanged += (s, e) =>
            {
                LineNumbersScrollViewer.ChangeView(
                    null,
                    EditorScrollViewer.VerticalOffset,
                    null,
                    true);
            };

            UpdateLineNumbers();
            CreateFindReplacePanel();
        }

        private void CodeEditor_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            bool isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (isCtrlPressed)
            {
                switch (e.Key)
                {
                    case VirtualKey.S: // Ctrl+S
                        e.Handled = true;
                        Save_Click(null, null);
                        break;
                    case VirtualKey.O: // Ctrl+O
                        e.Handled = true;
                        Open_Click(null, null);
                        break;
                    case VirtualKey.N: // Ctrl+N
                        e.Handled = true;
                        New_Click(null, null);
                        break;
                    case VirtualKey.F: // Ctrl+F
                        e.Handled = true;
                        ShowFindPanel();
                        break;
                }
                return; // Exit early for Ctrl combinations
            }

            switch (e.Key)
            {
                case Windows.System.VirtualKey.Tab:
                    e.Handled = true; // Prevent default tab behavior
                    HandleTabKey();
                    break;
                case Windows.System.VirtualKey.Back:
                    HandleDelKey(e);
                    break;
                case Windows.System.VirtualKey.Enter:
                    HandleEnterKey();
                    break;
            }
        }

        private void HandleEnterKey()
        {
            var selection = CodeEditor.Document.Selection;
            var cursorPosition = selection.StartPosition;
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
            int column = GetColumnPosition(text, cursorPosition);
        }

        private void HandleDelKey(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var selection = CodeEditor.Document.Selection;
            int cursorPosition = selection.StartPosition;
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

            if (cursorPosition >= tabsize)
            {
                int column = GetColumnPosition(text, cursorPosition);
                bool atTabStop = (column - 1) % tabsize == 0;
                if (atTabStop)
                {
                    bool allSpaces = true;
                    for (int i = 1; i <= tabsize; i++)
                    {
                        if (cursorPosition - i < 0 || text[cursorPosition - i] != ' ')
                        {
                            allSpaces = false;
                            break;
                        }
                    }

                    if (allSpaces)
                    {
                        e.Handled = true;
                        var range = CodeEditor.Document.GetRange(cursorPosition - tabsize, cursorPosition);
                        range.Text = "";
                        tabs--;
                        return;
                    }
                }
            }
        }

        private void HandleTabKey()
        {
            var selection = CodeEditor.Document.Selection;
            var cursorPosition = selection.StartPosition;

            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

            int column = GetColumnPosition(text, cursorPosition);

            int spacesToAdd = tabsize - ((column - 1) % tabsize);

            for (int i = 0; i < spacesToAdd; i++)
            {
                selection.TypeText(" ");
            }
            tabs++;
        }

        private int GetColumnPosition(string text, int cursorPosition)
        {
            int column = 1;
            for (int i = cursorPosition - 1; i >= 0; i--)
            {
                if (text[i] == '\n' || text[i] == '\r')
                    break;
                column++;
            }
            return column;
        }

        private void ScheduleSyntaxHighlighting()
        {
            // Stop existing timer
            syntaxHighlightingTimer?.Stop();

            // Create new timer if it doesn't exist
            if (syntaxHighlightingTimer == null)
            {
                syntaxHighlightingTimer = new DispatcherTimer();
                syntaxHighlightingTimer.Tick += (s, e) =>
                {
                    syntaxHighlightingTimer.Stop();
                    ApplySyntaxHighlighting();
                };
            }

            // Start the timer
            syntaxHighlightingTimer.Start();
        }

        private void ApplySyntaxHighlightingImmediate()
        {
            // Cancel any pending scheduled highlighting
            syntaxHighlightingTimer?.Stop();

            // Apply highlighting immediately without delay
            lastHighlightedText = ""; // Force re-highlighting
            ApplySyntaxHighlighting();
        }

        private void ApplySyntaxHighlighting()
        {
            if (isApplyingSyntaxHighlighting) return;

            isApplyingSyntaxHighlighting = true;

            try
            {
                // Get current text and selection
                string text;
                CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

                // Skip highlighting for very large files
                if (text.Length > MAX_HIGHLIGHT_LENGTH)
                {
                    return;
                }

                // Skip if text hasn't changed
                if (text == lastHighlightedText)
                {
                    return;
                }

                lastHighlightedText = text;

                var selection = CodeEditor.Document.Selection;
                int selectionStart = selection.StartPosition;
                int selectionEnd = selection.EndPosition;

                // Clear existing formatting
                var range = CodeEditor.Document.GetRange(0, text.Length);
                range.CharacterFormat.ForegroundColor = DefaultBrush.Color;

                // Apply syntax highlighting based on current language
                var patterns = GetSyntaxPatterns(currentLanguage);

                foreach (var pattern in patterns)
                {
                    var matches = Regex.Matches(text, pattern.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    foreach (Match match in matches)
                    {
                        var highlightRange = CodeEditor.Document.GetRange(match.Index, match.Index + match.Length);
                        highlightRange.CharacterFormat.ForegroundColor = pattern.Color;
                    }
                }

                // Restore selection
                CodeEditor.Document.Selection.SetRange(selectionStart, selectionEnd);
            }
            catch (Exception ex)
            {
                // Handle any errors silently to avoid breaking the editor
                System.Diagnostics.Debug.WriteLine($"Syntax highlighting error: {ex.Message}");
            }
            finally
            {
                isApplyingSyntaxHighlighting = false;
            }
        }

        private List<SyntaxPattern> GetSyntaxPatterns(string language)
        {
            var patterns = new List<SyntaxPattern>();

            switch (language)
            {
                case "c":
                    patterns.AddRange(new[]
                    {
                        // Comments (higher priority)
                        new SyntaxPattern(@"//.*", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color),
                        // String literals
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        // Preprocessor directives (more specific)
                        new SyntaxPattern(@"^\s*#\s*\w+", PreprocessorBrush.Color),
                        // Numbers
                        new SyntaxPattern(@"\b\d+\.?\d*[fFlL]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        // Control flow keywords (purple)
                        new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return)\b", ControlFlowBrush.Color),
                        // Type and declaration keywords (blue)
                        new SyntaxPattern(@"\b(int|char|float|double|void|struct|enum|typedef|const|static|extern|auto|register|volatile|sizeof|union|long|short|signed|unsigned)\b", KeywordBrush.Color)
                    });
                    break;

                case "cpp":
                    patterns.AddRange(new[]
                    {
                        // Comments
                        new SyntaxPattern(@"//.*", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color),
                        // String literals
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        // Preprocessor directives (more specific)
                        new SyntaxPattern(@"^\s*#\s*\w+", PreprocessorBrush.Color),
                        // Numbers
                        new SyntaxPattern(@"\b\d+\.?\d*[fFlL]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        // Control flow keywords (purple)
                        new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return|try|catch|throw|true|false)\b", ControlFlowBrush.Color),
                        // Type and declaration keywords (blue)
                        new SyntaxPattern(@"\b(int|char|float|double|void|bool|class|struct|enum|typedef|const|static|extern|auto|template|namespace|using|public|private|protected|virtual|override|new|delete|nullptr)\b", KeywordBrush.Color)
                    });
                    break;

                case "csharp":
                    patterns.AddRange(new[]
                    {
                        // Comments
                        new SyntaxPattern(@"//.*", CommentBrush.Color),
                        new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color),
                        // String literals
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"@""(?:[^""]|"""")*""", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        // Numbers
                        new SyntaxPattern(@"\b\d+\.?\d*[fFdDmM]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        // Control flow keywords (purple)
                        new SyntaxPattern(@"\b(if|else|for|foreach|while|do|switch|case|default|break|continue|goto|return|try|catch|throw|finally|true|false|null)\b", ControlFlowBrush.Color),
                        // Type and declaration keywords (blue)
                        new SyntaxPattern(@"\b(int|char|float|double|decimal|string|bool|void|var|class|struct|enum|interface|namespace|using|public|private|protected|internal|static|abstract|virtual|override|new|this|base|typeof|sizeof)\b", KeywordBrush.Color)
                    });
                    break;
            }

            return patterns;
        }

        private void UpdateCursorPosition()
        {
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
            var selection = CodeEditor.Document.Selection;
            int cursorIndex = selection.StartPosition;

            var position = GetCursorPosition(text, cursorIndex);
            char last = cursorIndex > 0 && text.Length > 0 ? text[Math.Min(cursorIndex - 1, text.Length - 1)] : '\0';
            if (last > 27 && last < 128)
                StatusBarText.Text = $"Ln {position.line}, Col {position.column}, Key: {last}";
            else
                StatusBarText.Text = $"Ln {position.line}, Col {position.column}";
        }

        private void UpdateLineNumbers()
        {
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
            var selection = CodeEditor.Document.Selection;
            int cursorIndex = selection.StartPosition;

            var position = GetCursorPosition(text, cursorIndex);
            int line = position.line;
            int column = position.column;

            int lineCount = 1;
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] == '\r' || text[i] == '\n')
                {
                    lineCount++;
                    if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                }
            }

            string lineNumbers = "";
            for (int i = 1; i <= lineCount; i++)
            {
                lineNumbers += i + "\n";
            }

            if (string.IsNullOrEmpty(lineNumbers))
            {
                lineNumbers = "1";
            }

            LineNumbers.Text = lineNumbers;
        }

        private (int line, int column) GetCursorPosition(string text, int cursorIndex)
        {
            int line = 1;
            int column = 1;

            for (int i = 0; i < cursorIndex && i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return (line, column);
        }

        public int MyProperty
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                string text = await FileIO.ReadTextAsync(file);
                CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, text);
                currentFilePath = file.Path;

                // Detect language from file extension
                DetectLanguageFromFile(file.Name);

                // Apply syntax highlighting immediately for the opened file
                ApplySyntaxHighlightingImmediate();

                StatusBarText.Text = $"Opened: {file.Name}";
            }
        }
        private string GetTemplateForLanguage(string language)
        {
            return language switch
            {
                "c" => "#include <stdio.h>\n\nint main() {\n    printf(\"Hello, World!\\n\");\n    return 0;\n}",
                "cpp" => "#include <iostream>\n\nint main() {\n    std::cout << \"Hello, World!\" << std::endl;\n    return 0;\n}",
                "csharp" => "using System;\n\nclass Program\n{\n    static void Main()\n    {\n        Console.WriteLine(\"Hello, World!\");\n    }\n}",
                _ => "#include <iostream>\n\nint main() {\n    std::cout << \"Hello, World!\" << std::endl;\n    return 0;\n}"
            };
        }

        // Event handlers
        private void New_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, GetTemplateForLanguage(currentLanguage));
            currentFilePath = "";

            // Apply syntax highlighting immediately for the new file template
            ApplySyntaxHighlightingImmediate();

            StatusBarText.Text = "New file created";
        }

        private void DetectLanguageFromFile(string fileName)
        {
            string extension = System.IO.Path.GetExtension(fileName).ToLower();
            string detectedLanguage = extension switch
            {
                ".c" or ".h" => "c",
                ".cpp" or ".cxx" or ".cc" or ".hpp" or ".hxx" => "cpp",
                ".cs" => "csharp",
                _ => currentLanguage // Keep current language if unknown extension
            };

            if (detectedLanguage != currentLanguage)
            {
                currentLanguage = detectedLanguage;
                StatusBarText.Text = $"Language auto-detected: {GetLanguageDisplayName(currentLanguage)}";
            }
        }

        private void SetLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string language)
            {
                currentLanguage = language;
                StatusBarText.Text = $"Language set to {GetLanguageDisplayName(language)}";

                // Apply highlighting immediately when language changes
                ApplySyntaxHighlightingImmediate();
            }
        }

        private string GetLanguageDisplayName(string language)
        {
            return language switch
            {
                "c" => "C",
                "cpp" => "C++",
                "csharp" => "C#",
                _ => "C"
            };
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath)) await SaveAsFile();
            else await SaveCurrentFile();
        }

        private async System.Threading.Tasks.Task SaveAsFile()
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("C file", new[] { ".c" });
            picker.FileTypeChoices.Add("C++ file", new[] { ".cpp" });
            picker.FileTypeChoices.Add("C# file", new[] { ".cs" });
            //picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                string text;
                CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
                await FileIO.WriteTextAsync(file, text);
                currentFilePath = file.Path;
                StatusBarText.Text = $"Saved: {file.Name}";
            }
        }

        private async System.Threading.Tasks.Task SaveCurrentFile()
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                var file = await StorageFile.GetFileFromPathAsync(currentFilePath);
                string text;
                CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
                await FileIO.WriteTextAsync(file, text);
                StatusBarText.Text = "File saved";
            }
        }

        // Helper class for syntax patterns
        public class SyntaxPattern
        {
            public string Pattern { get; set; }
            public Color Color { get; set; }

            public SyntaxPattern(string pattern, Color color)
            {
                Pattern = pattern;
                Color = color;
            }
        }

        private void CodeEditor_CharacterReceived(UIElement sender, Microsoft.UI.Xaml.Input.CharacterReceivedRoutedEventArgs args)
        {
            switch (args.Character)
            {
                case '{':
                    var selection = CodeEditor.Document.Selection;
                    selection.TypeText("}");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '(':
                    selection = CodeEditor.Document.Selection;
                    selection.TypeText(")");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '[':
                    selection = CodeEditor.Document.Selection;
                    selection.TypeText("]");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '"':
                    selection = CodeEditor.Document.Selection;
                    selection.TypeText("\"");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '\'':
                    selection = CodeEditor.Document.Selection;
                    selection.TypeText("'");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
            }
            
        }

        private async void ShowFindDialog()
        {
            // Simple input dialog for search term
            var dialog = new ContentDialog()
            {
                Title = "Find",
                PrimaryButtonText = "Find",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var textBox = new TextBox()
            {
                PlaceholderText = "Enter text to find...",
                Width = 300
            };

            dialog.Content = textBox;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(textBox.Text))
            {
                FindText(textBox.Text);
            }
        }

        private void FindText(string searchTerm)
        {
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

            var selection = CodeEditor.Document.Selection;
            int startPos = selection.EndPosition; // Start searching from current position

            // Find the text (case-insensitive)
            int foundIndex = text.IndexOf(searchTerm, startPos, StringComparison.OrdinalIgnoreCase);

            // If not found from current position, search from beginning
            if (foundIndex == -1)
            {
                foundIndex = text.IndexOf(searchTerm, 0, StringComparison.OrdinalIgnoreCase);
            }

            if (foundIndex != -1)
            {
                // Select the found text
                CodeEditor.Document.Selection.SetRange(foundIndex, foundIndex + searchTerm.Length);
                StatusBarText.Text = $"Found: {searchTerm}";
            }
            else
            {
                StatusBarText.Text = $"Not found: {searchTerm}";
            }
        }
        void CreateFindReplacePanel()
        {
            // Create the VS Code-style find panel
            findReplacePanel = new Grid()
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 37, 37, 38)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                BorderThickness = new Thickness(0, 1, 0, 1),
                Height = 100,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 0)
            };

            findReplacePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            findReplacePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Find row
            var findRow = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 8, 10, 4),
                Spacing = 8
            };

            findTextBox = new TextBox()
            {
                PlaceholderText = "Find",
                Width = 200,
                Height = 26,
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(8, 4, 8, 4)
            };

            var findPrevButton = new Button()
            {
                Content = "▲",
                Width = 34,
                Height = 26,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
                CornerRadius = new CornerRadius(0)
            };

            var findNextButton = new Button()
            {
                Content = "▼",
                Width = 34,
                Height = 26,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0)
            };

            matchCountText = new TextBlock()
            {
                Text = "",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 153, 153, 153)),
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0)
            };

            var closeButton = new Button()
            {
                Content = "✕",
                Width = 34,
                Height = 26,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 0)
            };

            findRow.Children.Add(findTextBox);
            findRow.Children.Add(findPrevButton);
            findRow.Children.Add(findNextButton);
            findRow.Children.Add(matchCountText);
            findRow.Children.Add(closeButton);

            Grid.SetRow(findRow, 0);
            findReplacePanel.Children.Add(findRow);

            // Replace row
            var replaceRow = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 4, 10, 4),
                Spacing = 8
            };

            replaceTextBox = new TextBox()
            {
                PlaceholderText = "Replace",
                Width = 200,
                Height = 26,
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(8, 4, 8, 4)
            };

            replaceButton = new Button()
            {
                Content = "Replace",
                Height = 26,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(12, 4, 12, 4)
            };

            replaceAllButton = new Button()
            {
                Content = "Replace All",
                Height = 26,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(12, 4, 12, 4)
            };

            replaceRow.Children.Add(replaceTextBox);
            replaceRow.Children.Add(replaceButton);
            replaceRow.Children.Add(replaceAllButton);

            Grid.SetRow(replaceRow, 1);
            findReplacePanel.Children.Add(replaceRow);

            // Event handlers
            findTextBox.TextChanged += FindTextBox_TextChanged;
            findPrevButton.Click += (s, e) => FindPrevious();
            findNextButton.Click += (s, e) => FindNext();
            replaceButton.Click += (s, e) => ReplaceNext();
            replaceAllButton.Click += (s, e) => ReplaceAll();
            closeButton.Click += (s, e) => HideFindPanel();

            // Key handlers for the find panel
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

            replaceTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    ReplaceNext();
                    e.Handled = true;
                }
                else if (e.Key == VirtualKey.Escape)
                {
                    HideFindPanel();
                    e.Handled = true;
                }
            };
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
                findReplacePanel.Width = 392;
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
            }
            else
            {
                matchCountText.Text = "No results";
            }
        }

        private void FindNext()
        {
            if (searchMatches.Count == 0) return;
            currentMatchIndex = (currentMatchIndex + 1) % searchMatches.Count;
            HighlightMatch();
        }

        private void FindPrevious()
        {
            if (searchMatches.Count == 0) return;
            currentMatchIndex = currentMatchIndex == 0 ? searchMatches.Count - 1 : currentMatchIndex - 1;
            HighlightMatch();
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