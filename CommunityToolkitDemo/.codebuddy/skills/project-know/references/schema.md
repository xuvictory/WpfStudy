# 数据契约：window.PROJECT_ORDER / window.PROJECTS

数据采用「清单 + 按项目分片」结构，避免单文件过大拖慢首屏：

- `web/data.js`：轻量清单 `window.PROJECT_ORDER = [{ id, name, chunk, summary, generatedAt }]`
  与缓存对象 `window.PROJECTS = {}`（占位，运行期填充）。
- `web/data/<id>.js`：每个项目一个分片，内容为 `window.PROJECTS["<id>"] = { ... }`，
  由 `web/app.js` 在首次需要该项目时动态 `<script>` 注入（file:// 协议也可用）。
- 分片内字段结构如下，同时约束 **analyze.js 的客观输出** 与 **AI 补全的方法论判断字段**。

---

## 分片顶层结构

```js
// web/data/<id>.js
window.PROJECTS["<id>"] = {
  id: string,            // 唯一标识（英文短横线，与清单一致）
  name: string,          // 展示名（可中文）
  repo: string,          // git 地址或本地路径
  generatedAt: string,   // ISO 时间，生成时刻
  summary: string,       // 项目一句话定位（纯文本，≤320 字）
  techStack: TechStack,  // 技术栈分类
  tree: TreeNode,        // 目录树
  stats: Stats,          // 统计
  readme: Readme,
  evolution: Evolution,
  ci: Ci,
  tests: Tests,
  quickstart: QuickStart,   // 由 analyze.js 从 package.json 解析
  quality: Quality,         // lint 由 analyze.js 解析；summary / errorHandling 由 AI 补全
  // ↓↓↓ AI 用方法论补全的字段 ↓↓↓
  architecture: Architecture,
  deploy: Deploy,
  components: Component[],
  sequences: Sequence[],
  dataModel: DataModel,
};
```

---

## 字段明细

### TechStack
```js
{ language: string[], framework: string[], middleware: string[], tool: string[] }
```
由 analyze.js 从依赖清单解析，AI 可在此补充识别到的隐含技术（如看到 Dockerfile 补 'Docker'）。

### TreeNode（目录树，递归）
```js
{ name: string, type: 'dir'|'file', path: string, size?: number, children?: TreeNode[], collapsed?: boolean }
```
- `collapsed: true` 表示超深节点已截断（网页默认折叠）。
- 网页默认展开前两层，其余折叠，点击展开。

### Stats
```js
{ fileCount: number, langDist: Record<string, number> }
```

### Readme
```js
{ file: string|null, length: number, summary: string }
```

### Evolution（Git 元数据）
```js
{
  commits: number,
  recent: [{ hash: string, author: string, msg: string }],
  topContributors: string[],
  branches: string[],
  releases: string[],
  remote: string
}
```

### Ci / Tests
```js
ci:    { has: boolean, files: string[] }
tests: { has: boolean, dirs: string[] }
```

### QuickStart（运行与调试维度）
```js
{
  requirements: string[],                      // 环境要求（如 "Node.js >= 0.10.0"）
  steps: [{ name: string, cmd: string }],      // 安装 / 启动 / 测试等可执行步骤
  scripts: [{ name: string, cmd: string, desc?: string }]  // package.json scripts 清单
}
```
由 analyze.js 从 package.json 的 `engines` / `scripts` 自动解析；AI 可补 `desc`。

### Quality（工程化与质量维度）
```js
{
  summary: string,               // AI 补全：一句话质量总评
  lint: { has: boolean, files: string[] },  // analyze.js 解析的规范配置
  errorHandling: string          // AI 补全：错误处理与日志机制说明
}
```

---

## AI 补全字段（方法论判断）

