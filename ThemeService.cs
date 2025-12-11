using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using Windows.Storage;

namespace FluentZip
{
    internal static class ThemeService
    {
        private const string ThemeSettingKey = "AppTheme";
        private static ElementTheme _currentTheme = ElementTheme.Default;
        private static readonly List<WeakReference<FrameworkElement>> _registeredRoots = new();

        public static ElementTheme CurrentTheme => _currentTheme;

        public static void Initialize(FrameworkElement? root)
        {
            RegisterRoot(root);
            var saved = ApplicationData.Current.LocalSettings.Values[ThemeSettingKey] as string;
            if (!Enum.TryParse(saved, out ElementTheme theme))
            {
                theme = ElementTheme.Default;
            }

            ApplyTheme(theme, root, savePreference: false);
        }

        public static void ApplyTheme(ElementTheme theme, FrameworkElement? rootOverride = null, bool savePreference = true)
        {
            _currentTheme = theme;
            if (savePreference)
            {
                ApplicationData.Current.LocalSettings.Values[ThemeSettingKey] = theme.ToString();
            }

            var target = rootOverride ?? App.StartupWindow?.Content as FrameworkElement;
            RegisterRoot(target);
            ApplyThemeToRegisteredRoots();
        }

        public static void RegisterRoot(FrameworkElement? root)
        {
            if (root == null) return;

            lock (_registeredRoots)
            {
                foreach (var weak in _registeredRoots)
                {
                    if (weak.TryGetTarget(out var existing) && ReferenceEquals(existing, root))
                    {
                        root.RequestedTheme = _currentTheme;
                        return;
                    }
                }

                _registeredRoots.Add(new WeakReference<FrameworkElement>(root));
            }

            root.RequestedTheme = _currentTheme;
        }

        private static void ApplyThemeToRegisteredRoots()
        {
            lock (_registeredRoots)
            {
                for (int i = _registeredRoots.Count - 1; i >= 0; i--)
                {
                    if (_registeredRoots[i].TryGetTarget(out var element))
                    {
                        element.RequestedTheme = _currentTheme;
                    }
                    else
                    {
                        _registeredRoots.RemoveAt(i);
                    }
                }
            }
        }
    }
}
