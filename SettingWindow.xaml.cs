using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using System;
using System.Linq;
using Windows.Graphics;
using System.Collections.Generic;

namespace FluentZip
{
    public sealed partial class SettingWindow : Window
    {
        private bool _isInitializing;
        private RadioButtons? _themeRadio;
        private RadioButtons? ThemeRadio
            => _themeRadio ??= (Content as FrameworkElement)?.FindName("ThemeRadioControl") as RadioButtons;
        private readonly Dictionary<string, FrameworkElement> _sections = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _sectionTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["General"] = "常规",
            ["FileAssoc"] = "文件关联",
            ["ContextMenu"] = "右键菜单",
            ["Extract"] = "解压",
            ["Compress"] = "压缩",
            ["Advanced"] = "高级"
        };

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

            RegisterSections();
            SettingsNavLoadedHook();

            _isInitializing = true;
            SelectThemeRadio(ThemeService.CurrentTheme);
            _isInitializing = false;
            CenterOverOwner();
        }

        private void RegisterSections()
        {
            _sections.Clear();
            if (GeneralSection != null) _sections["General"] = GeneralSection;
            if (FileAssocSection != null) _sections["FileAssoc"] = FileAssocSection;
            if (ContextMenuSection != null) _sections["ContextMenu"] = ContextMenuSection;
            if (ExtractSection != null) _sections["Extract"] = ExtractSection;
            if (CompressSection != null) _sections["Compress"] = CompressSection;
            if (AdvancedSection != null) _sections["Advanced"] = AdvancedSection;
        }

        private void SettingsNavLoadedHook()
        {
            if (SettingsNav == null)
            {
                return;
            }

            SettingsNav.Loaded += SettingsNav_Loaded;
        }

        private void SettingsNav_Loaded(object sender, RoutedEventArgs e)
        {
            if (SettingsNav?.SelectedItem is NavigationViewItem)
            {
                return;
            }

            var first = SettingsNav?.MenuItems?.OfType<NavigationViewItem>().FirstOrDefault();
            if (first != null)
            {
                SettingsNav.SelectedItem = first;
                ShowSection(first.Tag?.ToString());
            }
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

        private void SettingsNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                ShowSection(item.Tag?.ToString());
            }
        }

        private void SettingsNav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            var tag = args.InvokedItemContainer?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tag))
            {
                ShowSection(tag);
            }
        }

        private void ShowSection(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || _sections.Count == 0)
            {
                return;
            }

            foreach (var pair in _sections)
            {
                bool isVisible = string.Equals(pair.Key, tag, StringComparison.OrdinalIgnoreCase);
                pair.Value.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isVisible)
                {
                    RunSectionEntrance(pair.Value);
                }
            }

            if (SectionTitleText != null && _sectionTitles.TryGetValue(tag, out var title))
            {
                SectionTitleText.Text = title;
            }
        }

        private void RunSectionEntrance(FrameworkElement section)
        {
            if (section == null)
            {
                return;
            }

            if (section.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                section.RenderTransform = transform;
            }

            transform.X = 0;
            transform.Y = 40;
            section.Opacity = 0;

            var storyboard = new Storyboard();

            var riseAnimation = new DoubleAnimation
            {
                From = 40,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(260)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(riseAnimation, section);
            Storyboard.SetTargetProperty(riseAnimation, "(UIElement.RenderTransform).(TranslateTransform.Y)");

            var fadeAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fadeAnimation, section);
            Storyboard.SetTargetProperty(fadeAnimation, "Opacity");

            storyboard.Children.Add(riseAnimation);
            storyboard.Children.Add(fadeAnimation);

            storyboard.Begin();
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
