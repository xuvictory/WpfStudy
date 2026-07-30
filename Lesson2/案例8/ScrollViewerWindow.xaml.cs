using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Lesson2.案例8
{
    /// <summary>
    /// ScrollViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ScrollViewerWindow : Window
    {
        // 计数器，用于生成不同姓名
        private int _count = 1;

        public ScrollViewerWindow()
        {
            InitializeComponent();
        }

        // 点击添加按钮时，动态向 StackPanel 添加新的联系人卡片
        private void AddContact_Click(object sender, RoutedEventArgs e)
        {
            // 创建新的联系人卡片（Border）
            Border newCard = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1)
            };

            // 内部 StackPanel
            StackPanel innerStack = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock icon = new TextBlock
            {
                Text = "👤",
                FontSize = 20,
                Margin = new Thickness(0, 0, 10, 0)
            };
            TextBlock name = new TextBlock
            {
                Text = $"联系人 {_count}",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 20, 0)
            };
            TextBlock role = new TextBlock
            {
                Text = "新成员",
                Foreground = System.Windows.Media.Brushes.Gray
            };

            innerStack.Children.Add(icon);
            innerStack.Children.Add(name);
            innerStack.Children.Add(role);
            newCard.Child = innerStack;

            // 添加到 StackPanel
            ContactStackPanel.Children.Add(newCard);
            _count++;

            // 可选：滚动到最新添加的项
            // 需要获取 ScrollViewer 实例，因为我们在 XAML 中没有命名，可以给 ScrollViewer 加上 x:Name="MyScrollViewer"，然后调用：
            // MyScrollViewer.ScrollToBottom();
            // 此处为了简化，省略命名，但你可以自己试试。
        }
    }
}
