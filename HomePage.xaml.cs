using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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
                if (file != null)
                {
                    GetHostFrame()?.Navigate(typeof(ArchiveViewPage), file.Path);
                }
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
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.SuggestedFileName = "NewArchive";
            savePicker.FileTypeChoices.Add("Zip Compressed File", new List<string>() { ".zip" });

            var window = App.StartupWindow;
            var hWnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(savePicker, hWnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                await Windows.Storage.FileIO.WriteBytesAsync(file, new byte[] { 0x50, 0x4B, 0x05, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                GetHostFrame()?.Navigate(typeof(ArchiveViewPage), file.Path);
            }
        }

        private Frame? GetHostFrame()
        {
            if (Frame != null) return Frame;
            return App.StartupWindow?.Content as Frame;
        }
    }
}