### Architecture —— 模块架构图
```js
{
  mermaid: string,   // Mermaid graph TD / flowchart，描述模块划分与依赖方向
  modules: [
    { name: string, responsibility: string, entry: string, dependsOn: string[] }
  ]
}
```
**Mermaid 规范**：
- 用 `graph TD` 或 `flowchart LR`。
- 节点 id 用字母/数字，label 用 `["文本"]`；避免含特殊字符（`"`, `<`, `>`, `{`, `}`）直接进 label，必要时用引号包裹。
- 依赖方向：`A --> B` 表示 A 依赖 B。
- 模块按"层"分组可用 `subgraph`：
  ```
  graph TD
    subgraph 接入层
      ROUTE["路由模块"]
    end
    subgraph 业务层
      SVC["服务模块"]
    end
    ROUTE --> SVC
  ```

### Deploy —— 系统部署图
```js
{ mermaid: string, note: string }
```
**Mermaid 规范**：用 `graph TD` 或 `flowchart LR` 画部署拓扑（用户/浏览器 → 网关 → 服务 → 数据库/缓存/消息队列）。
可用 `subgraph` 区分「客户端 / 服务端 / 基础设施」。

### Component —— 功能组件矩阵
```js
[
  { name: string, duty: string, entry: string, deps: string[], layer: string }
]
```
- `layer`：用于网页分组（如 接入层 / 业务层 / 数据层 / 基础设施）。
- `entry`：组件入口文件或入口函数（相对路径）。
- `deps`：依赖的其他组件名。

### DataModel —— 数据模型与状态流转
```js
{
  summary: string,     // 一句话说明核心数据对象与流转
  mermaid: string      // erDiagram / stateDiagram-v2 / flowchart（数据流）
}
```
**Mermaid 规范**：
- 实体关系用 `erDiagram`；状态流转用 `stateDiagram-v2`；请求数据流用 `flowchart LR`。
- label 含中文/空格/符号时必须用 `["..."]` 包裹。

### Sequence —— 核心链路时序图
```js
[
  { title: string, mermaid: string }
]
```
**Mermaid 规范**：用 `sequenceDiagram`。示例：
```
sequenceDiagram
  participant U as 用户
  participant C as 控制器
  participant S as 服务
  participant D as 数据层
  U->>C: 发起请求
  C->>S: 调用业务
  S->>D: 读写数据
  D-->>S: 返回结果
  S-->>C: 业务结果
  C-->>U: 响应
```
- 至少 1 条，建议 2~4 条核心链路（启动、请求、数据、定时任务等）。
- `participant` 别名用 `as` 起可读名。

---

## Mermaid 通用约束（重要）
1. 所有 mermaid 字符串必须是合法 Mermaid 文本，网页用 `mermaid.run()` 渲染。
2. 字符串中的换行用 `\n`（在 JS 模板字符串里直接换行亦可）。
3. label 包含中文/空格必须放在 `["..."]` 或 `["文本"]` 中。
4. 不要在 label 里直接写 `-->`、`->>`、`---` 等箭头符号。
5. 图表方向：架构/部署推荐 `graph TD`（自上而下）或 `flowchart LR`（左右）；
   时序固定 `sequenceDiagram`。

---

## 多项目与数据扩展
- 每个项目一个分片 `web/data/<id>.js`，`id` 必须唯一。
- 新增项目：`node analyze.js <input> --id <id>` 生成 `output/<id>.json`，
  由 AI 补全 AI 字段后写入 `web/data/<id>.js`，并在 `web/data.js` 的
  `window.PROJECT_ORDER` 追加一条元信息，无需改动网页代码。
- 分片文件可能很大（真实目录树 / 图表），但它是按需加载的，不影响首屏。
- 网页对超大数据的处理：
  1. Mermaid 引擎由 app.js 空闲预取 + 按需渲染，不阻塞首屏；
  2. 目录树折叠分支采用懒构建（首次展开才创建 DOM）；
  3. 大数据区块使用 `content-visibility` 跳过离屏渲染（见 styles.css）。
