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
            private readonly int MAX_HIGHLIGHT_LENGTH = 1000000; // Skip highlighting for very large files
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

                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(
                    Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                        WinRT.Interop.WindowNative.GetWindowHandle(this)
                    )
                );
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

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
                        case VirtualKey.S:
                            e.Handled = true;
                            Save_Click(null, null);
                            break;
                        case VirtualKey.O:
                            e.Handled = true;
                            Open_Click(null, null);
                            break;
                        case VirtualKey.N:
                            e.Handled = true;
                            New_Click(null, null);
                            break;
                        case VirtualKey.F:
                            e.Handled = true;
                            ShowFindPanel();
                            break;
                    }
                    return;
                }

                switch (e.Key)
                {
                    case Windows.System.VirtualKey.Tab:
                        e.Handled = true;
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
                syntaxHighlightingTimer?.Stop();

                if (syntaxHighlightingTimer == null)
                {
                    syntaxHighlightingTimer = new DispatcherTimer();
                    syntaxHighlightingTimer.Tick += (s, e) =>
                    {
                        syntaxHighlightingTimer.Stop();
                        ApplySyntaxHighlighting();
                    };
                }

                syntaxHighlightingTimer.Start();
            }

            private void ApplySyntaxHighlightingImmediate()
            {
                syntaxHighlightingTimer?.Stop();

                lastHighlightedText = "";
                ApplySyntaxHighlighting();
            }

            private void ApplySyntaxHighlighting()
            {
                if (isApplyingSyntaxHighlighting) return;

                isApplyingSyntaxHighlighting = true;

                try
                {
                    string text;
                    CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

                    if (text.Length > MAX_HIGHLIGHT_LENGTH) return;

                    if (text == lastHighlightedText) return;

                    lastHighlightedText = text;

                    var selection = CodeEditor.Document.Selection;
                    int selectionStart = selection.StartPosition;
                    int selectionEnd = selection.EndPosition;

                    var range = CodeEditor.Document.GetRange(0, text.Length);
                    range.CharacterFormat.ForegroundColor = DefaultBrush.Color;

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

                    CodeEditor.Document.Selection.SetRange(selectionStart, selectionEnd);
                }
                catch (Exception ex)
                {
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
                            new SyntaxPattern(@"^\s*#\s*\w+", PreprocessorBrush.Color),
                            new SyntaxPattern(@"\b\d+\.?\d*[fFlL]?\b", NumberBrush.Color),
                            new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                            new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return)\b", ControlFlowBrush.Color),
                            new SyntaxPattern(@"\b(int|char|float|double|void|struct|enum|typedef|const|static|extern|auto|register|volatile|sizeof|union|long|short|signed|unsigned)\b", KeywordBrush.Color),
                            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                            new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                            new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color),
                        });
                        break;

                    case "cpp":
                        patterns.AddRange(new[]
                        {
                            new SyntaxPattern(@"^\s*#\s*\w+", PreprocessorBrush.Color),
                            new SyntaxPattern(@"\b\d+\.?\d*[fFlL]?\b", NumberBrush.Color),
                            new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                            new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return|try|catch|throw|true|false)\b", ControlFlowBrush.Color),
                            new SyntaxPattern(@"\b(int|char|float|double|void|bool|class|struct|enum|typedef|const|static|extern|auto|template|namespace|using|public|private|protected|virtual|override|new|delete|nullptr)\b", KeywordBrush.Color),
                            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                            new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                            new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color)
                        });
                        break;

                    case "csharp":
                        patterns.AddRange(new[]
                        {
                            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                            new SyntaxPattern(@"\b\d+\.?\d*[fFdDmM]?\b", NumberBrush.Color),
                            new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                            new SyntaxPattern(@"\b(if|else|for|foreach|while|do|switch|case|default|break|continue|goto|return|try|catch|throw|finally|true|false|null)\b", ControlFlowBrush.Color),
                            new SyntaxPattern(@"\b(int|char|float|double|decimal|string|bool|void|var|class|struct|enum|interface|namespace|using|public|private|protected|internal|static|abstract|virtual|override|new|this|base|typeof|sizeof)\b", KeywordBrush.Color),
                            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                            new SyntaxPattern(@"@""(?:[^""]|"""")*""", StringBrush.Color),
                            new SyntaxPattern(@"//.*?(?=\r|\n|$)", CommentBrush.Color),
                            new SyntaxPattern(@"/\*[\s\S]*?\*/", CommentBrush.Color)
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

                    DetectLanguageFromFile(file.Name);

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
                    _ => currentLanguage
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
                    case '*':
                        string text;
                        CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
                        selection = CodeEditor.Document.Selection;
                        int cursorPosition = selection.StartPosition;

                        if (cursorPosition > 0 && text[cursorPosition - 2] == '/')
                        {
                            selection.TypeText("*/");
                            selection.SetRange(cursorPosition, cursorPosition);
                        }
                        break;
                }

            }

            void CreateFindReplacePanel()
            {
                findReplacePanel = new Grid()
                {
                    Height = 100,
                    Visibility = Visibility.Collapsed,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0)
                };

                findReplacePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                findReplacePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

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
                    Height = 26
                };

                var findPrevButton = new Button()
                {
                    Content = "▲",
                    Width = 34,
                    Height = 26,
                    CornerRadius = new CornerRadius(0)
                };

                var findNextButton = new Button()
                {
                    Content = "▼",
                    Width = 34,
                    Height = 26,
                    FontSize = 12,
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

                findRow.Children.Add(findTextBox);
                findRow.Children.Add(findPrevButton);
                findRow.Children.Add(findNextButton);
                findRow.Children.Add(matchCountText);
                findRow.Children.Add(closeButton);

                Grid.SetRow(findRow, 0);
                findReplacePanel.Children.Add(findRow);

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
                    UseSystemFocusVisuals = false,
                };

                replaceButton = new Button()
                {
                    Content = "Replace",
                    Height = 32,
                    CornerRadius = new CornerRadius(0)
                };

                replaceAllButton = new Button()
                {
                    Content = "Replace All",
                    Height = 32,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    CornerRadius = new CornerRadius(0)
                };

                replaceRow.Children.Add(replaceTextBox);
                replaceRow.Children.Add(replaceButton);
                replaceRow.Children.Add(replaceAllButton);

                Grid.SetRow(replaceRow, 1);
                findReplacePanel.Children.Add(replaceRow);

                findTextBox.TextChanged += FindTextBox_TextChanged;
                findPrevButton.Click += (s, e) => FindPrevious();
                findNextButton.Click += (s, e) => FindNext();
                replaceButton.Click += (s, e) => ReplaceNext();
                replaceAllButton.Click += (s, e) => ReplaceAll();
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
                    findReplacePanel.Width = 406;
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
                    findReplacePanel.Width = 420;
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