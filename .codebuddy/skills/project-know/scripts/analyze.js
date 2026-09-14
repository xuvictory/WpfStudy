#!/usr/bin/env node
/**
 * analyze.js —— 项目自动认知分析脚本（零第三方依赖）
 * project-know skill 内置版本，可在任意工作区运行。
 *
 * 输入：git 地址或本地文件夹路径
 * 输出：<--out 指定目录>/<projectId>.json  （标准知识 JSON，供网页 data.js 消费）
 *
 * 设计要点：
 *  - 用 child_process.spawn 数组参数 + cwd，规避 Windows 中文路径被 shell 转码乱码
 *  - fs 读取统一 UTF-8
 *  - 只做"客观可量化"的事：目录树、依赖清单、语言分布、Git 元数据、README 摘要、
 *    CI/测试识别。架构判断、部署图、时序图由 AI 在生成阶段基于本输出补全。
 *
 * 用法：
 *   node analyze.js <git-url|local-path> [--id customId] [--out <dir>] [--clone-dir <dir>] [--keep-clone] [--shallow]
 *
 *   --out        输出目录（默认：当前工作区/output）
 *   --clone-dir  git 克隆临时目录（默认：当前工作区/.clones）
 *   --id         自定义 projectId（默认从输入推导）
 *   --keep-clone 保留克隆目录
 *   --shallow    浅克隆（--depth 1），默认全量克隆以保留完整 Git 历史
 */

'use strict';

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { spawnSync } = require('child_process');

// ---------- 配置常量 ----------
// 从 CLI 取参数（如 --out <dir> / --clone-dir <dir>），缺省落在"当前工作区"，
// 保证 skill 在任意工作区都能把产物输出到用户当前目录，而非脚本所在目录。
function argValue(flag, fallback) {
  const i = process.argv.indexOf(flag);
  // 守卫：下一个参数以 `--` 开头（或不存在）时，视为 flag 缺值，回退默认值
  const next = process.argv[i + 1];
  const v = i >= 0 && next && !next.startsWith('--') ? next : null;
  return v ? path.resolve(v) : fallback;
}
const OUTPUT_DIR = argValue('--out', path.join(process.cwd(), 'output'));
const CLONE_TMP_DIR = argValue('--clone-dir', path.join(process.cwd(), '.clones'));
const KEEP_CLONE = process.argv.includes('--keep-clone');
const SHALLOW = process.argv.includes('--shallow'); // 默认全量克隆以保留完整 Git 历史

// 忽略的目录/文件（不进入目录树、不计入统计）
const IGNORE_DIRS = new Set([
  'node_modules', '.git', 'dist', 'build', 'target', 'out',
  'vendor', '.idea', '.vscode', '__pycache__', '.next', '.cache',
  'coverage', '.turbo', 'bin', 'obj', 'venv', '.venv', 'env'
]);
const IGNORE_FILES = new Set([
  '.DS_Store', 'Thumbs.db', 'package-lock.json', 'yarn.lock',
  'pnpm-lock.yaml', 'go.sum', '*.min.js', '*.min.css', '.gitignore'
]);

// 语言识别（按扩展名）
const LANG_BY_EXT = {
  js: 'JavaScript', jsx: 'JavaScript', mjs: 'JavaScript', cjs: 'JavaScript',
  ts: 'TypeScript', tsx: 'TypeScript',
  py: 'Python', rb: 'Ruby', go: 'Go', java: 'Java', kt: 'Kotlin',
  rs: 'Rust', c: 'C', h: 'C', cpp: 'C++', cc: 'C++', hpp: 'C++',
  cs: 'C#', php: 'PHP', swift: 'Swift', dart: 'Dart',
  html: 'HTML', htm: 'HTML', css: 'CSS', scss: 'SCSS', less: 'LESS',
  vue: 'Vue', svelte: 'Svelte',
  json: 'JSON', yaml: 'YAML', yml: 'YAML', toml: 'TOML', xml: 'XML',
  md: 'Markdown', rst: 'reStructuredText', txt: 'Text',
  sh: 'Shell', bash: 'Shell', ps1: 'PowerShell', bat: 'Batch',
  sql: 'SQL', proto: 'Protobuf', graphql: 'GraphQL',
  Dockerfile: 'Docker'
};

