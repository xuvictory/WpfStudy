/* sample-split.js —— 项目数据分片参考样例（精简版）
 *
 * 这是 project-know skill 生成 web/data/<id>.js 分片时的格式参考。
 * 字段契约见 references/schema.md；补全判断依据见 references/方法论.md。
 *
 * 生成规则：
 *  1. 客观字段（techStack / tree / stats / readme / evolution / ci / tests /
 *     quickstart / quality.lint）从 output/<id>.json 原样搬入，不要臆造；
 *  2. 下列 AI 字段（architecture / deploy / components / sequences / dataModel /
 *     quality.summary / quality.errorHandling）由 AI 按方法论阅读源码后补全；
 *  3. 所有 Mermaid 字符串必须是合法 Mermaid 文本（网页用 mermaid.run() 渲染），
 *     label 含中文/空格用 ["..."] 包裹，label 内禁止出现 --> 等箭头符号；
 *  4. 分片体积可能很大（真实目录树/图表），属正常现象，按需加载不影响首屏。
 */
window.PROJECTS["express"] = {
  "id": "express",
  "name": "Express",
  "repo": "https://github.com/expressjs/express.git",
  "generatedAt": "2026-08-28T07:44:09.303Z",
  "summary": "Fast, unopinionated, minimalist web framework for Node.js —— 路由与中间件机制极简的 HTTP 服务框架，Node 生态的事实标准。",

  "techStack": {
    "language": ["JavaScript", "TypeScript"],
    "framework": ["Express"],
    "middleware": ["body-parser", "serve-static"],
    "tool": []
  },

  // 目录树：真实项目由 analyze.js 扫描（maxDepth=4，超深节点 collapsed:true）
  "tree": {
    "name": "express", "type": "dir", "path": "",
    "children": [
      { "name": "lib", "type": "dir", "path": "lib", "children": [
        { "name": "application.js", "type": "file", "path": "lib/application.js", "size": 14555 },
        { "name": "router", "type": "dir", "path": "lib/router", "children": [
          { "name": "index.js", "type": "file", "path": "lib/router/index.js", "size": 8200 }
        ]}
      ]},
      { "name": "test", "type": "dir", "path": "test", "children": [
        { "name": "app.test.js", "type": "file", "path": "test/app.test.js", "size": 812 }
      ]},
      { "name": "package.json", "type": "file", "path": "package.json", "size": 3100 }
    ]
  },

  "stats": { "fileCount": 124, "langDist": { "JavaScript": 96, "Markdown": 18, "JSON": 10 } },

  "readme": { "file": "Readme.md", "length": 10653, "summary": "Fast, unopinionated, minimalist web framework for Node.js Express is a minimal and flexible Node.js web application framework that provides a robust set of features for web and mobile applications" },

  "evolution": {
    "commits": 14472,
    "recent": [
      { "hash": "a1b2c3d", "author": "expressjs-bot", "msg": "4.19.2" },
      { "hash": "e4f5g6h", "author": "somebody", "msg": "docs: fix typo" }
    ],
    "topContributors": ["Douglas Christopher Wilson", "TJ Holowaychuk"],
    "branches": ["master", "4.20"],
    "releases": ["4.19.2", "4.19.1"],
    "remote": "https://github.com/expressjs/express.git"
  },

  "ci": { "has": true, "files": [".github/workflows/ci.yml", ".github/workflows/codeql.yml"] },
  "tests": { "has": true, "dirs": ["test", "benchmark"] },

  "quickstart": {
    "requirements": ["Node.js >= 0.10.0"],
    "steps": [
      { "name": "克隆仓库", "cmd": "git clone https://github.com/expressjs/express.git" },
      { "name": "安装依赖", "cmd": "npm install" },
      { "name": "运行测试", "cmd": "npm test" }
    ],
    "scripts": [
      { "name": "test", "cmd": "npm run test-ci", "desc": "运行测试套件" }
    ]
  },

  // ↓↓↓ 以下为 AI 按方法论补全的字段 ↓↓↓

  "quality": {
    "summary": "工程化成熟：完备的 lint 约束、超 5000 个测试用例覆盖路由/中间件核心链路，CI 矩阵覆盖多 Node 版本。",
    "lint": { "has": true, "files": [".eslintrc.yml", ".editorconfig", "package.json (scripts.lint)"] },
    "errorHandling": "内置默认错误处理器兜底，错误中间件以 (err, req, res, next) 四参签名识别；异步错误需显式 next(err) 转发。"
  },

  "architecture": {
    "mermaid": `graph TD
  subgraph 接入层
    ROUTE["路由模块"]
  end
  subgraph 业务层
    SVC["服务模块"]
  end
  subgraph 数据层
    DAO["数据访问模块"]
  end
  ROUTE --> SVC
  SVC --> DAO`,
    "modules": [
      { "name": "路由模块", "responsibility": "HTTP 请求分发与中间件编排", "entry": "lib/router/index.js", "dependsOn": [] },
      { "name": "服务模块", "responsibility": "核心业务逻辑", "entry": "lib/application.js", "dependsOn": ["路由模块"] },
      { "name": "数据访问模块", "responsibility": "数据读写", "entry": "lib/db.js", "dependsOn": ["服务模块"] }
    ]
  },

  "deploy": {
    "mermaid": `graph TD
  subgraph 客户端
    B["浏览器 / App"]
  end
  subgraph 服务端
    GW["Nginx 网关"]
    APP["Node.js 应用"]
  end
  subgraph 基础设施
    DB[("MySQL")]
    CACHE["Redis"]
  end
  B --> GW
  GW --> APP
  APP --> DB
  APP --> CACHE`,
    "note": "单机部署即可支撑中小流量；网关层处理 TLS/静态资源，应用层无状态可水平扩展。"
  },

  "components": [
    { "name": "路由分发器", "duty": "将 URL 匹配到处理器并执行中间件链", "entry": "lib/router/index.js", "deps": [], "layer": "接入层" },
    { "name": "应用核心", "duty": "应用生命周期与配置管理", "entry": "lib/application.js", "deps": ["路由分发器"], "layer": "业务层" },
    { "name": "数据访问", "duty": "统一数据读写入口", "entry": "lib/db.js", "deps": ["应用核心"], "layer": "数据层" }
  ],

  "sequences": [
    {
      "title": "一次完整 HTTP 请求",
      "mermaid": `sequenceDiagram
  participant U as 用户
  participant C as 控制器
  participant S as 服务
  participant D as 数据层
  U->>C: 发起请求
  C->>S: 调用业务
  S->>D: 读写数据
  D-->>S: 返回结果
  S-->>C: 业务结果
  C-->>U: 响应`
    }
  ],

  "dataModel": {
    "summary": "核心数据对象为订单实体，状态在 待支付→已支付→已发货→已完成 之间流转。",
    "mermaid": `stateDiagram-v2
  [*] --> 待支付
  待支付 --> 已支付: 支付成功
  已支付 --> 已发货: 商家发货
  已发货 --> 已完成: 确认收货
  已支付 --> 已取消: 超时未发货`
  }
};
