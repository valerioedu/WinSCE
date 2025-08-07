using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;

namespace SCE2
{
    public sealed partial class MainWindow : Window
    {
        private bool isExplorerPanelVisible = false;
        private bool isDraggingExplorerSplitter = false;
        private double explorerPanelWidth = 300;
        private Point lastExplorerPointerPosition;

        public static string currentFolderPath = string.Empty;

        private void ToggleExplorerPanel()
        {
            isExplorerPanelVisible = !isExplorerPanelVisible;

            if (isExplorerPanelVisible)
            {
                ExplorerColumn.Width = new GridLength(explorerPanelWidth);
                ExplorerPanel.Visibility = Visibility.Visible;
                ExplorerSplitter.Visibility = Visibility.Visible;

                FolderExplorerPanel.FileSelected += FolderExplorerPanel_FileSelected;
            }
            else
            {
                ExplorerColumn.Width = new GridLength(0);
                ExplorerPanel.Visibility = Visibility.Collapsed;
                ExplorerSplitter.Visibility = Visibility.Collapsed;

                FolderExplorerPanel.FileSelected -= FolderExplorerPanel_FileSelected;
            }
        }

        private void ExplorerSplitter_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            isDraggingExplorerSplitter = true;
            lastExplorerPointerPosition = e.GetCurrentPoint(ExplorerSplitter).Position;
            ExplorerSplitter.CapturePointer(e.Pointer);
        }