// 依赖清单文件 → 解析函数
const MANIFEST_FILES = {
  'package.json': parsePackageJson,
  'pom.xml': parsePomXml,
  'go.mod': parseGoMod,
  'requirements.txt': parseRequirementsTxt,
  'Cargo.toml': parseCargoToml,
  'build.gradle': parseGradle,
  'pubspec.yaml': parseYamlList,
  'Gemfile': parseGemfile,
  'composer.json': parsePackageJson
};

// CI / 测试 识别
const CI_FILE_HINTS = [
  '.github/workflows', '.gitlab-ci.yml', '.travis.yml', 'circle.yml',
  'jenkinsfile', 'Jenkinsfile', 'azure-pipelines.yml', '.drone.yml',
  'bitbucket-pipelines.yml'
];
const TEST_DIR_HINTS = ['test', 'tests', '__tests__', 'spec', 'specs', 'e2e', 'testsuite'];

// ---------- 工具函数 ----------
function logStage(name) {
  console.log(`\n[阶段] ${name}`);
}
function logInfo(msg) { console.log(`  · ${msg}`); }

function runGit(args, cwd) {
  const r = spawnSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 1024 * 1024 * 64 });
  if (r.status !== 0) return '';
  return (r.stdout || '').trim();
}

function isIgnoredDir(name) {
  return IGNORE_DIRS.has(name) || name.startsWith('.') && IGNORE_DIRS.has(name.slice(1));
}

// 克隆或定位项目根目录
function cloneOrResolve(input) {
  logStage('定位项目');
  const isGitUrl = /^https?:\/\//.test(input) || input.endsWith('.git') ||
    input.startsWith('git@') || input.startsWith('ssh://');
  const isLocal = fs.existsSync(input) && fs.statSync(input).isDirectory();

  if (isLocal) {
    logInfo(`本地路径：${input}`);
    return { root: path.resolve(input), isClone: false, localPath: path.resolve(input) };
  }
  if (isGitUrl) {
    if (!fs.existsSync(CLONE_TMP_DIR)) fs.mkdirSync(CLONE_TMP_DIR, { recursive: true });
    // 用「仓库名 + 输入地址短哈希」分目录，避免同名不同源仓库互相覆盖
    const repoName = input.split('/').pop().replace(/\.git$/, '') || 'repo';
    const hash = crypto.createHash('md5').update(input).digest('hex').slice(0, 8);
    const dest = path.join(CLONE_TMP_DIR, repoName + '-' + hash);
    if (fs.existsSync(dest)) {
      logInfo(`复用已克隆目录：${dest}`);
    } else {
      logInfo(`克隆 ${input} → ${dest}`);
      const cloneArgs = SHALLOW ? ['clone', '--depth', '1', input, dest]
                                 : ['clone', input, dest];
      const r = spawnSync('git', cloneArgs, {
        encoding: 'utf8', maxBuffer: 1024 * 1024 * 64
      });
      if (r.status !== 0) {
        throw new Error('git clone 失败：' + (r.stderr || r.stdout));
      }
    }
    return { root: dest, isClone: true, localPath: input };
  }
  throw new Error('输入既不是本地目录也不是可识别的 git 地址：' + input);
}

// 扫描目录树 + 语言统计
function scanTree(root, relBase = '', depth = 0, maxDepth = 4) {
  const entryName = path.basename(root);
  if (IGNORE_DIRS.has(entryName)) return null;

  let stat;
  try { stat = fs.statSync(root); } catch { return null; }

  if (!stat.isDirectory()) return null;
  if (depth > maxDepth) {
    return { name: entryName, type: 'dir', path: relBase, collapsed: true };
  }

  const children = [];
  let dirs = [];
  try { dirs = fs.readdirSync(root); } catch { return null; }

  for (const name of dirs.sort()) {
    if (IGNORE_DIRS.has(name)) continue;
    if (depth === 0 && (name === '.git')) continue;
    const full = path.join(root, name);
    let st;
    try { st = fs.statSync(full); } catch { continue; }
    const rel = relBase ? relBase + '/' + name : name;
    if (st.isDirectory()) {
      const sub = scanTree(full, rel, depth + 1, maxDepth);
      if (sub) children.push(sub);
    } else {
      if (IGNORE_FILES.has(name)) continue;
      children.push({ name, type: 'file', path: rel, size: st.size });
    }
  }
  return { name: entryName, type: 'dir', path: relBase, children };
}

