using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace FluentZip
{
    internal static class TitleBarThemeHelper
    {
        public static void Attach(Window window, FrameworkElement? themeRoot, UIElement? dragRegion = null)
        {
            if (window == null || themeRoot == null)
            {
                return;
            }

            var titleBar = window.AppWindow?.TitleBar;
            if (titleBar == null)
            {
                return;
            }

            void ApplyTheme()
            {
                var actualTheme = themeRoot.ActualTheme;
                bool isDark = actualTheme == ElementTheme.Dark;

                var primary = ResolveColor("TextFillColorPrimaryBrush", isDark ? Colors.White : Colors.Black);
                var secondary = ResolveColor("TextFillColorSecondaryBrush", isDark ? Color.FromArgb(0xFF, 0xB3, 0xB3, 0xB3) : Color.FromArgb(0xFF, 0x55, 0x55, 0x55));

                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.BackgroundColor = Colors.Transparent;
                titleBar.InactiveBackgroundColor = Colors.Transparent;
                titleBar.ForegroundColor = primary;
                titleBar.ButtonForegroundColor = primary;
                titleBar.ButtonInactiveForegroundColor = secondary;
                titleBar.ButtonHoverForegroundColor = primary;
                titleBar.ButtonPressedForegroundColor = primary;
            }

            themeRoot.ActualThemeChanged += (_, __) => ApplyTheme();
            ApplyTheme();

            if (dragRegion != null)
            {
                titleBar.ExtendsContentIntoTitleBar = true;
                try
                {
                    window.SetTitleBar(dragRegion);
                }
                catch
                {
                }
            }
            else
            {
                titleBar.ExtendsContentIntoTitleBar = false;
            }
        }

        private static Color ResolveColor(string resourceKey, Color fallback)
        {
            if (Application.Current?.Resources != null &&
                Application.Current.Resources.TryGetValue(resourceKey, out var value) &&
                value is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return fallback;
        }
    }
}
