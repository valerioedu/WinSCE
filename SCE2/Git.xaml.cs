using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SCE2
{
    public sealed partial class GitControl : UserControl
    {
        public ObservableCollection<GitFileChange> Changes { get; set; }
        public ObservableCollection<GitBranch> Branches { get; set; }
        public ObservableCollection<GitCommit> Commits { get; set; }

        private string currentDirectory = "";
        private MainWindow mainWindow;

        public GitControl()
        {
            this.InitializeComponent();

            Changes = new ObservableCollection<GitFileChange>();
            Branches = new ObservableCollection<GitBranch>();
            Commits = new ObservableCollection<GitCommit>();

            ChangesListView.ItemsSource = Changes;
            BranchesListView.ItemsSource = Branches;
            CommitsListView.ItemsSource = Commits;
        }

        public void SetWorkingDirectory(string path)
        {
            if (File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            var gitRoot = FindGitRepository(path);
            if (!string.IsNullOrEmpty(gitRoot))
            {
                currentDirectory = gitRoot;
                RefreshRepositoryStatus();
            }
            else
            {
                currentDirectory = path;
                RefreshRepositoryStatus();
            }
        }

        private string FindGitRepository(string startPath)
        {
            if (string.IsNullOrEmpty(startPath))
                return null;

            var currentPath = Path.GetFullPath(startPath);

            while (!string.IsNullOrEmpty(currentPath))
            {
                var gitDir = Path.Combine(currentPath, ".git");

                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    return currentPath;
                }

                var parentPath = Path.GetDirectoryName(currentPath);

                if (parentPath == null || parentPath == currentPath)
                    break;

                currentPath = parentPath;
            }

            return null;
        }

        public bool IsInGitRepository(string filePath = null)
        {
            var pathToCheck = filePath ?? currentDirectory;
            return !string.IsNullOrEmpty(FindGitRepository(pathToCheck));
        }

        public void SetFileContext(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            SetWorkingDirectory(filePath);
        }

        private async void RefreshRepositoryStatus()
        {
            if (string.IsNullOrEmpty(currentDirectory))
            {
                RepositoryPath.Text = "No repository detected";
                CurrentBranch.Text = "Branch: N/A";
                RepositoryStatus.Text = "No Git repository";
                Changes.Clear();
                Branches.Clear();
                Commits.Clear();
                return;
            }

            try
            {
                var gitRoot = FindGitRepository(currentDirectory);
                if (!string.IsNullOrEmpty(gitRoot))
                {
                    if (currentDirectory != gitRoot)
                    {
                        currentDirectory = gitRoot;
                    }

                    var repoName = Path.GetFileName(gitRoot);
                    await RefreshGitInfo();

                    var branchOutput = await ExecuteGitCommand("branch --show-current");
                    var branchName = !string.IsNullOrEmpty(branchOutput) ? branchOutput.Trim() : "HEAD";

                    var userOutput = await ExecuteGitCommand("config user.name");
                    var userName = !string.IsNullOrEmpty(userOutput) ? userOutput.Trim() : "unknown";

                    if (mainWindow != null && mainWindow.GitBarTextPublic != null)
                    {
                        mainWindow.GitBarTextPublic.Text = $"{repoName}:{branchName}@{userName}";
                    }

                    RepositoryPath.Text = $"Repository: {repoName}";
                    await RefreshChanges();
                    await RefreshBranches();
                    await RefreshCommits();
                }
                else
                {
                    RepositoryPath.Text = "No repository detected";
                    CurrentBranch.Text = "Branch: N/A";
                    RepositoryStatus.Text = "Not a Git repository";
                    Changes.Clear();
                    Branches.Clear();
                    Commits.Clear();
                }
            }
            catch (Exception ex)
            {
                RepositoryStatus.Text = $"Error: {ex.Message}";
            }
        }

        private async Task RefreshGitInfo()
        {
            await RefreshCurrentBranch();
            await RefreshWorkingTreeStatus();
        }

        private async Task RefreshCurrentBranch()
        {
            try
            {
                var branchOutput = await ExecuteGitCommand("branch --show-current");
                if (!string.IsNullOrEmpty(branchOutput))
                {
                    CurrentBranch.Text = $"Branch: {branchOutput.Trim()}";
                }
                else
                {
                    var headOutput = await ExecuteGitCommand("rev-parse --short HEAD");
                    CurrentBranch.Text = $"Branch: HEAD ({headOutput.Trim()})";
                }
            }
            catch
            {
                CurrentBranch.Text = "Branch: unknown";
            }
        }

        private async Task RefreshWorkingTreeStatus()
        {
            try
            {
                var statusOutput = await ExecuteGitCommand("status --porcelain");
                if (string.IsNullOrEmpty(statusOutput.Trim()))
                {
                    RepositoryStatus.Text = "Working tree clean";
                }
                else
                {
                    var lines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    RepositoryStatus.Text = $"{lines.Length} file(s) changed";
                }
            }
            catch
            {
                RepositoryStatus.Text = "Unable to get status";
            }
        }

        private async void InitButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentDirectory))
            {
                try
                {
                    var folderPicker = new FolderPicker();
                    folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
                    folderPicker.FileTypeFilter.Add("*");

                    var window = (App.Current as App)?._window;
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                    var folder = await folderPicker.PickSingleFolderAsync();
                    if (folder != null)
                    {
                        currentDirectory = folder.Path;
                    }
                }
                catch (Exception ex)
                {
                    RepositoryStatus.Text = $"Error selecting folder: {ex.Message}";
                    return;
                }
            }

            if (!string.IsNullOrEmpty(currentDirectory))
            {
                await ExecuteGitCommand("init");
                RefreshRepositoryStatus();
            }
        }

        private async void CloneButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog()
            {
                Title = "Clone Repository",
                PrimaryButtonText = "Clone",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var stackPanel = new StackPanel() { Spacing = 10 };
            var textBox = new TextBox()
            {
                PlaceholderText = "Enter repository URL (https://github.com/user/repo.git)",
                Width = 400
            };
            stackPanel.Children.Add(new TextBlock() { Text = "Repository URL:" });
            stackPanel.Children.Add(textBox);

            dialog.Content = stackPanel;

            try
            {
                var window = (App.Current as App)?._window;
                dialog.XamlRoot = window.Content.XamlRoot;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(textBox.Text))
                {
                    var url = textBox.Text.Trim();
                    var repoName = Path.GetFileNameWithoutExtension(url.Split('/').Last());

                    if (!string.IsNullOrEmpty(currentDirectory))
                    {
                        var clonePath = Path.Combine(currentDirectory, repoName);
                        await ExecuteGitCommand($"clone \"{url}\" \"{clonePath}\"");
                        currentDirectory = clonePath;
                    }
                    else
                    {
                        await ExecuteGitCommand($"clone \"{url}\"");
                    }

                    RefreshRepositoryStatus();
                }
            }
            catch (Exception ex)
            {
                RepositoryStatus.Text = $"Clone error: {ex.Message}";
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRepositoryStatus();
            await RefreshChanges();
            await RefreshBranches();
            await RefreshCommits();
        }

        private async void PullButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteGitCommand("pull");
            RefreshRepositoryStatus();
            await RefreshChanges();
            await RefreshCommits();
        }

        private async void PushButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteGitCommand("push");
            RefreshRepositoryStatus();
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteGitCommand("fetch");
            await RefreshBranches();
        }

        private async void StageAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteGitCommand("add .");
            await RefreshChanges();
        }

        private async void CommitButton_Click(object sender, RoutedEventArgs e)
        {
            var message = CommitMessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                RepositoryStatus.Text = "Please enter a commit message";
                return;
            }

            await ExecuteGitCommand($"commit -m \"{message.Replace("\"", "\\\"")}\"");
            CommitMessageTextBox.Text = "";
            await RefreshChanges();
            await RefreshCommits();
        }

        private async void CommitAndPushButton_Click(object sender, RoutedEventArgs e)
        {
            var message = CommitMessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                RepositoryStatus.Text = "Please enter a commit message";
                return;
            }

            await ExecuteGitCommand($"commit -m \"{message.Replace("\"", "\\\"")}\"");
            await ExecuteGitCommand("push");
            CommitMessageTextBox.Text = "";
            await RefreshChanges();
            await RefreshCommits();
        }

        private async void NewBranchButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog()
            {
                Title = "Create New Branch",
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var stackPanel = new StackPanel() { Spacing = 10 };
            var textBox = new TextBox()
            {
                PlaceholderText = "Enter branch name",
                Width = 300
            };
            var checkBox = new CheckBox()
            {
                Content = "Switch to new branch after creation",
                IsChecked = true
            };

            stackPanel.Children.Add(new TextBlock() { Text = "Branch name:" });
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(checkBox);

            dialog.Content = stackPanel;

            try
            {
                var window = (App.Current as App)?._window;
                dialog.XamlRoot = window.Content.XamlRoot;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(textBox.Text))
                {
                    var branchName = textBox.Text.Trim();
                    var switchToBranch = checkBox.IsChecked == true;

                    if (switchToBranch)
                    {
                        await ExecuteGitCommand($"checkout -b \"{branchName}\"");
                    }
                    else
                    {
                        await ExecuteGitCommand($"branch \"{branchName}\"");
                    }

                    await RefreshBranches();
                    await RefreshCurrentBranch();
                }
            }
            catch (Exception ex)
            {
                RepositoryStatus.Text = $"Branch creation error: {ex.Message}";
            }
        }

        private async Task<string> ExecuteGitCommand(string command)
        {
            try
            {
                if (string.IsNullOrEmpty(currentDirectory))
                    return "";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = command,
                        WorkingDirectory = currentDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    if (string.IsNullOrEmpty(output.Trim()))
                    {
                        RepositoryStatus.Text = "Command executed successfully";
                    }
                    return output;
                }
                else
                {
                    RepositoryStatus.Text = $"Git error: {error.Trim()}";
                    return "";
                }
            }
            catch (Exception ex)
            {
                RepositoryStatus.Text = $"Error executing git command: {ex.Message}";
                return "";
            }
        }

        private async Task RefreshChanges()
        {
            try
            {
                Changes.Clear();

                var statusOutput = await ExecuteGitCommand("status --porcelain");
                if (string.IsNullOrEmpty(statusOutput.Trim()))
                    return;

                var lines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (line.Length < 3) continue;

                    var indexStatus = line[0];
                    var workTreeStatus = line[1];
                    var fileName = line.Substring(3);

                    char primaryStatus;
                    SolidColorBrush statusColor;

                    if (indexStatus != ' ')
                    {
                        primaryStatus = indexStatus;
                        statusColor = GetStatusColor(indexStatus);
                    }
                    else
                    {
                        primaryStatus = workTreeStatus;
                        statusColor = GetStatusColor(workTreeStatus);
                    }

                    Changes.Add(new GitFileChange
                    {
                        Status = primaryStatus.ToString(),
                        FileName = fileName,
                        StatusColor = statusColor
                    });
                }
            }
            catch (Exception ex)
            {
                RepositoryStatus.Text = $"Error refreshing changes: {ex.Message}";
            }
        }

        private SolidColorBrush GetStatusColor(char status)
        {
            return status switch
            {
                'M' => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0)), // Orange - Modified
                'A' => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)),   // Green - Added
                'D' => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),   // Red - Deleted
                'R' => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 255)), // Cyan - Renamed
                'C' => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 255)), // Magenta - Copied
                'U' => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 0)), // Yellow - Unmerged
                '?' => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)), // Gray - Untracked
                _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))  // White - Default
            };
        }

        private async Task RefreshBranches()
        {
            try
            {
                Branches.Clear();

                var branchOutput = await ExecuteGitCommand("branch -a");
                if (string.IsNullOrEmpty(branchOutput.Trim()))
                    return;

                var lines = branchOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    var indicator = " ";
                    var name = trimmedLine;

                    if (trimmedLine.StartsWith("* "))
                    {
                        indicator = "*";
                        name = trimmedLine.Substring(2);
                    }
                    else if (trimmedLine.StartsWith("  "))
                    {
                        name = trimmedLine.Substring(2);
                    }

                    if (name.StartsWith("remotes/origin/") && !name.EndsWith("/HEAD"))
                    {
                        name = name.Substring("remotes/origin/".Length) + " (remote)";
                    }
                    else if (name.Contains("remotes/"))
                    {
                        continue;
                    }

                    Branches.Add(new GitBranch
                    {
                        Name = name,
                        Indicator = indicator
                    });
                }
            }
            catch (Exception ex)
            {
                RepositoryStatus.Text = $"Error refreshing branches: {ex.Message}";
            }
        }

        private async Task RefreshCommits()
        {
            try
            {
                Commits.Clear();

                var logOutput = await ExecuteGitCommand("log --oneline --format=\"%h|%s|%an|%ar\" -10");
                if (string.IsNullOrEmpty(logOutput.Trim()))
                    return;

                var lines = logOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 4)
                    {
                        Commits.Add(new GitCommit
                        {
                            Hash = parts[0],
                            Message = parts[1],
                            Author = parts[2],
                            Date = parts[3]
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                RepositoryStatus.Text = $"Error refreshing commits: {ex.Message}";
            }
        }

        public void SetMainWindow(MainWindow window)
        {
            mainWindow = window;
        }
    }

    public class GitFileChange
    {
        public string Status { get; set; }
        public string FileName { get; set; }
        public SolidColorBrush StatusColor { get; set; }
    }

    public class GitBranch
    {
        public string Name { get; set; }
        public string Indicator { get; set; }
    }

    public class GitCommit
    {
        public string Hash { get; set; }
        public string Message { get; set; }
        public string Author { get; set; }
        public string Date { get; set; }
    }
}