// 语言分布统计（全量，不限深度）
function statLang(root, relBase = '', acc) {
  let entryName = path.basename(root);
  if (IGNORE_DIRS.has(entryName)) return;
  let stat;
  try { stat = fs.statSync(root); } catch { return; }
  if (!stat.isDirectory()) return;

  let names = [];
  try { names = fs.readdirSync(root); } catch { return; }
  for (const name of names) {
    if (IGNORE_DIRS.has(name)) continue;
    const full = path.join(root, name);
    let st;
    try { st = fs.statSync(full); } catch { continue; }
    const rel = relBase ? relBase + '/' + name : name;
    if (st.isDirectory()) {
      statLang(full, rel, acc);
    } else {
      if (IGNORE_FILES.has(name)) continue;
      const ext = name.includes('.') ? name.split('.').pop().toLowerCase() : '';
      let lang = LANG_BY_EXT[ext] || (name === 'Dockerfile' ? 'Docker' : 'Other');
      acc[lang] = (acc[lang] || 0) + 1;
      acc.__files = (acc.__files || 0) + 1;
    }
  }
}

// 解析 package.json 的 engines 与 scripts，生成 Quick Start 客观数据
function parseQuickStart(root, remote) {
  const p = path.join(root, 'package.json');
  if (!fs.existsSync(p)) {
    return { requirements: [], steps: [], scripts: [] };
  }
  try {
    const j = JSON.parse(fs.readFileSync(p, 'utf8'));
    const requirements = [];
    if (j.engines && j.engines.node) requirements.push('Node.js ' + j.engines.node);
    if (j.packageManager) requirements.push('Package Manager: ' + j.packageManager);
    const scripts = [];
    const skip = new Set(['install', 'postinstall', 'preinstall', 'prepare', 'prepublishOnly']);
    for (const k of Object.keys(j.scripts || {})) {
      if (skip.has(k)) continue;
      scripts.push({ name: k, cmd: String(j.scripts[k]) });
    }
    const steps = [];
    if (remote) {
      steps.push({ name: '克隆仓库', cmd: remote });
    }
    steps.push({ name: '安装依赖', cmd: 'npm install' });
    if (j.scripts && j.scripts.start) steps.push({ name: '启动服务', cmd: 'npm start' });
    if (j.scripts && j.scripts.dev) steps.push({ name: '开发模式', cmd: 'npm run dev' });
    else if (j.scripts && j.scripts.development) steps.push({ name: '开发模式', cmd: 'npm run development' });
    if (j.scripts && j.scripts.test) steps.push({ name: '运行测试', cmd: 'npm test' });
    return { requirements, steps, scripts };
  } catch (e) {
    logWarn('package.json 解析失败：' + e.message);
    return { requirements: [], steps: [], scripts: [] };
  }
}

// 识别代码规范 / 格式约束配置文件
function detectLint(root) {
  const hints = [
    '.eslintrc', '.eslintrc.js', '.eslintrc.cjs', '.eslintrc.json', '.eslintrc.yml', '.eslintrc.yaml',
    '.prettierrc', '.prettierrc.js', '.prettierrc.json', '.prettierrc.yml', '.prettierrc.yaml',
    '.editorconfig', '.stylelintrc', '.stylelintrc.json', 'tslint.json', '.eslintignore', '.prettierignore'
  ];
  const files = [];
  for (const h of hints) {
    if (fs.existsSync(path.join(root, h))) files.push(h);
  }
  try {
    const p = path.join(root, 'package.json');
    if (fs.existsSync(p)) {
      const j = JSON.parse(fs.readFileSync(p, 'utf8'));
      if (j.eslintConfig) files.push('package.json (eslintConfig)');
      if (j.prettier) files.push('package.json (prettier)');
      if (j.scripts && j.scripts.lint && /eslint|prettier|stylelint/.test(j.scripts.lint)) {
        files.push('package.json (scripts.lint)');
      }
    }
  } catch (e) { /* ignore */ }
  return { has: files.length > 0, files };
}

