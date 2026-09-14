# project-know

> Generate comprehensive project documentation webpages from a remote git URL or a local project folder.
> 根据远程 git 仓库地址或本地项目文件夹，自动生成一份带架构图、组件矩阵、时序图、数据模型的详细项目说明网页。

project-know 是一个 AI Agent Skill：给定一个 **git 仓库地址**（https / git@ / ssh）或 **本地项目文件夹**，它先做客观分析（目录树、依赖、语言分布、Git 历史、README、CI/测试、Quick Start），再由 AI 补全架构判断，最终产出一份可直接双击打开的静态说明网页。

## 功能特性

- **12 个信息区块**：项目概览、README 自述、技术栈、模块架构图、系统部署图、功能组件矩阵、核心链路时序图、数据模型与状态流转、快速上手、质量评估、演进历史、目录树
- **Mermaid 图表**：架构图 / 部署图 / 时序图 / ER 图自动渲染，统一深色主题
- **按需加载**：项目数据按 id 拆分分片 + Mermaid 懒加载，项目再多也不拖慢首屏（file:// 协议同样可用）
- **零依赖分析脚本**：`scripts/analyze.js` 仅用 Node 内置模块，无需 npm install
- **跨平台**：Windows / macOS / Linux 均可运行；Windows 中文路径有内置防护

## 目录结构

```
project-know/
├── SKILL.md                 # Skill 指令（入口，各平台按规范读取）
├── README.md                # 本文档
├── LICENSE                  # MIT
├── package.json             # 元信息 / Node 版本声明
├── scripts/
│   └── analyze.js           # 客观分析脚本（零第三方依赖，Node >= 14）
├── references/
│   ├── schema.md            # 数据字段契约 + Mermaid 规范
│   ├── 方法论.md            # 认知方法论（如何补全 AI 字段）
│   └── sample-split.js      # 数据分片格式示例
└── assets/
    ├── icon.svg             # Skill 图标
    └── web/                 # 网页模板（复制到输出目录）
        ├── index.html
        ├── styles.css
        ├── app.js           # 渲染引擎（懒加载 / 分片 / 图表）
        ├── data.js          # 项目清单 manifest
        └── data/            # 项目分片（运行时按 id 生成）
```

## 环境要求

- **Node.js >= 14**（分析脚本使用 lookbehind 正则与 `spawnSync`）
- **git**（仅分析远程仓库时需要；本地文件夹分析不依赖）
- **网络**（网页渲染 Mermaid 图表默认走 CDN；离线方案见下）

## 安装

将本目录放置到目标平台的 skill 目录即可：

| 平台 | 放置位置 |
|---|---|
| CodeBuddy | 项目 `.codebuddy/skills/project-know/` 或用户级 skills 目录 |
| Claude Code | `~/.claude/skills/project-know/` 或项目 `.claude/skills/project-know/` |
| 其他兼容 Anthropic Skill 规范的工具 | 参照其文档，通常为 `<skills_dir>/project-know/` |

放置后确认目录内包含 `SKILL.md` 即完成安装。

## 使用

对 Agent 说：*"用 project-know 分析 https://github.com/expressjs/express"* 或 *"帮我生成这个项目的说明网页：<本地路径>"*。

Agent 将自动执行五步工作流：

1. 解析输入（远程 git 地址 / 本地目录）
2. 运行 `node scripts/analyze.js <input> --out <工作区>/output` 做客观分析
3. 复制 `assets/web/` 模板到 `<工作区>/project-know-output/web/`
4. 按 `references/schema.md` 补全架构 / 部署 / 组件 / 时序 / 数据模型等 AI 字段，生成数据分片
5. 注册清单并验证，产出可直接打开的 `index.html`

命令行参数（`scripts/analyze.js`）：

```
node analyze.js <git-url|local-path> [--id <id>] [--out <dir>] [--clone-dir <dir>] [--shallow] [--keep-clone]
```

## 输出产物

```
<工作区>/
├── output/<id>.json                 # 客观分析结果（中间产物，可删除）
├── .clones/<repo>-<hash>/           # 克隆源码（中间产物，可删除）
└── project-know-output/web/         # 最终网页，双击 index.html 即可浏览
    ├── index.html / styles.css / app.js / data.js
    └── data/<id>.js                 # 每个项目一个分片
```

## 离线部署

默认 Mermaid 图表通过 `https://cdn.jsdelivr.net` 加载。内网 / 离线环境：

1. 下载 `https://cdn.jsdelivr.net/npm/mermaid@10.9.1/dist/mermaid.min.js`；
2. 放入 `<输出目录>/web/vendor/mermaid.min.js`；
3. 将 `web/app.js` 中的 `MERMAID_SRC` 改为 `'vendor/mermaid.min.js'`。

## 开发与自检

```bash
node --check scripts/analyze.js      # 语法检查
node scripts/analyze.js <本地目录>    # 冒烟测试（本地目录无需 git/网络）
```

修改模板后，`assets/web/` 与旧输出目录需重新复制生效。

## License

[MIT](./LICENSE)
