using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Markup;
using IOPath = System.IO.Path;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace FluentZip
{
    public sealed partial class ArchiveViewPage : Page
    {
        private static bool IsX64() => RuntimeInformation.OSArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.X64;
        private static bool IsArm64() => RuntimeInformation.OSArchitecture == Architecture.Arm64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        public ObservableCollection<FileItem> FileItems { get; set; } = new();
        private List<FileItem> _allFilesCache = new();
        private const string RecentFilesKey = "RecentOpenedFiles";
        private string _currentArchivePath;

        // 列宽拖拽状态（表头自定义 resize grip）
        private bool _isHeaderResizing;
        private int _resizeColIndex = -1;
        private double _resizeStartX;
        private double _resizeStartWidth;

        // 记录各表头列默认宽度（用于显示/隐藏列）
        private readonly Dictionary<int, double> _headerDefaultWidths = new();
        private MenuFlyoutSubItem? _recentFilesSubItem;
        private MenuFlyoutSeparator? _recentHistorySeparator;
        private MenuFlyoutItem? _clearHistoryMenuItem;
        private MenuFlyoutItem? _noHistoryMenuItem;
        private readonly List<MenuFlyoutItem> _historyMenuItems = new();
        private MenuFlyoutItem? _contextRenameItem;
        private MenuFlyoutItem? _contextDeleteItem;
        private MenuFlyoutItem? _contextCopyItem;
        private MenuFlyoutItem? _contextCopyFullPathItem;
        private MenuFlyoutItem? _contextAddItem;
        private double _savedTreeColumnWidth = 260;
        private double _savedTreeColumnMinWidth = 150;
        private string _currentViewMode = "Detail";
        private bool _treePaneInitialized;
        private CancellationTokenSource? _treePaneAnimationCts;
        private static readonly TimeSpan TreePaneAnimationDuration = TimeSpan.FromMilliseconds(240);
        private bool _canDeleteCurrentArchive;
        private Grid? _treeToggleContainer;
        private Border? _treeToggleMask;
        private Grid? TreeToggleContainerHost => _treeToggleContainer ??= FindName("TreeToggleContainer") as Grid;
        private Border? TreeToggleMaskElement => _treeToggleMask ??= FindName("TreeToggleMask") as Border;
        private double _savedPreviewColumnWidth = 320;
        private bool _isPreviewPaneVisible = true;
        private CancellationTokenSource? _previewCts;
        private bool _isUpdatingPreviewToggle;
        private ToggleButton? _previewPaneToggleButton;
        private ToggleButton? PreviewPaneToggleButtonHost => _previewPaneToggleButton ??= FindName("PreviewPaneToggleButton") as ToggleButton;
        private static readonly HashSet<string> _previewableImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".webp",
            ".tif",
            ".tiff"
        };
        private string _currentArchiveComment = string.Empty;
        private static bool _codePagesRegistered;
        private static readonly string[] _codePageSupportedExtensions = new[] { ".zip" };
        private enum ExtractDestinationBehavior
        {
            Direct,
            ForceSubfolder,
            Smart
        }

        public ArchiveViewPage()
        {
            this.InitializeComponent();
            UpdateCodePageButtonState(null);
            FileListView.ItemsSource = FileItems;
            CacheHeaderDefaultWidths();
            SetTreePaneVisibility(true, false);
            FileListView.IsItemClickEnabled = true;
            FileListView.ItemClick += FileListView_ItemClick;
            FileListView.DoubleTapped += FileListView_DoubleTapped;
            FolderTreeView.ItemInvoked += FolderTreeView_ItemInvoked;
            CacheContextMenuItems();
            ApplyViewMode(_currentViewMode);
            var accelFind = new KeyboardAccelerator { Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control };
            accelFind.Invoked += (s, e) => { SearchFiles_Click(this, new RoutedEventArgs()); e.Handled = true; };
            this.KeyboardAccelerators.Add(accelFind);
            UpdateDeleteButtonState();
            _recentFilesSubItem = FindName("RecentFilesSubItem") as MenuFlyoutSubItem;
            _recentHistorySeparator = FindName("RecentHistorySeparator") as MenuFlyoutSeparator;
            _clearHistoryMenuItem = FindName("ClearHistoryMenuItem") as MenuFlyoutItem;
            _noHistoryMenuItem = FindName("NoHistoryItem") as MenuFlyoutItem;
            SetPreviewPaneVisibility(ToggleImagePreviewItem?.IsChecked != false);
        }

        private void CacheHeaderDefaultWidths()
        {
            try
            {
                if (HeaderGrid?.ColumnDefinitions is ColumnDefinitionCollection cols)
                {
                    for (int i = 1; i < cols.Count; i++)
                    {
                        var col = cols[i];
                        double fallback = GetColumnFallbackWidth(i);
                        double w = col.Width.IsAbsolute ? col.Width.Value : (col.ActualWidth > 0 ? col.ActualWidth : fallback);
                        _headerDefaultWidths[i] = w;
                    }
                }
            }
            catch { }
        }

        private static double GetColumnFallbackWidth(int columnIndex) => columnIndex switch
        {
            1 => 250,
            2 => 120,
            3 => 160,
            4 => 120,
            5 => 120,
            6 => 140,
            7 => 140,
            8 => 120,
            9 => 140,
            10 => 160,
            11 => 160,
            _ => 100
        };

        private static double GetColumnMinWidth(int columnIndex) => columnIndex switch
        {
            2 => 60,
            3 => 90,
            4 => 60,
            5 => 60,
            6 => 80,
            7 => 80,
            8 => 80,
            9 => 80,
            10 => 100,
            11 => 100,
            _ => 40
        };

        private void SetColumnVisibility(int columnIndex, bool isVisible)
        {
            if (HeaderGrid?.ColumnDefinitions == null) return;
            if (columnIndex < 0 || columnIndex >= HeaderGrid.ColumnDefinitions.Count) return;

            var column = HeaderGrid.ColumnDefinitions[columnIndex];

            if (!_headerDefaultWidths.ContainsKey(columnIndex))
            {
                double fallback = GetColumnFallbackWidth(columnIndex);
                double width = column.Width.IsAbsolute
                    ? column.Width.Value
                    : (column.ActualWidth > 0 ? column.ActualWidth : fallback);
                _headerDefaultWidths[columnIndex] = width;
            }

            if (isVisible)
            {
                double width = _headerDefaultWidths.TryGetValue(columnIndex, out double stored) && stored > 0
                    ? stored
                    : GetColumnFallbackWidth(columnIndex);
                column.Width = new GridLength(width, GridUnitType.Pixel);
                column.MinWidth = GetColumnMinWidth(columnIndex);
            }
            else
            {
                double currentWidth = column.ActualWidth > 0
                    ? column.ActualWidth
                    : (column.Width.IsAbsolute ? column.Width.Value : GetColumnFallbackWidth(columnIndex));
                _headerDefaultWidths[columnIndex] = currentWidth;
                column.Width = new GridLength(0, GridUnitType.Pixel);
                column.MinWidth = 0;
            }

            UpdateHeaderElementVisibility(columnIndex, isVisible);
        }

        private void UpdateHeaderElementVisibility(int columnIndex, bool isVisible)
        {
            if (HeaderGrid == null) return;
            foreach (var element in HeaderGrid.Children.OfType<FrameworkElement>())
            {
                if (!TryParseColumnIndex(element.Tag, out int tagIndex)) continue;
                if (tagIndex == columnIndex)
                {
                    element.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private static bool TryParseColumnIndex(object tag, out int columnIndex)
        {
            columnIndex = -1;
            switch (tag)
            {
                case int i:
                    columnIndex = i;
                    return true;
                case string s when int.TryParse(s, out var parsed):
                    columnIndex = parsed;
                    return true;
                default:
                    return false;
            }
        }

        private void ToggleColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleMenuFlyoutItem item) return;
            if (!TryParseColumnIndex(item.Tag, out int columnIndex)) return;
            SetColumnVisibility(columnIndex, item.IsChecked);
        }


        private class SearchResultItem
        {
            public string Path { get; set; } = string.Empty; // 目录/文件在归档内的路径
            public string Name { get; set; } = string.Empty;
            public string ParentPath { get; set; } = string.Empty;
            public string CompressedSize { get; set; } = string.Empty;
            public string OriginalSize { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string ModifiedTime { get; set; } = string.Empty;
            public bool IsFolder { get; set; }
            public string Key => NormalizePath(string.IsNullOrEmpty(ParentPath) ? Name : $"{ParentPath}/{Name}");
        }

        // 查找文件：弹出查询对话框并聚合全局匹配结果（支持列显示、双击定位、删除）
        private async void SearchFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new SearchWindow();

                var results = new ObservableCollection<SearchResultItem>();
                win.ItemsSource = results;

                void RunSearch(string q)
                {
                    q = q?.Trim() ?? string.Empty;
                    results.Clear();
                    if (string.IsNullOrEmpty(q)) return;

                    static bool ContainsIgnoreCase(string src, string sub) => src?.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;

                    foreach (var f in _allFilesCache)
                    {
                        string full = string.IsNullOrEmpty(f.ParentPath) ? f.Name : $"{f.ParentPath}/{f.Name}";
                        if (ContainsIgnoreCase(f.Name, q) || ContainsIgnoreCase(full, q))
                        {
                            results.Add(new SearchResultItem
                            {
                                Name = f.Name,
                                ParentPath = f.ParentPath ?? string.Empty,
                                Path = full,
                                CompressedSize = f.CompressedSize,
                                OriginalSize = f.Size,
                                Type = f.Type,
                                ModifiedTime = f.ModifiedTime,
                                IsFolder = false
                            });
                        }
                    }

                    foreach (var folder in EnumerateAllFolders())
                    {
                        var full = folder.FullPath;
                        if (string.IsNullOrEmpty(full)) continue;
                        if (ContainsIgnoreCase(folder.Name, q) || ContainsIgnoreCase(full, q))
                        {
                            string parent = IOPath.GetDirectoryName(full)?.Replace("\\", "/") ?? string.Empty;
                            if (parent == ".") parent = string.Empty;
                            results.Add(new SearchResultItem
                            {
                                Name = folder.Name,
                                ParentPath = parent,
                                Path = full,
                                CompressedSize = string.Empty,
                                OriginalSize = string.Empty,
                                Type = "文件夹",
                                ModifiedTime = "-",
                                IsFolder = true
                            });
                        }
                    }
                }

                win.QuerySubmitted += (s, q) => RunSearch(q);
                win.ItemInvoked += (s, item) =>
                {
                    if (item is SearchResultItem it)
                    {
                        var fi = new FileItem { Name = it.Name, ParentPath = it.ParentPath };
                        NavigateToAndSelect(fi);
                    }
                };
                win.DeleteRequested += async (s, args2) =>
                {
                    var selected = win.SelectedItems?.OfType<SearchResultItem>().ToList() ?? new();
                    if (selected.Count == 0)
                    {
                        StatusText?.SetValue(TextBlock.TextProperty, "未选择要删除的项目");
                        return;
                    }

                    var removalKeys = new List<string>();
                    foreach (var it in selected)
                    {
                        if (it.IsFolder)
                            removalKeys.Add(NormalizePath(it.Path.TrimEnd('/')) + "/");
                        else
                            removalKeys.Add(NormalizePath(it.Key));
                    }

                    var ok = await DeleteArchiveItemsAsync(removalKeys);
                    if (ok) win.Close();
                };

                // Center the search window relative to main window
                var owner = App.StartupWindow;
                if (owner?.AppWindow is Microsoft.UI.Windowing.AppWindow ownerAw)
                {
                    var desired = new Windows.Graphics.SizeInt32 { Width = 820, Height = 560 };
                    try { win.AppWindow.Resize(desired); } catch { }
                    try
                    {
                        var pos = ownerAw.Position;
                        var size = ownerAw.Size;
                        var x = pos.X + Math.Max(0, (size.Width - desired.Width) / 2);
                        var y = pos.Y + Math.Max(0, (size.Height - desired.Height) / 2);
                        win.AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
                    }
                    catch { }
                }

                win.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SearchFiles_Click error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, "打开查找窗口失败");
            }
        }

        private IEnumerable<FolderNode> EnumerateAllFolders()
        {
            if (FolderTreeView?.RootNodes == null) yield break;
            foreach (var root in FolderTreeView.RootNodes)
            {
                foreach (var f in EnumerateFoldersRecursive(root))
                    yield return f;
            }
        }

        private IEnumerable<FolderNode> EnumerateFoldersRecursive(TreeViewNode node)
        {
            if (node?.Content is FolderNode fn)
            {
                yield return fn;
            }
            if (node == null) yield break;
            foreach (var c in node.Children)
            {
                foreach (var f in EnumerateFoldersRecursive(c))
                    yield return f;
            }
        }

        private async Task<bool> DeleteArchiveItemsAsync(List<string> removalKeys)
        {
            if (string.IsNullOrEmpty(_currentArchivePath) || !File.Exists(_currentArchivePath))
            {
                StatusText?.SetValue(TextBlock.TextProperty, "未打开任何归档");
                return false;
            }

            if (removalKeys == null || removalKeys.Count == 0) return false;

            var ext = IOPath.GetExtension(_currentArchivePath)?.ToLowerInvariant();
            if (ext == ".zip")
            {
                var (dialog, bar, text) = await ShowProgressDialogSafeAsync("删除文件", "正在重建归档… 0%", "正在删除…");
                if (dialog == null) return false;

                var dispatcher = DispatcherQueue;
                var progress = new Progress<(int processed, int total)>(p =>
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        int percent = p.total > 0 ? (int)(p.processed * 100.0 / p.total) : 0;
                        bar.Value = percent;
                        text.Text = $"正在重建归档… {percent}%（{p.processed}/{p.total}）";
                    });
                });

                await Task.Run(async () =>
                {
                    await RebuildZipArchiveWithProgress(
                        additions: new List<(string path, Func<Stream> factory)>(),
                        removals: removalKeys,
                        ensureDirectories: new List<string>(),
                        progress: progress,
                        cancellationToken: CancellationToken.None
                    );
                });

                dialog.Hide();
            }
            else if (ext == ".7z" || ext == ".rar")
            {
                var (dialog, bar, text) = await ShowProgressDialogSafeAsync("删除文件", "正在准备删除…", "正在删除…");
                if (dialog != null)
                {
                    bar.IsIndeterminate = true;
                    bar.Value = 0;
                }

                var ok = await DeleteWithSevenZipAsync(_currentArchivePath, removalKeys, dialog, CancellationToken.None);
                dialog?.Hide();

                if (!ok)
                {
                    StatusText?.SetValue(TextBlock.TextProperty, "7z 删除失败（可能是路径不匹配或归档损坏）");
                    return false;
                }
            }
            else
            {
                StatusText?.SetValue(TextBlock.TextProperty, "当前格式不支持删除（仅支持 ZIP/7z/RAR）");
                return false;
            }

            // 刷新
            LoadArchive(_currentArchivePath);
            StatusText?.SetValue(TextBlock.TextProperty, $"删除完成，共删除 {removalKeys.Count} 项");
            return true;
        }

        private void NavigateToAndSelect(FileItem item)
        {
            var parent = item.ParentPath ?? string.Empty;
            var node = GetTreeNodeByFullPath(parent);
            if (node != null)
            {
                ExpandAndSelectNode(node);
                UpdateFileList(node);

                var found = FileItems.FirstOrDefault(f =>
                    string.Equals(f.Name, item.Name, StringComparison.Ordinal) &&
                    string.Equals(f.ParentPath ?? string.Empty, parent, StringComparison.Ordinal));

                if (found != null)
                {
                    FileListView.SelectedItem = found;
                    try { FileListView.ScrollIntoView(found); } catch { }
                }

                StatusText?.SetValue(TextBlock.TextProperty, $"已定位: {item.Name}");
            }
        }

        private void OpenFlyout_Opening(object sender, object e)
        {
            var recentSubItem = _recentFilesSubItem;
            if (recentSubItem == null) return;

            foreach (var item in _historyMenuItems)
            {
                recentSubItem.Items.Remove(item);
            }
            _historyMenuItems.Clear();

            var localSettings = ApplicationData.Current.LocalSettings;
            string historyRaw = localSettings.Values[RecentFilesKey] as string;
            bool hasHistory = !string.IsNullOrEmpty(historyRaw);

            if (_noHistoryMenuItem != null)
            {
                _noHistoryMenuItem.Visibility = hasHistory ? Visibility.Collapsed : Visibility.Visible;
            }
            if (_recentHistorySeparator != null)
            {
                _recentHistorySeparator.Visibility = hasHistory ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_clearHistoryMenuItem != null)
            {
                _clearHistoryMenuItem.Visibility = hasHistory ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!hasHistory || recentSubItem.Items == null) return;

            var paths = historyRaw.Split('|');
            int insertIndex = _recentHistorySeparator != null
                ? recentSubItem.Items.IndexOf(_recentHistorySeparator)
                : recentSubItem.Items.Count;
            if (insertIndex < 0) insertIndex = recentSubItem.Items.Count;

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                var item = new MenuFlyoutItem
                {
                    Text = path,
                    Icon = new SymbolIcon(Symbol.OpenFile),
                    Tag = "History"
                };
                item.Click += (s, a) =>
                {
                    LoadArchive(path);
                    AddToHistory(path);
                };

                recentSubItem.Items.Insert(insertIndex, item);
                _historyMenuItems.Add(item);
                insertIndex++;
            }
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDeleteButtonState();
            UpdatePreviewPane();
        }

        private void UpdateDeleteButtonState()
        {
            if (DeleteButton == null) return;
            var hasSelection = FileListView?.SelectedItems?.Count > 0;
            var canDeleteNow = _canDeleteCurrentArchive && hasSelection;
            DeleteButton.IsEnabled = canDeleteNow;
            DeleteButton.Opacity = canDeleteNow ? 1.0 : 0.5;

            // 同步编辑菜单启用状态
            try
            {
                if (_contextDeleteItem != null) _contextDeleteItem.IsEnabled = canDeleteNow;
                if (_contextRenameItem != null) _contextRenameItem.IsEnabled = hasSelection;
                if (_contextCopyItem != null) _contextCopyItem.IsEnabled = hasSelection;
                if (_contextCopyFullPathItem != null) _contextCopyFullPathItem.IsEnabled = hasSelection;
            }
            catch { }
        }

        private void UpdateCodePageButtonState(string? path)
        {
            if (CodePageButton != null)
            {
                var ext = IOPath.GetExtension(path ?? string.Empty);
                bool isSupported = !string.IsNullOrEmpty(path) &&
                    Array.Exists(_codePageSupportedExtensions, s =>
                        string.Equals(s, ext, StringComparison.OrdinalIgnoreCase));
                CodePageButton.IsEnabled = isSupported;
                CodePageButton.Opacity = isSupported ? 1.0 : 0.5;
            }

            UpdateArchiveActionStates(path);
        }

        private void UpdateArchiveActionStates(string? path)
        {
            string extension = IOPath.GetExtension(path ?? string.Empty);
            bool hasArchive = !string.IsNullOrEmpty(path);
            bool canAdd = hasArchive && (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase) || 
                                         string.Equals(extension, ".7z", StringComparison.OrdinalIgnoreCase));

            if (AddButton != null)
            {
                AddButton.IsEnabled = canAdd;
                AddButton.Opacity = canAdd ? 1.0 : 0.5;
            }
            if (_contextAddItem != null)
            {
                _contextAddItem.IsEnabled = canAdd;
            }

            _canDeleteCurrentArchive = hasArchive && !string.Equals(extension, ".rar", StringComparison.OrdinalIgnoreCase);
            UpdateDeleteButtonState();
        }

        private bool TryGetCurrentArchivePath(out string archivePath)
        {
            archivePath = _currentArchivePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                StatusText?.SetValue(TextBlock.TextProperty, "未打开任何归档");
                return false;
            }
            return true;
        }

        private static string GetDefaultExtractionFolderName(string archivePath)
        {
            var folderName = IOPath.GetFileNameWithoutExtension(archivePath);
            return string.IsNullOrWhiteSpace(folderName) ? "Extracted" : folderName;
        }

        private static string? GetDesktopDirectory()
        {
            try
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                return string.IsNullOrWhiteSpace(path) ? null : path;
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> ShouldUseSmartSubfolderAsync(string archivePath)
        {
            return await Task.Run(() =>
            {
                var rootEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var archive = ArchiveFactory.Open(archivePath);
                foreach (var entry in archive.Entries)
                {
                    var root = GetRootSegment(entry.Key);
                    if (!string.IsNullOrEmpty(root))
                    {
                        rootEntries.Add(root);
                        if (rootEntries.Count > 1)
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        private static string GetRootSegment(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            var normalized = key.Replace("\\", "/").Trim('/');
            if (string.IsNullOrEmpty(normalized)) return string.Empty;
            int slashIndex = normalized.IndexOf('/');
            return slashIndex >= 0 ? normalized.Substring(0, slashIndex) : normalized;
        }

        private async Task ExtractToArchiveDirectoryAsync(ExtractDestinationBehavior behavior)
        {
            if (!TryGetCurrentArchivePath(out var archivePath))
            {
                return;
            }

            var archiveDirectory = IOPath.GetDirectoryName(archivePath);
            if (string.IsNullOrWhiteSpace(archiveDirectory))
            {
                StatusText?.SetValue(TextBlock.TextProperty, "无法确定压缩文件所在目录");
                return;
            }

            await ExtractArchiveAsync(archiveDirectory, behavior);
        }

        private async Task ExtractToDesktopAsync(ExtractDestinationBehavior behavior)
        {
            var desktop = GetDesktopDirectory();
            if (string.IsNullOrWhiteSpace(desktop))
            {
                StatusText?.SetValue(TextBlock.TextProperty, "无法定位桌面目录");
                return;
            }

            await ExtractArchiveAsync(desktop, behavior);
        }

        private async Task ExtractToCustomFolderAsync()
        {
            if (!TryGetCurrentArchivePath(out _))
            {
                return;
            }

            try
            {
                var picker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.Downloads
                };
                picker.FileTypeFilter.Add("*");

                var hostWindow = App.StartupWindow;
                if (hostWindow != null)
                {
                    var hwnd = WindowNative.GetWindowHandle(hostWindow);
                    InitializeWithWindow.Initialize(picker, hwnd);
                }

                var folder = await picker.PickSingleFolderAsync();
                if (folder == null)
                {
                    StatusText?.SetValue(TextBlock.TextProperty, "已取消选择目标文件夹");
                    return;
                }

                await ExtractArchiveAsync(folder.Path, ExtractDestinationBehavior.Direct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractToCustomFolderAsync error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, "选择目标文件夹失败");
            }
        }

        private async Task<bool> ExtractArchiveAsync(string baseDirectory, ExtractDestinationBehavior behavior)
        {
            if (!TryGetCurrentArchivePath(out var archivePath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                StatusText?.SetValue(TextBlock.TextProperty, "目标目录无效");
                return false;
            }

            try
            {
                Directory.CreateDirectory(baseDirectory);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractArchiveAsync base dir error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, $"创建目标目录失败: {ex.Message}");
                return false;
            }

            bool useSubfolder = behavior switch
            {
                ExtractDestinationBehavior.ForceSubfolder => true,
                ExtractDestinationBehavior.Smart => await ShouldUseSmartSubfolderAsync(archivePath),
                _ => false
            };

            string destination = useSubfolder
                ? IOPath.Combine(baseDirectory, GetDefaultExtractionFolderName(archivePath))
                : baseDirectory;

            try
            {
                Directory.CreateDirectory(destination);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractArchiveAsync destination error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, $"无法创建解压目录: {ex.Message}");
                return false;
            }

            ContentDialog progressDialog = null;
            ProgressBar progressBar = null;
            TextBlock progressText = null;

            try
            {
                (progressDialog, progressBar, progressText) = await ShowProgressDialogSafeAsync("解压文件", "正在准备解压…", "正在解压…");

                var dispatcher = DispatcherQueue;
                IProgress<(int processed, int total)> extractionProgress = null;
                if (dispatcher != null && (progressBar != null || progressText != null))
                {
                    extractionProgress = new Progress<(int processed, int total)>(p =>
                    {
                        dispatcher.TryEnqueue(() =>
                        {
                            int percent = p.total > 0 ? (int)(p.processed * 100.0 / p.total) : 100;
                            if (progressBar != null)
                            {
                                progressBar.IsIndeterminate = false;
                                progressBar.Value = percent;
                            }
                            if (progressText != null)
                            {
                                progressText.Text = $"正在解压… {percent}%（{p.processed}/{p.total}）";
                            }
                        });
                    });
                }

                await Task.Run(() => ExtractArchiveEntriesWithProgress(archivePath, destination, extractionProgress, CancellationToken.None));

                if (dispatcher != null && (progressBar != null || progressText != null))
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        if (progressBar != null)
                        {
                            progressBar.Value = 100;
                        }
                        if (progressText != null)
                        {
                            progressText.Text = "解压完成";
                        }
                    });
                }

                StatusText?.SetValue(TextBlock.TextProperty, $"解压完成: {destination}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractArchiveAsync error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, $"解压失败: {ex.Message}");
                return false;
            }
            finally
            {
                progressDialog?.Hide();
            }
        }

        private void ExtractArchiveEntriesWithProgress(string archivePath, string destination, IProgress<(int processed, int total)> progress, CancellationToken token)
        {
            using var archive = ArchiveFactory.Open(archivePath);
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            int total = entries.Count;
            int processed = 0;

            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                entry.WriteToDirectory(destination, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                    PreserveFileTime = true
                });
                processed++;
                progress?.Report((processed, total));
            }

            if (total == 0)
            {
                progress?.Report((0, 0));
            }
        }

        private void CacheContextMenuItems()
        {
            if (FileListView?.ContextFlyout is not MenuFlyout menu) return;

            static MenuFlyoutItem? FindMenuItem(MenuFlyout flyout, string name) =>
                flyout.Items.OfType<MenuFlyoutItem>().FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.Ordinal));

            _contextRenameItem = FindMenuItem(menu, "ContextRenameItem");
            _contextDeleteItem = FindMenuItem(menu, "ContextDeleteItem");
            _contextCopyItem = FindMenuItem(menu, "ContextCopyItem");
            _contextCopyFullPathItem = FindMenuItem(menu, "ContextCopyFullPathItem");
            _contextAddItem = FindMenuItem(menu, "ContextAddItem");
        }

        private async Task<(ContentDialog dialog, ProgressBar bar, TextBlock text)> ShowLoadDialogAsync(string title, string message)
        {
            var hostRoot = (App.StartupWindow?.Content as FrameworkElement)?.XamlRoot ?? this.Content?.XamlRoot;
            if (hostRoot == null)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "无法显示进度窗口：XamlRoot 未就绪");
                return (null, null, null);
            }

            var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
            var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
            var bar = new ProgressBar { IsIndeterminate = true, Minimum = 0, Maximum = 100, Value = 0 };
            panel.Children.Add(text);
            panel.Children.Add(bar);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = panel,
                PrimaryButtonText = "正在加载…",
                IsPrimaryButtonEnabled = false,
                XamlRoot = hostRoot,
                RequestedTheme = ThemeService.CurrentTheme
            };

            _ = dialog.ShowAsync();
            await Task.Delay(50);
            return (dialog, bar, text);
        }

        private async Task LoadArchiveAsync(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    UpdateCodePageButtonState(null);
                    return;
                }

                _currentArchivePath = path;
                UpdateCodePageButtonState(path);
                _currentArchiveComment = string.Empty;
                var commentTask = Task.Run(() => TryReadArchiveComment(path));

                // 清空现有 UI
                FileItems.Clear();
                _allFilesCache.Clear();
                FolderTreeView.RootNodes.Clear();

                if (ExtractToFolderItem != null)
                {
                    ExtractToFolderItem.Text = $"解压到“{IOPath.GetFileNameWithoutExtension(path)}”文件夹";
                }

                var (dialog, bar, text) = await ShowLoadDialogAsync("打开文件", "正在加载归档…");
                var dispatcher = DispatcherQueue;

                // 根节点先建立
                var rootData = new FolderNode
                {
                    Name = IOPath.GetFileName(path),
                    FullPath = "",
                    IconImage = ShellIconService.GetFolderIcon(small: true)
                };
                var rootTreeNode = new TreeViewNode { Content = rootData, IsExpanded = true };
                FolderTreeView.RootNodes.Add(rootTreeNode);

                // 后台读取并构建树/右侧缓存
                await Task.Run(() =>
                {
                    // 先统计总条目数（用于百分比）
                    int total = 0;
                    using (var countArchive = ArchiveFactory.Open(path))
                    {
                        total = countArchive.Entries.Count();
                    }

                    int processed = 0;

                    using (var archive = ArchiveFactory.Open(path))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            // 统一路径
                            string entryPath = entry.Key.Replace("\\", "/");

                            // 更新 UI（树和文件缓存）
                            if (entry.IsDirectory)
                            {
                                // 目录：在 UI 线程更新树节点
                                dispatcher.TryEnqueue(() =>
                                {
                                    AddFolderToTree(rootTreeNode, entryPath);
                                });
                            }
                            else
                            {
                                // 确保父目录在树中
                                string directoryPath = IOPath.GetDirectoryName(entryPath)?.Replace("\\", "/") ?? "";
                                if (!string.IsNullOrEmpty(directoryPath))
                                {
                                    dispatcher.TryEnqueue(() =>
                                    {
                                        AddFolderToTree(rootTreeNode, directoryPath + "/");
                                    });
                                }

                                // 过滤伪目录项
                                string cleanPath = entryPath.TrimEnd('/');
                                string fileName = cleanPath.Split('/').Last();
                                bool isZero = entry.Size == 0;
                                bool hasExt = !string.IsNullOrEmpty(IOPath.GetExtension(fileName));

                                // 注意：GetTreeNodeByFullPath 需在 UI 线程调用
                                bool isTreeFolder = false;
                                dispatcher.TryEnqueue(() =>
                                {
                                    isTreeFolder = GetTreeNodeByFullPath(cleanPath) != null;
                                });

                                // 等待一次队列以保证 isTreeFolder 更新（避免强同步阻塞）
                                Thread.Yield();

                                if (isZero && !hasExt && isTreeFolder)
                                {
                                    // 跳过伪目录项
                                }
                                else
                                {
                                    var item = CreateFileItem(entry);
                                    item.ParentPath = directoryPath;

                                    dispatcher.TryEnqueue(() =>
                                    {
                                        _allFilesCache.Add(item);
                                    });
                                }
                            }

                            processed++;
                            // 更新进度条与提示
                            dispatcher.TryEnqueue(() =>
                            {
                                bar.IsIndeterminate = false;
                                int percent = total > 0 ? (int)(processed * 100.0 / total) : 0;
                                bar.Value = percent;
                                text.Text = $"正在加载归档… {percent}%（{processed}/{total}）";
                            });
                        }
                    }
                });

                // 加载完成：刷新当前根目录列表
                UpdateFileList(FolderTreeView.RootNodes.FirstOrDefault());

                dialog?.Hide();

                try
                {
                    _currentArchiveComment = await commentTask;
                }
                catch
                {
                    _currentArchiveComment = string.Empty;
                }

                await ShowArchiveCommentDialogAsync(force: false);

                StatusText?.SetValue(TextBlock.TextProperty, $"已打开: {IOPath.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadArchiveAsync Error ({path}): {ex}");
                var fileName = IOPath.GetFileName(path);
                var errorDetail = string.IsNullOrEmpty(fileName)
                    ? ex.Message
                    : $"{fileName}: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorDetail += $" | {ex.InnerException.Message}";
                }
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    errorDetail += "（请确认 7z 文件未损坏或使用受支持的压缩方式）";
                }
                StatusText?.SetValue(TextBlock.TextProperty, $"打开失败: {errorDetail}");
            }
        }

        private void AddFolderToTree(TreeViewNode rootTreeNode, string folderPath)
        {
            folderPath = (folderPath ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrEmpty(folderPath)) return;
            var parts = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentTreeNode = rootTreeNode;
            foreach (var part in parts)
            {
                var foundChild = currentTreeNode.Children.FirstOrDefault(c => (c.Content as FolderNode)?.Name == part);
                if (foundChild != null)
                {
                    currentTreeNode = foundChild;
                }
                else
                {
                    var parentData = currentTreeNode.Content as FolderNode;
                    string newFullPath = string.IsNullOrEmpty(parentData?.FullPath) ? part : parentData.FullPath + "/" + part;
                    var newData = new FolderNode { Name = part, FullPath = newFullPath, IconImage = ShellIconService.GetFolderIcon(small: true) };
                    var newTreeNode = new TreeViewNode { Content = newData };
                    currentTreeNode.Children.Add(newTreeNode);
                    currentTreeNode = newTreeNode;
                }
            }
        }

        private void AddToHistory(string path)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string historyRaw = localSettings.Values[RecentFilesKey] as string ?? "";
            var list = historyRaw.Split('|').Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (list.Contains(path)) list.Remove(path);
            list.Insert(0, path);
            if (list.Count > 10) list = list.Take(10).ToList();
            localSettings.Values[RecentFilesKey] = string.Join("|", list);
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values.Remove(RecentFilesKey);
        }

        private FileItem CreateFileItem(SharpCompress.Archives.IArchiveEntry entry)
        {
            string cleanPath = (entry.Key ?? string.Empty).Replace("\\", "/");
            string fileName = cleanPath.Split('/').LastOrDefault();
            if (string.IsNullOrWhiteSpace(fileName)) fileName = entry.Key;
            string ext = IOPath.GetExtension(fileName)?.ToLowerInvariant();
            return new FileItem
            {
                Name = fileName,
                Size = FormatSize(entry.Size),
                OriginalSize = entry.Size,
                CompressedSize = FormatSize(entry.CompressedSize),
                ModifiedTime = entry.LastModifiedTime?.ToString("yyyy年M月d日") ?? "-",
                Type = string.IsNullOrEmpty(ext) ? "文件" : (ext.Replace(".", "").ToUpper() + " 文件"),
                FullPath = entry.Key,
                ParentPath = string.Empty,
                CompressionMethod = entry.CompressionType.ToString(),
                EncryptionMethod = entry.IsEncrypted ? "已加密" : "未加密",
                CrcCheck = FormatCrc(entry.Crc),
                FileAttributes = FormatAttributes(entry.Attrib),
                Remarks = string.IsNullOrWhiteSpace(entry.LinkTarget)
                    ? entry.ArchivedTime?.ToString("yyyy年M月d日 HH:mm") ?? "-"
                    : entry.LinkTarget,
                ExtraInfo = BuildExtraInfo(entry),
                IsFolder = entry.IsDirectory
            };
        }

        private bool EnsureWritableArchive()
        {
            if (string.IsNullOrEmpty(_currentArchivePath) || !File.Exists(_currentArchivePath))
            {
                StatusText?.SetValue(TextBlock.TextProperty, "未打开任何归档");
                return false;
            }
            var ext = IOPath.GetExtension(_currentArchivePath)?.ToLowerInvariant();
            if (ext != ".zip" && ext != ".7z")
            {
                StatusText?.SetValue(TextBlock.TextProperty, "当前仅支持 ZIP 和 7z 写入操作");
                return false;
            }
            return true;
        }

        private void FolderTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args?.InvokedItem is TreeViewNode node)
            {
                UpdateFileList(node);
            }
        }

        private string GenerateUniqueFolderName(string parentPath, string baseName)
        {
            var siblings = _allFilesCache
                .Where(f => string.Equals(f.ParentPath, parentPath ?? "", StringComparison.Ordinal))
                .Select(f => f.Name)
                .Concat(
                    FolderTreeView.SelectedNode?.Children
                        .Select(c => (c.Content as FolderNode)?.Name)
                        .Where(n => !string.IsNullOrEmpty(n)) ?? Enumerable.Empty<string>()
                )
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string name = baseName;
            int i = 1;
            while (siblings.Contains(name))
            {
                name = $"{baseName} ({i++})";
            }
            return name;
        }

        private string GetCurrentFolderPath()
        {
            var node = FolderTreeView.SelectedNode;
            if (node?.Content is FolderNode fn) return fn.FullPath;
            return "";
        }

        private void LoadArchive(string path)
        {
            _ = LoadArchiveAsync(path);
        }

        private async Task RebuildZipArchive(
            List<(string path, Func<Stream> factory)> additions,
            IEnumerable<string> removals,
            List<string> ensureDirectories)
        {
            var removeSet = new HashSet<string>(removals.Select(NormalizePath), StringComparer.Ordinal);
            var tempPath = IOPath.Combine(IOPath.GetTempPath(), $"fz_{Guid.NewGuid():N}.zip");
            var writtenKeys = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                using var outStream = File.Create(tempPath);
                using var zip = ZipArchive.Create();
                using (var archive = ArchiveFactory.Open(_currentArchivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var key = NormalizePath(entry.Key);
                        bool shouldRemove = removeSet.Any(r =>
                            key.Equals(r, StringComparison.Ordinal) ||
                            (r.EndsWith("/") && key.StartsWith(r, StringComparison.Ordinal)) ||
                            (r.Length > 0 && r[^1] == '/' && key.StartsWith(r, StringComparison.Ordinal)));
                        if (shouldRemove) continue;
                        if (entry.IsDirectory) continue;
                        if (!writtenKeys.Add(key)) continue;
                        var es = new MemoryStream();
                        entry.WriteTo(es);
                        es.Position = 0;
                        zip.AddEntry(key, es, true);
                    }
                }
                if (additions != null)
                {
                    foreach (var add in additions)
                    {
                        var key = NormalizePath(add.path);
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        if (removeSet.Contains(key)) continue;
                        if (!writtenKeys.Add(key)) continue;
                        var s = add.factory.Invoke();
                        if (s.CanSeek) s.Position = 0;
                        zip.AddEntry(key, s, true);
                    }
                }
                var writerOptions = new WriterOptions(CompressionType.Deflate)
                {
                    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 }
                };
                zip.SaveTo(outStream, writerOptions);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Copy(tempPath, _currentArchivePath, overwrite: true); } catch { }
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        private void UpdateFileList(TreeViewNode currentNode)
        {
            if (currentNode == null || currentNode.Content is not FolderNode currentFolderData) return;
            FileItems.Clear();
            foreach (var childNode in currentNode.Children)
            {
                if (childNode.Content is FolderNode childFolderData)
                {
                    FileItems.Add(new FileItem
                    {
                        Name = childFolderData.Name,
                        Size = "",
                        OriginalSize = 0,
                        CompressedSize = "",
                        ModifiedTime = "-",
                        Type = "文件夹",
                        IconImage = ShellIconService.GetFolderIcon(small: true),
                        FullPath = childFolderData.FullPath,
                        ParentPath = currentFolderData.FullPath,
                        CompressionMethod = string.Empty,
                        EncryptionMethod = string.Empty,
                        CrcCheck = string.Empty,
                        FileAttributes = string.Empty,
                        Remarks = string.Empty,
                        ExtraInfo = string.Empty,
                        IsFolder = true
                    });
                }
            }
            var currentPath = currentFolderData.FullPath ?? "";
            var files = _allFilesCache.Where(f => string.Equals(f.ParentPath ?? "", currentPath, StringComparison.Ordinal));
            foreach (var file in files)
            {
                var ext = IOPath.GetExtension(file.Name);
                file.IconImage = ShellIconService.GetFileIconByExtension(ext, small: true);
                FileItems.Add(file);
            }
            StatusText?.SetValue(TextBlock.TextProperty, $"选中: {currentFolderData.Name} - 共 {FileItems.Count} 个项目");
            UpdatePreviewPane();
        }

        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            double number = bytes;
            while (Math.Abs(number) >= 1024 && counter < suffixes.Length - 1)
            {
                counter++;
                number /= 1024d;
            }
            return $"{number:0.##} {suffixes[counter]}";
        }

        private static string NormalizePath(string p)
        {
            p = p?.Replace("\\", "/") ?? string.Empty;
            if (p.StartsWith("/")) p = p.TrimStart('/');
            return p;
        }

        private static string FormatCrc(long crc) => crc > 0 ? $"0x{crc:X8}" : "-";

        private static string FormatAttributes(int? attrib)
        {
            if (attrib is int value)
            {
                try
                {
                    return ((System.IO.FileAttributes)value).ToString();
                }
                catch
                {
                    return $"0x{value:X}";
                }
            }
            return "-";
        }

        private static string BuildExtraInfo(SharpCompress.Common.IEntry entry)
        {
            var parts = new List<string>();
            if (entry.IsSolid) parts.Add("Solid");
            if (entry.IsSplitAfter) parts.Add("Split");
            if (entry.VolumeIndexFirst >= 0)
            {
                var range = entry.VolumeIndexLast > entry.VolumeIndexFirst
                    ? $"{entry.VolumeIndexFirst}-{entry.VolumeIndexLast}"
                    : entry.VolumeIndexFirst.ToString();
                parts.Add($"Vol {range}");
            }
            return parts.Count == 0 ? string.Empty : string.Join(" | ", parts);
        }

        private static string TryReadArchiveComment(string path)
        {
            try
            {
                var ext = IOPath.GetExtension(path)?.ToLowerInvariant();
                if (string.Equals(ext, ".zip", StringComparison.Ordinal))
                {
                    return TryReadZipComment(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryReadArchiveComment error: {ex}");
            }
            return string.Empty;
        }

        private static string TryReadZipComment(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                if (stream.Length < 22)
                {
                    return string.Empty;
                }

                long searchLength = Math.Min(ushort.MaxValue + 22L, stream.Length);
                stream.Seek(-searchLength, SeekOrigin.End);
                var buffer = new byte[searchLength];
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read < 22)
                {
                    return string.Empty;
                }

                for (int i = read - 22; i >= 0; i--)
                {
                    if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B && buffer[i + 2] == 0x05 && buffer[i + 3] == 0x06)
                    {
                        int commentLength = buffer[i + 20] | (buffer[i + 21] << 8);
                        int commentStart = i + 22;
                        if (commentStart + commentLength <= read)
                        {
                            if (commentLength <= 0)
                            {
                                return string.Empty;
                            }

                            var span = buffer.AsSpan(commentStart, commentLength);
                            return DecodeZipComment(span);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryReadZipComment error: {ex}");
            }

            return string.Empty;
        }

        private static string DecodeZipComment(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return string.Empty;

            EnsureCodePageProvider();

            var strictCandidates = new Encoding?[]
            {
                new UTF8Encoding(false, true),
                GetEncodingOrNull(936, strict: true),   // GBK/GB2312
                GetEncodingOrNull(950, strict: true),   // Big5
                GetEncodingOrNull(932, strict: true),   // Shift-JIS
                GetEncodingOrNull(949, strict: true),   // Korean
                GetEncodingOrNull(1252, strict: true)   // Western Latin
            };

            foreach (var enc in strictCandidates)
            {
                if (enc == null) continue;
                try
                {
                    return enc.GetString(data);
                }
                catch (DecoderFallbackException)
                {
                    // try next encoding
                }
                catch (ArgumentException)
                {
                }
            }

            var lenientCandidates = new Encoding?[]
            {
                Encoding.UTF8,
                GetEncodingOrNull(936, strict: false),
                GetEncodingOrNull(950, strict: false),
                GetEncodingOrNull(932, strict: false),
                GetEncodingOrNull(949, strict: false),
                GetEncodingOrNull(1252, strict: false)
            };

            foreach (var enc in lenientCandidates)
            {
                if (enc == null) continue;
                try
                {
                    return enc.GetString(data);
                }
                catch
                {
                }
            }

            return Encoding.UTF8.GetString(data);
        }

        private static Encoding? GetEncodingOrNull(int codePage, bool strict)
        {
            try
            {
                return strict
                    ? Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                    : Encoding.GetEncoding(codePage);
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureCodePageProvider()
        {
            if (_codePagesRegistered) return;
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // ignore if provider already registered by other components
            }
            finally
            {
                _codePagesRegistered = true;
            }
        }

         private void FileListView_ItemClick(object sender, ItemClickEventArgs e)
         {
            if (e.ClickedItem is FileItem item)
            {
                if (StatusText != null)
                {
                    StatusText.Text = $"选中: {item.Name} ({item.Type})";
                }
            }
        }

        private void FileListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (FileListView.SelectedItem is not FileItem item) return;

            if (string.Equals(item.Type, "文件夹", StringComparison.OrdinalIgnoreCase))
            {
                var node = GetTreeNodeByFullPath(item.FullPath);
                if (node != null)
                {
                    ExpandAndSelectNode(node);
                    UpdateFileList(node);

                    if (StatusText != null)
                    {
                        StatusText.Text = $"进入文件夹: {item.Name}";
                    }
                }
            }
            else
            {
                if (StatusText != null)
                {
                    StatusText.Text = $"双击文件: {item.Name}";
                }
            }
        }

        private async void AddFile_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureWritableArchive())
            {
                return;
            }

            string targetFolder = GetCurrentFolderPath() ?? string.Empty;
            AddFilesDialogResult? dialogResult;
            try
            {
                dialogResult = await ShowAddFilesWindowAsync(targetFolder);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddFile_Click dialog error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, "无法打开添加文件窗口");
                return;
            }

            if (dialogResult?.Files == null || dialogResult.Files.Count == 0)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "已取消添加文件");
                return;
            }

            var additions = new List<(string path, Func<Stream> factory)>();
            var removals = new List<string>();
            var removalSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingKeys = new HashSet<string>(
                _allFilesCache.Select(f => NormalizePath(f.FullPath)),
                StringComparer.OrdinalIgnoreCase);

            string normalizedFolder = NormalizePath(targetFolder);
            foreach (var file in dialogResult.Files)
            {
                if (file == null || string.IsNullOrEmpty(file.SourcePath) || !File.Exists(file.SourcePath))
                {
                    continue;
                }

                string relativeKey = string.IsNullOrEmpty(normalizedFolder)
                    ? file.DisplayName
                    : $"{normalizedFolder}/{file.DisplayName}";
                string normalizedKey = NormalizePath(relativeKey);

                if (string.IsNullOrEmpty(normalizedKey))
                {
                    continue;
                }

                if (!removalSet.Contains(normalizedKey) && existingKeys.Contains(normalizedKey))
                {
                    removals.Add(normalizedKey);
                    removalSet.Add(normalizedKey);
                }

                var physicalPath = file.SourcePath;
                additions.Add((normalizedKey, () => File.OpenRead(physicalPath)));
            }

            if (additions.Count == 0)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "未找到可添加的文件");
                return;
            }

            StatusText?.SetValue(TextBlock.TextProperty, "正在添加文件…");

            try
            {
                var ext = IOPath.GetExtension(_currentArchivePath)?.ToLowerInvariant();
                
                if (ext == ".7z")
                {
                    // For 7z archives, use 7za.exe
                    await AddFilesWithSevenZipAsync(_currentArchivePath, additions, CancellationToken.None);
                }
                else
                {
                    // For ZIP archives, use the existing SharpCompress-based method
                    await Task.Run(() => RebuildZipArchive(additions, removals, new List<string>()));
                }

                if (!string.IsNullOrEmpty(_currentArchivePath))
                {
                    await LoadArchiveAsync(_currentArchivePath);
                    var node = GetTreeNodeByFullPath(normalizedFolder);
                    if (node != null)
                    {
                        ExpandAndSelectNode(node);
                        UpdateFileList(node);
                    }
                }

                StatusText?.SetValue(TextBlock.TextProperty, $"已添加 {additions.Count} 个文件");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddFile_Click add error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, $"添加文件失败: {ex.Message}");
            }
        }

        private Task<AddFilesDialogResult?> ShowAddFilesWindowAsync(string destinationDisplay)
        {
            var tcs = new TaskCompletionSource<AddFilesDialogResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                var owner = App.StartupWindow;
                IntPtr ownerHwnd = owner != null ? WindowNative.GetWindowHandle(owner) : IntPtr.Zero;
                var window = new AddFilesWindow(ownerHwnd, destinationDisplay ?? string.Empty);

                void ClosedHandler(object sender, WindowEventArgs args)
                {
                    window.Closed -= ClosedHandler;
                    tcs.TrySetResult(window.ConfirmedResult);
                }

                window.Closed += ClosedHandler;
                window.Activate();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }

        private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!_canDeleteCurrentArchive)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "当前归档格式不支持删除");
                return;
            }

            var snapshot = FileListView?.SelectedItems?.OfType<FileItem>().ToList();
            if (snapshot == null || snapshot.Count == 0)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "未选择要删除的项目");
                return;
            }

            var removalKeys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in snapshot)
            {
                if (item == null) continue;
                var normalized = NormalizePath(item.FullPath ?? string.Empty);
                if (string.IsNullOrEmpty(normalized))
                {
                    continue;
                }

                if (item.IsFolder && !normalized.EndsWith("/", StringComparison.Ordinal))
                {
                    normalized += "/";
                }

                if (seen.Add(normalized))
                {
                    removalKeys.Add(normalized);
                }
            }

            if (removalKeys.Count == 0)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "无法确定要删除的路径");
                return;
            }

            await DeleteArchiveItemsAsync(removalKeys);
        }

        private async void ExtractHere_Click(object sender, RoutedEventArgs e)
        {
            await ExtractToArchiveDirectoryAsync(ExtractDestinationBehavior.Direct);
        }

        private async void SmartExtractHere_Click(object sender, RoutedEventArgs e)
        {
            await ExtractToArchiveDirectoryAsync(ExtractDestinationBehavior.Smart);
        }

        private async void ExtractToFolder_Click(object sender, RoutedEventArgs e)
        {
            await ExtractToArchiveDirectoryAsync(ExtractDestinationBehavior.ForceSubfolder);
        }

        private async void ExtractToDesktop_Click(object sender, RoutedEventArgs e)
        {
            await ExtractToDesktopAsync(ExtractDestinationBehavior.Direct);
        }

        private async void SmartExtractToDesktop_Click(object sender, RoutedEventArgs e)
        {
            await ExtractToDesktopAsync(ExtractDestinationBehavior.Smart);
        }

        private async void ExtractTo_Click(object sender, RoutedEventArgs e)
        {
            await ExtractToCustomFolderAsync();
        }

        private async void OpenArchive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.Downloads,
                    ViewMode = PickerViewMode.List
                };
                picker.FileTypeFilter.Add(".zip");
                picker.FileTypeFilter.Add(".7z");
                picker.FileTypeFilter.Add(".rar");
                picker.FileTypeFilter.Add(".tar");
                picker.FileTypeFilter.Add(".gz");
                picker.FileTypeFilter.Add("*");

                if (App.StartupWindow is not null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.StartupWindow);
                    InitializeWithWindow.Initialize(picker, hwnd);
                }

                var file = await picker.PickSingleFileAsync();
                if (file == null)
                {
                    StatusText?.SetValue(TextBlock.TextProperty, "已取消打开文件");
                    return;
                }

                var path = file.Path;
                if (string.IsNullOrWhiteSpace(path))
                {
                    StatusText?.SetValue(TextBlock.TextProperty, "无法获取文件路径");
                    return;
                }

                AddToHistory(path);
                LoadArchive(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenArchive_Click error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, "打开文件对话框失败");
            }
        }

        private void RenameSelected_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "重命名功能尚未实现");
        }

        private void EditArchiveComment_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "编辑注释功能尚未实现");
        }

        private void CopySelectedToClipboard_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "复制选中项到剪贴板功能尚未实现");
        }

        private void PasteFromClipboard_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "从剪贴板粘贴功能尚未实现");
        }

        private void CopyFullPathToClipboard_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "复制完整路径功能尚未实现");
        }
        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "另存为功能尚未实现");
            UpdateCodePageButtonState(null);
        }
        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "清空列表功能尚未实现");
            UpdateCodePageButtonState(null);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "退出功能尚未实现");
        }

        private TreeViewNode GetTreeNodeByFullPath(string fullPath)
        {
            if (FolderTreeView?.RootNodes == null || FolderTreeView.RootNodes.Count == 0) return null;
            foreach (var root in FolderTreeView.RootNodes)
            {
                var result = FindNodeRecursive(root, fullPath);
                if (result != null) return result;
            }
            return null;
        }

        private TreeViewNode FindNodeRecursive(TreeViewNode node, string targetFullPath)
        {
            if (node?.Content is FolderNode f)
            {
                if (string.Equals(f.FullPath, targetFullPath, StringComparison.Ordinal)) return node;
            }
            if (node == null) return null;
            foreach (var child in node.Children)
            {
                var found = FindNodeRecursive(child, targetFullPath);
                if (found != null) return found;
            }
            return null;
        }

        private void ExpandAndSelectNode(TreeViewNode node)
        {
            if (node == null || FolderTreeView == null) return;
            var current = node;
            while (current != null)
            {
                current.IsExpanded = true;
                current = current.Parent;
            }
            FolderTreeView.SelectedNode = node;
            FolderTreeView.UpdateLayout();
            var container = FolderTreeView.ContainerFromNode(node) as TreeViewItem;
            container?.StartBringIntoView();
        }

        private async Task<(ContentDialog dialog, ProgressBar bar, TextBlock text)> ShowProgressDialogSafeAsync(string title, string message, string primaryButtonText = "正在处理…")
        {
            var hostRoot = (App.StartupWindow?.Content as FrameworkElement)?.XamlRoot ?? this.Content?.XamlRoot;
            if (hostRoot == null)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "无法显示进度窗口：XamlRoot 未就绪");
                return (null, null, null);
            }
            var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
            var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
            var bar = new ProgressBar { IsIndeterminate = false, Minimum = 0, Maximum = 100, Value = 0 };
            panel.Children.Add(text);
            panel.Children.Add(bar);
            var dialog = new ContentDialog
            {
                Title = title,
                Content = panel,
                PrimaryButtonText = primaryButtonText,
                IsPrimaryButtonEnabled = false,
                XamlRoot = hostRoot,
                RequestedTheme = ThemeService.CurrentTheme
            };

            _ = dialog.ShowAsync();
            await Task.Delay(50);
            return (dialog, bar, text);
        }

        private async Task RebuildZipArchiveWithProgress(
            List<(string path, Func<Stream> factory)> additions,
            IEnumerable<string> removals,
            List<string> ensureDirectories,
            IProgress<(int processed, int total)> progress,
            CancellationToken cancellationToken)
        {
            var removeSet = new HashSet<string>(removals.Select(NormalizePath), StringComparer.Ordinal);
            var tempPath = IOPath.Combine(IOPath.GetTempPath(), $"fz_{Guid.NewGuid():N}.zip");
            var writtenKeys = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                using var outStream = File.Create(tempPath);
                using var zip = ZipArchive.Create();
                int total = 0;
                using (var countArchive = ArchiveFactory.Open(_currentArchivePath))
                {
                    foreach (var entry in countArchive.Entries)
                    {
                        var key = NormalizePath(entry.Key);
                        bool shouldRemove = removeSet.Any(r =>
                            key.Equals(r, StringComparison.Ordinal) ||
                            (r.EndsWith("/") && key.StartsWith(r, StringComparison.Ordinal)) ||
                            (r.Length > 0 && r[^1] == '/' && key.StartsWith(r, StringComparison.Ordinal)));
                        if (shouldRemove) continue;
                        if (entry.IsDirectory) continue;
                        total++;
                    }
                }
                int processed = 0;
                using (var archive = ArchiveFactory.Open(_currentArchivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var key = NormalizePath(entry.Key);
                        bool shouldRemove = removeSet.Any(r =>
                            key.Equals(r, StringComparison.Ordinal) ||
                            (r.EndsWith("/") && key.StartsWith(r, StringComparison.Ordinal)) ||
                            (r.Length > 0 && r[^1] == '/' && key.StartsWith(r, StringComparison.Ordinal)));
                        if (shouldRemove) continue;
                        if (entry.IsDirectory) continue;
                        if (!writtenKeys.Add(key)) continue;
                        var es = new MemoryStream();
                        entry.WriteTo(es);
                        es.Position = 0;
                        zip.AddEntry(key, es, true);
                        processed++;
                        progress?.Report((processed, total));
                    }
                }
                if (additions != null)
                {
                    total += additions.Count(a =>
                    {
                        var k = NormalizePath(a.path);
                        return !string.IsNullOrWhiteSpace(k) && !removeSet.Contains(k) && !writtenKeys.Contains(k);
                    });
                    foreach (var add in additions)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var key = NormalizePath(add.path);
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        if (removeSet.Contains(key)) continue;
                        if (!writtenKeys.Add(key)) continue;
                        var s = add.factory.Invoke();
                        if (s.CanSeek) s.Position = 0;
                        zip.AddEntry(key, s, true);
                        processed++;
                        progress?.Report((processed, total));
                    }
                }
                var writerOptions = new WriterOptions(CompressionType.Deflate)
                {
                    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 }
                };
                zip.SaveTo(outStream, writerOptions);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Copy(tempPath, _currentArchivePath, overwrite: true); } catch { }
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        private async Task<bool> DeleteWithSevenZipAsync(string archivePath, List<string> removalKeys, ContentDialog? dialog = null, CancellationToken ct = default)
        {
            try
            {
                var entries = await ListSevenZipEntriesAsync(archivePath, ct);
                if (entries.Count == 0)
                {
                    StatusText?.SetValue(TextBlock.TextProperty, "7z 列表为空或解析失败");
                    return false;
                }
                var toDelete = new HashSet<string>(StringComparer.Ordinal);
                foreach (var key in removalKeys)
                {
                    var k = NormalizePath(key);
                    bool isDirPrefix = k.EndsWith("/");
                    if (isDirPrefix)
                    {
                        foreach (var e in entries)
                        {
                            var en = NormalizePath(e);
                            if (en.StartsWith(k, StringComparison.Ordinal)) toDelete.Add(en);
                        }
                    }
                    else
                    {
                        foreach (var e in entries)
                        {
                            var en = NormalizePath(e);
                            if (string.IsNullOrEmpty(en)) continue;
                            if (string.Equals(en, k, StringComparison.Ordinal)) toDelete.Add(en);
                        }
                    }
                }
                if (toDelete.Count == 0)
                {
                    StatusText?.SetValue(TextBlock.TextProperty, "未匹配到需要删除的 7z 项");
                    return false;
                }
                var listFile = IOPath.Combine(IOPath.GetTempPath(), $"fz_del_{Guid.NewGuid():N}.txt");
                await File.WriteAllLinesAsync(listFile, toDelete.Select(s => s.Replace("\\", "/")), Encoding.UTF8, ct);
                var args = $"d \"{archivePath}\" @{listFile} -r -y";
                var ok = await RunSevenZipAsync(args, ct);
                try { File.Delete(listFile); } catch { }
                return ok;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteWithSevenZipAsync error: {ex}");
                return false;
            }
        }

        private async Task<bool> AddFilesWithSevenZipAsync(
            string archivePath,
            List<(string path, Func<Stream> factory)> additions,
            CancellationToken ct = default)
        {
            try
            {
                if (additions == null || additions.Count == 0)
                {
                    return false;
                }

                // Create a temporary directory for the files to add
                var tempDir = IOPath.Combine(IOPath.GetTempPath(), $"fz_add_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Write all files to the temp directory maintaining their structure
                    foreach (var add in additions)
                    {
                        var key = NormalizePath(add.path);
                        if (string.IsNullOrWhiteSpace(key)) continue;

                        var tempFilePath = IOPath.Combine(tempDir, key.Replace("/", "\\"));
                        var tempFileDir = IOPath.GetDirectoryName(tempFilePath);
                        if (!string.IsNullOrEmpty(tempFileDir))
                        {
                            Directory.CreateDirectory(tempFileDir);
                        }

                        using (var sourceStream = add.factory.Invoke())
                        using (var destStream = File.Create(tempFilePath))
                        {
                            if (sourceStream.CanSeek) sourceStream.Position = 0;
                            await sourceStream.CopyToAsync(destStream, ct);
                        }
                    }

                    // Build the 7za command to add files
                    // Use "u" (update) command which adds new files and updates existing ones
                    // Set working directory to tempDir so files are added with relative paths
                    var args = $"u \"{archivePath}\" * -r -y";
                    
                    var ok = await RunSevenZipAsync(args, tempDir, ct);
                    return ok;
                }
                finally
                {
                    // Clean up temp directory
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddFilesWithSevenZipAsync error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, $"7z 添加文件失败: {ex.Message}");
                return false;
            }
        }

        private void ShowInfo_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "此功能尚未实现");
        }

        private void TestArchive_Click(object sender, RoutedEventArgs e)
        {
            StatusText?.SetValue(TextBlock.TextProperty, "测试归档完整性功能尚未实现");
        }

        private string? GetSevenZipExePath()
        {
            var baseDir = AppContext.BaseDirectory;
            string[] archCandidates =
            {
                IsArm64() ? IOPath.Combine(baseDir, "arm64", "7za.exe") : null,
                IsX64()   ? IOPath.Combine(baseDir, "x64",   "7za.exe") : null,
                IOPath.Combine(baseDir, "7za.exe")
            };
            foreach (var p in archCandidates)
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
            }
            string[] fallback =
            {
                IOPath.Combine(baseDir, "Assets", "SevenZip", "arm64", "7za.exe"),
                IOPath.Combine(baseDir, "Assets", "SevenZip", "x64", "7za.exe"),
                IOPath.Combine(baseDir, "Assets", "SevenZip", "7za.exe")
            };
            foreach (var p in fallback)
            {
                if (File.Exists(p)) return p;
            }
            try
            {
                var paths = Environment.GetEnvironmentVariable("PATH")?.Split(IOPath.PathSeparator) ?? Array.Empty<string>();
                foreach (var p in paths)
                {
                    var cand = IOPath.Combine(p.Trim('"'), "7za.exe");
                    if (File.Exists(cand)) return cand;
                }
            }
            catch { }
            return null;
        }

        private async Task<bool> RunSevenZipAsync(string arguments, CancellationToken ct = default)
        {
            return await RunSevenZipAsync(arguments, null, ct);
        }

        private async Task<bool> RunSevenZipAsync(string arguments, string? workingDirectory, CancellationToken ct = default)
        {
            var exe = GetSevenZipExePath();
            if (exe == null)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "未找到 7za.exe，请将 7-Zip Extra 的 7za 文件随应用发布（含 x64/arm64 目录）。");
                return false;
            }
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory ?? IOPath.GetDirectoryName(exe) ?? AppContext.BaseDirectory
            };
            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            if (!proc.Start())
            {
                StatusText?.SetValue(TextBlock.TextProperty, "启动 7za 失败");
                return false;
            }
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) stdOut.AppendLine(e.Data); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stdErr.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await Task.Run(() =>
            {
                while (!proc.HasExited)
                {
                    if (ct.IsCancellationRequested)
                    {
                        try { proc.Kill(); } catch { }
                        break;
                    }
                    Thread.Sleep(50);
                }
            });
            var code = proc.ExitCode;
            if (code != 0)
            {
                Debug.WriteLine($"7za exit {code}");
                StatusText?.SetValue(TextBlock.TextProperty, $"7za 命令失败 (ExitCode={code})");
                return false;
            }
            return true;
        }

        private async Task<List<string>> ListSevenZipEntriesAsync(string archivePath, CancellationToken ct = default)
        {
            var exe = GetSevenZipExePath();
            if (exe == null)
            {
                StatusText?.SetValue(TextBlock.TextProperty, "未找到 7za.exe");
                return new List<string>();
            }
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"l -slt \"{archivePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = IOPath.GetDirectoryName(exe) ?? AppContext.BaseDirectory
            };
            using var proc = new Process { StartInfo = psi };
            var entries = new List<string>();
            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                var line = await proc.StandardOutput.ReadLineAsync();
                if (line == null) break;
                if (line.StartsWith("Path = ", StringComparison.Ordinal))
                {
                    var path = line.Substring("Path = ".Length).Trim();
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = path.Replace("\\", "/");
                        if (path.StartsWith("/")) path = path.TrimStart('/');
                        entries.Add(path);
                    }
                }
            }
            await Task.Run(() =>
            {
                while (!proc.HasExited) { Thread.Sleep(10); }
            });
            return entries.Distinct(StringComparer.Ordinal).ToList();
        }

        private void HeaderResizeGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (HeaderGrid?.ColumnDefinitions == null) return;
            if (sender is not FrameworkElement grip) return;
            if (!TryParseColumnIndex(grip.Tag, out int columnIndex)) return;
            if (columnIndex < 1 || columnIndex >= HeaderGrid.ColumnDefinitions.Count) return;

            var pt = e.GetCurrentPoint(HeaderGrid).Position;
            _resizeColIndex = columnIndex;
            _resizeStartX = pt.X;
            var col = HeaderGrid.ColumnDefinitions[columnIndex];
            _resizeStartWidth = col.Width.IsAbsolute ? col.Width.Value : (col.ActualWidth > 0 ? col.ActualWidth : GetColumnFallbackWidth(columnIndex));

            _isHeaderResizing = true;
            grip.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void HeaderResizeGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isHeaderResizing || HeaderGrid == null || _resizeColIndex < 1) return;
            var pt = e.GetCurrentPoint(HeaderGrid).Position;
            double delta = pt.X - _resizeStartX;
            var col = HeaderGrid.ColumnDefinitions[_resizeColIndex];
            double newWidth = Math.Max(GetColumnMinWidth(_resizeColIndex), _resizeStartWidth + delta);
            col.Width = new GridLength(newWidth, GridUnitType.Pixel);
            e.Handled = true;
        }

        private void HeaderResizeGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement grip)
            {
                try { grip.ReleasePointerCaptures(); } catch { }
            }
            _isHeaderResizing = false;
            _resizeColIndex = -1;
            _resizeStartX = 0;
            _resizeStartWidth = 0;
            e.Handled = true;
        }

        private void NewArchive_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现新建归档的逻辑
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e?.Parameter is string path && !string.IsNullOrWhiteSpace(path))
            {
                if (!string.Equals(_currentArchivePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    _ = LoadArchiveAsync(path);
                }
            }
        }

        private async void OpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var uri = new Uri("https://fluentzip.app");
                await Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenWebsite_Click error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, "无法打开官网链接");
            }
        }

        private async void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new SettingWindow();
                window.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenSettings_Click error: {ex}");
                StatusText?.SetValue(TextBlock.TextProperty, "无法打开设置窗口");
            }
        }

        private void TreePaneToggle_Checked(object sender, RoutedEventArgs e)
        {
            SetTreePaneVisibility(true);
        }

        private void TreePaneToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            SetTreePaneVisibility(false);
        }

        private void SetTreePaneVisibility(bool isVisible, bool useAnimation = true)
        {
            if (!_treePaneInitialized)
            {
                ApplyTreePaneVisibilityInstant(isVisible);
                _treePaneInitialized = true;
                return;
            }

            if (!useAnimation)
            {
                ApplyTreePaneVisibilityInstant(isVisible);
                return;
            }

            _ = UpdateTreePaneVisibilityAsync(isVisible);
        }

        private async Task UpdateTreePaneVisibilityAsync(bool isVisible)
        {
            if (TreeColumn == null || FolderSplitter == null || FolderTreeView == null)
            {
                return;
            }

            double collapsedWidth = GetCollapsedTreePaneWidth();
            double currentWidth = GetTreeColumnCurrentWidth();

            if (isVisible)
            {
                FolderTreeView.Visibility = Visibility.Visible;
                FolderSplitter.Visibility = Visibility.Visible;
                TreeColumn.MinWidth = _savedTreeColumnMinWidth > 0 ? _savedTreeColumnMinWidth : 150;
                double fallbackWidth = _savedTreeColumnWidth > 0 ? _savedTreeColumnWidth : 260;
                double targetWidth = Math.Max(fallbackWidth, collapsedWidth);
                await RunTreePaneAnimationAsync(currentWidth, targetWidth);
            }
            else
            {
                _savedTreeColumnWidth = currentWidth > 0 ? currentWidth : _savedTreeColumnWidth;
                _savedTreeColumnMinWidth = TreeColumn.MinWidth > 0 ? TreeColumn.MinWidth : _savedTreeColumnMinWidth;
                TreeColumn.MinWidth = 0;
                await RunTreePaneAnimationAsync(currentWidth, collapsedWidth);
                FolderTreeView.Visibility = Visibility.Collapsed;
                FolderSplitter.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyTreePaneVisibilityInstant(bool isVisible)
        {
            if (TreeColumn == null || FolderSplitter == null || FolderTreeView == null)
            {
                return;
            }

            double collapsedWidth = GetCollapsedTreePaneWidth();
            if (isVisible)
            {
                FolderTreeView.Visibility = Visibility.Visible;
                FolderSplitter.Visibility = Visibility.Visible;
                TreeColumn.MinWidth = _savedTreeColumnMinWidth > 0 ? _savedTreeColumnMinWidth : 150;
                double width = _savedTreeColumnWidth > 0 ? _savedTreeColumnWidth : 260;
                UpdateTreeColumnWidth(Math.Max(width, collapsedWidth));
            }
            else
            {
                _savedTreeColumnWidth = GetTreeColumnCurrentWidth();
                _savedTreeColumnMinWidth = TreeColumn.MinWidth > 0 ? TreeColumn.MinWidth : _savedTreeColumnMinWidth;
                TreeColumn.MinWidth = 0;
                UpdateTreeColumnWidth(collapsedWidth);
                FolderTreeView.Visibility = Visibility.Collapsed;
                FolderSplitter.Visibility = Visibility.Collapsed;
            }
        }

        private double GetTreeColumnCurrentWidth()
        {
            if (TreeColumn == null) return 0;
            if (TreeColumn.ActualWidth > 0) return TreeColumn.ActualWidth;
            if (TreeColumn.Width.IsAbsolute) return TreeColumn.Width.Value;
            return _savedTreeColumnWidth > 0 ? _savedTreeColumnWidth : 260;
        }

        private void UpdateTreeColumnWidth(double width)
        {
            if (TreeColumn == null) return;
            double clamped = Math.Max(0, width);
            TreeColumn.Width = new GridLength(clamped, GridUnitType.Pixel);
        }

        private double GetCollapsedTreePaneWidth()
        {
            double containerWidth = TreeToggleContainerHost?.ActualWidth ?? 0;
            double maskWidth = TreeToggleMaskElement?.ActualWidth ?? 0;
            containerWidth = Math.Max(containerWidth, maskWidth);

            if (containerWidth <= 0)
            {
                double buttonWidth = ToggleTreeButton?.ActualWidth ?? ToggleTreeButton?.Width ?? 44;
                containerWidth = buttonWidth + 24;
            }

            double containerMargin = TreeToggleContainerHost?.Margin.Left + TreeToggleContainerHost?.Margin.Right ?? 0;
            double minWidth = TreeToggleContainerHost?.MinWidth ?? 68;
            return Math.Max(containerWidth + containerMargin, minWidth);
        }

        private async Task RunTreePaneAnimationAsync(double from, double to)
        {
            if (Math.Abs(from - to) < 0.5)
            {
                UpdateTreeColumnWidth(to);
                return;
            }

            _treePaneAnimationCts?.Cancel();
            var cts = new CancellationTokenSource();
            _treePaneAnimationCts = cts;
            try
            {
                await AnimateTreePaneAsync(from, to, cts.Token);
            }
            catch (TaskCanceledException)
            {
            }
        }

        private async Task AnimateTreePaneAsync(double from, double to, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            double duration = TreePaneAnimationDuration.TotalMilliseconds;

            while (!token.IsCancellationRequested)
            {
                double progress = Math.Min(1.0, stopwatch.Elapsed.TotalMilliseconds / duration);
                double eased = EaseInOut(progress);
                double value = from + (to - from) * eased;
                UpdateTreeColumnWidth(value);

                if (progress >= 1.0)
                {
                    break;
                }

                await Task.Delay(16, token);
            }

            UpdateTreeColumnWidth(to);
        }

        private static double EaseInOut(double t)
        {
            return t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        private void TogglePreviewPane_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = sender switch
            {
                ToggleMenuFlyoutItem menuItem => menuItem.IsChecked,
                ToggleButton toggleButton => toggleButton.IsChecked == true,
                _ => true
            };
            SetPreviewPaneVisibility(isChecked);
        }

        private async void ShowArchiveComment_Click(object sender, RoutedEventArgs e)
        {
            await ShowArchiveCommentDialogAsync(force: true);
        }

        private void SetPreviewPaneVisibility(bool isVisible)
        {
            _isPreviewPaneVisible = isVisible;

            if (!_isUpdatingPreviewToggle)
            {
                _isUpdatingPreviewToggle = true;
                try
                {
                    if (ToggleImagePreviewItem != null && ToggleImagePreviewItem.IsChecked != isVisible)
                    {
                        ToggleImagePreviewItem.IsChecked = isVisible;
                    }
                    var quickToggle = PreviewPaneToggleButtonHost;
                    if (quickToggle != null && quickToggle.IsChecked != isVisible)
                    {
                        quickToggle.IsChecked = isVisible;
                    }
                }
                finally
                {
                    _isUpdatingPreviewToggle = false;
                }
            }

            if (PreviewColumn == null || PreviewPane == null || PreviewSplitter == null)
            {
                return;
            }

            if (isVisible)
            {
                PreviewPane.Visibility = Visibility.Visible;
                PreviewSplitter.Visibility = Visibility.Visible;
                double targetWidth = _savedPreviewColumnWidth > 0 ? _savedPreviewColumnWidth : 320;
                PreviewColumn.MinWidth = 200;
                PreviewColumn.Width = new GridLength(targetWidth, GridUnitType.Pixel);
                UpdatePreviewPane();
            }
            else
            {
                if (PreviewColumn.ActualWidth > 0)
                {
                    _savedPreviewColumnWidth = PreviewColumn.ActualWidth;
                }
                PreviewColumn.Width = new GridLength(0, GridUnitType.Pixel);
                PreviewColumn.MinWidth = 0;
                PreviewPane.Visibility = Visibility.Collapsed;
                PreviewSplitter.Visibility = Visibility.Collapsed;
                _previewCts?.Cancel();
                SetPreviewLoadingState(false);
            }
        }

        private void UpdatePreviewPane()
        {
            if (!_isPreviewPaneVisible || PreviewPane == null)
            {
                return;
            }

            if (FileListView?.SelectedItems == null || FileListView.SelectedItems.Count == 0)
            {
                ShowPreviewPlaceholder("选择图像文件以查看预览");
                return;
            }

            if (FileListView.SelectedItems.Count > 1)
            {
                ShowPreviewPlaceholder("一次仅支持预览单个图像文件");
                return;
            }

            if (FileListView.SelectedItem is not FileItem item)
            {
                ShowPreviewPlaceholder("选择图像文件以查看预览");
                return;
            }

            if (!IsPreviewableImage(item))
            {
                ShowPreviewPlaceholder("所选项目不支持图像预览");
                return;
            }

            _ = LoadPreviewImageAsync(item);
        }

        private static bool IsPreviewableImage(FileItem item)
        {
            if (item == null) return false;
            var ext = IOPath.GetExtension(item.Name ?? string.Empty);
            return !string.IsNullOrEmpty(ext) && _previewableImageExtensions.Contains(ext);
        }

        private void ShowPreviewPlaceholder(string message)
        {
            _previewCts?.Cancel();
            SetPreviewLoadingState(false);
            if (PreviewPlaceholder != null)
            {
                PreviewPlaceholder.Visibility = Visibility.Visible;
            }
            if (PreviewInfoText != null)
            {
                PreviewInfoText.Text = message;
            }
            if (PreviewImage != null)
            {
                PreviewImage.Source = null;
                PreviewImage.Visibility = Visibility.Collapsed;
            }
            if (PreviewMetaText != null)
            {
                PreviewMetaText.Text = string.Empty;
            }
        }

        private void SetPreviewLoadingState(bool isLoading)
        {
            if (PreviewLoadingRing == null)
            {
                return;
            }
            PreviewLoadingRing.IsActive = isLoading;
            PreviewLoadingRing.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task LoadPreviewImageAsync(FileItem item)
        {
            _previewCts?.Cancel();
            var cts = new CancellationTokenSource();
            _previewCts = cts;
            var token = cts.Token;

            if (PreviewPlaceholder != null)
            {
                PreviewPlaceholder.Visibility = Visibility.Collapsed;
            }
            if (PreviewImage != null)
            {
                PreviewImage.Visibility = Visibility.Collapsed;
                PreviewImage.Source = null;
            }
            if (PreviewMetaText != null)
            {
                PreviewMetaText.Text = string.Empty;
            }
            SetPreviewLoadingState(true);

            try
            {
                var stream = await Task.Run(() => ReadEntryStream(item, token), token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (stream == null)
                {
                    ShowPreviewPlaceholder("无法加载图像预览");
                    return;
                }

                using (stream)
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (PreviewImage != null)
                    {
                        PreviewImage.Source = bitmap;
                        PreviewImage.Visibility = Visibility.Visible;
                    }
                    if (PreviewPlaceholder != null)
                    {
                        PreviewPlaceholder.Visibility = Visibility.Collapsed;
                    }
                    if (PreviewMetaText != null)
                    {
                        PreviewMetaText.Text = BuildPreviewMeta(item, bitmap);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPreviewImageAsync error: {ex}");
                ShowPreviewPlaceholder("无法加载图像预览");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    SetPreviewLoadingState(false);
                }
            }
        }

        private IRandomAccessStream? ReadEntryStream(FileItem item, CancellationToken token)
        {
            if (item == null || string.IsNullOrEmpty(item.FullPath) || string.IsNullOrEmpty(_currentArchivePath) || !File.Exists(_currentArchivePath))
            {
                return null;
            }

            var target = NormalizePath(item.FullPath);
            using var archive = ArchiveFactory.Open(_currentArchivePath);
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (entry.IsDirectory)
                {
                    continue;
                }

                if (string.Equals(NormalizePath(entry.Key), target, StringComparison.OrdinalIgnoreCase))
                {
                    var ms = new MemoryStream();
                    entry.WriteTo(ms);
                    token.ThrowIfCancellationRequested();
                    ms.Position = 0;
                    return ms.AsRandomAccessStream();
                }
            }

            return null;
        }

        private static string BuildPreviewMeta(FileItem item, BitmapImage bitmap)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(item?.Name))
            {
                parts.Add(item.Name);
            }

            var sizeText = !string.IsNullOrEmpty(item?.Size) ? item.Size : FormatSize(item?.OriginalSize ?? 0);
            if (!string.IsNullOrEmpty(sizeText))
            {
                parts.Add(sizeText);
            }

            if (bitmap != null && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
            {
                parts.Add($"{bitmap.PixelWidth}×{bitmap.PixelHeight}px");
            }

            return parts.Count == 0 ? string.Empty : string.Join("  •  ", parts);
        }

        private async Task ShowArchiveCommentDialogAsync(bool force)
        {
            if (!force && string.IsNullOrWhiteSpace(_currentArchiveComment))
            {
                return;
            }

            var hostRoot = (App.StartupWindow?.Content as FrameworkElement)?.XamlRoot ?? this.Content?.XamlRoot;
            if (hostRoot == null)
            {
                return;
            }

            string text = string.IsNullOrWhiteSpace(_currentArchiveComment)
                ? "当前压缩文件没有注释。"
                : _currentArchiveComment.Trim();

            var contentText = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460
            };

            var container = new ScrollViewer
            {
                Content = contentText,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 320,
                Padding = new Thickness(0)
            };

            var dialog = new ContentDialog
            {
                Title = "压缩文件注释",
                Content = container,
                CloseButtonText = "关闭",
                XamlRoot = hostRoot,
                RequestedTheme = ThemeService.CurrentTheme
            };

            await dialog.ShowAsync();
        }

        private void ChangeViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioMenuFlyoutItem item)
            {
                ApplyViewMode(item.Tag?.ToString());
            }
        }

        private void ApplyViewMode(string? modeKey)
        {
            if (FileListView == null) return;
            _currentViewMode = modeKey ?? "Detail";

            switch (_currentViewMode)
            {
                case "List":
                    FileListView.ItemTemplate = Resources["ListItemTemplate"] as DataTemplate;
                    FileListView.ItemsPanel = Resources["VerticalListPanel"] as ItemsPanelTemplate;
                    break;
                case "SmallIcon":
                    FileListView.ItemTemplate = Resources["SmallIconItemTemplate"] as DataTemplate;
                    FileListView.ItemsPanel = Resources["WrapPanelTemplate"] as ItemsPanelTemplate;
                    break;
                case "LargeIcon":
                    FileListView.ItemTemplate = Resources["LargeIconItemTemplate"] as DataTemplate;
                    FileListView.ItemsPanel = Resources["WrapPanelTemplate"] as ItemsPanelTemplate;
                    break;
                default:
                    _currentViewMode = "Detail";
                    FileListView.ItemTemplate = Resources["DetailItemTemplate"] as DataTemplate;
                    FileListView.ItemsPanel = Resources["VerticalListPanel"] as ItemsPanelTemplate;
                    break;
            }
        }
    }

    public class FolderNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public BitmapImage IconImage { get; set; }
    }

    public class FileItem
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public long OriginalSize { get; set; }
        public string CompressedSize { get; set; }
        public string ModifiedTime { get; set; }
        public string Type { get; set; }
        public BitmapImage IconImage { get; set; }
        public string FullPath { get; set; }
        public string ParentPath { get; set; }
        public string CompressionMethod { get; set; } = string.Empty;
        public string EncryptionMethod { get; set; } = string.Empty;
        public string CrcCheck { get; set; } = string.Empty;
        public string FileAttributes { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string ExtraInfo { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
    }

    internal static class StreamExtensions
    {
        public static Stream Clone(this Stream source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var ms = new MemoryStream();
            long originalPos = 0;
            if (source.CanSeek) { originalPos = source.Position; source.Position = 0; }
            source.CopyTo(ms);
            if (source.CanSeek) source.Position = originalPos;
            ms.Position = 0;
            return ms;
        }
    }
}