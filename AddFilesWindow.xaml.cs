using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FluentZip
{
    public sealed partial class AddFilesWindow : Window
    {
        private readonly ObservableCollection<AddFileCandidate> _items = new();
        private readonly IntPtr _ownerHwnd;
        private ComboBox? _compressionLevelControl;
        private CheckBox? _testArchiveOption;
        private CheckBox? _deleteSourceOption;

        public AddFilesWindow(IntPtr ownerHwnd, string destinationDisplay)
        {
            _ownerHwnd = ownerHwnd;
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            

            // 设置窗口尺寸（也可以在构造函数中设置）
            SetWindowSize();

            // 设置主题
            if (RootGrid != null)
            {
                ThemeService.RegisterRoot(RootGrid);
                TitleBarThemeHelper.Attach(this, RootGrid, AppTitleBar);
            }

            // 更新提示文本
            DestinationHintTextBlock.Text = BuildDestinationHint(destinationDisplay);

            // 设置列表数据源
            FileListView.ItemsSource = _items;

            // 订阅事件
            _items.CollectionChanged += Items_CollectionChanged;
            FileListView.SelectionChanged += FileListView_SelectionChanged;

            // 设置关闭按钮事件
            this.Closed += AddFilesWindow_Closed;
        }

        private ComboBox? CompressionLevelSelector =>
            _compressionLevelControl ??= RootGrid?.FindName("CompressionLevelComboBox") as ComboBox;

        private CheckBox? TestArchiveOption =>
            _testArchiveOption ??= RootGrid?.FindName("TestArchiveCheckBox") as CheckBox;

        private CheckBox? DeleteSourceOption =>
            _deleteSourceOption ??= RootGrid?.FindName("DeleteSourceCheckBox") as CheckBox;

        private void SetWindowSize()
        {
            // 在代码中设置窗口尺寸
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // 设置最小尺寸
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 800, Height = 600 });

            // 设置首选尺寸
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 850, Height = 650 });

            // 或者使用 SetPresenter 来设置大小行为
            // appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.CompactOverlay);
        }

        private void AddFilesWindow_Closed(object sender, WindowEventArgs args)
        {
            // 清理资源
            _items.CollectionChanged -= Items_CollectionChanged;
            FileListView.SelectionChanged -= FileListView_SelectionChanged;
            this.Closed -= AddFilesWindow_Closed;
        }

        internal AddFilesDialogResult? ConfirmedResult { get; private set; }

        private static string BuildDestinationHint(string folder)
        {
            folder = (folder ?? string.Empty).Replace("\\", "/").Trim('/');
            var normalized = string.IsNullOrEmpty(folder) ? "/" : $"/{folder}";
            return $"添加路径: {normalized}";
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            StartButton.IsEnabled = _items.Count > 0;
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemoveButton.IsEnabled = FileListView.SelectedItems?.Count > 0;
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            await AddFilesAsync();
        }

        private async Task AddFilesAsync()
        {
            try
            {
                var picker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.Downloads
                };
                picker.FileTypeFilter.Add("*");

                // 获取当前窗口句柄
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hwnd);

                var files = await picker.PickMultipleFilesAsync();
                if (files == null || files.Count == 0)
                {
                    return;
                }

                foreach (var file in files)
                {
                    if (file == null || string.IsNullOrEmpty(file.Path))
                    {
                        continue;
                    }

                    // 检查是否已存在
                    if (_items.Any(i => string.Equals(i.SourcePath, file.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    long size = 0;
                    try
                    {
                        BasicProperties props = await file.GetBasicPropertiesAsync();
                        size = (long)props.Size;
                    }
                    catch
                    {
                        try
                        {
                            var info = new FileInfo(file.Path);
                            if (info.Exists)
                            {
                                size = info.Length;
                            }
                        }
                        catch
                        {
                            size = 0;
                        }
                    }

                    _items.Add(new AddFileCandidate(file.Name, file.Path, size));
                }
            }
            catch (Exception ex)
            {
                // 可以添加错误处理逻辑
                Console.WriteLine($"添加文件时出错: {ex.Message}");

                // 显示错误信息
                var dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = $"添加文件失败: {ex.Message}",
                    PrimaryButtonText = "确定",
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var snapshot = FileListView.SelectedItems?.OfType<AddFileCandidate>().ToList();
            if (snapshot == null || snapshot.Count == 0)
            {
                return;
            }

            foreach (var item in snapshot)
            {
                _items.Remove(item);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                return;
            }

            ConfirmedResult = new AddFilesDialogResult
            {
                Files = _items.Select(item => new AddFileCandidate(item.DisplayName, item.SourcePath, item.Size)).ToList(),
                CompressionLevel = GetSelectedCompressionLevel(),
                ShouldTestArchive = IsOptionChecked(TestArchiveOption),
                DeleteSourceAfterAdd = IsOptionChecked(DeleteSourceOption)
            };

            // 设置对话框结果并关闭窗口
            this.Close();
        }

        private async void AdvancedSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var control = new AdvencedSettings();
                var dialog = new ContentDialog
                {
                    Title = "高级设置",
                    Content = control,
                    CloseButtonText = "关闭",
                    XamlRoot = (RootGrid ?? Content as FrameworkElement)?.XamlRoot,
                    RequestedTheme = ThemeService.CurrentTheme
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Advanced settings dialog failed: {ex.Message}");
            }
        }

        private static bool IsOptionChecked(CheckBox? option) => option?.IsChecked == true;

        private int GetSelectedCompressionLevel()
        {
            if (CompressionLevelSelector?.SelectedItem is ComboBoxItem comboItem)
            {
                if (comboItem.Tag is int raw)
                {
                    return raw;
                }

                if (comboItem.Tag is string tagString && int.TryParse(tagString, out var parsed))
                {
                    return parsed;
                }
            }

            return 2;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 取消操作，关闭窗口
            ConfirmedResult = null;
            this.Close();
        }
    }
}