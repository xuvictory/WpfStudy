using System.Windows;

namespace CommunityToolkitDemo.Views;

/// <summary>
/// 主窗口外壳。这里只保留纯视图职责（窗口按钮），
/// 业务逻辑一律放在 <see cref="ViewModels.MainWindowViewModel"/> 中。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
