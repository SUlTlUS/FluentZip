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
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AdvencedSettings : UserControl
    {
        public AdvencedSettings()
        {
            InitializeComponent();
        }

        private void ThreadHelp_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "CPU 线程数",
                Content = "自动模式会根据可用逻辑处理器数在后台选择最优线程数。若需限制资源占用，可在下拉列表中指定固定值。",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };

            _ = dialog.ShowAsync();
        }
    }
}
