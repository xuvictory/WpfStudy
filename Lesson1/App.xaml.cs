using Lesson1.案例1;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Lesson1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        #region 案例1
        //// 重写 OnStartup 方法，在应用程序启动时执行自定义逻辑
        //protected override void OnStartup(StartupEventArgs e)
        //{
        //    // 调用基类，确保触发 Startup 事件等
        //    base.OnStartup(e);

        //    // 此处可以添加启动前的初始化代码，例如检查配置、用户认证等

        //    // 手动创建主窗口并显示
        //    NewWindow newWindow = new NewWindow();
        //    this.MainWindow = newWindow;// 这句话很关键，漏掉会有影响（猜猜到底会怎样？？？）
        //    newWindow.Show();
        //} 
        #endregion
    }

}
