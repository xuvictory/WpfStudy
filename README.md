# WpfStudy

简体中文 — WPF 学习示例工程，包含两个教学项目：`Lesson1` 与 `Lesson2`，用于演示 XAML、布局容器与常用控件用法。

## 目录结构
- `WpfStudy.slnx` — 解决方案文件
- `Lesson1/` — 入门示例（XAML 基础、简单窗口与案例）
- `Lesson2/` — 布局容器示例（Grid、StackPanel、WrapPanel 等）
- 项目均为 WPF 应用（`UseWPF=true`）

## 环境要求
- Windows 10/11
- .NET SDK 10（TargetFramework: `net10.0-windows`）
- 推荐使用 Microsoft Visual Studio（示例环境为 Visual Studio Community 2026）

## 快速开始

在 Visual Studio 中
1. 打开解决方案：双击 `WpfStudy.slnx` 或在 __Solution Explorer__ 中加载。
2. 在需要运行的项目上右键，选择 `Set as Startup Project`。
3. 按 __F5__ 调试运行或按 __Ctrl+F5__ 非调试运行。

使用命令行（PowerShell）
1. 切换到某个项目目录，例如：
   - `cd C:\Users\Administrator\source\repos\WpfStudy\Lesson1`
2. 构建并运行：
   - `dotnet build`
   - `dotnet run`

（注意：WPF 应用只能在 Windows 上运行）

## 每个项目简介
- `Lesson1`：介绍 WPF 项目结构、XAML 基础以及若干简单案例（含 `案例1\NewWindow` 等）。
- `Lesson2`：演示常用布局容器与装饰器（Grid、StackPanel、WrapPanel、DockPanel、Canvas、UniformGrid；以及 Border、ScrollViewer、Viewbox 等）。

## 教学目标（摘要）
- 理解 XAML 与 Code-behind 的关系
- 掌握常用布局容器的使用场景与属性
- 能够独立搭建简单的 WPF 窗口与控件布局

## 建议改进（供参考）
- 为每个示例窗口补充运行截图与说明
- 在每个项目添加 README/注释，解释每个案例的目的与关键点
- 可添加单元测试（业务逻辑分离后）与静态分析工具以提升质量

## 贡献
欢迎提交 Issues 或 Pull Requests。提交前请说明改动目的与影响的示例文件。
