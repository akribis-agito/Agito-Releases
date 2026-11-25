/* Interactive tools: error lookup and status-bit decoder.
   A page places <div class="ag-widget" data-widget="NAME"></div>. Data files are loaded on demand from
   ../data/ next to this script, so the same build works online and from file://. */
(function () {
  "use strict";
  const SCRIPT = document.currentScript && document.currentScript.src;
  const DATA = SCRIPT ? new URL("../data/", SCRIPT) : null;
  const zh = () => (document.documentElement.lang || "zh").toLowerCase().startsWith("zh");
  const L = () => (zh() ? "zh" : "en");
  const T = (a, b) => (zh() ? a : b);
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const esc = (s) => String(s == null ? "" : s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
  const loaded = {};
  function load(file) {
    if (!loaded[file]) loaded[file] = new Promise((res, rej) => {
      const s = document.createElement("script");
      s.src = new URL(file, DATA).href; s.onload = res; s.onerror = () => rej(new Error(file));
      document.head.appendChild(s);
    });
    return loaded[file];
  }
  /* language root of the site, from the header logo link; then a docs path -> page URL */
  function pageUrl(mdPath) {
    const logo = document.querySelector("a.md-logo, .md-header__button.md-logo");
    const root = new URL(logo ? logo.getAttribute("href") : "./", location.href);
    const [p, hash] = mdPath.split("#");
    const offline = /\.html$/.test(location.pathname);
    let path = p.replace(/\.md$/, offline ? ".html" : "/");
    if (!offline && path.endsWith("index/")) path = path.slice(0, -6);
    const base = root.href.endsWith(".html") ? new URL("./", root) : root;
    return new URL(path, base).href + (hash ? "#" + hash : "");
  }
  function field(label, input) { const f = el("label", "ag-field"); f.appendChild(el("span", null, label)); f.appendChild(input); return f; }
  function input(type, attrs) { const i = el("input", "ag-input"); i.type = type; for (const k in attrs) i.setAttribute(k, attrs[k]); return i; }
  function select(opts, value) {
    const s = el("select", "ag-select");
    opts.forEach(([v, t]) => { const o = el("option", null, esc(t)); o.value = v; if (String(v) === String(value)) o.selected = true; s.appendChild(o); });
    return s;
  }
  function copyable(pre) {
    const b = el("button", "ag-btn ghost ag-copy", T("复制", "Copy")); b.type = "button";
    b.onclick = () => {
      const text = pre.textContent;
      const done = () => { b.textContent = T("已复制", "Copied"); setTimeout(() => (b.textContent = T("复制", "Copy")), 1200); };
      if (navigator.clipboard) navigator.clipboard.writeText(text).then(done, () => { selectText(pre); });
      else selectText(pre);
    };
    return b;
  }
  function selectText(node) { const r = document.createRange(); r.selectNodeContents(node); const s = getSelection(); s.removeAllRanges(); s.addRange(r); }
  function parseNum(s) {
    s = String(s).trim().replace(/_/g, "");
    if (/^-?0x[0-9a-f]+$/i.test(s)) return parseInt(s, 16) | 0;
    if (/^0b[01]+$/i.test(s)) return parseInt(s.slice(2), 2) | 0;
    if (/^-?\d+(\.\d+)?$/.test(s)) return Number(s);
    return NaN;
  }

  /* ---------- 1. error lookup ---------- */
  function errlookup(root) {
    const q = input("search", { placeholder: T("输入错误码或文字，例如 31、ERR 4010、位置", "Code or words, e.g. 31, ERR 4010, limit"), id: "ag-err-q", "aria-label": T("查询错误码", "Look up an error") });
    const out = el("div", "ag-results"), stat = el("div", "ag-muted");
    root.append(q, stat, out);
    stat.textContent = T("正在加载错误码表…", "Loading the error table…");
    load("errors.js").then(() => {
      const all = window.AG_ERRORS || [];
      stat.textContent = T(`共 ${all.length} 个错误码。可以输入数字、“ERR n”，或说明中的文字。`, `${all.length} error codes. Type a number, "ERR n", or words from a description.`);
      const run = () => {
        const s = q.value.trim(); out.innerHTML = "";
        if (!s) return;
        const m = s.match(/(-?\d+)/), n = m ? Number(m[1]) : null;
        let hits = n != null && /^\D*-?\d+\D*$/.test(s) ? all.filter((e) => e[0] === n) : [];
        if (!hits.length) { const w = s.toLowerCase(); hits = all.filter((e) => e[1].toLowerCase().includes(w) || e[2].toLowerCase().includes(w) || String(e[0]).startsWith(w)); }
        if (n != null && n >= 5001 && n <= 6000 && !hits.length) hits = all.filter((e) => e[2] === "Fault by Draw Control").slice(0, 1);
        if (!hits.length) { out.appendChild(el("div", "ag-empty", T("没有找到。控制器的 ERR 编号和 SDK 的 PC Suite 错误码都在这张表里。", "Not found. The table holds both controller ERR numbers and SDK PC Suite codes."))); return; }
        hits.slice(0, 40).forEach((e) => {
          const card = el("div", "ag-result");
          card.innerHTML = `<div class="ag-code">ERR ${e[0]}</div><div><div>${esc(zh() ? e[1] : e[2])}</div><div class="ag-muted">${esc(zh() ? e[2] : e[1])}</div></div>`;
          out.appendChild(card);
        });
        if (hits.length > 40) out.appendChild(el("div", "ag-muted", T(`还有 ${hits.length - 40} 条，请输入更具体的内容。`, `${hits.length - 40} more; be more specific.`)));
      };
      q.addEventListener("input", run);
      const fromHash = decodeURIComponent((location.hash.match(/err=([^&]+)/) || [])[1] || "");
      if (fromHash) { q.value = fromHash; run(); }
    }, () => { stat.textContent = T("错误码表加载失败。", "Could not load the error table."); });
  }

  /* ---------- 2. status-bit decoder ---------- */
  function bitdecode(root) {
    const stat = el("div", "ag-muted", T("正在加载…", "Loading…"));
    root.appendChild(stat);
    load("enums.js").then(() => {
      stat.remove();
      const E = window.AG_ENUMS || {};
      const names = Object.keys(E).filter((n) => E[n].kw);
      const sel = select(names.map((n) => [n, `${n} — ${E[n].title[L()]}`]), "MotionStat");
      const val = input("text", { value: "0x31", id: "ag-bit-v", inputmode: "text", "aria-label": T("数值", "Value") });
      const ctrls = el("div", "ag-ctrls ag-form");
      ctrls.append(field(T("关键字", "Keyword"), sel), field(T("读到的值（十进制或 0x 十六进制）", "Value read (decimal or 0x hex)"), val));
      const grid = el("div", "ag-bits"), out = el("div", "ag-results");
      root.append(ctrls, grid, out);
      const run = () => {
        const e = E[sel.value], v = parseNum(val.value);
        grid.innerHTML = ""; out.innerHTML = "";
        if (Number.isNaN(v)) { out.appendChild(el("div", "ag-empty", T("请输入数字，例如 49 或 0x31。", "Enter a number such as 49 or 0x31."))); return; }
        if (e.flags) {
          const u = v >>> 0;
          for (let b = 31; b >= 0; b--) {
            const on = (u >>> b) & 1, def = e.values.find((x) => (x.v >>> 0) === ((1 << b) >>> 0));
            const c = el("div", "ag-bit" + (on ? " on" : "") + (def ? "" : " unused"), `<b>${b}</b><span>${on}</span>`);
            c.title = def ? `${def.n}: ${def[L()]}` : T("未定义", "undefined");
            grid.appendChild(c);
          }
          const set = e.values.filter((x) => x.v !== 0 && ((u & x.v) >>> 0) === (x.v >>> 0));
          const head = el("div", "ag-summary", `<b>${u}</b> = <code>0x${u.toString(16).toUpperCase().padStart(8, "0")}</code> · ${T("置位的位", "bits set")}: <b>${set.length}</b>`);
          out.appendChild(head);
          if (!u) { const z = e.values.find((x) => x.v === 0); out.appendChild(el("div", "ag-result", `<div class="ag-code">0</div><div>${esc(z ? z[L()] : "0")}</div>`)); }
          set.forEach((x) => {
            const bit = Math.round(Math.log2(x.v >>> 0));
            out.appendChild(el("div", "ag-result", `<div class="ag-code">${T("位", "bit")} ${bit}</div><div><div><code>${esc(x.n)}</code> · <code>${esc(x.c)}</code></div><div>${esc(x[L()])}</div></div>`));
          });
          const known = e.values.reduce((m, x) => m | (x.v >>> 0), 0) >>> 0, unknown = (u & ~known) >>> 0;
          if (unknown) out.appendChild(el("div", "ag-empty", T(`以下位没有定义：0x${unknown.toString(16).toUpperCase()}`, `Undefined bits set: 0x${unknown.toString(16).toUpperCase()}`)));
        } else {
          const x = e.values.find((y) => y.v === v);
          out.appendChild(x ? el("div", "ag-result", `<div class="ag-code">${v}</div><div><div><code>${esc(x.n)}</code> · <code>${esc(x.c)}</code></div><div>${esc(x[L()])}</div></div>`)
            : el("div", "ag-empty", T(`${sel.value} 中没有值 ${v}。`, `${sel.value} has no value ${v}.`)));
        }
      };
      sel.onchange = run; val.oninput = run; run();
    });
  }

  const W = { errlookup, bitdecode };
  function progress() {
    let bar = document.querySelector(".ag-progress");
    if (!bar) {
      bar = el("div", "ag-progress"); bar.setAttribute("aria-hidden", "true"); document.body.appendChild(bar);
      const upd = () => { const h = document.documentElement, max = h.scrollHeight - h.clientHeight; bar.style.width = (max > 0 ? h.scrollTop / max * 100 : 0) + "%"; };
      addEventListener("scroll", upd, { passive: true }); addEventListener("resize", upd); upd();
    }
  }
  function init() {
    progress();
    document.querySelectorAll(".ag-widget[data-widget]").forEach((root) => {
      const f = W[root.dataset.widget];
      if (!f || root.dataset.ready) return;
      root.dataset.ready = "1"; root.innerHTML = ""; f(root);
    });
  }
  if (window.document$ && typeof window.document$.subscribe === "function") window.document$.subscribe(init);
  else if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();
