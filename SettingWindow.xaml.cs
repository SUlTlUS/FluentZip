using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using System;
using System.Linq;
using Windows.Graphics;

namespace FluentZip
{
    public sealed partial class SettingWindow : Window
    {
        private bool _isInitializing;
        private RadioButtons? _themeRadio;
        private RadioButtons? ThemeRadio
            => _themeRadio ??= (Content as FrameworkElement)?.FindName("ThemeRadioControl") as RadioButtons;

        public SettingWindow()
        {
            InitializeComponent();
            Title = "设置";
            TrySetBackdrop();

            if (Content is FrameworkElement root)
            {
                ThemeService.RegisterRoot(root);
                TitleBarThemeHelper.Attach(this, root, TitleBarDragRegion);
            }

            _isInitializing = true;
            SelectThemeRadio(ThemeService.CurrentTheme);
            _isInitializing = false;
            CenterOverOwner();
        }

        private void SelectThemeRadio(ElementTheme theme)
        {
            if (ThemeRadio?.Items == null)
            {
                return;
            }

            var targetTag = theme.ToString();
            var buttons = ThemeRadio.Items.Cast<object?>().OfType<RadioButton>();
            var target = buttons.FirstOrDefault(rb => string.Equals(rb.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase));
            if (target == null && theme == ElementTheme.Default)
            {
                target = buttons.FirstOrDefault(rb => string.Equals(rb.Tag?.ToString(), "Default", StringComparison.OrdinalIgnoreCase));
            }

            ThemeRadio.SelectedItem = target;
        }

        private void ThemeRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            if (ThemeRadio?.SelectedItem is RadioButton rb && Enum.TryParse(rb.Tag?.ToString(), out ElementTheme theme))
            {
                ThemeService.ApplyTheme(theme);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CenterOverOwner()
        {
            if (AppWindow == null)
            {
                return;
            }

            var owner = App.StartupWindow?.AppWindow;
            if (owner == null)
            {
                return;
            }

            var ownerPos = owner.Position;
            var ownerSize = owner.Size;
            var selfSize = AppWindow.Size;
            var targetX = ownerPos.X + Math.Max(0, (ownerSize.Width - selfSize.Width) / 2);
            var targetY = ownerPos.Y + Math.Max(0, (ownerSize.Height - selfSize.Height) / 2);
            AppWindow.Move(new PointInt32(targetX, targetY));
        }

        private void TrySetBackdrop()
        {
            try
            {
                SystemBackdrop = new MicaBackdrop();
            }
            catch
            {
            }
        }
    }
}
