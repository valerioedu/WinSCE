using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
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
    }
}
