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

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
        private string currentFilePath = "";
        private string currentLanguage = "c";

        public MainWindow()
        {
            this.InitializeComponent();

            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(
                Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                    WinRT.Interop.WindowNative.GetWindowHandle(this)
                )
            );
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0); // Transparent buttons

            this.SetTitleBar(CustomTitleBar);

            CodeEditor.SelectionChanged += (s, e) =>
            {
                UpdateCursorPosition();
            };

            CodeEditor.TextChanged += (s, e) =>
            {
                UpdateLineNumbers();
                UpdateCursorPosition();
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
            for (int i = 1; i < text.Length; i++)
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
            StatusBarText.Text = "New file created";
        }

        private void SetLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string language)
            {
                currentLanguage = language;
                StatusBarText.Text = $"Language set to {GetLanguageDisplayName(language)}";
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
    }
}
