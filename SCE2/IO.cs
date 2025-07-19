using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
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
            try
            {
                var picker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };

                picker.FileTypeFilter.Add("*");

                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

                var file = await picker.PickSingleFileAsync();

                if (file != null)
                {
                    try
                    {
                        string text = await FileIO.ReadTextAsync(file);

                        CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, text);
                        currentFilePath = file.Path;

                        DetectLanguageFromFile(file.Name);
                        ApplySyntaxHighlightingImmediate();

                        StatusBarText.Text = $"Opened: {file.Name}";
                    }
                    catch (UnauthorizedAccessException)
                    {
                        StatusBarText.Text = "Access to the file was denied.";
                    }
                    catch (IOException ioEx)
                    {
                        StatusBarText.Text = $"File I/O error: {ioEx.Message}";
                    }
                    catch (Exception ex)
                    {
                        StatusBarText.Text = $"Unexpected error reading file: {ex.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Error opening file: {ex.Message}";
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(currentFilePath))
                    await SaveAsFile();
                else
                    await SaveCurrentFile();
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Error saving file: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task SaveAsFile()
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            picker.FileTypeChoices.Add("C file", new[] { ".c" });
            picker.FileTypeChoices.Add("C++ file", new[] { ".cpp", ".cxx", ".cc" });
            picker.FileTypeChoices.Add("Header file", new[] { ".h", ".hpp", ".hxx" });
            picker.FileTypeChoices.Add("C# file", new[] { ".cs" });
            picker.FileTypeChoices.Add("Java file", new[] { ".java" });
            picker.FileTypeChoices.Add("JavaScript file", new[] { ".js", ".mjs", ".jsx" });
            picker.FileTypeChoices.Add("Python file", new[] { ".py", ".pyw" });
            picker.FileTypeChoices.Add("Rust file", new[] { ".rs" });
            picker.FileTypeChoices.Add("Go file", new[] { ".go" });
            picker.FileTypeChoices.Add("HTML file", new[] { ".html", ".htm" });
            picker.FileTypeChoices.Add("CSS file", new[] { ".css" });
            picker.FileTypeChoices.Add("XML file", new[] { ".xml", ".xsd", ".xsl", ".xslt" });
            picker.FileTypeChoices.Add("JSON file", new[] { ".json", ".jsonc" });
            picker.FileTypeChoices.Add("Text file", new[] { ".txt" });
            picker.FileTypeChoices.Add("Markdown file", new[] { ".md", ".markdown" });
            picker.FileTypeChoices.Add("Unknown", new List<string>() { "." });

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    string text;
                    CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
                    await FileIO.WriteTextAsync(file, text);
                    currentFilePath = file.Path;

                    DetectLanguageFromFile(file.Name);

                    StatusBarText.Text = $"Saved: {file.Name}";
                }
                catch (Exception ex)
                {
                    StatusBarText.Text = $"Error saving file: {ex.Message}";
                }
            }
        }

        private async System.Threading.Tasks.Task SaveCurrentFile()
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(currentFilePath);
                    string text;
                    CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out text);
                    await FileIO.WriteTextAsync(file, text);
                    StatusBarText.Text = "File saved";
                }
                catch (Exception ex)
                {
                    StatusBarText.Text = $"Error saving file: {ex.Message}";
                }
            }
        }

        private string GetTemplateForLanguage(string language)
        {
            return language switch
            {
                "c" => "#include <stdio.h>\n\nint main() {\n    printf(\"Hello, World!\\n\");\n    return 0;\n}",
                "cpp" => "#include <iostream>\n\nint main() {\n    std::cout << \"Hello, World!\" << std::endl;\n    return 0;\n}",
                "csharp" => "using System;\n\nclass Program\n{\n    static void Main()\n    {\n        Console.WriteLine(\"Hello, World!\");\n    }\n}",
                "java" => "public class Main {\n    public static void main(String[] args) {\n        System.out.println(\"Hello, World!\");\n    }\n}",
                "javascript" => "console.log('Hello, World!');",
                "python" => "print('Hello, World!')",
                "rust" => "fn main() {\n    println!(\"Hello, World!\");\n}",
                "go" => "package main\n\nimport \"fmt\"\n\nfunc main() {\n    fmt.Println(\"Hello, World!\")\n}",
                "html" => "<!DOCTYPE html>\n<html>\n<head>\n    <title>Document</title>\n</head>\n<body>\n    <h1>Hello, World!</h1>\n</body>\n</html>",
                "css" => "/* CSS Styles */\nbody {\n    font-family: Arial, sans-serif;\n    margin: 0;\n    padding: 20px;\n}",
                "xml" => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<root>\n    <message>Hello, World!</message>\n</root>",
                "json" => "{\n    \"message\": \"Hello, World!\",\n    \"language\": \"json\"\n}",
                "markdown" => "# Hello, World!\n\nThis is a markdown document.",
                _ => "// New file\n"
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
                ".java" => "java",
                ".js" or ".mjs" or ".jsx" => "javascript",
                ".py" or ".pyw" => "python",
                ".rs" => "rust",
                ".go" => "go",
                ".html" or ".htm" => "html",
                ".css" => "css",
                ".xml" or ".xsd" or ".xsl" or ".xslt" => "xml",
                ".json" or ".jsonc" => "json",
                ".md" or ".markdown" => "markdown",
                ".txt" => "text",
                _ => "generic"
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
                "java" => "Java",
                "javascript" => "JavaScript",
                "python" => "Python",
                "rust" => "Rust",
                "go" => "Go",
                "html" => "HTML",
                "css" => "CSS",
                "xml" => "XML",
                "json" => "JSON",
                "markdown" => "Markdown",
                "text" => "Text",
                "generic" => "Generic",
                _ => "Unknown"
            };
        }
    }
}