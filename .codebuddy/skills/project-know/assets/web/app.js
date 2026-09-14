/* app.js —— 消费 window.PROJECT_ORDER / window.PROJECTS，渲染项目认知网页
 *
 * 性能设计（应对"数据变大"）：
 *  1. Mermaid 懒加载：不再在 <head> 同步引 CDN，首屏不阻塞；空闲时预取。
 *  2. 项目数据按 id 拆分到 web/data/<id>.js，切到哪个项目才加载哪个
 *     （动态 <script> 注入，file:// 协议下也能用），数据再大也不拖慢首屏。
 *  3. 目录树折叠分支懒构建 DOM：超大目录树首屏只建可见节点，展开时才补建。
 *  4. 大数据区块 content-visibility 延迟渲染（见 styles.css）。
 *
 * 数据契约见 skill 内 references/schema.md。
 */
(function () {
  'use strict';

  // Mermaid 脚本地址。默认走 CDN；如需完全离线，把 mermaid.min.js
  // 下载到 web/vendor/ 后改成本地路径，如 'vendor/mermaid.min.js'。
  var MERMAID_SRC = 'https://cdn.jsdelivr.net/npm/mermaid@10.9.1/dist/mermaid.min.js';

  var order = window.PROJECT_ORDER || [];  // [{id,name,chunk,summary,generatedAt}]
  var store = window.PROJECTS || {};       // 已加载项目数据缓存（按 id）
  var current = 0;
  var seqIndex = 0;

  var $ = function (id) { return document.getElementById(id); };
  function el(tag, cls, html) {
    var n = document.createElement(tag);
    if (cls) n.className = cls;
    if (html != null) n.innerHTML = html;
    return n;
  }
  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  /* ---------- Mermaid：懒加载 + 串行渲染 ---------- */
  var mermaidPromise = null;
  var mermaidChain = Promise.resolve();

  // Mermaid 的 participant/actor 别名（as 后面的文字）若含 ()、/ 等特殊字符，
  // 部分版本会解析失败导致整张图渲染失败；提前用引号包裹即可。
  // 仅处理形如 "participant X as Y" 的整行；Y 若含括号 / 斜杠 / 冒号等就加双引号。
  function quoteParticipantLabels(code) {
    return code.replace(
      /(^[\t ]*participant[\t ]+[A-Za-z_][\w]*[\t ]+as[\t ]+)([^\n]+?)$/gm,
      function (m, pre, label) {
        var t = label.trim();
        if (!t) return m;
        if (t.charAt(0) === '"' || t.charAt(0) === "'") return m;
        // 纯字母数字下划线、含中文、或纯空白都是安全的
        if (/^[\w\u4e00-\u9fff \t]+$/.test(t)) return m;
        // 含 () / () / : / / 等特殊字符：用双引号包裹，转义内部的双引号
        return pre + '"' + t.replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"';
      }
    );
  }

  // Mermaid 的 flowchart/graph 使用 -->|label|<target> 的连线标签语法。
  // 但当 label 内含 ()、等被 mermaid lexer 视作 token 的字符时，解析会爆
  // (Parse error on line ... 'H -->|next(err)|' 之类)。Mermaid 支持
  // -->|"label"|<target> 的双引号包裹写法，凡 label 含 ()/冒号/斜杠/管道符
  // / 引号 / 美元符 / 等可能冲突的字符，都用双引号包裹稳妥。
  function quoteFlowchartEdgeLabels(code) {
    // 匹配任意位置上的箭头 + 边标签 + 目标节点 ID：
    //   "  H  -->| body-parser|-->|next(err)| E[...]" 之类。
    // 箭头形式：-->、==>、-.->、-> ；后面直接 | 再到第二个 | 闭合。
    return code.replace(
      /((?:\-\->|==>|\-\.\-\>|\-\>)[ \t]*\|)([^|\n]+?)(\|[ \t]*[A-Za-z_][\w]*)/g,
      function (m, head, label, tail) {
        var t = label.trim();
        if (!t) return m;
        if (t.charAt(0) === '"' || t.charAt(0) === "'") return m;
        // 中英文 / 字母数字 / 空格 / 短横线 / 加号 是安全的
        if (/^[\w\u4e00-\u9fff \-_+]+$/.test(t)) return m;
        // 含 () / \ / | / : / ; / & / $ / < / > / @ 等可能干扰 mermaid lexer 的字符：包引号
        return head + '"' + t.replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"' + tail;
      }
    );
  }

  // 统一入口：对 sequenceDiagram 加 participant 引号；对 flowchart / graph
  // 加 edge label 引号。其它语法不动。
  function sanitizeMermaid(code) {
    if (!code) return code;
    if (/^\s*(?:sequenceDiagram)\b/m.test(code)) code = quoteParticipantLabels(code);
    if (/^\s*(?:flowchart|graph)\b/m.test(code)) code = quoteFlowchartEdgeLabels(code);
    return code;
  }

  function initMermaid() {
    if (!window.mermaid) return;
    mermaid.initialize({
      startOnLoad: false,
      theme: 'dark',
      // strict 禁止 HTML 注入与点击跳转，图表本身不受影响（安全默认）
      securityLevel: 'strict',
      themeVariables: {
        background: '#050813',
        primaryColor: '#131A2C',
        primaryTextColor: '#E8ECF7',
        primaryBorderColor: '#A78BFA',
        lineColor: '#22D3EE',
        secondaryColor: '#0A0F1F',
        tertiaryColor: '#0A0F1F',
        noteBkgColor: '#131A2C',
        noteTextColor: '#E8ECF7',
        noteBorderColor: '#A78BFA',
        activationBorderColor: '#A78BFA',
        activationBkgColor: 'rgba(167,139,250,0.18)',
        actorBorder: '#A78BFA',
        actorBkg: '#131A2C',
        actorTextColor: '#E8ECF7',
        actorLineColor: '#22D3EE',
        signalColor: '#22D3EE',
        signalTextColor: '#E8ECF7',
        labelBoxBkgColor: '#131A2C',
        labelBoxBorderColor: '#A78BFA',
        labelTextColor: '#E8ECF7',
        fontFamily: 'PingFang SC, Microsoft YaHei, sans-serif'
      }
    });
  }

  function ensureMermaid() {
    if (!mermaidPromise) {
      mermaidPromise = new Promise(function (resolve) {
        if (window.mermaid) { resolve(window.mermaid); return; }
        var s = document.createElement('script');
        s.src = MERMAID_SRC;
        s.async = true;
        s.onload = function () { initMermaid(); resolve(window.mermaid); };
        s.onerror = function () { mermaidPromise = null; resolve(null); };
        document.head.appendChild(s);
      });
    }
    return mermaidPromise;
  }

  function renderMermaid(container, code) {
    if (!code) { container.innerHTML = ''; return; }
    container.innerHTML = '<div class="mermaid-loading">图表加载中…</div>';
    ensureMermaid().then(function (mmd) {
      if (!mmd) {
        container.innerHTML = '<pre class="mermaid-fallback">Mermaid 引擎未加载（可能离线）。联网后刷新即可显示图表。</pre>';
        return;
      }
      // mermaid.run 对无 id 节点会自动生成内部 id，无需手写随机 id
      container.innerHTML = '<div class="mermaid"></div>';
      var target = container.firstChild;
      target.textContent = sanitizeMermaid(code);
      mermaidChain = mermaidChain.then(function () {
        if (!target.isConnected) return; // 切换项目后旧节点已被移除，跳过
        // 必须 .catch()：mermaid 解析失败时 run() 返回 rejected promise，
        // 不吸收会让共享的 mermaidChain 进入 rejected 状态，后续所有图表都跳过渲染。
        return Promise.resolve(mmd.run({ nodes: [target] })).catch(function (e) {
          target.outerHTML = '<pre class="mermaid-fallback">' +
            esc('Mermaid 渲染失败：' + (e && e.message ? e.message : e) + '\n\n' + code) + '</pre>';
        });
      });
    });
  }

  /* ---------- 项目数据：按需加载（script 注入） ---------- */
  function loadProject(meta, cb) {
    if (store[meta.id]) { cb(store[meta.id]); return; }
    var s = document.createElement('script');
    s.src = meta.chunk;
    s.onload = function () { cb(store[meta.id] || null); };
    s.onerror = function () { cb(null); };
    document.body.appendChild(s);
  }

  /* ---------- 项目切换标签 ---------- */
  function renderProjSwitch() {
    var box = $('projSwitch');
    box.innerHTML = '';
    order.forEach(function (p, i) {
      var t = el('div', 'proj-tab' + (i === current ? ' active' : ''), esc(p.name));
      t.onclick = function () { if (i !== current) { current = i; seqIndex = 0; renderAll(); } };
      box.appendChild(t);
    });
  }

  /* ---------- 锚点导航（滚动高亮） ---------- */
  function renderAnchors() {
    // id 与显示名一一对应，合并为单个数组，避免两个数组错位的隐患
    var navs = [
      { id: 'overview', name: '概览' }, { id: 'readme', name: '自述' },
      { id: 'tech', name: '技术栈' }, { id: 'arch', name: '架构' },
      { id: 'deploy', name: '部署' }, { id: 'components', name: '组件' },
      { id: 'sequences', name: '时序' }, { id: 'data', name: '数据' },
      { id: 'quickstart', name: '上手' }, { id: 'quality', name: '质量' },
      { id: 'evolution', name: '演进' }, { id: 'tree', name: '目录' }
    ];
    var box = $('anchors');
    box.innerHTML = '';
    navs.forEach(function (n) {
      var a = el('div', 'anchor', esc(n.name));
      a.onclick = function () {
        var t = $(n.id);
        if (t) t.scrollIntoView({ behavior: 'smooth' });
      };
      box.appendChild(a);
    });
    var ticking = false;
    window.addEventListener('scroll', function () {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(function () {
        ticking = false;
        var pos = window.scrollY + window.innerHeight * 0.35;
        var cur = -1;
        navs.forEach(function (n, i) {
          var sec = $(n.id);
          if (sec && sec.offsetTop <= pos) cur = i;
        });
        var anchors = box.children;
        for (var j = 0; j < anchors.length; j++) {
          anchors[j].classList.toggle('active', j === cur);
        }
      });
    });
  }

  /* ---------- Hero ---------- */
  function setHeroTitle(meta) {
    $('heroTitle').textContent = meta.name;
    $('heroDesc').textContent = meta.summary || '（暂无项目描述）';
  }

  function renderHeroLight(meta) {
    // 数据分片尚未加载时的轻量首屏：先渲染标题 + 简介，统计区显示占位
    setHeroTitle(meta);
    $('heroMeta').innerHTML = [0, 1, 2, 3].map(function () {
      return '<div class="m"><b>…</b><span>加载中</span></div>';
    }).join('');
    $('heroChips').innerHTML = '<span class="chip">数据加载中…</span>';
  }

  function renderHero(p) {
    setHeroTitle(p);
    var stats = p.stats || {};
    var evo = p.evolution || {};
    var meta = [
      { b: stats.fileCount != null ? stats.fileCount : '—', s: '源文件数' },
      { b: Object.keys(stats.langDist || {}).length || '—', s: '语言种类' },
      { b: evo.commits != null ? evo.commits : '—', s: 'Git 提交' },
      { b: (evo.topContributors || []).length || '—', s: '贡献者' }
    ];
    $('heroMeta').innerHTML = meta.map(function (m) {
      return '<div class="m"><b>' + esc(m.b) + '</b><span>' + esc(m.s) + '</span></div>';
    }).join('');
    var ts = p.techStack || {};
    var chips = (ts.language || []).concat(ts.framework || []);
    $('heroChips').innerHTML = chips.length
      ? chips.map(function (c) { return '<span class="chip">' + esc(c) + '</span>'; }).join('')
      : '<span class="chip">—</span>';
  }

  /* ---------- 技术栈 ---------- */
  function renderTech(p) {
    var ts = p.techStack || {};
    var groups = [
      ['语言', 'language', '编程语言 / 运行时'],
      ['框架', 'framework', '核心框架 / 库'],
      ['中间件', 'middleware', '中间件 / 基础设施'],
      ['工具', 'tool', '构建 / 测试 / 部署工具']
    ];
    var html = '';
    groups.forEach(function (g) {
      var arr = ts[g[1]] || [];
      if (arr.length) {
        html += '<div class="tech-group"><h3>' + esc(g[0]) + ' · ' + esc(g[2]) + '</h3>' +
          arr.map(function (c) { return '<span class="chip">' + esc(c) + '</span>'; }).join('') +
          '</div>';
      }
    });
    var view = $('techWrap');
    view.innerHTML = html || '<p style="color:var(--text-1)">暂无技术栈数据</p>';
  }

  /* ---------- 架构 ---------- */
  function renderArch(p) {
    var arch = p.architecture || {};
    $('archSummary').textContent = arch.summary || '（暂无架构说明）';
    renderMermaid($('archMermaid'), arch.mermaid);
    var view = $('moduleGrid');
    var modules = arch.modules || [];
    view.innerHTML = modules.length
      ? modules.map(function (m) {
          var deps = (m.dependsOn || []).map(function (d) { return '<span>' + esc(d) + '</span>'; }).join('');
          return '<div class="module-card"><h4>' + esc(m.name) + '</h4>' +
            '<p>' + esc(m.responsibility) + '</p>' +
            (m.entry ? '<span class="entry">↳ ' + esc(m.entry) + '</span>' : '') +
            (deps ? '<div class="deps">' + deps + '</div>' : '') +
            '</div>';
        }).join('')
      : '<p style="color:var(--text-1)">暂无模块数据</p>';
  }

  /* ---------- 部署 ---------- */
  function renderDeploy(p) {
    var dep = p.deploy || {};
    renderMermaid($('deployMermaid'), dep.mermaid);
    var note = $('deployNote');
    note.innerHTML = dep.note ? '部署要点：' + esc(dep.note) : '';
  }

  /* ---------- 组件矩阵 ---------- */
  function renderComponents(p) {
    var view = $('compLayers');
    var list = p.components || [];
    if (!list.length) {
      view.innerHTML = '<p style="color:var(--text-1)">暂无组件数据</p>';
      return;
    }
    var layers = {};
    list.forEach(function (c) {
      var k = c.layer || '其他';
      (layers[k] = layers[k] || []).push(c);
    });
    view.innerHTML = Object.keys(layers).map(function (layer) {
      var cards = layers[layer].map(function (c) {
        var deps = (c.deps || []).map(function (d) { return '<span>' + esc(d) + '</span>'; }).join('');
        return '<div class="comp-card"><div class="name">' + esc(c.name) + '</div>' +
          '<div class="duty">' + esc(c.duty || '') + '</div>' +
          (c.entry ? '<div class="entry">↳ ' + esc(c.entry) + '</div>' : '') +
          (deps ? '<div class="deps">' + deps + '</div>' : '') +
          '</div>';
      }).join('');
      return '<div class="comp-layer"><h3>' + esc(layer) + '</h3><div class="comp-grid">' + cards + '</div></div>';
    }).join('');
  }

  /* ---------- 时序 tab ---------- */
  function renderSequences(p) {
    var seqs = p.sequences || [];
    var tabs = $('seqTabs');
    var view = $('seqMermaid');
    if (!seqs.length) {
      tabs.innerHTML = '';
      view.innerHTML = '<p style="color:var(--text-1)">暂无时序数据</p>';
      return;
    }
    seqIndex = Math.min(seqIndex, seqs.length - 1);
    tabs.innerHTML = '';
    seqs.forEach(function (s, i) {
      var t = el('div', 'seq-tab' + (i === seqIndex ? ' active' : ''), esc(s.title));
      t.onclick = function () {
        seqIndex = i;
        renderSequences(p);
      };
      tabs.appendChild(t);
    });
    renderMermaid(view, seqs[seqIndex].mermaid);
  }

  /* ---------- 演进历程 ---------- */
  function renderEvolution(p) {
    var evo = p.evolution || {};
    // 兼容多种字段形态：{msg,author,date} / {desc} / 纯字符串
    var fmtRecent = function (i) {
      if (i && i.msg) return i.msg + (i.author ? ' · ' + i.author : '');
      if (i && i.desc) return i.desc;
      return String(i);
    };
    var fmtContrib = function (c) {
      if (c && typeof c === 'object') {
        if (c.count != null) return c.count + ' 次提交';
        return c.name || c.email || String(c);
      }
      return String(c).split(' <')[0]; // "dependabot[bot] <email>" 只留名字
    };
    var fmtRelease = function (r) {
      if (r && typeof r === 'object') return (r.tag || r.name || '') + (r.date ? ' · ' + r.date : '');
      return String(r);
    };
    var listHtml = function (arr, fmt) {
      if (!arr || !arr.length) return '<ul class="evo-list"><li>—</li></ul>';
      return '<ul class="evo-list">' + arr.slice(0, 8).map(function (i) {
        var d = i && i.date != null ? i.date : (i.hash ? i.hash.slice(0, 7) : '');
        return '<li>' + (d ? '<b>' + esc(d) + '</b> ' : '') + esc(fmt(i)) + '</li>';
      }).join('') + '</ul>';
    };
    var grid = [
      { label: '总提交数', big: evo.commits != null ? evo.commits : '—', list: listHtml(evo.recent, fmtRecent) },
      { label: '贡献者', big: (evo.topContributors || []).length || '—', list: listHtml(evo.topContributors, fmtContrib) },
      { label: '近期提交', big: (evo.recent || []).length || '—', list: listHtml(evo.recent, fmtRecent) },
      { label: '版本发布', big: (evo.releases || []).length || '—', list: listHtml(evo.releases, fmtRelease) }
    ];
    $('evoGrid').innerHTML = grid.map(function (g) {
      return '<div class="evo-card"><h4>' + esc(g.label) + '</h4>' +
        '<div class="big">' + esc(g.big) + '</div>' + g.list + '</div>';
    }).join('');

    var ci = p.ci;
    $('ciInfo').innerHTML = ci && ci.has
      ? 'CI 配置：' + esc((ci.files || []).join('、') || '有')
      : '未检测到 CI 配置';
    var tests = p.tests;
    $('testsInfo').innerHTML = tests && tests.has
      ? '测试目录：' + esc((tests.dirs || []).join('、') || '有')
      : '未检测到测试配置';
  }

  /* ---------- 目录树（折叠 + 懒构建 DOM） ---------- */
  function renderTree(p) {
    var view = $('treeView');
    view.innerHTML = '';
    if (!p.tree) {
      view.innerHTML = '<p style="color:var(--text-1)">无目录数据</p>';
      return;
    }
    view.appendChild(buildTree(p.tree, 0));
  }

  function buildTree(node, depth) {
    var li = document.createElement('li');
    li.className = node.type === 'dir' ? 'dir' : 'file';
    var hasKids = node.type === 'dir' && node.children && node.children.length;
    // 折叠规则：默认展开前两层，深度 >= 2 的目录默认折叠（首屏不构建子节点，
    // 首次展开才懒构建 DOM）。数据可显式标记 collapsed:false 强制默认展开。
    var initiallyCollapsed = hasKids && depth >= 2 && node.collapsed !== false;
    if (initiallyCollapsed) li.className += ' collapsed';

    var nodeDiv = document.createElement('div');
    nodeDiv.className = 'node';
    var ico = node.type === 'dir' ? '📁' : '📄';
    var toggle = hasKids ? '<span class="toggle">▾</span>' : '<span class="toggle"></span>';
    nodeDiv.innerHTML = toggle + '<span class="ico">' + ico + '</span><span>' + esc(node.name) + '</span>';
    li.appendChild(nodeDiv);

    if (hasKids) {
      var ul = document.createElement('ul');
      var built = !initiallyCollapsed;
      if (built) {
        node.children.forEach(function (ch) { ul.appendChild(buildTree(ch, depth + 1)); });
      }
      li.appendChild(ul);
      nodeDiv.onclick = function () {
        var willExpand = li.classList.contains('collapsed');
        li.classList.toggle('collapsed');
        var t = nodeDiv.querySelector('.toggle');
        if (t) t.textContent = willExpand ? '▾' : '▸';
        if (willExpand && !built) { // 首次展开时再构建子节点，避免大目录树一次性建大量 DOM
          built = true;
          node.children.forEach(function (ch) { ul.appendChild(buildTree(ch, depth + 1)); });
        }
      };
    }
    return li;
  }

  /* ---------- 总渲染 ---------- */
  function renderAll() {
    var meta = order[current];
    if (!meta) return;
    renderProjSwitch();
    var p = store[meta.id];
    if (p) {
      renderProject(p);
    } else {
      renderHeroLight(meta);
      loadProject(meta, function (loaded) {
        if (current !== order.indexOf(meta)) return; // 期间用户已切换项目，丢弃本次结果
        if (!loaded) {
          $('heroChips').innerHTML = '<span class="chip">数据加载失败：请确认 web/' + esc(meta.chunk) + ' 存在</span>';
          return;
        }
        renderProject(loaded);
      });
    }
  }

  /* ---------- README 项目自述 ---------- */
  function renderReadme(p) {
    var r = p.readme || {};
    $('readmeSummary').textContent = r.summary || '（未提取到 README 摘要）';
    var repo = p.evolution && p.evolution.remote;
    var tags = [];
    if (r.file) tags.push('来源 <b>' + esc(r.file) + '</b>');
    if (r.length) tags.push('约 <b>' + esc(r.length) + '</b> 字符');
    if (repo) tags.push('<a class="tag-link" href="' + esc(repo) + '" target="_blank" rel="noopener">' + esc(repo) + '</a>');
    $('readmeMeta').innerHTML = tags.map(function (t) {
      return '<span class="tag">' + t + '</span>';
    }).join('') || '<span class="tag">—</span>';
  }

  /* ---------- 数据模型与状态流转 ---------- */
  function renderData(p) {
    var d = p.dataModel || {};
    $('dataSummary').textContent = d.summary || '（暂无数据模型说明）';
    renderMermaid($('dataMermaid'), d.mermaid);
  }

  /* ---------- 快速上手 ---------- */
  function renderQuickstart(p) {
    var q = p.quickstart || {};
    var grid = $('qsGrid');
    var reqs = q.requirements || [];
    var reqCard = el('div', 'qs-card glass');
    reqCard.innerHTML = '<h4>环境要求</h4><ul>' + (reqs.length
      ? reqs.map(function (r) { return '<li>' + esc(r) + '</li>'; }).join('')
      : '<li>—</li>') + '</ul>';
    var verifyCard = el('div', 'qs-card glass');
    verifyCard.innerHTML = '<h4>验证是否跑通</h4><ul>' +
      '<li>查看 package.json 的 scripts 清单</li>' +
      '<li>运行 npm test 观察用例是否通过</li>' +
      '<li>按上述命令启动后访问默认地址</li>' +
      '<li>确认响应 / 页面与预期一致</li>' +
      '</ul>';
    grid.innerHTML = '';
    grid.appendChild(reqCard);
    grid.appendChild(verifyCard);

    var steps = q.steps || [];
    $('qsSteps').innerHTML = steps.length
      ? steps.map(function (s, i) {
          return '<span class="step"><span class="cmt"># ' + esc(s.name || ('步骤 ' + (i + 1))) + '</span> <span class="c">' + esc(s.cmd) + '</span></span>';
        }).join('')
      : '<span class="step"><span class="cmt">暂无运行命令数据</span></span>';

    var scripts = q.scripts || [];
    $('qsScripts').innerHTML = scripts.length
      ? '<ul class="script-list">' + scripts.map(function (s) {
          return '<li><span class="sname">' + esc(s.name) + '</span>' +
            '<span class="scmd">' + esc(s.cmd) + '</span>' +
            (s.desc ? '<span class="sdesc">' + esc(s.desc) + '</span>' : '') + '</li>';
        }).join('') + '</ul>'
      : '<p style="color:var(--text-1)">暂无脚本数据</p>';
  }

  /* ---------- 工程化与质量 ---------- */
  function renderQuality(p) {
    var q = p.quality || {};
    var ci = p.ci;
    var tests = p.tests;
    function filesHtml(files) {
      return files && files.length
        ? '<div class="files">' + files.slice(0, 8).map(function (f) {
            return '<span>' + esc(f) + '</span>';
          }).join('') + '</div>'
        : '';
    }
    var cards = [
      {
        name: '测试体系',
        body: tests && tests.has
          ? '检测到测试目录 <b>' + esc((tests.dirs || []).join('、')) + '</b>，可补用例跑通作为质量基线。'
          : '未检测到测试配置。',
        files: tests && tests.has ? tests.dirs : []
      },
      {
        name: 'CI / CD',
        body: ci && ci.has
          ? '检测到持续集成配置，提交即触发自动化检查。'
          : '未检测到 CI 配置。',
        files: ci && ci.has ? ci.files : []
      },
      {
        name: '代码规范',
        body: q.lint && q.lint.has
          ? '检测到 Lint / 格式规范配置，编码风格有统一约束。'
          : '未检测到 Lint / 格式配置。',
        files: q.lint && q.lint.has ? q.lint.files : []
      },
      {
        name: '错误处理与日志',
        body: q.errorHandling || '暂无说明',
        files: []
      }
    ];
    $('qualitySummary').textContent = q.summary || '';
    $('qualityGrid').innerHTML = cards.map(function (c) {
      return '<div class="quality-card glass"><h4>' + esc(c.name) + '</h4>' +
        '<p>' + c.body + '</p>' + filesHtml(c.files) + '</div>';
    }).join('');
  }

  function renderProject(p) {
    renderHero(p);
    renderReadme(p);
    renderTech(p);
    renderArch(p);
    renderDeploy(p);
    renderComponents(p);
    renderSequences(p);
    renderData(p);
    renderQuickstart(p);
    renderQuality(p);
    renderEvolution(p);
    renderTree(p);
    $('genTime').textContent = '生成于 ' + (p.generatedAt ? new Date(p.generatedAt).toLocaleString('zh-CN') : '—');
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  document.addEventListener('DOMContentLoaded', function () {
    if (!order.length) {
      $('content').innerHTML = '<p style="padding:40px;color:var(--text-1)">未找到项目清单，请检查 web/data.js 中的 window.PROJECT_ORDER。</p>';
      return;
    }
    renderAnchors();
    renderAll();

    // Hero 主按钮：跳到架构区
    var bp = $('btnPrimary');
    if (bp) bp.onclick = function () {
      var t = $('arch');
      if (t) t.scrollIntoView({ behavior: 'smooth' });
    };

    // 空闲时预取 Mermaid（最多等 3s），切到图表区时不卡顿；不阻塞首屏
    if ('requestIdleCallback' in window) {
      window.requestIdleCallback(function () { ensureMermaid(); }, { timeout: 3000 });
    } else {
      setTimeout(ensureMermaid, 1500);
    }
  });
})();
