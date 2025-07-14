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
    public sealed partial class MainWindow : Window
    {
        private string currentFilePath = "";
        private string currentLanguage = "c";
        private bool isApplyingSyntaxHighlighting = false;
        private DispatcherTimer syntaxHighlightingTimer;
        private string lastHighlightedText = "";
        private readonly int MAX_HIGHLIGHT_LENGTH = 1000000;
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
    }
}