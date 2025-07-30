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
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
        private string currentFilePath = "";
        private string currentLanguage = "c";
        private bool isApplyingSyntaxHighlighting = false;
        private DispatcherTimer syntaxHighlightingTimer;
        private string lastHighlightedText = "";
        private readonly int maxHighlightLength = 1000000;

        private short tabSize = 4;
        private bool autoIndentationEnabled = true;
        private bool autoCompletionEnabled = true;
        private bool autoBraceClosingEnabled = true;
        private bool lineNumbersEnabled = true;
        private bool wordWrapEnabled = false;
        
        private bool autoSaveEnabled = false;
        private bool restoreSessionEnabled = true;
        private DispatcherTimer autoSaveTimer;
        private int autoSaveInterval = 30;
        private bool hasUnsavedChanges = false;

        private Grid findReplacePanel;
        private TextBox findTextBox;
        private TextBox replaceTextBox;
        private TextBlock matchCountText;
        private Button replaceButton;
        private Button replaceAllButton;
        private readonly List<int> searchMatches = new List<int>();
        private int currentMatchIndex = -1;

        private Button toggleReplaceButton;
        private Popup replacePopup;
        private readonly Queue<List<int>> _searchMatchesPool = new();

        private readonly SolidColorBrush KeywordBrush = new SolidColorBrush(Color.FromArgb(255, 86, 156, 214));
        private readonly SolidColorBrush ControlFlowBrush = new SolidColorBrush(Color.FromArgb(255, 216, 160, 223));
        private readonly SolidColorBrush StringBrush = new SolidColorBrush(Color.FromArgb(255, 206, 145, 120));
        private readonly SolidColorBrush CommentBrush = new SolidColorBrush(Color.FromArgb(255, 106, 153, 85));
        private readonly SolidColorBrush NumberBrush = new SolidColorBrush(Color.FromArgb(255, 181, 206, 168));
        private readonly SolidColorBrush PreprocessorBrush = new SolidColorBrush(Color.FromArgb(255, 155, 155, 155));
        private readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromArgb(255, 220, 220, 220));
        private readonly SolidColorBrush FunctionBrush = new SolidColorBrush(Color.FromArgb(255, 220, 220, 170));
        private readonly SolidColorBrush EscapeSequenceBrush = new SolidColorBrush(Color.FromArgb(255, 255, 206, 84));

        private SettingsWindow settingsWindow;

        private bool isTerminalVisible = false;
        private bool isDraggingSplitter = false;
        private double terminalHeight = 300;
        private Point lastPointerPosition;

        private bool isGitPanelVisible = false;
        private bool isDraggingGitSplitter = false;
        private double gitPanelWidth = 380;
        private Point lastGitPointerPosition;

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

            LoadSettings();

            if (restoreSessionEnabled)
            {
                LoadLastSession();
            }

            InitializeAutoSave();

            CodeEditor.SelectionChanged += (s, e) =>
            {
                UpdateCursorPosition();
            };

            CodeEditor.TextChanged += (s, e) =>
            {
                if (lineNumbersEnabled)
                {
                    UpdateLineNumbers();
                }
                UpdateCursorPosition();
                ScheduleSyntaxHighlighting();

                hasUnsavedChanges = true;

                if (autoSaveEnabled)
                {
                    SaveLastSession();
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
            ApplySettings();

            this.Closed += (s, e) =>
            {
                if (restoreSessionEnabled)
                {
                    SaveLastSession();
                }

                if (autoSaveTimer != null)
                {
                    autoSaveTimer.Stop();
                }

                if (settingsWindow != null)
                {
                    settingsWindow.Close();
                }
            };

        }

        private void FileMenuShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            FileButton.Flyout.ShowAt(FileButton);
            args.Handled = true;
        }

        private void InitializeAutoSave()
        {
            if (autoSaveEnabled)
            {
                autoSaveTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(autoSaveInterval)
                };
                autoSaveTimer.Tick += AutoSaveTimer_Tick;
                autoSaveTimer.Start();
            }
        }

        private void AutoSaveTimer_Tick(object sender, object e)
        {
            if (hasUnsavedChanges && autoSaveEnabled)
            {
                SaveLastSession();

                var currentStatus = StatusBarText.Text;
                StatusBarText.Text = currentStatus.Contains("[Auto-saved]") ? currentStatus : currentStatus + " [Auto-saved]";

                var clearTimer = new DispatcherTimer();
                clearTimer.Interval = TimeSpan.FromSeconds(2);
                clearTimer.Tick += (s, args) =>
                {
                    clearTimer.Stop();
                    UpdateCursorPosition();
                };
                clearTimer.Start();

                hasUnsavedChanges = false;
            }
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
                        catch (Exception ex)
                        {
                            StatusBarText.Text = $"Error saving file: {ex.Message}";
                        }
                        break;
                    case VirtualKey.J:
                        e.Handled = true;
                        ToggleTerminal();
                        break;
                    case VirtualKey.G:
                        e.Handled = true;
                        ToggleGitPanel();
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
                    HandleEnterKey(e);
                    break;
            }
        }

        private void HandleEnterKey(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (!autoIndentationEnabled) return;
            var selection = CodeEditor.Document.Selection;
            var cursorPosition = selection.StartPosition;
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

            var currentIndentation = GetCurrentLineIndentation(text, cursorPosition);

            if (cursorPosition > 0 && cursorPosition < text.Length &&
                text[cursorPosition - 1] == '{' && text[cursorPosition] == '}')
            {
                e.Handled = true;

                var indentedText = "\n" + currentIndentation + GetIndentString() + "\n" + currentIndentation;
                selection.TypeText(indentedText);

                var newCursorPosition = cursorPosition + currentIndentation.Length + GetIndentString().Length + 1;
                selection.SetRange(newCursorPosition, newCursorPosition);
            }
            else
            {
                var shouldIndentNext = ShouldIndentNextLine(text, cursorPosition);

                e.Handled = true;

                if (shouldIndentNext)
                {
                    var indentedText = "\n" + currentIndentation + GetIndentString();
                    selection.TypeText(indentedText);
                }
                else
                {
                    var indentedText = "\n" + currentIndentation;
                    selection.TypeText(indentedText);
                }
            }
        }

        private string GetCurrentLineIndentation(string text, int cursorPosition)
        {
            var lineStart = cursorPosition;
            while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
            {
                lineStart--;
            }

            var indentation = "";
            for (var i = lineStart; i < text.Length && (text[i] == ' ' || text[i] == '\t'); i++)
            {
                indentation += text[i];
            }

            return indentation;
        }

        private bool ShouldIndentNextLine(string text, int cursorPosition)
        {
            var lineStart = cursorPosition;
            while (lineStart > 0 && text[lineStart - 1] != '\n' && text[lineStart - 1] != '\r')
            {
                lineStart--;
            }

            var lineContent = "";
            for (var i = lineStart; i < cursorPosition && i < text.Length; i++)
            {
                lineContent += text[i];
            }

            var trimmedLine = lineContent.TrimEnd();

            return trimmedLine.EndsWith("{") ||
                   trimmedLine.EndsWith(":") ||
                   IsControlStructure(trimmedLine);
        }

        private bool IsControlStructure(string line)
        {
            var trimmed = line.Trim();

            return trimmed.StartsWith("if ") ||
                   trimmed.StartsWith("else") ||
                   trimmed.StartsWith("while ") ||
                   trimmed.StartsWith("for ") ||
                   trimmed.StartsWith("do") ||
                   trimmed.StartsWith("switch ") ||
                   trimmed.StartsWith("case ") ||
                   trimmed.StartsWith("default:");
        }

        private string GetIndentString()
        {
            return new string(' ', tabSize);
        }

        private void HandleDelKey(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var selection = CodeEditor.Document.Selection;
            var cursorPosition = selection.StartPosition;
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

            if (cursorPosition >= tabSize)
            {
                var column = GetColumnPosition(text, cursorPosition);
                var atTabStop = (column - 1) % tabSize == 0;
                if (atTabStop)
                {
                    var allSpaces = true;
                    for (var i = 1; i <= tabSize; i++)
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
                        var range = CodeEditor.Document.GetRange(cursorPosition - tabSize, cursorPosition);
                        range.Text = "";
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

            var column = GetColumnPosition(text, cursorPosition);

            var spacesToAdd = tabSize - ((column - 1) % tabSize);

            for (var i = 0; i < spacesToAdd; i++)
            {
                selection.TypeText(" ");
            }
        }

        private int GetColumnPosition(string text, int cursorPosition)
        {
            var column = 1;
            for (var i = cursorPosition - 1; i >= 0; i--)
            {
                if (text[i] == '\n' || text[i] == '\r')
                    break;
                column++;
            }
            return column;
        }

        private void UpdateCursorPosition()
        {
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
            var selection = CodeEditor.Document.Selection;
            var cursorIndex = selection.StartPosition;

            var position = GetCursorPosition(text, cursorIndex);
            var last = cursorIndex > 0 && text.Length > 0 ? text[Math.Min(cursorIndex - 1, text.Length - 1)] : '\0';

            var fileName = string.IsNullOrEmpty(currentFilePath) ? "Untitled" : System.IO.Path.GetFileName(currentFilePath);

            if (last > 27 && last < 128)
                StatusBarText.Text = $"Ln {position.line}, Col {position.column}, Key: {last} | {fileName}";
            else
                StatusBarText.Text = $"Ln {position.line}, Col {position.column} | {fileName}";
        }

        private void UpdateLineNumbers()
        {
            string text;
            CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
            var selection = CodeEditor.Document.Selection;
            var cursorIndex = selection.StartPosition;

            var position = GetCursorPosition(text, cursorIndex);
            var line = position.line;
            var column = position.column;

            var lineCount = 1;
            for (var i = 0; i < text.Length - 1; i++)
            {
                if (text[i] == '\r' || text[i] == '\n')
                {
                    lineCount++;
                    if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                }
            }

            var lineNumbers = "";
            for (var i = 1; i <= lineCount; i++)
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
            var line = 1;
            var column = 1;

            for (var i = 0; i < cursorIndex && i < text.Length; i++)
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

        private void CodeEditor_CharacterReceived(UIElement sender, Microsoft.UI.Xaml.Input.CharacterReceivedRoutedEventArgs args)
        {
            if (!autoBraceClosingEnabled && !autoCompletionEnabled) return;
            switch (args.Character)
            {
                case '{':
                    if (!autoBraceClosingEnabled) return;
                    var selection = CodeEditor.Document.Selection;
                    selection.TypeText("}");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '(':
                    if (!autoBraceClosingEnabled) return;
                    selection = CodeEditor.Document.Selection;
                    selection.TypeText(")");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '[':
                    if (!autoBraceClosingEnabled) return;
                    selection = CodeEditor.Document.Selection;
                    selection.TypeText("]");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '"':
                    if (!autoCompletionEnabled) return;
                    selection = CodeEditor.Document.Selection;
                    selection.TypeText("\"");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '\'':
                    if (!autoCompletionEnabled) return;
                    selection = CodeEditor.Document.Selection;
                    selection.TypeText("'");
                    selection.SetRange(selection.StartPosition - 1, selection.StartPosition - 1);
                    break;
                case '*':
                    if (!autoCompletionEnabled) return;
                    string text;
                    CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
                    selection = CodeEditor.Document.Selection;
                    var cursorPosition = selection.StartPosition;

                    if (cursorPosition > 0 && text[cursorPosition - 2] == '/')
                    {
                        selection.TypeText("*/");
                        selection.SetRange(cursorPosition, cursorPosition);
                    }
                    break;
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

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (settingsWindow != null)
            {
                settingsWindow.Close();
                settingsWindow = null;
                return;
            }

            settingsWindow = new SettingsWindow(this);
            settingsWindow.Activate();
        }

        private void LoadSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                tabSize = (short)(localSettings.Values["TabSize"] ?? 4);
                autoIndentationEnabled = (bool)(localSettings.Values["AutoIndentation"] ?? true);
                autoCompletionEnabled = (bool)(localSettings.Values["AutoCompletion"] ?? true);
                autoBraceClosingEnabled = (bool)(localSettings.Values["AutoBraceClosing"] ?? true);
                lineNumbersEnabled = (bool)(localSettings.Values["LineNumbers"] ?? true);
                wordWrapEnabled = (bool)(localSettings.Values["WordWrap"] ?? false);

                autoSaveEnabled = (bool)(localSettings.Values["AutoSave"] ?? false);
                restoreSessionEnabled = (bool)(localSettings.Values["RestoreSession"] ?? true);
                autoSaveInterval = (int)(localSettings.Values["AutoSaveInterval"] ?? 30);
            }
            catch
            {
                tabSize = 4;
                autoIndentationEnabled = true;
                autoCompletionEnabled = true;
                autoBraceClosingEnabled = true;
                lineNumbersEnabled = true;
                wordWrapEnabled = false;
                autoSaveEnabled = false;
                restoreSessionEnabled = true;
                autoSaveInterval = 30;
            }
        }

        private void SaveSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                localSettings.Values["TabSize"] = tabSize;
                localSettings.Values["AutoIndentation"] = autoIndentationEnabled;
                localSettings.Values["AutoCompletion"] = autoCompletionEnabled;
                localSettings.Values["AutoBraceClosing"] = autoBraceClosingEnabled;
                localSettings.Values["LineNumbers"] = lineNumbersEnabled;
                localSettings.Values["WordWrap"] = wordWrapEnabled;

                localSettings.Values["AutoSave"] = autoSaveEnabled;
                localSettings.Values["RestoreSession"] = restoreSessionEnabled;
                localSettings.Values["AutoSaveInterval"] = autoSaveInterval;
            }
            catch { }
        }

        private void LoadLastSession()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                var lastContent = localSettings.Values["LastContent"] as string;
                var lastFilePath = localSettings.Values["LastFilePath"] as string;
                var lastLanguage = localSettings.Values["LastLanguage"] as string;

                if (!string.IsNullOrEmpty(lastContent))
                {
                    if (lastContent.EndsWith("\r"))
                    {
                        lastContent = lastContent.TrimEnd('\r');
                    }
                    if (lastContent.EndsWith("\n"))
                    {
                        lastContent = lastContent.TrimEnd('\n');
                    }
                    if (lastContent.EndsWith("\r\n"))
                    {
                        lastContent = lastContent.TrimEnd('\r', '\n');
                    }

                    CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, lastContent);
                }

                if (!string.IsNullOrEmpty(lastFilePath))
                {
                    currentFilePath = lastFilePath;
                }

                if (!string.IsNullOrEmpty(lastLanguage))
                {
                    currentLanguage = lastLanguage;
                }

                StatusBarText.Text = "Session restored";
                hasUnsavedChanges = false;
            }
            catch
            {
                currentLanguage = "c";
            }
        }

        private void SaveLastSession()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                string text;
                CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);

                localSettings.Values["LastContent"] = text;
                localSettings.Values["LastFilePath"] = currentFilePath;
                localSettings.Values["LastLanguage"] = currentLanguage;
            }
            catch { }
        }

        private void ApplySettings()
        {
            LineNumbersScrollViewer.Visibility = lineNumbersEnabled ? Visibility.Visible : Visibility.Collapsed;

            CodeEditor.TextWrapping = wordWrapEnabled ? TextWrapping.Wrap : TextWrapping.NoWrap;

            var editorParent = CodeEditor.Parent as FrameworkElement;
            if (editorParent != null)
            {
                if (lineNumbersEnabled)
                {
                    Grid.SetColumn(editorParent, 1);
                    Grid.SetColumnSpan(editorParent, 1);
                }
                else
                {
                    Grid.SetColumn(editorParent, 0);
                    Grid.SetColumnSpan(editorParent, 2);
                }
            }

            InitializeAutoSave();
        }

        public void UpdateSettings(short newTabSize, bool newAutoIndent, bool newAutoCompletion,
            bool newAutoBraceClosing, bool newLineNumbers, bool newWordWrap, bool newAutoSave, bool newRestoreSession, int newAutoSaveInterval)
        {
            tabSize = newTabSize;
            autoIndentationEnabled = newAutoIndent;
            autoCompletionEnabled = newAutoCompletion;
            autoBraceClosingEnabled = newAutoBraceClosing;
            lineNumbersEnabled = newLineNumbers;
            wordWrapEnabled = newWordWrap;
            autoSaveEnabled = newAutoSave;
            restoreSessionEnabled = newRestoreSession;
            autoSaveInterval = newAutoSaveInterval;

            SaveSettings();
            ApplySettings();
        }

        public (short tabSize, bool autoIndent, bool autoCompletion, bool autoBraceClosing, bool lineNumbers, bool wordWrap, bool autoSave, bool restoreSession, int autoSaveInterval) GetCurrentSettings()
        {
            return (tabSize, autoIndentationEnabled, autoCompletionEnabled, autoBraceClosingEnabled, lineNumbersEnabled, wordWrapEnabled, autoSaveEnabled, restoreSessionEnabled, autoSaveInterval);
        }

        private async void Quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }
        private void Terminal_Click(object sender, RoutedEventArgs e)
        {
            ToggleTerminal();
        }

        private void ToggleTerminal()
        {
            isTerminalVisible = !isTerminalVisible;

            if (isTerminalVisible)
            {
                TerminalRow.Height = new GridLength(terminalHeight);
                TerminalPanel.Visibility = Visibility.Visible;
                TerminalSplitter.Visibility = Visibility.Visible;

                TerminalPanel.FocusInput();
            }
            else
            {
                TerminalRow.Height = new GridLength(0);
                TerminalPanel.Visibility = Visibility.Collapsed;
                TerminalSplitter.Visibility = Visibility.Collapsed;

                CodeEditor.Focus(FocusState.Programmatic);
            }
        }
        private void TerminalSplitter_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            isDraggingSplitter = true;
            lastPointerPosition = e.GetCurrentPoint(TerminalSplitter).Position;
            TerminalSplitter.CapturePointer(e.Pointer);
        }

        private void TerminalSplitter_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (isDraggingSplitter)
            {
                var currentPosition = e.GetCurrentPoint(TerminalSplitter).Position;
                var deltaY = lastPointerPosition.Y - currentPosition.Y;

                var newHeight = terminalHeight + deltaY;
                var windowHeight = ((FrameworkElement)this.Content).ActualHeight;

                if (newHeight >= 100 && newHeight <= windowHeight - 200)
                {
                    terminalHeight = newHeight;
                    TerminalRow.Height = new GridLength(terminalHeight);
                }

                lastPointerPosition = currentPosition;
            }
        }

        private void TerminalSplitter_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            isDraggingSplitter = false;
            TerminalSplitter.ReleasePointerCapture(e.Pointer);
        }

        private void Git_Click(object sender, RoutedEventArgs e)
        {
            ToggleGitPanel();
        }

        private void ToggleGitPanel()
        {
            isGitPanelVisible = !isGitPanelVisible;

            if (isGitPanelVisible)
            {
                GitColumn.Width = new GridLength(gitPanelWidth);
                GitPanel.Visibility = Visibility.Visible;
                GitSplitter.Visibility = Visibility.Visible;

                UpdateGitContext();
            }
            else
            {
                GitColumn.Width = new GridLength(0);
                GitPanel.Visibility = Visibility.Collapsed;
                GitSplitter.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateGitContext()
        {
            try
            {
                if (!string.IsNullOrEmpty(currentFilePath))
                {
                    GitControlPanel.SetFileContext(currentFilePath);
                }
                else
                {
                    var currentDir = System.IO.Directory.GetCurrentDirectory();
                    GitControlPanel.SetWorkingDirectory(currentDir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Git context update error: {ex.Message}");
            }
        }

        private void GitSplitter_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            isDraggingGitSplitter = true;
            lastGitPointerPosition = e.GetCurrentPoint(GitSplitter).Position;
            GitSplitter.CapturePointer(e.Pointer);
        }

        private void GitSplitter_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (isDraggingGitSplitter)
            {
                var currentPosition = e.GetCurrentPoint(GitSplitter).Position;
                var deltaX = currentPosition.X - lastGitPointerPosition.X;

                var newWidth = gitPanelWidth + deltaX;
                var windowWidth = ((FrameworkElement)this.Content).ActualWidth;

                if (newWidth >= 200 && newWidth <= windowWidth / 2)
                {
                    gitPanelWidth = newWidth;
                    GitColumn.Width = new GridLength(gitPanelWidth);
                }

                lastGitPointerPosition = currentPosition;
            }
        }

        private void GitSplitter_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            isDraggingGitSplitter = false;
            GitSplitter.ReleasePointerCapture(e.Pointer);
        }

    }
}