// 解析依赖清单，返回 { language, framework, middleware, tool } 分类标签
function parseManifest(root) {
  logStage('解析依赖清单');
  const tech = { language: new Set(), framework: new Set(), middleware: new Set(), tool: new Set() };
  const manifests = {};

  for (const mf of Object.keys(MANIFEST_FILES)) {
    const p = path.join(root, mf);
    if (fs.existsSync(p)) {
      try {
        const content = fs.readFileSync(p, 'utf8');
        manifests[mf] = content;
        MANIFEST_FILES[mf](content, tech, mf);
      } catch (e) { logInfo(`解析 ${mf} 失败：${e.message}`); }
    }
  }
  // 语言信号（基于清单文件本身）
  if (manifests['package.json']) { tech.language.add('JavaScript'); tech.language.add('TypeScript'); }
  if (manifests['pom.xml'] || manifests['build.gradle']) tech.language.add('Java');
  if (manifests['go.mod']) tech.language.add('Go');
  if (manifests['requirements.txt'] || manifests['pubspec.yaml']) {/* handled below */}
  if (manifests['Cargo.toml']) tech.language.add('Rust');
  if (manifests['Gemfile']) tech.language.add('Ruby');
  if (manifests['composer.json']) tech.language.add('PHP');

  return {
    raw: manifests,
    tech: {
      language: [...tech.language],
      framework: [...tech.framework],
      middleware: [...tech.middleware],
      tool: [...tech.tool]
    }
  };
}

function parsePackageJson(content, tech) {
  let j; try { j = JSON.parse(content); } catch { return; }
  const deps = { ...(j.dependencies || {}), ...(j.devDependencies || {}) };
  const knownFw = {
    react: 'React', vue: 'Vue', 'next': 'Next.js', '@angular/core': 'Angular',
    nuxt: 'Nuxt', svelte: 'Svelte', express: 'Express', koa: 'Koa', nest: 'NestJS',
    'socket.io': 'Socket.IO', webpack: 'Webpack', vite: 'Vite', rollup: 'Rollup',
    tailwindcss: 'Tailwind', typescript: 'TypeScript', electron: 'Electron',
    'react-native': 'React Native', jest: 'Jest', axios: 'Axios', graphql: 'GraphQL',
    mongodb: 'MongoDB', mongoose: 'Mongoose', sequelize: 'Sequelize', prisma: 'Prisma',
    redis: 'Redis', 'socket.io-client': 'Socket.IO'
  };
  const knownMid = {
    mongodb: 'MongoDB', redis: 'Redis', mysql: 'MySQL', pg: 'PostgreSQL',
    'aws-sdk': 'AWS', '@aws-sdk': 'AWS', '@google-cloud': 'GCP', 'ali-oss': 'Aliyun OSS'
  };
  for (const k of Object.keys(deps)) {
    const base = k.split('/').pop();
    if (knownFw[base] || knownFw[k]) tech.framework.add(knownFw[base] || knownFw[k]);
    if (knownMid[base] || knownMid[k]) tech.middleware.add(knownMid[base] || knownMid[k]);
  }
  if (j.scripts && (j.scripts.build || j.scripts.start)) tech.tool.add('Node.js');
}

function parsePomXml(content, tech) {
  const fw = ['spring-boot', 'spring-web', 'spring-cloud', 'mybatis', 'hibernate-core',
    'struts', 'quarkus', 'micrometer'];
  for (const f of fw) {
    if (content.includes(f)) {
      const map = { 'spring-boot': 'Spring Boot', 'spring-web': 'Spring MVC',
        'spring-cloud': 'Spring Cloud', mybatis: 'MyBatis', 'hibernate-core': 'Hibernate',
        struts: 'Struts', quarkus: 'Quarkus', micrometer: 'Micrometer' };
      tech.framework.add(map[f]);
    }
  }
  if (/<artifactId>mysql-connector/.test(content)) tech.middleware.add('MySQL');
  if (/<artifactId>postgresql/.test(content)) tech.middleware.add('PostgreSQL');
  if (/<artifactId>redis/.test(content)) tech.middleware.add('Redis');
}

function parseGoMod(content, tech) {
  if (/gin-gonic\/gin/.test(content)) tech.framework.add('Gin');
  if (/labstack\/echo/.test(content)) tech.framework.add('Echo');
  if (/gorilla\/mux/.test(content)) tech.framework.add('Gorilla Mux');
  if (/gorm/.test(content)) tech.framework.add('GORM');
  if (/go-redis/.test(content)) tech.middleware.add('Redis');
}

function parseRequirementsTxt(content, tech) {
  tech.language.add('Python');
  const lines = content.split('\n').map(l => l.split(/[=<>~]/)[0].trim()).filter(Boolean);
  const map = { django: 'Django', flask: 'Flask', fastapi: 'FastAPI', tornado: 'Tornado',
    sqlalchemy: 'SQLAlchemy', redis: 'Redis', pymysql: 'MySQL', psycopg2: 'PostgreSQL',
    celery: 'Celery', numpy: 'NumPy', pandas: 'Pandas', torch: 'PyTorch', tensorflow: 'TensorFlow' };
  for (const l of lines) {
    if (map[l]) tech.framework.add(map[l]);
  }
  if (lines.length) tech.tool.add('pip');
}

