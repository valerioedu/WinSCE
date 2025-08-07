using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextControlBoxNS;
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

                var files = await picker.PickMultipleFilesAsync();

                if (files != null)
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            string text = await FileIO.ReadTextAsync(file);
                            var filePath = file.Path;
                            var fileName = file.Name;

                            var existingTab = openTabs.FirstOrDefault(t => t.FilePath == filePath);
                            if (existingTab != null)
                            {
                                SwitchToTab(existingTab.TabId, true);
                                return;
                            }

                            if (activeTabId != null)
                            {
                                SaveCurrentTabPosition();
                            }

                            CreateTab(fileName, filePath);

                            CodeEditor.LoadText(text);
                            currentFilePath = filePath;

                            DetectLanguageFromFile(fileName);

                            StatusBarText.Text = $"Opened: {fileName}";

                            var dir = System.IO.Path.GetDirectoryName(currentFilePath);

                            try
                            {
                                TerminalPanel.ExecuteCommand($"cd {dir}");
                            }
                            catch
                            {
                                StatusBarText.Text = "Couldn't access the file directory";
                            }

                            UpdateGitContext();
                            UpdateCursorPosition();

                            hasUnsavedChanges = false;
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
                if (string.IsNullOrEmpty(currentFilePath)) await SaveAsFile();
                else await SaveCurrentFile();

                var currentTab = openTabs.FirstOrDefault(t => t.TabId == activeTabId);
                if (currentTab != null)
                {
                    currentTab.Saved = true;
                    while (currentTab.TabText.EndsWith("*"))
                    {
                        currentTab.TabText = currentTab.TabText.TrimEnd('*');
                        UpdateTabButtonText(currentTab.TabId, currentTab.TabText);
                    }
                }

                UpdateGitContext();
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
                    string text = CodeEditor.Text;
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
                    string text = CodeEditor.Text;
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
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                CodeEditor.Text = GetTemplateForLanguage(currentLanguage);
                currentFilePath = "";

                StatusBarText.Text = "New file created";
            }
            else
            {
                CreateTab("Untitled");
                CodeEditor.Text = GetTemplateForLanguage(currentLanguage);
                currentFilePath = "";

                StatusBarText.Text = "New file created";
            }
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
            SelectLanguage(currentLanguage);
        }

        private void SetLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string language)
            {
                currentLanguage = language;
                StatusBarText.Text = $"Language set to {GetLanguageDisplayName(language)}";
                SelectLanguage(language);
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
        private void NewSession_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tab in openTabs.ToList())
            {
                DestroyTab(tab.TabId);
            }

            CleanupTabContent();
            CreateTab("Untitled");
        }

        private async void Quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void NewWindow_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new MainWindow();
            newWindow.Activate();
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();

            currentFolderPath = folder.Path;


            if (folder != null)
            {
                if (!isExplorerPanelVisible)
                {
                    ToggleExplorerPanel();
                }

                FolderExplorerPanel.SetFolderPath(folder.Path);

                try
                {
                    TerminalPanel.ExecuteCommand($"cd \"{folder.Path}\"");
                }
                catch { }

                StatusBarText.Text = $"Opened folder: {folder.Name}";
            }
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
    }
}