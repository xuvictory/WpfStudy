/* PrismDemo 项目数据分片 —— 由 project-know skill 生成
 * 客观字段来自 output/prismdemo.json（analyze.js 扫描），AI 字段基于源码阅读补全。
 */
window.PROJECTS["prismdemo"] = {
  "id": "prismdemo",
  "name": "PrismDemo（智汇 POS 收银终端）",
  "repo": "c:\\code\\WpfStudy\\PrismDemo",
  "generatedAt": "2026-09-14T04:58:24.060Z",
  "summary": "基于 WPF + Prism 8（DryIoc 容器）的智能 POS 收银终端演示项目：以区域导航 + 事件聚合器组织三页面（收银台 / 设备监控 / 设置），用 Markdown 表格充当数据源，内置 5 种模拟工控协议驱动（Modbus TCP / OPC UA / 串口 / Socket / MQTT）模拟扫码枪、客显屏、钱箱、小票机等外设，并附带 xUnit 单元测试与查询性能基准。",

  "techStack": {
    "language": ["C#"],
    "framework": ["WPF (.NET 10, net10.0-windows)", "Prism 8.1.97 (Prism.DryIoc)", "DryIoc IoC 容器"],
    "middleware": [],
    "tool": ["dotnet CLI", "xUnit 2.9.2", "Microsoft.NET.Test.Sdk 17.12.0"]
  },

  "tree": {
    "name": "PrismDemo", "type": "dir", "path": "", "children": [
      { "name": "App.xaml", "type": "file", "path": "App.xaml", "size": 1190 },
      { "name": "App.xaml.cs", "type": "file", "path": "App.xaml.cs", "size": 14290 },
      { "name": "Common", "type": "dir", "path": "Common", "children": [
        { "name": "Behaviors", "type": "dir", "path": "Common/Behaviors", "children": [
          { "name": "AttachedProps.cs", "type": "file", "path": "Common/Behaviors/AttachedProps.cs", "size": 8077 }
        ]},
        { "name": "Collection", "type": "dir", "path": "Common/Collection", "children": [
          { "name": "RangeObservableCollection.cs", "type": "file", "path": "Common/Collection/RangeObservableCollection.cs", "size": 3249 }
        ]},
        { "name": "Commands", "type": "dir", "path": "Common/Commands", "children": [
          { "name": "AsyncDelegateCommand.cs", "type": "file", "path": "Common/Commands/AsyncDelegateCommand.cs", "size": 2994 }
        ]},
        { "name": "Controls", "type": "dir", "path": "Common/Controls", "children": [
          { "name": "VirtualizingWrapPanel.cs", "type": "file", "path": "Common/Controls/VirtualizingWrapPanel.cs", "size": 16687 }
        ]},
        { "name": "Converters", "type": "dir", "path": "Common/Converters", "children": [
          { "name": "Converters.cs", "type": "file", "path": "Common/Converters/Converters.cs", "size": 14141 }
        ]},
        { "name": "Dialogs", "type": "dir", "path": "Common/Dialogs", "children": [
          { "name": "DialogServiceExtensions.cs", "type": "file", "path": "Common/Dialogs/DialogServiceExtensions.cs", "size": 3443 },
          { "name": "NotificationKind.cs", "type": "file", "path": "Common/Dialogs/NotificationKind.cs", "size": 816 }
        ]},
        { "name": "Mvvm", "type": "dir", "path": "Common/Mvvm", "children": [
          { "name": "ValidatableBindableBase.cs", "type": "file", "path": "Common/Mvvm/ValidatableBindableBase.cs", "size": 5304 },
          { "name": "ViewModelBase.cs", "type": "file", "path": "Common/Mvvm/ViewModelBase.cs", "size": 6903 }
        ]},
        { "name": "Styles", "type": "dir", "path": "Common/Styles", "children": [
          { "name": "Colors.xaml", "type": "file", "path": "Common/Styles/Colors.xaml", "size": 4845 },
          { "name": "Controls.xaml", "type": "file", "path": "Common/Styles/Controls.xaml", "size": 38873 }
        ]},
        { "name": "UiDispatcher.cs", "type": "file", "path": "Common/UiDispatcher.cs", "size": 1739 }
      ]},
      { "name": "Data", "type": "dir", "path": "Data", "children": [
        { "name": "categories.md", "type": "file", "path": "Data/categories.md", "size": 750 },
        { "name": "devices.md", "type": "file", "path": "Data/devices.md", "size": 1595 },
        { "name": "large", "type": "dir", "path": "Data/large", "children": [
          { "name": "products.md（8.4MB 大数据量基准）", "type": "file", "path": "Data/large/products.md", "size": 8802232, "collapsed": true }
        ]},
        { "name": "orders.md", "type": "file", "path": "Data/orders.md", "size": 1528 },
        { "name": "products.md", "type": "file", "path": "Data/products.md", "size": 3593 }
      ]},
      { "name": "Events", "type": "dir", "path": "Events", "children": [
        { "name": "BarcodeScannedEvent.cs", "type": "file", "path": "Events/BarcodeScannedEvent.cs", "size": 535 },
        { "name": "CartChangedEvent.cs", "type": "file", "path": "Events/CartChangedEvent.cs", "size": 463 },
        { "name": "DataLoadedEvent.cs", "type": "file", "path": "Events/DataLoadedEvent.cs", "size": 317 },
        { "name": "DeviceAlarmEvent.cs", "type": "file", "path": "Events/DeviceAlarmEvent.cs", "size": 243 },
        { "name": "EventPayloads.cs", "type": "file", "path": "Events/EventPayloads.cs", "size": 2013 },
        { "name": "NavigateEvent.cs", "type": "file", "path": "Events/NavigateEvent.cs", "size": 514 },
        { "name": "PaymentCompletedEvent.cs", "type": "file", "path": "Events/PaymentCompletedEvent.cs", "size": 239 },
        { "name": "SettingsChangedEvent.cs", "type": "file", "path": "Events/SettingsChangedEvent.cs", "size": 376 },
        { "name": "StatusNotificationEvent.cs", "type": "file", "path": "Events/StatusNotificationEvent.cs", "size": 316 }
      ]},
      { "name": "Models", "type": "dir", "path": "Models", "children": [
        { "name": "CartItem.cs", "type": "file", "path": "Models/CartItem.cs", "size": 2887 },
        { "name": "DeviceInfo.cs", "type": "file", "path": "Models/DeviceInfo.cs", "size": 1143 },
        { "name": "Order.cs", "type": "file", "path": "Models/Order.cs", "size": 1425 },
        { "name": "PosSettings.cs", "type": "file", "path": "Models/PosSettings.cs", "size": 1088 },
        { "name": "Product.cs", "type": "file", "path": "Models/Product.cs", "size": 1124 },
        { "name": "ProtocolDisplay.cs", "type": "file", "path": "Models/ProtocolDisplay.cs", "size": 2263 },
        { "name": "ProtocolFrame.cs", "type": "file", "path": "Models/ProtocolFrame.cs", "size": 1706 },
        { "name": "ProtocolType.cs", "type": "file", "path": "Models/ProtocolType.cs", "size": 1171 }
      ]},
      { "name": "PrismDemo.csproj", "type": "file", "path": "PrismDemo.csproj", "size": 2207 },
      { "name": "Protocols", "type": "dir", "path": "Protocols", "children": [
        { "name": "IProtocolDriver.cs", "type": "file", "path": "Protocols/IProtocolDriver.cs", "size": 1667 },
        { "name": "IProtocolManager.cs", "type": "file", "path": "Protocols/IProtocolManager.cs", "size": 2108 },
        { "name": "ProtocolDriverBase.cs", "type": "file", "path": "Protocols/ProtocolDriverBase.cs", "size": 9103 },
        { "name": "ProtocolKeys.cs", "type": "file", "path": "Protocols/ProtocolKeys.cs", "size": 1771 },
        { "name": "ProtocolManager.cs", "type": "file", "path": "Protocols/ProtocolManager.cs", "size": 8120 },
        { "name": "Simulated", "type": "dir", "path": "Protocols/Simulated", "children": [
          { "name": "ModbusTcpSimDriver.cs", "type": "file", "path": "Protocols/Simulated/ModbusTcpSimDriver.cs", "size": 2000 },
          { "name": "MqttSimDriver.cs", "type": "file", "path": "Protocols/Simulated/MqttSimDriver.cs", "size": 2157 },
          { "name": "OpcUaSimDriver.cs", "type": "file", "path": "Protocols/Simulated/OpcUaSimDriver.cs", "size": 1729 },
          { "name": "SerialPortSimDriver.cs", "type": "file", "path": "Protocols/Simulated/SerialPortSimDriver.cs", "size": 3914 },
          { "name": "SocketSimDriver.cs", "type": "file", "path": "Protocols/Simulated/SocketSimDriver.cs", "size": 2409 }
        ]}
      ]},
      { "name": "Services", "type": "dir", "path": "Services", "children": [
        { "name": "CartLimits.cs", "type": "file", "path": "Services/CartLimits.cs", "size": 1441 },
        { "name": "CartService.cs", "type": "file", "path": "Services/CartService.cs", "size": 4574 },
        { "name": "CheckoutCoordinator.cs", "type": "file", "path": "Services/CheckoutCoordinator.cs", "size": 3327 },
        { "name": "DialogService.cs", "type": "file", "path": "Services/DialogService.cs", "size": 1843 },
        { "name": "ICartService.cs", "type": "file", "path": "Services/ICartService.cs", "size": 1874 },
        { "name": "IDeviceCatalog.cs", "type": "file", "path": "Services/IDeviceCatalog.cs", "size": 910 },
        { "name": "IDialogService.cs", "type": "file", "path": "Services/IDialogService.cs", "size": 801 },
        { "name": "IMarkdownDataService.cs", "type": "file", "path": "Services/IMarkdownDataService.cs", "size": 629 },
        { "name": "IOrderService.cs", "type": "file", "path": "Services/IOrderService.cs", "size": 596 },
        { "name": "IProductCatalog.cs", "type": "file", "path": "Services/IProductCatalog.cs", "size": 2438 },
        { "name": "IProductQuery.cs", "type": "file", "path": "Services/IProductQuery.cs", "size": 2404 },
        { "name": "ISettingsService.cs", "type": "file", "path": "Services/ISettingsService.cs", "size": 668 },
        { "name": "MarkdownDataService.cs", "type": "file", "path": "Services/MarkdownDataService.cs", "size": 10399 },
        { "name": "MarkdownHeader.cs", "type": "file", "path": "Services/MarkdownHeader.cs", "size": 3257 },
        { "name": "MarkdownRow.cs", "type": "file", "path": "Services/MarkdownRow.cs", "size": 3451 },
        { "name": "MarkdownRowReader.cs", "type": "file", "path": "Services/MarkdownRowReader.cs", "size": 3386 },
        { "name": "MarkdownTableParser.cs", "type": "file", "path": "Services/MarkdownTableParser.cs", "size": 4391 },
        { "name": "MarkdownValue.cs", "type": "file", "path": "Services/MarkdownValue.cs", "size": 2011 },
        { "name": "OrderService.cs", "type": "file", "path": "Services/OrderService.cs", "size": 1439 },
        { "name": "PosRepository.cs", "type": "file", "path": "Services/PosRepository.cs", "size": 12851 },
        { "name": "ProductQueryService.cs", "type": "file", "path": "Services/ProductQueryService.cs", "size": 5677 },
        { "name": "ProtocolMessageBridge.cs", "type": "file", "path": "Services/ProtocolMessageBridge.cs", "size": 2324 },
        { "name": "SettingsService.cs", "type": "file", "path": "Services/SettingsService.cs", "size": 785 }
      ]},
      { "name": "ViewModels", "type": "dir", "path": "ViewModels", "children": [
        { "name": "CashierViewModel.cs", "type": "file", "path": "ViewModels/CashierViewModel.cs", "size": 28493 },
        { "name": "DeviceCardViewModel.cs", "type": "file", "path": "ViewModels/DeviceCardViewModel.cs", "size": 3253 },
        { "name": "DeviceMonitorViewModel.cs", "type": "file", "path": "ViewModels/DeviceMonitorViewModel.cs", "size": 13983 },
        { "name": "LiveMetricItem.cs", "type": "file", "path": "ViewModels/LiveMetricItem.cs", "size": 1423 },
        { "name": "MainWindowViewModel.cs", "type": "file", "path": "ViewModels/MainWindowViewModel.cs", "size": 12874 },
        { "name": "NavItem.cs", "type": "file", "path": "ViewModels/NavItem.cs", "size": 377 },
        { "name": "NotificationDialogViewModel.cs", "type": "file", "path": "ViewModels/NotificationDialogViewModel.cs", "size": 3900 },
        { "name": "ProtocolStatusItem.cs", "type": "file", "path": "ViewModels/ProtocolStatusItem.cs", "size": 864 },
        { "name": "SettingsViewModel.cs", "type": "file", "path": "ViewModels/SettingsViewModel.cs", "size": 9993 }
      ]},
      { "name": "Views", "type": "dir", "path": "Views", "children": [
        { "name": "CashierView.xaml", "type": "file", "path": "Views/CashierView.xaml", "size": 31891 },
        { "name": "CashierView.xaml.cs", "type": "file", "path": "Views/CashierView.xaml.cs", "size": 170 },
        { "name": "DeviceMonitorView.xaml", "type": "file", "path": "Views/DeviceMonitorView.xaml", "size": 30625 },
        { "name": "DeviceMonitorView.xaml.cs", "type": "file", "path": "Views/DeviceMonitorView.xaml.cs", "size": 182 },
        { "name": "MainWindow.xaml", "type": "file", "path": "Views/MainWindow.xaml", "size": 13035 },
        { "name": "MainWindow.xaml.cs", "type": "file", "path": "Views/MainWindow.xaml.cs", "size": 765 },
        { "name": "NotificationDialogView.xaml", "type": "file", "path": "Views/NotificationDialogView.xaml", "size": 6889 },
        { "name": "NotificationDialogView.xaml.cs", "type": "file", "path": "Views/NotificationDialogView.xaml.cs", "size": 5817 },
        { "name": "NotificationDialogWindow.xaml", "type": "file", "path": "Views/NotificationDialogWindow.xaml", "size": 1031 },
        { "name": "NotificationDialogWindow.xaml.cs", "type": "file", "path": "Views/NotificationDialogWindow.xaml.cs", "size": 1421 },
        { "name": "SettingsView.xaml", "type": "file", "path": "Views/SettingsView.xaml", "size": 17646 },
        { "name": "SettingsView.xaml.cs", "type": "file", "path": "Views/SettingsView.xaml.cs", "size": 172 }
      ]},
      { "name": "tests", "type": "dir", "path": "tests", "children": [
        { "name": "PrismDemo.Tests", "type": "dir", "path": "tests/PrismDemo.Tests", "children": [
          { "name": "CartLimitsTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/CartLimitsTests.cs", "size": 1712 },
          { "name": "CartServiceTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/CartServiceTests.cs", "size": 6094 },
          { "name": "ConvertersTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/ConvertersTests.cs", "size": 6146 },
          { "name": "MarkdownRowReaderTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/MarkdownRowReaderTests.cs", "size": 7829 },
          { "name": "MarkdownTableParserTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/MarkdownTableParserTests.cs", "size": 3241 },
          { "name": "Performance", "type": "dir", "path": "tests/PrismDemo.Tests/Performance", "children": [
            { "name": "LoadBenchmarkTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/Performance/LoadBenchmarkTests.cs", "size": 9670 },
            { "name": "PerformanceTestCollection.cs", "type": "file", "path": "tests/PrismDemo.Tests/Performance/PerformanceTestCollection.cs", "size": 698 },
            { "name": "QueryBenchmarkTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/Performance/QueryBenchmarkTests.cs", "size": 17507 },
            { "name": "TestProductData.cs", "type": "file", "path": "tests/PrismDemo.Tests/Performance/TestProductData.cs", "size": 6477 }
          ]},
          { "name": "PosRepositoryIndexTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/PosRepositoryIndexTests.cs", "size": 7888 },
          { "name": "PrismDemo.Tests.csproj", "type": "file", "path": "tests/PrismDemo.Tests/PrismDemo.Tests.csproj", "size": 1403 },
          { "name": "ProductQueryServiceTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/ProductQueryServiceTests.cs", "size": 7672 },
          { "name": "ProtocolDisplayTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/ProtocolDisplayTests.cs", "size": 2058 },
          { "name": "ProtocolDriverBaseTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/ProtocolDriverBaseTests.cs", "size": 4208 },
          { "name": "StaTestRunner.cs", "type": "file", "path": "tests/PrismDemo.Tests/StaTestRunner.cs", "size": 1349 },
          { "name": "VirtualizingWrapPanelTests.cs", "type": "file", "path": "tests/PrismDemo.Tests/VirtualizingWrapPanelTests.cs", "size": 9514 }
        ]}
      ]}
    ]
  },

  "stats": { "fileCount": 107, "langDist": { "Other": 11, "C#": 91, "Markdown": 5 } },

  "readme": {
    "file": null, "length": 0,
    "summary": "项目无独立 README；自述来自源码：这是一个 WPF + Prism 8 学习/演示项目（所属仓库 WpfStudy 的子项目），以『智汇 POS 收银终端』为业务载体，系统性覆盖 PrismApplication、RegisterTypes 集中注册、区域导航（Region）、IEventAggregator 事件聚合、IDialogService 对话框、ViewModelLocator 约定装配等 Prism 核心知识点，并补齐 AsyncDelegateCommand 等自定义基础设施。csproj 注释表明它从 CommunityToolkit.Mvvm 的 Ioc.Default 手写容器方案迁移到 Prism。"
  },

  "evolution": {
    "commits": 0,
    "recent": [],
    "topContributors": [],
    "branches": [],
    "releases": [],
    "remote": ""
  },

  "ci": { "has": false, "files": [] },
  "tests": { "has": true, "dirs": ["tests"] },

  "quickstart": {
    "requirements": [
      ".NET 10 SDK（net10.0-windows）",
      "Windows 操作系统（WPF 桌面应用）"
    ],
    "steps": [
      { "name": "构建主程序", "cmd": "dotnet build c:/code/WpfStudy/PrismDemo/PrismDemo.csproj" },
      { "name": "运行收银终端", "cmd": "dotnet run --project c:/code/WpfStudy/PrismDemo/PrismDemo.csproj" },
      { "name": "运行单元测试与基准", "cmd": "dotnet test c:/code/WpfStudy/PrismDemo/tests/PrismDemo.Tests/PrismDemo.Tests.csproj" }
    ],
    "scripts": []
  },

  // ↓↓↓ 以下为 AI 按源码阅读补全的字段 ↓↓↓

  "quality": {
    "summary": "工程化程度在演示项目中属上乘：分层清晰（Views/ViewModels/Services/Protocols/Models），依赖全部面向最小接口；107 个文件中 91 个为 C#，XML 文档注释覆盖设计意图；附 12 个测试文件覆盖购物车/Markdown 解析/协议驱动/虚拟化面板，并含加载与查询性能基准（8.4MB 大数据集）。不足：无 CI、无 lint 配置、无独立 README，且无独立 git 提交历史（为父仓库子目录）。",
    "lint": { "has": false, "files": [] },
    "errorHandling": "三层兜底：1) App 构造即订阅 DispatcherUnhandledException，未处理异常详情落盘到 %LocalAppData%/PrismDemo/error.log，界面只展示可读文案（写日志失败也不抛出，避免递归崩溃）；2) 协议层 ProtocolManager 对每个驱动的连接/断开逐个隔离异常，单个设备失败不影响其它设备，未连接时下发指令安全返回 false（支持『离线收银』）；3) 启动初始化失败经 IEventAggregator 的 StatusNotificationEvent 推送到全局状态提示条。设备监控页还实现了报警确认与 200 条环形报文日志缓冲。"
  },

  "architecture": {
    "mermaid": `graph TD
  subgraph L1["表现层"]
    MAIN["MainWindow 外壳"]
    CASH["CashierView 收银台"]
    DEV["DeviceMonitorView 设备监控"]
    SET["SettingsView 设置"]
    DIALOG["NotificationDialog 对话框"]
  end
  subgraph L2["事件总线"]
    EA["IEventAggregator 全局事件"]
    BRIDGE["ProtocolMessageBridge 协议报文翻译"]
  end
  subgraph L3["业务服务层"]
    CART["CartService 购物车"]
    ORDER["OrderService 订单"]
    QUERY["ProductQueryService 商品查询"]
    CHECKOUT["CheckoutCoordinator 结算编排"]
    SETTINGS["SettingsService 设置"]
    DIALOGSVC["DialogService 对话框适配"]
  end
  subgraph L4["数据层"]
    REPO["PosRepository 仓储 商品与设备索引"]
    MD["MarkdownDataService 数据加载"]
    FILES[("Data 目录 Markdown 数据文件")]
  end
  subgraph L5["协议层"]
    PM["ProtocolManager 协议管理器"]
    D1["ModbusTcp 驱动"]
    D2["OpcUa 驱动"]
    D3["SerialPort 驱动"]
    D4["Socket 驱动"]
    D5["Mqtt 驱动"]
  end
  subgraph L6["基础设施"]
    COMMON["Common 命令与集合与转换器与虚拟化面板与 Mvvm 基类与样式"]
  end
  MAIN --> CASH
  MAIN --> DEV
  MAIN --> SET
  CASH --> EA
  DEV --> EA
  SET --> EA
  DIALOG --> DIALOGSVC
  CASH --> QUERY
  CASH --> CART
  CASH --> CHECKOUT
  CART --> REPO
  QUERY --> REPO
  ORDER --> REPO
  REPO --> MD
  MD --> FILES
  CHECKOUT --> PM
  PM --> D1
  PM --> D2
  PM --> D3
  PM --> D4
  PM --> D5
  D1 --> BRIDGE
  BRIDGE --> EA
  EA --> MAIN`,
    "modules": [
      { "name": "应用入口", "responsibility": "PrismApplication 生命周期：集中注册类型（DryIoc）、创建外壳、按序初始化（桥接协议→首次导航→后台加载数据→连接设备）、退出时优雅释放", "entry": "App.xaml.cs", "dependsOn": ["业务服务层", "协议层", "表现层"] },
      { "name": "表现层", "responsibility": "MVVM 视图与 ViewModel：主窗口导航外壳（IRegionManager + ContentRegion 区域导航）、收银台、设备监控（IActiveAware 激活期订阅）、设置页、通知对话框", "entry": "Views/MainWindow.xaml + ViewModels/MainWindowViewModel.cs", "dependsOn": ["事件总线", "业务服务层"] },
      { "name": "事件总线", "responsibility": "Prism IEventAggregator 承载 9 类全局事件（购物车/状态提示/设备报警/支付完成/设置变更/导航/数据就绪/扫码）；ProtocolMessageBridge 把协议报文翻译为全局事件", "entry": "Events/ + Services/ProtocolMessageBridge.cs", "dependsOn": [] },
      { "name": "业务服务层", "responsibility": "购物车（含库存/超卖上限约束）、订单、商品查询（后台线程过滤 + 结果截断）、结算外设编排（打印/开钱箱/云端上报并行下发）、设置、对话框适配", "entry": "Services/", "dependsOn": ["数据层", "协议层"] },
      { "name": "数据层", "responsibility": "PosRepository 单例充当全应用唯一数据落点，以最小接口（IProductCatalog/IDeviceCatalog）对外；MarkdownDataService 解析 Markdown 表格数据源（含自研表解析器）", "entry": "Services/PosRepository.cs", "dependsOn": [] },
      { "name": "协议层", "responsibility": "IProtocolManager 聚合 5 种模拟驱动（Modbus TCP/OPC UA/串口/Socket/MQTT），统一连接状态机、报文收发、报警上报；新增协议只需注册一行", "entry": "Protocols/ProtocolManager.cs", "dependsOn": ["数据层"] },
      { "name": "基础设施", "responsibility": "AsyncDelegateCommand（Prism 8 缺失的异步命令）、RangeObservableCollection、值转换器、VirtualizingWrapPanel、ViewModelBase（事件订阅管理）、UiDispatcher 线程调度、主题样式", "entry": "Common/", "dependsOn": [] }
    ]
  },

  "deploy": {
    "mermaid": `graph TD
  subgraph D1["客户端单机"]
    USER["收银员"]
    APP["PrismDemo.exe WPF 桌面进程"]
    DATA[("Data 目录 Markdown 数据文件")]
    LOG[("error.log 本地日志")]
  end
  subgraph D2["模拟外设 进程内仿真"]
    SCAN["串口扫码枪"]
    DISP["Socket 客显屏"]
    CASHBOX["钱箱"]
    PRINTER["小票打印机"]
    MQTTSIM["云端 MQTT 模拟"]
    PLC["Modbus TCP 与 OPC UA 仿真"]
  end
  USER --> APP
  APP --> DATA
  APP --> LOG
  APP --> SCAN
  APP --> DISP
  APP --> CASHBOX
  APP --> PRINTER
  APP --> MQTTSIM
  APP --> PLC`,
    "note": "纯本地单机部署：所有『外设』均为进程内模拟驱动（不依赖真实硬件与网络服务），数据源为随程序复制的 Markdown 文件，未连接云端。产品定位『智汇 POS 收银终端』若接入真实环境，仅需把 Simulated 驱动替换为对应协议实现，架构不变。"
  },

  "components": [
    { "name": "应用入口（App）", "duty": "PrismApplication 生命周期管理与集中注册", "entry": "App.xaml.cs", "deps": ["协议管理器", "仓储", "各页面 ViewModel"], "layer": "应用层" },
    { "name": "主窗口外壳", "duty": "左侧导航 + 标题栏协议状态灯 + 底部状态提示条，区域导航宿主", "entry": "Views/MainWindow.xaml", "deps": ["IRegionManager", "事件总线"], "layer": "表现层" },
    { "name": "收银台页面", "duty": "商品浏览/分类切片/关键字防抖搜索/扫码加购/购物车增删改/结算支付", "entry": "ViewModels/CashierViewModel.cs", "deps": ["商品查询服务", "购物车服务", "订单服务", "结算编排", "对话框服务"], "layer": "表现层" },
    { "name": "设备监控页面", "duty": "5 种协议连接开关、实时指标表、报警横幅、200 条环形报文日志（150ms 批量刷新）", "entry": "ViewModels/DeviceMonitorViewModel.cs", "deps": ["协议管理器", "事件总线"], "layer": "表现层" },
    { "name": "设置页面", "duty": "门店信息、超卖开关等 PosSettings 编辑与持久化", "entry": "ViewModels/SettingsViewModel.cs", "deps": ["设置服务", "事件总线"], "layer": "表现层" },
    { "name": "通知对话框", "duty": "Prism IDialogService 自定义宿主窗口 + IDialogAware 内容视图（通知/确认/结算结果）", "entry": "Views/NotificationDialogWindow.xaml", "deps": ["Prism 对话框服务"], "layer": "表现层" },
    { "name": "购物车服务", "duty": "行项集合维护、库存/超卖上限约束、条码加购、汇总事件广播", "entry": "Services/CartService.cs", "deps": ["仓储", "设置服务", "事件总线"], "layer": "业务层" },
    { "name": "结算编排", "duty": "支付完成后并行下发打印小票/开钱箱/云端上报，并汇总设备结果文案", "entry": "Services/CheckoutCoordinator.cs", "deps": ["协议管理器"], "layer": "业务层" },
    { "name": "商品查询服务", "duty": "分类切片走仓储索引、关键字后台线程过滤、连续输入取消旧查询、结果上限截断", "entry": "Services/ProductQueryService.cs", "deps": ["仓储"], "layer": "业务层" },
    { "name": "协议管理器", "duty": "聚合全部驱动、并行连接/断开（异常隔离）、扫码/客显/钱箱/打印/上报等语义指令、事件转发", "entry": "Protocols/ProtocolManager.cs", "deps": ["5 种模拟驱动"], "layer": "协议层" },
    { "name": "协议驱动基座", "duty": "连接状态机、后台仿真循环、报警上报的公共实现，子类只需定义协议细节", "entry": "Protocols/ProtocolDriverBase.cs", "deps": [], "layer": "协议层" },
    { "name": "协议消息桥", "duty": "把协议报文/报警翻译为全局事件，页面无需感知协议细节", "entry": "Services/ProtocolMessageBridge.cs", "deps": ["协议管理器", "事件总线"], "layer": "协议层" },
    { "name": "仓储", "duty": "全应用唯一数据落点：商品（含条码/分类索引）、设备配置，以最小接口暴露", "entry": "Services/PosRepository.cs", "deps": ["Markdown 数据服务"], "layer": "数据层" },
    { "name": "Markdown 数据服务", "duty": "解析 Data/*.md 表格为强类型对象（自研表头/行/值解析器），记录加载错误", "entry": "Services/MarkdownDataService.cs", "deps": [], "layer": "数据层" },
    { "name": "MVVM 基础设施", "duty": "ViewModelBase（事件订阅/释放）、AsyncDelegateCommand、RangeObservableCollection、VirtualizingWrapPanel、转换器、UiDispatcher", "entry": "Common/", "deps": [], "layer": "基础设施" },
    { "name": "测试工程", "duty": "xUnit 单测（购物车/解析器/协议/转换器/虚拟化面板）+ 加载与查询性能基准", "entry": "tests/PrismDemo.Tests", "deps": ["主工程"], "layer": "测试" }
  ],

  "sequences": [
    {
      "title": "应用启动初始化链路",
      "mermaid": `sequenceDiagram
  participant APP as App 应用入口
  participant C as DryIoc 容器
  participant SHELL as MainWindow
  participant MD as MarkdownDataService
  participant EA as IEventAggregator
  participant PM as ProtocolManager
  APP->>C: RegisterTypes 集中注册
  APP->>C: CreateShell 解析 MainWindow
  APP->>SHELL: 显示外壳（窗口先出，数据后到）
  APP->>PM: 建立 ProtocolMessageBridge 协议桥接
  SHELL-->>APP: Loaded 事件（ContentRegion 就绪）
  APP->>SHELL: 首次区域导航到收银台
  APP->>MD: 后台 LoadAsync 加载 Markdown 数据
  MD-->>EA: 广播 DataLoadedEvent（含加载错误）
  EA-->>SHELL: 页面据此重建分类与商品视图
  APP->>PM: ConnectAllAsync 并行连接 5 种驱动`
    },
    {
      "title": "扫码枪加购链路",
      "mermaid": `sequenceDiagram
  participant U as 收银员
  participant VM as CashierViewModel
  participant PM as ProtocolManager
  participant DRV as SerialPortSimDriver
  participant BR as ProtocolMessageBridge
  participant EA as IEventAggregator
  participant CART as CartService
  participant REPO as PosRepository
  U->>VM: 点击扫码按钮（AsyncDelegateCommand）
  VM->>PM: RequestBarcodeScanAsync
  PM->>DRV: 下发扫码指令（未连接则返回 false）
  DRV-->>BR: 收到条码报文
  BR->>EA: 发布 BarcodeScannedEvent
  EA-->>VM: 订阅回调（切回 UI 线程）
  VM->>CART: TryAddByBarcode
  CART->>REPO: FindByBarcode 查商品
  REPO-->>CART: 命中商品
  CART->>EA: 广播 CartChangedEvent 汇总
  EA-->>VM: 更新购物车合计（主窗口状态栏同步刷新）`
    },
    {
      "title": "结算支付与外设联动链路",
      "mermaid": `sequenceDiagram
  participant U as 收银员
  participant VM as CashierViewModel
  participant DLG as DialogService
  participant ORD as OrderService
  participant CO as CheckoutCoordinator
  participant PM as ProtocolManager
  participant EA as IEventAggregator
  U->>VM: 确认结算（AsyncDelegateCommand）
  VM->>DLG: 弹出确认对话框（Prism IDialogService）
  DLG-->>VM: 用户确认支付
  VM->>ORD: 生成订单并扣减库存
  ORD-->>VM: Order 订单
  VM->>CO: CompleteAsync（含是否开钱箱）
  par 并行外设编排
    CO->>PM: PrintReceiptAsync 打印小票（串口）
    CO->>PM: OpenCashBoxAsync 开钱箱（Socket）
    CO->>PM: PublishOrderAsync 上报云端（MQTT）
  end
  PM-->>CO: 各设备执行结果（未连接返回 false）
  CO-->>VM: OrderFollowUpResult + 设备说明文案
  VM->>EA: 广播 PaymentCompletedEvent
  EA-->>DLG: 结算对话框展示结果（订单号/金额/设备状态）
  EA-->>VM: 主窗口提示条显示支付成功`
    },
    {
      "title": "设备监控高频报文处理链路",
      "mermaid": `sequenceDiagram
  participant DRV as 后台驱动线程
  participant Q as ConcurrentQueue
  participant T as DispatcherTimer 150ms
  participant VM as DeviceMonitorViewModel
  participant UI as 设备监控界面
  DRV->>Q: 高频报文入无锁队列
  DRV->>VM: DriverStateChanged（切回 UI 线程）
  VM->>UI: 刷新设备卡片状态灯
  T->>Q: 定时批量取出待处理帧
  Q-->>T: 一批 ProtocolFrame
  T->>UI: 批量刷新实时指标表与报文日志
  Note over VM,UI: 环形缓冲上限 200 条；离开页面（IActiveAware）即停表退订`
    }
  ],

  "dataModel": {
    "summary": "核心实体为 商品 Product → 购物车行项 CartItem → 订单 Order/OrderItem 的销售主链；设备侧 DeviceInfo 描述外设配置，ProtocolFrame 描述协议报文（方向/地址/负载），PosSettings 为门店设置。协议驱动在 断开→连接中→已连接 状态机间流转，购物车行项数量受库存与超卖开关约束。",
    "mermaid": `erDiagram
  Product ||--o{ CartItem : "扫码或点击加购"
  Product {
    string Code "商品编码/条码"
    string Name "名称"
    string Spec "规格"
    decimal Price "单价"
    int Stock "库存"
    string Category "分类"
  }
  CartItem ||--o{ OrderItem : "结算时快照"
  CartItem {
    Product Product "商品引用"
    int Quantity "数量（受上限约束）"
    int MaxQuantity "库存上限"
    decimal Subtotal "小计（派生）"
  }
  Order ||--|{ OrderItem : "包含"
  Order {
    string OrderNo "订单号"
    decimal Total "合计"
    decimal Paid "实收"
    string PaymentText "支付方式"
    int ItemCount "件数"
    DateTime CreatedAt "时间"
  }
  DeviceInfo ||--o{ ProtocolFrame : "收发报文"
  DeviceInfo {
    ProtocolType Type "协议类型"
    string Name "设备名"
    string Address "地址"
  }
  ProtocolFrame {
    ProtocolType Type "所属协议"
    ProtocolDirection Direction "收或发"
    string Payload "报文内容"
    string Description "语义说明"
  }
  PosSettings ||--o{ CartItem : "超卖开关约束上限"
  PosSettings {
    string StoreName "门店名"
    bool AllowOversell "允许超卖"
  }`
  }
};
