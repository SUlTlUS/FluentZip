using Microsoft.UI.Xaml;

namespace FluentZip
{
    public partial class App : Application
    {
        // ★★★ 1. 定义全局静态属性，让任何页面都能获取主窗口 ★★★
        public static MainWindow StartupWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
            // test comment
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            // ★★★ 2. 这里必须赋值，否则 HomePage 里会报空指针，导致点击没反应 ★★★
            StartupWindow = m_window;

            ThemeService.Initialize(m_window.Content as FrameworkElement);

            m_window.Activate();
        }

        private MainWindow m_window;
    }
}