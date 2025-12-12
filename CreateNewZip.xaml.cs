using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FluentZip
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CreateNewZip : Window
    {
        public CreateNewZip()
        {
            InitializeComponent(); 
            ExtendsContentIntoTitleBar = true;
            

            if (RootGrid is FrameworkElement root)
            {
                ThemeService.RegisterRoot(root);
                TitleBarThemeHelper.Attach(this, root, AppTitleBar);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: implement file picker logic
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: implement removal logic
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: update selection-dependent UI state
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: finalize creation workflow
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
                System.Diagnostics.Debug.WriteLine($"打开高级设置失败: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
