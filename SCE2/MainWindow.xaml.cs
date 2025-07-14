using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;

namespace SCE2
{
    public class EditorState
    {
        public string Text { get; set; }
        public int CursorPosition { get; set; }
        public int SelectionLength { get; set; }

        public EditorState(string text, int cursorPosition, int selectionLength = 0)
        {
            Text = text;
            CursorPosition = cursorPosition;
            SelectionLength = selectionLength;
        }
    }

    public class UndoRedoManager
    {
        private readonly List<EditorState> _undoStack = new List<EditorState>();
        private readonly List<EditorState> _redoStack = new List<EditorState>();
        private const int MAX_UNDO_LEVELS = 100;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void SaveState(EditorState state)
        {
            if (_undoStack.Count > 0 && _undoStack[_undoStack.Count - 1].Text == state.Text)
                return;

            _undoStack.Add(state);

            if (_undoStack.Count > MAX_UNDO_LEVELS)
            {
                _undoStack.RemoveAt(0);
            }

            _redoStack.Clear();
        }

        public EditorState Undo(EditorState currentState)
        {
            if (!CanUndo) return null;

            _redoStack.Add(currentState);

            var state = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            return state;
        }

        public EditorState Redo()
        {
            if (!CanRedo) return null;

            var state = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            _undoStack.Add(state);

            return state;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }

    public sealed partial class MainWindow : Window
    {
        private string currentFilePath = "";
        private string currentLanguage = "c";
        private bool isApplyingSyntaxHighlighting = false;
        private DispatcherTimer syntaxHighlightingTimer;
        private string lastHighlightedText = "";
        private readonly int MAX_HIGHLIGHT_LENGTH = 1000000;
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

        private Button toggleReplaceButton;
        private Popup replacePopup;
        private string lastStatusText = "";
        private readonly Queue<List<int>> _searchMatchesPool = new();

        private UndoRedoManager undoRedoManager = new UndoRedoManager();
        private bool isRestoringState = false;
        private DateTime lastKeyTime = DateTime.MinValue;
        private string lastSavedText = "";
        private DispatcherTimer undoSaveTimer;

        private readonly SolidColorBrush KeywordBrush = new SolidColorBrush(Color.FromArgb(255, 86, 156, 214));
        private readonly SolidColorBrush ControlFlowBrush = new SolidColorBrush(Color.FromArgb(255, 216, 160, 223));
        private readonly SolidColorBrush StringBrush = new SolidColorBrush(Color.FromArgb(255, 206, 145, 120));
        private readonly SolidColorBrush CommentBrush = new SolidColorBrush(Color.FromArgb(255, 106, 153, 85));
        private readonly SolidColorBrush NumberBrush = new SolidColorBrush(Color.FromArgb(255, 181, 206, 168));
        private readonly SolidColorBrush PreprocessorBrush = new SolidColorBrush(Color.FromArgb(255, 155, 155, 155));
        private readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromArgb(255, 220, 220, 220));
        private readonly SolidColorBrush FunctionBrush = new SolidColorBrush(Color.FromArgb(255, 220, 220, 170));
        private readonly SolidColorBrush EscapeSequenceBrush = new SolidColorBrush(Color.FromArgb(255, 255, 206, 84));

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
                if (!isRestoringState)
                {
                    UpdateLineNumbers();
                    UpdateCursorPosition();
                    ScheduleSyntaxHighlighting();
                    ScheduleUndoStateSave();
                }
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
                    case VirtualKey.Z:
                        e.Handled = true;
                        PerformUndo();
                        break;
                    case VirtualKey.Y:
                        e.Handled = true;
                        PerformRedo();
                        break;
                    case VirtualKey.L:
                        e.Handled = true;