function parseCargoToml(content, tech) {
  tech.language.add('Rust');
  if (/actix/.test(content)) tech.framework.add('Actix');
  if (/rocket/.test(content)) tech.framework.add('Rocket');
  if (/tokio/.test(content)) tech.framework.add('Tokio');
  if (/serde/.test(content)) tech.tool.add('Serde');
}

function parseGradle(content, tech) {
  tech.language.add('Java');
  if (/spring-boot/.test(content)) tech.framework.add('Spring Boot');
  if (/'java'/.test(content) || /java plugin/.test(content)) tech.tool.add('Gradle');
}

function parseYamlList(content, tech) {
  if (/flutter:/.test(content)) tech.framework.add('Flutter');
  if (/angular:/.test(content)) tech.framework.add('Angular');
  tech.language.add('Dart');
}

function parseGemfile(content, tech) {
  tech.language.add('Ruby');
  if (/rails/.test(content)) tech.framework.add('Rails');
  if (/sinatra/.test(content)) tech.framework.add('Sinatra');
}

// README 摘要（清洗 HTML/标记符，提取纯文本前几句）
function readmeSummary(root) {
  logStage('抽取 README 摘要');
  const candidates = ['README.md', 'README.markdown', 'README.txt', 'readme.md', 'Readme.md'];
  for (const c of candidates) {
    const p = path.join(root, c);
    if (fs.existsSync(p)) {
      try {
        const raw = fs.readFileSync(p, 'utf8');
        // 去掉 HTML 标签、markdown 链接/图片语法、代码围栏、badge
        let text = raw
          .replace(/```[\s\S]*?```/g, ' ')
          .replace(/`[^`]*`/g, ' ')
          .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')
          .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
          .replace(/<[^>]+>/g, ' ')
          .replace(/[#>*_|`-]/g, ' ')
          .replace(/\s+/g, ' ')
          .trim();
        const sentences = text.split(/(?<=[。.!?！？])\s*/).filter(s => s.trim()).slice(0, 3);
        const summary = sentences.join(' ').slice(0, 320).trim();
        return { file: c, length: raw.length, summary };
      } catch { /* ignore */ }
    }
  }
  return { file: null, length: 0, summary: '' };
}

// Git 元数据
function gitMeta(root, remoteUrl) {
  logStage('收集 Git 元数据');
  if (!fs.existsSync(path.join(root, '.git'))) {
    return { isGit: false, commits: 0, recent: [], topContributors: [], branches: [], releases: [] };
  }
  const commits = runGit(['rev-list', '--count', 'HEAD'], root) || '0';
  const recentRaw = runGit(['log', '-5', '--pretty=format:%h|%an|%s'], root);
  const recent = recentRaw ? recentRaw.split('\n').map(l => {
    const [hash, author, msg] = l.split('|');
    return { hash, author, msg };
  }) : [];
  const contribRaw = runGit(['shortlog', '-sne', 'HEAD'], root);
  const topContributors = contribRaw ? contribRaw.split('\n').slice(0, 8).map(l => {
    const m = l.trim().match(/^\d+\s+(.*)$/);
    return m ? m[1] : l.trim();
  }).filter(Boolean) : [];
  const branches = runGit(['branch', '-a'], root).split('\n').map(b => b.replace('*', '').trim()).filter(Boolean);
  const releases = runGit(['tag', '--sort=-creatordate'], root).split('\n').slice(0, 10).filter(Boolean);
  const remote = remoteUrl || runGit(['remote', 'get-url', 'origin'], root);
  return { isGit: true, commits: parseInt(commits, 10) || 0, recent, topContributors, branches, releases, remote };
}

// CI / 测试识别
function detectCiTest(root) {
  logStage('识别 CI / 测试');
  const ciFiles = [];
  for (const hint of CI_FILE_HINTS) {
    const p = path.join(root, hint);
    if (fs.existsSync(p)) ciFiles.push(hint);
  }
  // 扫描 workflows 目录
  const wf = path.join(root, '.github', 'workflows');
  if (fs.existsSync(wf)) {
    try {
      for (const f of fs.readdirSync(wf)) if (f.endsWith('.yml') || f.endsWith('.yaml')) ciFiles.push('.github/workflows/' + f);
    } catch { /* */ }
  }
  const testDirs = [];
  let names = [];
  try { names = fs.readdirSync(root); } catch { /* */ }
  for (const n of names) {
    if (TEST_DIR_HINTS.includes(n) && fs.existsSync(path.join(root, n)) && fs.statSync(path.join(root, n)).isDirectory()) {
      testDirs.push(n);
    }
  }
  return { ci: { has: ciFiles.length > 0, files: ciFiles }, tests: { has: testDirs.length > 0, dirs: testDirs } };
}

