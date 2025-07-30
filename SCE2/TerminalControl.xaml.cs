using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using System.Text;

// TODO: Handle arrows movement and better cursor positioning with spaces

namespace SCE2
{
    public sealed partial class TerminalControl : UserControl
    {
        private Process shellProcess;
        private StreamWriter shellInput;
        private string currentShell = "powershell";
        private List<string> commandHistory = new List<string>();
        private int historyIndex = -1;
        private string currentDirectory;
        private string currentPrompt = "";
        private bool isProcessing = false;
        private bool waitingForPrompt = true;
        private StringBuilder outputBuffer = new StringBuilder();

        private readonly SolidColorBrush NormalTextBrush = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204));
        private readonly SolidColorBrush ErrorTextBrush = new SolidColorBrush(Color.FromArgb(255, 255, 85, 85));
        private readonly SolidColorBrush SuccessTextBrush = new SolidColorBrush(Color.FromArgb(255, 85, 255, 85));
        private readonly SolidColorBrush InfoTextBrush = new SolidColorBrush(Color.FromArgb(255, 85, 170, 255));
        private readonly SolidColorBrush PromptTextBrush = new SolidColorBrush(Color.FromArgb(255, 85, 170, 255));
        private readonly SolidColorBrush PathTextBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0));

        public TerminalControl()
        {
            this.InitializeComponent();

            this.Loaded += TerminalControl_Loaded;

            this.Unloaded += (s, e) =>
            {
                if (shellProcess != null && !shellProcess.HasExited)
                {
                    try
                    {
                        shellProcess.Kill();
                        shellProcess.Dispose();
                    }
                    catch { }
                }
            };
        }

        private void TerminalControl_Loaded(object sender, RoutedEventArgs e)
        {
            currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            StartShellProcess();

            this.DispatcherQueue.TryEnqueue(() =>
            {
                InputTextBox.Focus(FocusState.Programmatic);
            });
        }

        private void StartShellProcess()
        {
            try
            {
                if (shellProcess != null && !shellProcess.HasExited)
                {
                    shellProcess.Kill();
                    shellProcess.Dispose();
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = GetShellPath(),
                    WorkingDirectory = currentDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                switch (currentShell)
                {
                    case "powershell":
                        startInfo.Arguments = "-NoLogo -NoExit -NoProfile";
                        break;
                    case "cmd":
                        startInfo.Arguments = "/Q";
                        break;
                }

                shellProcess = new Process { StartInfo = startInfo };
                shellProcess.OutputDataReceived += ShellProcess_OutputDataReceived;
                shellProcess.ErrorDataReceived += ShellProcess_ErrorDataReceived;

                shellProcess.Start();
                shellProcess.BeginOutputReadLine();
                shellProcess.BeginErrorReadLine();

                shellInput = shellProcess.StandardInput;
                shellInput.AutoFlush = true;

                waitingForPrompt = true;
                outputBuffer.Clear();

                Task.Run(async () =>
                {
                    await Task.Delay(500);

                    if (currentShell == "cmd")
                    {
                        await shellInput.WriteLineAsync("@echo off");
                        await Task.Delay(200);
                        await shellInput.WriteLineAsync("prompt");
                        await Task.Delay(200);
                    }
                    else if (currentShell == "powershell")
                    {
                        await shellInput.WriteLineAsync("function prompt { return 'PS ' + (Get-Location) + '> ' }");
                        await Task.Delay(500);
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        waitingForPrompt = false;
                        UpdatePromptDisplay();
                    });
                });
            }
            catch (Exception ex)
            {
                AppendOutput($"Failed to start shell: {ex.Message}", ErrorTextBrush);
            }
        }

        private string GetShellPath()
        {
            switch (currentShell)
            {
                case "powershell":
                    return "powershell.exe";
                default:
                    return "cmd.exe";
            }
        }

        private void UpdatePromptDisplay()
        {
            if (waitingForPrompt) return;

            string promptDisplay = "";

            try
            {
                switch (currentShell)
                {
                    case "powershell":
                        promptDisplay = $"PS {currentDirectory}> ";
                        PromptText.Foreground = PromptTextBrush;
                        break;
                    case "cmd":
                    default:
                        promptDisplay = $"{currentDirectory}> ";
                        PromptText.Foreground = PathTextBrush;
                        break;
                }
            }
            catch
            {
                promptDisplay = "> ";
                PromptText.Foreground = NormalTextBrush;
            }

            currentPrompt = promptDisplay;
            PromptText.Text = promptDisplay;
        }

        private void ShellProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    string data = CleanAnsiSequences(e.Data);

                    if (string.IsNullOrWhiteSpace(data) ||
                        data.Trim().EndsWith(">") ||
                        data.StartsWith("PS ") ||
                        data.Contains("Microsoft Windows") ||
                        data.Contains("Copyright (c)"))
                    {
                        return;
                    }

                    AppendOutput(data, NormalTextBrush);
                    isProcessing = false;

                    UpdateCurrentDirectoryFromOutput(data);
                });
            }
        }

        private string CleanAnsiSequences(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var ansiPattern = @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])";
            var result = System.Text.RegularExpressions.Regex.Replace(input, ansiPattern, "");

            result = result.Replace("\x1B", "").Replace("\x07", "");

            return result;
        }

        private void UpdateCurrentDirectoryFromOutput(string output)
        {
            try
            {
                if (output.Contains(":\\") && Directory.Exists(output.Trim()))
                {
                    currentDirectory = output.Trim();
                    UpdatePromptDisplay();
                }
            }
            catch { }
        }

        private void ShellProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && !string.IsNullOrWhiteSpace(e.Data))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    string cleanData = CleanAnsiSequences(e.Data);
                    if (!string.IsNullOrWhiteSpace(cleanData))
                    {
                        AppendOutput(cleanData, ErrorTextBrush);
                    }
                    isProcessing = false;
                });
            }
        }

        private void AppendOutput(string text, SolidColorBrush brush)
        {
            if (OutputParagraph == null || string.IsNullOrEmpty(text)) return;

            var run = new Run
            {
                Text = text + "\n",
                Foreground = brush
            };

            OutputParagraph.Inlines.Add(run);

            DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(10);
                OutputScrollViewer.ChangeView(null, OutputScrollViewer.ScrollableHeight, null);
                InputTextBox.Focus(FocusState.Programmatic);
            });
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            InputDisplayText.Text = InputTextBox.Text;
        }

        public async void ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            isProcessing = true;

            commandHistory.Add(command);
            historyIndex = commandHistory.Count;

            var commandRun = new Run
            {
                Text = currentPrompt + command + "\n",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
            };
            OutputParagraph.Inlines.Add(commandRun);

            InputDisplayText.Text = "";

            if (command.Trim().ToLower() == "clear" || command.Trim().ToLower() == "cls")
            {
                if (OutputParagraph != null)
                {
                    OutputParagraph.Inlines.Clear();
                }
                isProcessing = false;
                return;
            }

            if (command.Trim().ToLower().StartsWith("cd "))
            {
                var newDir = command.Substring(3).Trim();
                if (newDir == "..")
                {
                    var parent = Directory.GetParent(currentDirectory);
                    if (parent != null)
                    {
                        currentDirectory = parent.FullName;
                    }
                }
                else if (Path.IsPathRooted(newDir) && Directory.Exists(newDir))
                {
                    currentDirectory = newDir;
                }
                else
                {
                    var fullPath = Path.Combine(currentDirectory, newDir);
                    if (Directory.Exists(fullPath))
                    {
                        currentDirectory = fullPath;
                    }
                }
                UpdatePromptDisplay();
            }

            try
            {
                if (shellInput != null && !shellProcess.HasExited)
                {
                    await shellInput.WriteLineAsync(command);

                    if (command.Trim().ToLower().StartsWith("cd "))
                    {
                        await Task.Delay(200);
                        try
                        {
                            await shellInput.WriteLineAsync("cd");
                            await Task.Delay(100);
                        }
                        catch { }
                    }
                }
                else
                {
                    AppendOutput("Shell process is not running", ErrorTextBrush);
                    isProcessing = false;
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"Error executing command: {ex.Message}", ErrorTextBrush);
                isProcessing = false;
            }

            await Task.Delay(100);
            isProcessing = false;
        }

        private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Enter:
                    if (!isProcessing)
                    {
                        e.Handled = true;
                        var command = InputTextBox.Text;
                        InputTextBox.Text = "";
                        ExecuteCommand(command);
                    }
                    break;
            }
        }

        private void InputTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Up:
                    e.Handled = true;
                    if (historyIndex > 0)
                    {
                        historyIndex--;
                        InputTextBox.Text = commandHistory[historyIndex];
                        InputTextBox.SelectionStart = InputTextBox.Text.Length;
                    }
                    break;

                case VirtualKey.Down:
                    e.Handled = true;
                    if (historyIndex < commandHistory.Count - 1)
                    {
                        historyIndex++;
                        InputTextBox.Text = commandHistory[historyIndex];
                        InputTextBox.SelectionStart = InputTextBox.Text.Length;
                    }
                    else if (historyIndex == commandHistory.Count - 1)
                    {
                        historyIndex = commandHistory.Count;
                        InputTextBox.Text = "";
                    }
                    break;

                case VirtualKey.Tab: e.Handled = true; break;
                case VirtualKey.Space:
                    break;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (OutputParagraph != null)
            {
                OutputParagraph.Inlines.Clear();
            }
            InputTextBox.Focus(FocusState.Programmatic);
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Text Files", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = $"terminal_output_{DateTime.Now:yyyyMMdd_HHmmss}";

            var xamlRoot = this.XamlRoot;
            if (xamlRoot != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(xamlRoot.Content);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
            }

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null && OutputParagraph != null)
            {
                var outputText = new StringBuilder();
                foreach (var inline in OutputParagraph.Inlines)
                {
                    if (inline is Run run)
                    {
                        outputText.Append(run.Text);
                    }
                }

                await FileIO.WriteTextAsync(file, outputText.ToString());
                AppendOutput($"Output exported to: {file.Path}", SuccessTextBrush);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (OutputParagraph != null)
            {
                OutputParagraph.Inlines.Clear();
            }
            commandHistory.Clear();
            historyIndex = -1;
            StartShellProcess();
        }

        private void ShellSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShellSelector.SelectedItem is ComboBoxItem item)
            {
                currentShell = item.Tag as string;
                AppendOutput($"Switching to {item.Content}...", InfoTextBrush);
                StartShellProcess();
            }
        }

        public void FocusInput()
        {
            InputTextBox.Focus(FocusState.Programmatic);
        }
    }
}