using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System;
using System.Collections;
using System.Collections.ObjectModel;

namespace FluentZip
{
    public sealed partial class SearchWindow : Window
    {
        private TextBox QueryBox;
        private ListView ResultList;
        private Button DeleteBtn;
        private Grid TitleDragRegion;

        public SearchWindow()
        {
             Title = "查找文件";
 
             // Enable Mica backdrop for Fluent Design
             try { SystemBackdrop = new MicaBackdrop(); } catch { }
 
            var root = new Grid { Padding = new Thickness(8) };
            root.RequestedTheme = ThemeService.CurrentTheme;
            ThemeService.RegisterRoot(root);
             root.RowDefinitions.Clear();
             root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar region
             root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input panel (auto height)
             root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
 
             // Custom title bar region for Fluent drag
             TitleDragRegion = new Grid { Height = 32, Background = new SolidColorBrush(Colors.Transparent) };
             var titleText = new TextBlock
             {
                 Text = "查找文件",
                 Margin = new Thickness(12, 0, 0, 0),
                 VerticalAlignment = VerticalAlignment.Center,
                 Foreground = ResolveBrush("TextFillColorPrimaryBrush", Colors.White)
             };
             TitleDragRegion.Children.Add(titleText);
             root.Children.Add(TitleDragRegion); Grid.SetRow(TitleDragRegion, 0);
 
             TitleBarThemeHelper.Attach(this, root, TitleDragRegion);
 
             // Input panel (ensure visible styles)
            var inputPanel = new Grid { Margin = new Thickness(8,4,8,12), ColumnSpacing = 16 };
             inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
             inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
             inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
 
            var lbl = new TextBlock
            {
                Text = "查找内容(F):",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ResolveBrush("TextFillColorPrimaryBrush", Colors.White)
            };
            QueryBox = new TextBox
            {
                PlaceholderText = "输入文件名或路径片段",
                MinWidth = 360,
                MinHeight = 40,
                Margin = new Thickness(0, 0, 0, 0)
            };
            QueryBox.Background = ResolveBrush("TextBoxBackgroundFillColorDefaultBrush", Colors.Transparent);
            QueryBox.Foreground = ResolveBrush("TextFillColorPrimaryBrush", Colors.White);
            QueryBox.PlaceholderForeground = ResolveBrush("TextFillColorSecondaryBrush", Colors.Silver);
            DeleteBtn = new Button { Content = "删除", MinWidth = 64, Height = 36, Margin = new Thickness(16, 0, 0, 0) };
 
             inputPanel.Children.Add(lbl); Grid.SetColumn(lbl, 0);
             inputPanel.Children.Add(QueryBox); Grid.SetColumn(QueryBox, 1);
             inputPanel.Children.Add(DeleteBtn); Grid.SetColumn(DeleteBtn, 2);
 
             root.Children.Add(inputPanel); Grid.SetRow(inputPanel, 1);
 
             var header = new Grid { Margin = new Thickness(4, 0, 4, 0), BorderThickness = new Thickness(0, 0, 0, 1) };
             header.BorderBrush = new SolidColorBrush(Colors.Black) { Opacity = 0.25 };
             header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
             header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
             header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
             header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
             header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
             header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
 
            FrameworkElement AddHeader(string text, int col, Thickness? margin = null)
             {
                var tb = new TextBlock
                {
                    Text = text,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13,
                    Margin = margin ?? new Thickness(0),
                    Foreground = ResolveBrush("TextFillColorSecondaryBrush", Colors.White)
                };
                 header.Children.Add(tb);
                 Grid.SetColumn(tb, col);
                 return tb;
             }
 
            AddHeader("名称", 1);
            AddHeader("标签", 2, new Thickness(10, 0, 0, 0));
            AddHeader("修改日期", 3, new Thickness(10, 0, 0, 0));
            AddHeader("类型", 4, new Thickness(10, 0, 0, 0));
            AddHeader("大小", 5, new Thickness(10, 0, 0, 0));
 
             ResultList = new ListView { SelectionMode = ListViewSelectionMode.Extended, Padding = new Thickness(4) };
 
             var contentGrid = new Grid();
             contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
             contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
             contentGrid.Children.Add(header); Grid.SetRow(header, 0);
             contentGrid.Children.Add(ResultList); Grid.SetRow(ResultList, 1);
 
             root.Children.Add(contentGrid); Grid.SetRow(contentGrid, 2);
 
             this.Content = root;
 
             try { AppWindow?.Resize(new SizeInt32(820, 560)); } catch { }
 
            QueryBox.KeyDown += QueryBox_KeyDown;
             DeleteBtn.Click += DeleteBtn_Click;
             ResultList.DoubleTapped += ResultList_DoubleTapped;
         }
 
        public object? ItemsSource
        {
            get => ResultList.ItemsSource;
            set => ResultList.ItemsSource = value;
        }
 
        public IList SelectedItems => (IList)ResultList.SelectedItems;
 
        public string QueryText
        {
            get => QueryBox.Text;
            set => QueryBox.Text = value ?? string.Empty;
        }
 
        public event EventHandler<string>? QuerySubmitted;
        public event EventHandler? DeleteRequested;
        public event EventHandler<object?>? ItemInvoked;
 
        private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                QuerySubmitted?.Invoke(this, QueryText);
                e.Handled = true;
            }
        }
 
        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
 
        private void ResultList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            ItemInvoked?.Invoke(this, ResultList.SelectedItem);
        }

        private static Brush ResolveBrush(string resourceKey, Windows.UI.Color fallback)
        {
            if (Application.Current?.Resources != null && Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(fallback);
        }
    }
}