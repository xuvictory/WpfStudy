/**
 * data.js —— 项目清单（manifest）
 *
 * 只保留轻量元信息用于首屏秒开与标签切换；完整项目数据按 id 拆分到
 * web/data/<id>.js，由 app.js 在首次需要该项目时动态 <script> 注入
 * （file:// 协议下也能正常加载）。
 *
 * ── 新增项目的标准流程 ──────────────────────────────
 *   1. 运行 project-know skill：node <skill-root>/scripts/analyze.js
 *      <git-url|local-path> --id <id> --out <输出目录>/output
 *      （<skill-root> 为 skill 所在目录，即 SKILL.md 所在目录）
 *   2. 基于 output/<id>.json 补全架构/部署/组件/时序等 AI 字段
 *   3. 写入 <输出目录>/web/data/<id>.js（内容为 window.PROJECTS["<id>"] = {...}）
 *   4. 在下方 window.PROJECT_ORDER 追加一条元信息（id 必须唯一）
 *   无需改动任何网页代码。
 *
 * 数据量越大，本方案收益越大：首屏只加载清单 + 当前项目分片，
 * Mermaid 引擎也在空闲时按需加载，全程不阻塞首屏渲染。
 */
window.PROJECTS = {}; // 已加载项目数据缓存（key = 项目 id），app.js 消费

// 由 project-know skill 在每次分析项目后追加元信息，结构示例：
// { id: "express", name: "Express", chunk: "data/express.js",
//   summary: "一句话项目定位", generatedAt: "2026-08-28T07:44:09.303Z" }
window.PROJECT_ORDER = [
  {
    id: "communitytoolkitdemo",
    name: "智汇 POS 收银终端",
    chunk: "data/communitytoolkitdemo.js",
    summary: "基于 .NET 10 + WPF + CommunityToolkit.Mvvm 的单机 POS 收银终端示例，含收银台/设备监控/设置三大页面与五类模拟协议驱动外设联动。",
    generatedAt: "2026-09-11T09:34:47.487Z"
  }
];
