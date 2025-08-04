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
    public sealed partial class MainWindow : Window
    {
        private string currentFilePath = "";
        private string currentLanguage = "c";
        private bool isApplyingSyntaxHighlighting = false;
        private DispatcherTimer syntaxHighlightingTimer;
        private string lastHighlightedText = "";
        private readonly int maxHighlightLength = 1000000;

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

        private SettingsWindow settingsWindow;

        private bool isTerminalVisible = false;
        private bool isDraggingSplitter = false;
        private double terminalHeight = 300;
        private Point lastPointerPosition;

        private bool isGitPanelVisible = false;
        private bool isDraggingGitSplitter = false;
        private double gitPanelWidth = 380;
        private Point lastGitPointerPosition;

        private List<TabInfo> openTabs = new List<TabInfo>();
        private string activeTabId = null;

        public TextBlock GitBarTextPublic => GitBarText;

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

            CursorSize cursorSize = CodeEditor.CursorSize;
            CodeEditor.Focus(FocusState.Keyboard);

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

            CodeEditor.TextChanged += (e) =>
            {
                try
                {
                    var currentTab = openTabs.FirstOrDefault(t => t.TabId == activeTabId);
                    currentTab.Saved = false;
                    if (!currentTab.TabText.EndsWith("*"))
                    {
                        UpdateTabButtonText(currentTab.TabId, currentTab.TabText += "*");
                        currentTab.TabText += "*";
                    }
                }
                catch { }

                if (activeTabId != null)
                {
                    var currentTab = openTabs.FirstOrDefault(t => t.TabId == activeTabId);
                    if (currentTab != null)
                    {
                        currentTab.Text = CodeEditor.Text;
                        SaveCurrentTabPosition();
                    }
                }
                UpdateCursorPosition();
            };

            CodeEditor.GotFocus += (e) =>
            {
                CodeEditor.CursorSize = cursorSize;

            };

            CodeEditor.LosingFocus += (s,e) =>
            {
                CodeEditor.CursorSize = new CursorSize(0, 0);
            };

            CodeEditor.PointerPressed += (s, e) =>
            {
                CodeEditor.Focus(FocusState.Keyboard);
            };

            CreateFindReplacePanel();
            ApplySettings();
            CodeEditor.UseSpacesInsteadTabs = true;

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

            SelectLanguage(currentLanguage);
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
                        SelectCurrentLine();
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
                case Windows.System.VirtualKey.Enter:
                    HandleEnterKey(e);
                    break;
            }
        }

        private void SelectCurrentLine()
        {
            try
            {
                var cursorPos = CodeEditor.CursorPosition;
                var currentLine = cursorPos.LineNumber;
                CodeEditor.SelectLine(currentLine);
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Error selecting line: {ex.Message}";
            }
        }

        private void HandleEnterKey(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (!autoIndentationEnabled) return;

            try
            {
                var cursorPos = CodeEditor.CursorPosition;
                var currentLine = cursorPos.LineNumber;
                var currentChar = cursorPos.CharacterPosition;
                string currentLineText = CodeEditor.GetLineText(currentLine);

                var currentIndentation = GetCurrentLineIndentation(currentLineText);

                if (currentChar > 0 && currentChar == currentLineText.Length - 1 &&
                    currentLineText[currentChar - 1] == '{' && currentLineText[currentChar] == '}')
                {
                    e.Handled = true;
                    string text = currentLineText.Substring(0, currentChar);
                    CodeEditor.SetLineText(currentLine, text);
                    CodeEditor.AddLine(currentLine + 1, currentIndentation + GetIndentString());
                    CodeEditor.AddLine(currentLine + 2, currentIndentation);
                    CodeEditor.SetLineText(currentLine + 2, currentIndentation + "}");

                    CodeEditor.SetCursorPosition(currentLine + 1, (currentIndentation + GetIndentString()).Length);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-indentation error: {ex.Message}");
            }
        }

        private string GetCurrentLineIndentation(string lineText)
        {
            var indentation = "";
            for (var i = 0; i < lineText.Length && (lineText[i] == ' ' || lineText[i] == '\t'); i++)
            {
                indentation += lineText[i];
            }
            return indentation;
        }

        private bool ShouldIndentNextLine(string lineText)
        {
            var trimmedLine = lineText.TrimEnd();

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
            return new string(' ', CodeEditor.NumberOfSpacesForTab);
        }

        private void UpdateCursorPosition()
        {
            try
            {
                var cursorPos = CodeEditor.CursorPosition;
                var currentLine = cursorPos.LineNumber;
                var currentChar = cursorPos.CharacterPosition;

                var currentLineText = CodeEditor.GetLineText(currentLine);
                var displayChar = '\0';
                if (currentChar > 0 && currentChar <= currentLineText.Length)
                {
                    displayChar = currentLineText[Math.Min(currentChar - 1, currentLineText.Length - 1)];
                }

                var fileName = string.IsNullOrEmpty(currentFilePath) ? "Untitled" : System.IO.Path.GetFileName(currentFilePath);

                if (displayChar > 27 && displayChar < 128)
                {
                    StatusBarText.Text = $"Ln {currentLine + 1}, Col {currentChar + 1}, Key: {displayChar}";
                    GitBarText.Text = GitBarText.Text;
                }
                else
                {
                    StatusBarText.Text = $"Ln {currentLine + 1}, Col {currentChar + 1}";
                    FileNameBarText.Text = GitBarText.Text;
                }
            }
            catch (Exception ex)
            {
                var fileName = string.IsNullOrEmpty(currentFilePath) ? "Untitled" : System.IO.Path.GetFileName(currentFilePath);
                StatusBarText.Text = $"Ln 1, Col 1";
                FileNameBarText.Text = GitBarText.Text;
                System.Diagnostics.Debug.WriteLine($"UpdateCursorPosition error: {ex.Message}");
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

                CodeEditor.NumberOfSpacesForTab = (short)(localSettings.Values["TabSize"] ?? 4);
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
                CodeEditor.NumberOfSpacesForTab = 4;
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

                localSettings.Values["TabSize"] = CodeEditor.NumberOfSpacesForTab;
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

        private void SaveLastSession()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                if (activeTabId != null)
                {
                    SaveCurrentTabPosition();

                    var currentTab = openTabs.FirstOrDefault(t => t.TabId == activeTabId);
                    if (currentTab != null)
                    {
                        if (string.IsNullOrEmpty(currentTab.FilePath))
                        {
                            localSettings.Values[$"TabContent_{currentTab.TabId}"] = CodeEditor.Text;
                        }
                    }
                }

                var serializedTabs = new List<string>();
                foreach (var tab in openTabs)
                {
                    serializedTabs.Add(SerializeTabInfo(tab));
                }

                localSettings.Values["OpenTabsCount"] = openTabs.Count;
                for (int i = 0; i < openTabs.Count; i++)
                {
                    localSettings.Values[$"Tab_{i}"] = serializedTabs[i];
                }

                localSettings.Values["ActiveTabId"] = activeTabId;

                localSettings.Values["LastContent"] = CodeEditor.Text;
                localSettings.Values["LastFilePath"] = currentFilePath;
                localSettings.Values["LastLanguage"] = currentLanguage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session: {ex.Message}");
            }
        }

        private void LoadLastSession()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                var lastLanguage = localSettings.Values["LastLanguage"] as string;
                if (!string.IsNullOrEmpty(lastLanguage))
                {
                    currentLanguage = lastLanguage;
                }

                var tabCount = (int)(localSettings.Values["OpenTabsCount"] ?? 0);
                var savedActiveTabId = localSettings.Values["ActiveTabId"] as string;

                if (tabCount > 0)
                {
                    openTabs.Clear();
                    TabContainer.Children.Clear();
                    activeTabId = null;

                    for (int i = 0; i < tabCount; i++)
                    {
                        var serializedTab = localSettings.Values[$"Tab_{i}"] as string;
                        if (!string.IsNullOrEmpty(serializedTab))
                        {
                            var tabInfo = DeserializeTabInfo(serializedTab);
                            if (tabInfo != null)
                            {
                                CreateTab(tabInfo.TabText, tabInfo.FilePath, tabInfo.TabId);

                                var restoredTab = openTabs.FirstOrDefault(t => t.TabId == tabInfo.TabId);
                                if (restoredTab != null)
                                {
                                    restoredTab.CursorLine = tabInfo.CursorLine;

                                    if (!string.IsNullOrEmpty(tabInfo.FilePath))
                                    {
                                        try
                                        {
                                            restoredTab.Text = File.ReadAllText(tabInfo.FilePath);
                                            CodeEditor.Text = restoredTab.Text;
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Error loading file {tabInfo.FilePath}: {ex.Message}");
                                            restoredTab.Text = "";
                                        }
                                    }
                                    else
                                    {
                                        var savedContent = localSettings.Values[$"TabContent_{tabInfo.TabId}"] as string;
                                        restoredTab.Text = savedContent ?? "";
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(savedActiveTabId) && openTabs.Any(t => t.TabId == savedActiveTabId))
                    {
                        SwitchToTab(savedActiveTabId);
                    }
                    else if (openTabs.Count > 0)
                    {
                        SwitchToTab(openTabs[0].TabId);
                    }
                }
                else
                {
                    var lastContent = localSettings.Values["LastContent"] as string;
                    var lastFilePath = localSettings.Values["LastFilePath"] as string;

                    if (!string.IsNullOrEmpty(lastContent))
                    {
                        CodeEditor.SetText(lastContent);
                    }

                    if (!string.IsNullOrEmpty(lastFilePath))
                    {
                        currentFilePath = lastFilePath;
                    }
                }

                StatusBarText.Text = "Session restored";
                hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading session: {ex.Message}");
                currentLanguage = "c";
                if (openTabs.Count == 0)
                {
                    CreateTab("Untitled", "");
                }
            }
        }

        private void ApplySettings()
        {
            if (lineNumbersEnabled == true) CodeEditor.ShowLineNumbers = true;
            else CodeEditor.ShowLineNumbers = false;

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
            CodeEditor.NumberOfSpacesForTab = newTabSize;
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
            return ((short)CodeEditor.NumberOfSpacesForTab, autoIndentationEnabled, autoCompletionEnabled, autoBraceClosingEnabled, lineNumbersEnabled, wordWrapEnabled, autoSaveEnabled, restoreSessionEnabled, autoSaveInterval);
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

        private void LFButton_Click(object sender, RoutedEventArgs e)
        {
            if (LFButton.Content == "LF")
            {
                LFButton.Content = "CRLF";
                CodeEditor.LineEnding = LineEnding.CRLF;
            }
            else if (LFButton.Content == "CRLF")
            {
                LFButton.Content = "CR";
                CodeEditor.LineEnding = LineEnding.CR;
            }
            else
            {
                LFButton.Content = "LF";
                CodeEditor.LineEnding = LineEnding.LF;
            }
        }
    }
}