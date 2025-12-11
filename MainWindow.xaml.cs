using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
// ★★★ 新增引用 ★★★
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace FluentZip
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // 1. 设置标题栏
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // 2. 设置 Mica 材质
            SystemBackdrop = new MicaBackdrop();//{ Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt } ;

            // 3. ★★★ 设置窗口大小 (例如: 宽900, 高600) ★★★
            SetWindowSize(900, 600);

            // 4. 导航到主页
            ContentFrame.Navigate(typeof(HomePage));
        }

        // ★★★ 核心方法: 调整窗口大小 ★★★
        private void SetWindowSize(int width, int height)
        {
            // 1. 获取当前窗口的句柄 (HWND)
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // 2. 通过句柄获取 WindowId
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            // 3. 获取 AppWindow 实例
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // 4. 调整大小 (单位是像素)
            appWindow.Resize(new SizeInt32(width, height));

            // (可选) 如果你想让窗口在屏幕居中，可以解开下面这行的注释:
            // CenterWindow(appWindow);
        }

        // (可选) 屏幕居中辅助方法
        /*
        private void CenterWindow(AppWindow appWindow)
        {
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var centeredPosition = appWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
            appWindow.Move(centeredPosition);
        }
        */
    }
}