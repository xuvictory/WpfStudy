---
name: project-know
version: 1.0.0
description: Generate a comprehensive project documentation webpage from a remote git repository URL (https/git@/ssh) or a local project folder, covering tech stack, architecture diagrams, component matrix, sequence diagrams, data model, quick start and evolution history. Use when the user inputs a repo URL or local project folder and wants an automatically generated project overview. 用户输入仓库地址或本地文件夹并希望自动生成项目说明网页时使用。
---

# project-know

根据用户输入的**远程 git 仓库地址**或**本地项目文件夹**，自动生成该项目的详细说明网页，独立输出到当前工作区 `project-know-output/`（可自定义目录），包含 12 个信息区块：项目概览、README 自述、技术栈、模块架构图、系统部署图、功能组件矩阵、核心链路时序图、数据模型与状态流转、快速上手、质量评估、演进历史、目录树。

## 环境要求

- Node.js >= 14（`scripts/analyze.js` 零第三方依赖）
- git 命令（分析远程仓库时用于克隆）
- 网页渲染 Mermaid 图表需要联网；离线方案见 `README.md`「离线部署」

## 路径约定（重要）

SKILL.md 所在目录为 skill 根目录，下文用 `SKILL_ROOT` 表示。所有相对路径均相对该目录，不依赖任何平台占位符：

- `scripts/analyze.js`：分析脚本
- `assets/web/`：网页模板（index.html / styles.css / app.js / data.js / data/）
- `references/schema.md`：字段契约与 Mermaid 规范
- `references/方法论.md`：认知方法论
- `references/sample-split.js`：数据分片格式参考

## 输入识别

1. 用户可能给出 git URL（`https://…`、`git@…:…`、`ssh://…`、以 `.git` 结尾）或本地文件夹路径。
2. 若为本地路径且含中文，遵循附文「Windows 环境注意事项」。

## 工作流（五步）

### 步骤 1：解析输入

- 读取用户输入，判断类型：远程 git 地址 或 本地目录。
- 确定 projectId：默认由 analyze.js 从输入自动推导，用户要求时可加 `--id <自定义id>` 指定。

### 步骤 2：客观分析

- 运行：`node <SKILL_ROOT>/scripts/analyze.js <git-url|local-path> --out <工作区>/output`
- 可选参数：`--id <id>`（自定义 id）、`--clone-dir <dir>`（克隆临时目录，默认 `工作区/.clones`）、`--shallow`（浅克隆）、`--keep-clone`（保留克隆目录）。
- 产物：`<工作区>/output/<id>.json`，含目录树、依赖清单、语言分布、Git 元数据、README 摘要、CI/测试识别、Quick Start、lint 配置。
- 克隆使用 spawn 数组参数（脚本已内置），**禁止把用户输入拼进 shell 字符串**；克隆失败时向用户展示 stderr 并终止流程。
- 克隆目录按「仓库名 + 输入地址短哈希」命名（如 `.clones/express-3f2a9b1c`），同名不同源的仓库不会互相覆盖。

### 步骤 3：复制模板

- 将 `SKILL_ROOT/assets/web/` 完整复制到 `<工作区>/project-know-output/web/`（保留 index.html / styles.css / app.js / data.js / data/ 结构）。
- 用户指定了输出目录时，以用户目录替代 `project-know-output`。
- 涉及中文路径时用文件读写工具完成复制，禁止在命令行直接粘贴中文路径（见附）。

### 步骤 4：AI 补全并生成数据分片

- 读取 `<工作区>/output/<id>.json`。
- 参考 `SKILL_ROOT/references/schema.md`（字段契约 + Mermaid 规范）与 `SKILL_ROOT/references/方法论.md`（认知方法论），必要时阅读克隆源码目录（`<工作区>/.clones/`）或本地项目源码补全判断。
- 按 `SKILL_ROOT/references/sample-split.js` 的格式，生成完整数据对象：
  - **客观字段**（techStack / tree / stats / readme / evolution / ci / tests / quickstart / quality.lint）从 JSON 原样搬入，不要臆造；
  - **补全字段**：`architecture`（graph TD + modules[]）、`deploy`（部署拓扑 + note）、`components`（功能组件矩阵）、`sequences`（2~4 条 sequenceDiagram）、`dataModel`（erDiagram / stateDiagram-v2 / flowchart）、`quality.summary` 与 `quality.errorHandling`、`summary` 润色为连贯自述。
- 写入 `<工作区>/project-know-output/web/data/<id>.js`，内容为 `window.PROJECTS["<id>"] = { ... };`。

### 步骤 5：注册清单并验证

- 编辑 `<工作区>/project-know-output/web/data.js`：在 `window.PROJECT_ORDER` 数组追加一条元信息 `{ id, name, chunk: "data/<id>.js", summary, generatedAt }`。
- 验证：
  - 用 list_dir 确认分片与模板文件就位；
  - 用 node 脚本 `new Function()` 解析分片 JS 确认语法正确；
  - 检查 Mermaid 字符串符合 schema.md 约束（label 引号、禁止裸箭头符号）。
- 告知用户输出目录，提示打开 `<工作区>/project-know-output/web/index.html` 浏览（file:// 协议可直接打开）。

## Mermaid 规范要点

- 架构/部署用 `graph TD` 或 `flowchart LR`；时序固定 `sequenceDiagram`；数据模型用 `erDiagram` / `stateDiagram-v2` / `flowchart`。
- label 含中文/空格必须写成 `["文本"]`；label 内禁止出现 `-->`、`->>`、`---` 等箭头符号。
- mermaid 字符串在 JS 中可用模板字符串（反引号）书写，换行即 `\n`，网页用 `mermaid.run()` 渲染。

## 其他约束

- 不要修改 `SKILL_ROOT` 内的任何资产文件（脚本与模板由 skill 维护，用户侧只改输出目录里的文件）。
- 分析产生的 `<工作区>/output/<id>.json` 与 `.clones/` 属于分析中间产物，默认保留（`.clones/` 可随时删除）。
- 分片体积可能很大（真实目录树 + 图表），属正常现象，网页按需加载不影响首屏。

## 附：Windows 环境注意事项

仅当运行环境为 Windows（尤其 PowerShell）时参考：

- PowerShell 命令行传中文路径会被转码乱码（如「上位机自动化控制」变成「涓婁綅鏈鸿嚜鍔ㄥ寲鎺у埗」），导致文件被复制到错误目录。
- 涉及中文路径的复制、验证、启动操作，一律改用文件读写工具（read_file / write_to_file / list_dir / search_file），或写 node 脚本用 String.fromCharCode 转义构造路径。
- 验证目录结构用 list_dir；验证 JS 语法用 node 脚本 `new Function()` 解析（node --check 的 stdout 可能被环境吞掉）。
- analyze.js 内部已用 spawn 数组参数 + cwd，可安全接收中文路径参数，但其余步骤仍建议优先使用文件读写工具。
