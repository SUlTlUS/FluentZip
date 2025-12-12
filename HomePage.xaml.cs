using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
// ★★★ 确保引用了这个，否则无法处理窗口句柄 ★★★
using WinRT.Interop;

namespace FluentZip
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            this.InitializeComponent();
        }

        private async void OpenArchive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openPicker = new FileOpenPicker();
                openPicker.ViewMode = PickerViewMode.List;
                openPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                openPicker.FileTypeFilter.Add(".zip");
                openPicker.FileTypeFilter.Add(".7z");
                openPicker.FileTypeFilter.Add(".rar");

                // ★★★ 获取 App.xaml.cs 里存的窗口 ★★★
                var window = App.StartupWindow;
                if (window == null)
                {
                    // 如果这行弹出来了，说明 App.xaml.cs 没改对
                    System.Diagnostics.Debug.WriteLine("窗口句柄为空！");
                    return;
                }

                // 初始化句柄
                var hWnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(openPicker, hWnd);

                var file = await openPicker.PickSingleFileAsync();
                if (file == null || string.IsNullOrWhiteSpace(file.Path))
                {
                    return;
                }

                AddRecentFile(file.Path);
                GetHostFrame()?.Navigate(typeof(ArchiveViewPage), file.Path);

            }
            catch (Exception ex)
            {
                // 如果还不行，这里会捕获错误，请在 VS 的“输出”窗口看打印了什么
                System.Diagnostics.Debug.WriteLine($"出错啦: {ex.Message}");
            }
        }

        // ... NewArchive_Click 代码同理 ...
        private async void NewArchive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new CreateNewZip();
                window.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开新建窗口失败: {ex.Message}");
            }
        }

        private Frame? GetHostFrame()
        {
            if (Frame != null) return Frame;
            return App.StartupWindow?.Content as Frame;
        }

        private static void AddRecentFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var settings = ApplicationData.Current.LocalSettings;
            var existing = (settings.Values["RecentOpenedFiles"] as string ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            existing.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            existing.Insert(0, path);
            if (existing.Count > 10)
            {
                existing = existing.Take(10).ToList();
            }

            settings.Values["RecentOpenedFiles"] = string.Join("|", existing);
        }
    }
}