        private void ExplorerSplitter_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (isDraggingExplorerSplitter)
            {
                var currentPosition = e.GetCurrentPoint(ExplorerSplitter).Position;
                var deltaX = currentPosition.X - lastExplorerPointerPosition.X;

                var newWidth = explorerPanelWidth + deltaX;
                var windowWidth = ((FrameworkElement)this.Content).ActualWidth;

                if (newWidth >= 200 && newWidth <= windowWidth / 2)
                {
                    explorerPanelWidth = newWidth;
                    ExplorerColumn.Width = new GridLength(explorerPanelWidth);
                }

                lastExplorerPointerPosition = currentPosition;
            }
        }

        private void ExplorerSplitter_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            isDraggingExplorerSplitter = false;
            ExplorerSplitter.ReleasePointerCapture(e.Pointer);
        }

        private async void FolderExplorerPanel_FileSelected(object sender, FileSelectedEventArgs e)
        {
            try
            {
                if (File.Exists(e.FilePath))
                {
                    foreach (var tab in openTabs.ToList())
                    {
                        if (tab.Saved == true && tab.IsFolder == true)
                        {
                            DestroyTab(tab.TabId);
                        }
                    }

                    var filePath = e.FilePath;
                    var fileName = Path.GetFileName(filePath);

                    var file = new FileInfo(filePath);

                    var text = await File.ReadAllTextAsync(filePath);

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

                    CreateTab(fileName, filePath, true);

                    CodeEditor.LoadText(text);
                    currentFilePath = filePath;
                }
            }
            catch (Exception ex)
            {
                StatusBarText.Text = $"Error opening file: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error opening file {e.FilePath}: {ex.Message}");
            }
        }

        private void Explorer_Click(object sender, RoutedEventArgs e)
        {
            ToggleExplorerPanel();
            if (GitPanel.Visibility == Visibility.Visible) ToggleGitPanel();
        }
    }

    public sealed partial class FolderExplorerControl : UserControl
    {
        public string CurrentFolderPath { get; private set; }
        public event EventHandler<FileSelectedEventArgs> FileSelected;
        public event EventHandler<FileSelectedEventArgs> FileOpenRequested;

        private FileSystemItem _selectedItem;
        private readonly Dictionary<string, FileIconInfo> _fileIcons = new Dictionary<string, FileIconInfo>
        {
            { ".cs", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/csharp-original.png" } },
            { ".cpp", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/cplusplus-original.png" } },
            { ".c", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/c-original.png" } },
            { ".h", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/c-original.png" } },
            { ".hpp", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/cplusplus-original.png" } },
            { ".java", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/java-original.png" } },
            { ".js", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/javascript-original.png" } },
            { ".ts", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/typescript-original.png" } },
            { ".html", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/html5-original.png" } },
            { ".css", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/css3-original.png" } },
            { ".xml", new FileIconInfo { IsEmoji = true, Icon = "📰" } },
            { ".json", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/javascript-original.png" } },
            { ".txt", new FileIconInfo { IsEmoji = true, Icon = "📝" } },
            { ".md", new FileIconInfo { IsEmoji = true, Icon = "📝" } },
            { ".py", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/python-original.png" } },
            { ".go", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/go-original-wordmark.png" } },
            { ".rs", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/Rust.png" } },
            { ".yml", new FileIconInfo { IsEmoji = true, Icon = "⚙️" } },
            { ".yaml", new FileIconInfo { IsEmoji = true, Icon = "⚙️" } },
            { ".config", new FileIconInfo { IsEmoji = true, Icon = "⚙️" } },
            { ".sln", new FileIconInfo { IsEmoji = true, Icon = "🏗️" } },
            { ".csproj", new FileIconInfo { IsEmoji = true, Icon = "🏗️" } },
            { ".gitignore", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/git-original.png" } },
            { ".gitattributes", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/git-original.png" } },
            { ".git", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/git-original.png" } },
            { ".sh", new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/bash-original.png"} }
        };

        private FileSystemWatcher _watcher;

        public FolderExplorerControl()
        {
            this.InitializeComponent();
        }

        public async void SetFolderPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;

            CurrentFolderPath = folderPath;
            FolderPathText.Text = Path.GetFileName(folderPath);

            SetupFileWatcher(folderPath);

            await LoadFolderStructure();
        }

        private void SetupFileWatcher(string folderPath)
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }

            _watcher = new FileSystemWatcher(folderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            _watcher.Created += (s, e) => OnFileSystemChanged();
            _watcher.Deleted += (s, e) => OnFileSystemChanged();
            _watcher.Renamed += (s, e) => OnFileSystemChanged();
            _watcher.Changed += (s, e) => OnFileSystemChanged();

            _watcher.EnableRaisingEvents = true;
        }

        private void OnFileSystemChanged()
        {
            _ = DispatcherQueue.TryEnqueue(async () =>
            {
                await UpdateRootChildrenDiff();
            });
        }

        private async Task UpdateRootChildrenDiff()
        {
            if (FileTreeView.ItemsSource is ObservableCollection<FileSystemItem> items && items.Count > 0)
            {
                var root = items[0];
                var newChildren = await LoadDirectoryContents(root.FullPath);

                for (int i = root.Children.Count - 1; i >= 0; i--)
                {
                    var oldItem = root.Children[i];
                    if (!newChildren.Any(nc => nc.FullPath == oldItem.FullPath))
                    {
                        root.Children.RemoveAt(i);
                    }
                }

                foreach (var newItem in newChildren)
                {
                    if (!root.Children.Any(oc => oc.FullPath == newItem.FullPath))
                    {
                        root.Children.Add(newItem);
                    }
                }

                foreach (var child in root.Children)
                {
                    var match = newChildren.FirstOrDefault(nc => nc.FullPath == child.FullPath);
                    if (match != null && child.Name != match.Name)
                    {
                        child.Name = match.Name;
                    }
                }
            }
        }

        public async Task LoadFolderStructure()
        {
            if (string.IsNullOrEmpty(CurrentFolderPath))
                return;

            try
            {
                var rootItem = await CreateFileSystemItem(CurrentFolderPath, true);
                if (rootItem != null)
                {
                    var items = new ObservableCollection<FileSystemItem> { rootItem };
                    FileTreeView.ItemsSource = items;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading folder structure: {ex.Message}");
            }
        }

        private async Task<FileSystemItem> CreateFileSystemItem(string path, bool isRoot = false, int maxDepth = 3, int currentDepth = 0)
        {
            try
            {
                var info = new DirectoryInfo(path);
                if (!info.Exists) return null;

                var item = new FileSystemItem
                {
                    Name = isRoot ? Path.GetFileName(path) : info.Name,
                    FullPath = path,
                    IsDirectory = true,
                    IconInfo = new FileIconInfo { IsEmoji = true, Icon = "📁" },
                    IsExpanded = isRoot,
                    Children = new ObservableCollection<FileSystemItem>()
                };

                if (currentDepth < maxDepth)
                {
                    await LoadChildrenRecursively(item, currentDepth);
                }

                return item;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating file system item for {path}: {ex.Message}");
                return null;
            }
        }

        private async Task LoadChildrenRecursively(FileSystemItem parentItem, int currentDepth, int maxDepth = 3)
        {
            if (currentDepth >= maxDepth) return;

            try
            {
                var info = new DirectoryInfo(parentItem.FullPath);
                if (!info.Exists) return;

                try
                {
                    var directories = info.GetDirectories()
                        .Where(d => !d.Name.StartsWith(".") || d.Name == ".git")
                        .OrderBy(d => d.Name);

                    foreach (var dir in directories)
                    {
                        var childItem = new FileSystemItem
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            IsDirectory = true,
                            IconInfo = dir.Name == ".git"
                                ? new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/git-original.png" }
                                : new FileIconInfo { IsEmoji = true, Icon = "📁" },
                            IsExpanded = false,
                            Children = new ObservableCollection<FileSystemItem>()
                        };

                        await LoadChildrenRecursively(childItem, currentDepth + 1, maxDepth);

                        parentItem.Children.Add(childItem);
                    }
                }
                catch (UnauthorizedAccessException) { }

                try
                {
                    var files = info.GetFiles()
                        .Where(f => !f.Name.StartsWith(".") ||
                                   f.Name == ".gitignore" ||
                                   f.Name == ".gitattributes")
                        .OrderBy(f => f.Name);

                    foreach (var file in files)
                    {
                        var extension = file.Extension.ToLower();
                        var iconInfo = _fileIcons.ContainsKey(extension)
                            ? _fileIcons[extension]
                            : new FileIconInfo { IsEmoji = true, Icon = "📄" };

                        var fileItem = new FileSystemItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            IsDirectory = false,
                            IconInfo = iconInfo,
                            Children = null
                        };

                        parentItem.Children.Add(fileItem);
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading children for {parentItem.FullPath}: {ex.Message}");
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CurrentFolderPath))
            {
                await LoadFolderStructure();
            }
        }

        private void FileTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is FileSystemItem item)
            {
                if (item.IsDirectory)
                {
                    item.IsExpanded = !item.IsExpanded;
                }
                else
                {
                    FileSelected?.Invoke(this, new FileSelectedEventArgs(item.FullPath, false));
                }
            }
        }

        private async Task<List<FileSystemItem>> LoadDirectoryContents(string directoryPath)
        {
            var items = new List<FileSystemItem>();
            try
            {
                var info = new DirectoryInfo(directoryPath);

                var directories = info.GetDirectories()
                    .Where(d => !d.Name.StartsWith(".") || d.Name == ".git")
                    .OrderBy(d => d.Name);

                foreach (var dir in directories)
                {
                    var childItem = new FileSystemItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        IconInfo = dir.Name == ".git"
                            ? new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/git-original.png" }
                            : new FileIconInfo { IsEmoji = true, Icon = "📁" },
                        IsExpanded = false,
                        Children = new ObservableCollection<FileSystemItem>()
                    };
                    items.Add(childItem);
                }

                var files = info.GetFiles()
                    .Where(f => !f.Name.StartsWith(".") ||
                               f.Name == ".gitignore" ||
                               f.Name == ".gitattributes")
                    .OrderBy(f => f.Name);

                foreach (var file in files)
                {
                    var extension = file.Extension.ToLower();
                    var iconInfo = _fileIcons.ContainsKey(extension)
                        ? _fileIcons[extension]
                        : new FileIconInfo { IsEmoji = true, Icon = "📄" };

                    if (file.Name.StartsWith(".git"))
                        iconInfo = new FileIconInfo { IsEmoji = false, Icon = "ms-appx:///Icons/git-original.png" };

                    var fileItem = new FileSystemItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        IconInfo = iconInfo,
                        Children = null
                    };
                    items.Add(fileItem);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading directory contents for {directoryPath}: {ex.Message}");
            }
            return items;
        }

        private void FileTreeView_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(sender as UIElement);

            if (pointer.Properties.IsRightButtonPressed)
            {
                e.Handled = true;

                var treeView = sender as TreeView;
                var element = e.OriginalSource as FrameworkElement;

                while (element != null && !(element is TreeViewItem))
                {
                    element = element.Parent as FrameworkElement ??
                             Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element) as FrameworkElement;
                }

                if (element is TreeViewItem treeViewItem)
                {
                    _selectedItem = treeViewItem.DataContext as FileSystemItem;
                    if (_selectedItem == null)
                        _selectedItem = treeViewItem.Content as FileSystemItem;

                    if (_selectedItem != null)
                    {
                        var contextMenu = this.Resources["FileContextMenu"] as MenuFlyout;
                        contextMenu?.ShowAt(treeView, pointer.Position);
                    }
                }
            }
        }

        private void ScrollViewer_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            ShowEmptySpaceContextMenu(sender, e.GetPosition(sender as FrameworkElement));
        }

        private void Grid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            ShowEmptySpaceContextMenu(sender, e.GetPosition(sender as FrameworkElement));
        }

        private void ShowEmptySpaceContextMenu(object sender, Windows.Foundation.Point position)
        {
            if (!string.IsNullOrEmpty(CurrentFolderPath))
            {
                var contextMenu = this.Resources["EmptySpaceContextMenu"] as MenuFlyout;
                contextMenu?.ShowAt(sender as FrameworkElement, position);
            }
        }

        private async void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentFolderPath)) return;

            var dialog = new ContentDialog
            {
                Title = "New File",
                Content = new TextBox
                {
                    Text = "NewFile.txt",
                    SelectionStart = 0,
                    SelectionLength = "NewFile".Length
                },
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var fileName = textBox?.Text?.Trim();

                if (!string.IsNullOrEmpty(fileName))
                {
                    try
                    {
                        var filePath = Path.Combine(CurrentFolderPath, fileName);

                        if (!File.Exists(filePath))
                        {
                            await File.WriteAllTextAsync(filePath, string.Empty);
                            await LoadFolderStructure();
                        }
                        else
                        {
                            await ShowErrorDialog("File already exists", $"A file named '{fileName}' already exists.");
                        }
                    }
                    catch (Exception ex)
                    {
                        await ShowErrorDialog("Error creating file", ex.Message);
                        System.Diagnostics.Debug.WriteLine($"Error creating file: {ex.Message}");
                    }
                }
            }
        }

        private async void NewFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentFolderPath)) return;

            var dialog = new ContentDialog
            {
                Title = "New Folder",
                Content = new TextBox
                {
                    Text = "NewFolder",
                    SelectionStart = 0,
                    SelectionLength = "NewFolder".Length
                },
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var folderName = textBox?.Text?.Trim();

                if (!string.IsNullOrEmpty(folderName))
                {
                    try
                    {
                        var folderPath = Path.Combine(CurrentFolderPath, folderName);

                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                            await LoadFolderStructure();
                        }
                        else
                        {
                            await ShowErrorDialog("Folder already exists", $"A folder named '{folderName}' already exists.");
                        }
                    }
                    catch (Exception ex)
                    {
                        await ShowErrorDialog("Error creating folder", ex.Message);
                        System.Diagnostics.Debug.WriteLine($"Error creating folder: {ex.Message}");
                    }
                }
            }
        }

        private async void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await LoadFolderStructure();
        }

        private async Task ShowErrorDialog(string title, string message)
        {
            var errorDialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }

        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem != null && !_selectedItem.IsDirectory)
            {
                FileSelected?.Invoke(this, new FileSelectedEventArgs(_selectedItem.FullPath, false));
            }
        }

        private void OpenInNewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem != null && !_selectedItem.IsDirectory)
            {
                FileSelected?.Invoke(this, new FileSelectedEventArgs(_selectedItem.FullPath, true));
            }
        }

        private async void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;

            var dialog = new ContentDialog
            {
                Title = "Rename",
                Content = new TextBox { Text = _selectedItem.Name, SelectionStart = 0, SelectionLength = _selectedItem.Name.Length },
                PrimaryButtonText = "Rename",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                var newName = textBox?.Text?.Trim();

                if (!string.IsNullOrEmpty(newName) && newName != _selectedItem.Name)
                {
                    try
                    {
                        var oldPath = _selectedItem.FullPath;
                        var newPath = Path.Combine(Path.GetDirectoryName(oldPath), newName);

                        if (_selectedItem.IsDirectory)
                        {
                            Directory.Move(oldPath, newPath);
                        }
                        else
                        {
                            File.Move(oldPath, newPath);
                        }

                        await LoadFolderStructure();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error renaming item: {ex.Message}");
                    }
                }
            }
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;

            var dialog = new ContentDialog
            {
                Title = "Delete",
                Content = $"Are you sure you want to delete '{_selectedItem.Name}'?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    if (_selectedItem.IsDirectory)
                    {
                        Directory.Delete(_selectedItem.FullPath, true);
                    }
                    else
                    {
                        File.Delete(_selectedItem.FullPath);
                    }

                    await LoadFolderStructure();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting item: {ex.Message}");
                }
            }
        }

        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(_selectedItem.FullPath);
                Clipboard.SetContent(dataPackage);
            }
        }

        private async void OpenInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem != null)
            {
                try
                {
                    var path = _selectedItem.IsDirectory ? _selectedItem.FullPath : Path.GetDirectoryName(_selectedItem.FullPath);
                    await Launcher.LaunchFolderPathAsync(path);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening in explorer: {ex.Message}");
                }
            }
        }
    }

    public class FileIconInfo
    {
        public bool IsEmoji { get; set; }
        public string Icon { get; set; }
    }

    public class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public FileIconInfo IconInfo { get; set; }
        public bool IsExpanded { get; set; }
        public ObservableCollection<FileSystemItem> Children { get; set; }
    }

    public class FileSelectedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public bool OpenInNewTab { get; }

        public FileSelectedEventArgs(string filePath, bool openInNewTab = false)
        {
            FilePath = filePath;
            OpenInNewTab = openInNewTab;
        }
    }
}