                        try
                        {
                            var selection = CodeEditor.Document.Selection;
                            int cursorPosition = selection.StartPosition;
                            string text;
                            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

                            int lineStart = cursorPosition;
                            while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
                            {
                                lineStart--;
                            }

                            int lineEnd = cursorPosition;
                            while (lineEnd < text.Length && text[lineEnd] != '\n' && text[lineEnd] != '\r')
                            {
                                lineEnd++;
                            }

                            if (lineEnd < text.Length && (text[lineEnd] == '\n' || text[lineEnd] == '\r'))
                            {
                                lineEnd++;
                                if (lineEnd < text.Length && text[lineEnd - 1] == '\r' && text[lineEnd] == '\n')
                                {
                                    lineEnd++;
                                }
                            }

                            CodeEditor.Document.Selection.SetRange(lineStart, lineEnd);
                        }
                        catch { }
                        break;
                }
                return;
            }

            if (ShouldSaveUndoState(e.Key))
            {
                SaveCurrentStateForUndo();
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

        private bool ShouldSaveUndoState(VirtualKey key)
        {
            return key == VirtualKey.Back ||
                   key == VirtualKey.Delete ||
                   key == VirtualKey.Enter ||
                   key == VirtualKey.Tab ||
                   IsTypableKey(key);
        }

        private bool IsTypableKey(VirtualKey key)
        {
            return (key >= VirtualKey.A && key <= VirtualKey.Z) ||
                   (key >= VirtualKey.Number0 && key <= VirtualKey.Number9) ||
                   (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9) ||
                   key == VirtualKey.Space ||
                   key == VirtualKey.Decimal ||
                   key == VirtualKey.Add ||
                   key == VirtualKey.Subtract ||
                   key == VirtualKey.Multiply ||
                   key == VirtualKey.Divide ||
                   (key >= (VirtualKey)186 && key <= (VirtualKey)222);
        }

        private void SaveCurrentStateForUndo()
        {
            if (isRestoringState) return;

            string currentText;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out currentText);

            var selection = CodeEditor.Document.Selection;
            var state = new EditorState(currentText, selection.StartPosition, selection.Length);

            undoRedoManager.SaveState(state);
        }

        private void PerformUndo()
        {
            if (!undoRedoManager.CanUndo) return;

            string currentText;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out currentText);
            var selection = CodeEditor.Document.Selection;
            var currentState = new EditorState(currentText, selection.StartPosition, selection.Length);

            var previousState = undoRedoManager.Undo(currentState);
            if (previousState != null)
            {
                RestoreState(previousState);
            }
        }

        private void PerformRedo()
        {
            if (!undoRedoManager.CanRedo) return;

            var nextState = undoRedoManager.Redo();
            if (nextState != null)
            {
                RestoreState(nextState);
            }
        }

        private void RestoreState(EditorState state)
        {
            isRestoringState = true;

            try
            {
                CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, state.Text);

                if (state.SelectionLength > 0)
                {
                    CodeEditor.Document.Selection.SetRange(state.CursorPosition, state.CursorPosition + state.SelectionLength);
                }
                else
                {
                    CodeEditor.Document.Selection.SetRange(state.CursorPosition, state.CursorPosition);
                }

                UpdateLineNumbers();
                UpdateCursorPosition();
                ApplySyntaxHighlightingImmediate();
            }
            finally
            {
                isRestoringState = false;
            }
        }

        private void ScheduleUndoStateSave()
        {
            undoSaveTimer?.Stop();

            if (undoSaveTimer == null)
            {
                undoSaveTimer = new DispatcherTimer();
                undoSaveTimer.Interval = TimeSpan.FromSeconds(1);
                undoSaveTimer.Tick += (s, e) =>
                {
                    undoSaveTimer.Stop();
                    if (!isRestoringState)
                    {
                        SaveCurrentStateForUndo();
                    }
                };
            }

            undoSaveTimer.Start();
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
                        new SyntaxPattern(@"\b\d+\.?\d*[fFlL]?\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b0[xX][0-9a-fA-F]+\b", NumberBrush.Color),
                        new SyntaxPattern(@"\b([a-zA-Z_]\w*)\s*(?=\()", FunctionBrush.Color),
                        new SyntaxPattern(@"\b(if|else|for|while|do|switch|case|default|break|continue|goto|return)\b", ControlFlowBrush.Color),
                        new SyntaxPattern(@"\b(int|char|float|double|void|struct|enum|typedef|const|static|extern|auto|register|volatile|sizeof|union|long|short|signed|unsigned)\b", KeywordBrush.Color),
                        new SyntaxPattern(@"^\s*#\s*\w+", PreprocessorBrush.Color),
                        new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", StringBrush.Color),
                        new SyntaxPattern(@"(?<=#\s*include\s*)[<""][^>""]+[>""]", StringBrush.Color),
                        new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", StringBrush.Color),
                        new SyntaxPattern(@"\\[abfnrtv\\'\""]", EscapeSequenceBrush.Color),
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

                undoRedoManager.Clear();

                CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, text);
                currentFilePath = file.Path;

                DetectLanguageFromFile(file.Name);
                ApplySyntaxHighlightingImmediate();

                SaveCurrentStateForUndo();

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
            undoRedoManager.Clear();

            CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, GetTemplateForLanguage(currentLanguage));
            currentFilePath = "";

            ApplySyntaxHighlightingImmediate();

            SaveCurrentStateForUndo();

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

        private double GetLineHeight()
        {
            try
            {
                string text;
                CodeEditor.Document.GetText(TextGetOptions.None, out text);

                if (string.IsNullOrEmpty(text))
                {
                    CodeEditor.Document.SetText(TextSetOptions.None, "A");
                    var tempRange = CodeEditor.Document.GetRange(0, 1);
                    tempRange.GetRect(PointOptions.None, out Rect tempRect, out int tempHit);
                    CodeEditor.Document.SetText(TextSetOptions.None, "");
                    return tempRect.Height;
                }
                else
                {
                    var range = CodeEditor.Document.GetRange(0, 1);
                    range.GetRect(PointOptions.None, out Rect rect, out int hit);
                    return rect.Height;
                }
            }
            catch
            {
                return CodeEditor.FontSize * 1.2;
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