// 生成 projectId
function makeId(input, customId) {
  if (customId) return customId;
  const base = input.replace(/[\\/]/g, ' ').split(' ').filter(Boolean).pop().replace(/\.git$/, '');
  return base.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'project';
}

// 解析非 flag 位置参数（跳过 --flag 及其取值，避免把 --out 的值误当输入）
function positionalArgs() {
  const argv = process.argv.slice(2);
  const out = [];
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) {
      if (['--out', '--clone-dir', '--id'].includes(a)) i++; // 跳过 flag 的值
      continue;
    }
    out.push(a);
  }
  return out;
}

// ---------- 主流程 ----------
function main() {
  const args = positionalArgs();
  if (args.length === 0) {
    console.error('用法：node analyze.js <git-url|local-path> [--id customId] [--out <dir>] [--clone-dir <dir>] [--keep-clone] [--shallow]');
    process.exit(1);
  }
  const input = args[0];
  const idIdx = process.argv.indexOf('--id');
  const customId = idIdx >= 0 ? process.argv[idIdx + 1] : null;

  console.log('========================================');
  console.log('  项目自动认知分析器');
  console.log('========================================');

  const { root, isClone, localPath } = cloneOrResolve(input);
  const tree = scanTree(root);
  const langAcc = {};
  statLang(root, '', langAcc);
  const fileCount = langAcc.__files || 0;
  delete langAcc.__files;
  const langDist = langAcc;
  const manifest = parseManifest(root);
  const readme = readmeSummary(root);
  const git = gitMeta(root, isClone ? localPath : null);
  const ciTest = detectCiTest(root);
  const quickstart = parseQuickStart(root, git.remote);
  const lint = detectLint(root);

  const projectId = makeId(input, customId);
  const result = {
    id: projectId,
    name: projectId,
    repo: isClone ? localPath : path.resolve(input),
    isClone,
    generatedAt: new Date().toISOString(),
    summary: readme.summary,
    techStack: manifest.tech,
    tree,
    stats: { fileCount, langDist },
    manifests: Object.keys(manifest.raw),
    readme,
    evolution: {
      commits: git.commits,
      recent: git.recent,
      topContributors: git.topContributors,
      branches: git.branches,
      releases: git.releases,
      remote: git.remote
    },
    ci: ciTest.ci,
    tests: ciTest.tests,
    quickstart,
    quality: { lint },
    // ↓↓↓ 以下字段由 AI 在生成阶段补全（方法论判断） ↓↓↓
    _aiTodo: {
      architecture: 'AI 补充：模块架构 Mermaid 图 + modules[]',
      deploy: 'AI 补充：系统部署 Mermaid 图 + note',
      components: 'AI 补充：功能组件矩阵 [{name,duty,entry,deps,layer}]',
      sequences: 'AI 补充：核心链路时序图 [{title,mermaid}]',
      dataModel: 'AI 补充：数据模型与状态流转 {summary, mermaid}（erDiagram / stateDiagram-v2）',
      quality: 'AI 补充：quality.errorHandling（错误处理与日志说明）与 quality.summary',
      readme: 'AI 润色：readme.summary 为连贯项目自述'
    }
  };

  if (!fs.existsSync(OUTPUT_DIR)) fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  const outFile = path.join(OUTPUT_DIR, projectId + '.json');
  fs.writeFileSync(outFile, JSON.stringify(result, null, 2), 'utf8');

  logStage('完成');
  logInfo(`输出文件：${outFile}`);
  logInfo(`文件数：${fileCount}，语言：${Object.keys(langDist).join('/')}`);
  logInfo(`提交数：${git.commits}，贡献者：${git.topContributors.length}`);
  if (!KEEP_CLONE && isClone) {
    logInfo('（克隆目录保留在 .clones/，可用 --keep-clone 控制）');
  }
  console.log('\n下一步：AI 基于本 JSON 补全架构/部署/组件/时序，写入 web/data/' + projectId + '.js（window.PROJECTS["' + projectId + '"] = {...}），并在 web/data.js 的 window.PROJECT_ORDER 注册一条元信息');
}

main();
