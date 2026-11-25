/* Page-level reading aids: reading progress, code-block language labels and folding, a scroll hint on wide
   tables, a search shortcut hint and a "report a problem" link. Plain DOM, no network, works from file://. */
(function () {
  "use strict";
  const zh = (document.documentElement.lang || "zh").toLowerCase().startsWith("zh");
  const T = (a, b) => (zh ? a : b);
  const ISSUES = "https://github.com/akribis-agito/Agito-Releases/issues/new";
  const LANGS = { csharp: "C#", cs: "C#", c: "C", cpp: "C++", python: "Python", py: "Python", bash: "Shell", powershell: "PowerShell", json: "JSON", xml: "XML", text: "" };
  const FOLD_LINES = 40;

  function progress() {
    const article = document.querySelector("article.md-content__inner");
    if (!article || article.scrollHeight < window.innerHeight * 2) return;
    const bar = document.createElement("div");
    bar.className = "ag-progress";
    document.body.appendChild(bar);
    const update = () => {
      const top = article.getBoundingClientRect().top + window.scrollY;
      const span = article.scrollHeight - window.innerHeight;
      const f = span > 0 ? Math.min(1, Math.max(0, (window.scrollY - top + 80) / span)) : 1;
      bar.style.transform = "scaleX(" + f + ")";
    };
    window.addEventListener("scroll", update, { passive: true });
    window.addEventListener("resize", update);
    update();
  }

  function codeBlocks() {
    document.querySelectorAll("div.highlight").forEach((block) => {
      const m = /language-(\w+)/.exec(block.className);
      const label = m ? LANGS[m[1]] ?? m[1] : "";
      if (label) block.setAttribute("data-lang", label);
      const code = block.querySelector("code");
      if (!code || block.closest(".ag-nofold")) return;
      const lines = code.textContent.replace(/\n$/, "").split("\n").length;
      if (lines <= FOLD_LINES) return;
      block.classList.add("ag-folded");
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "ag-fold";
      const set = () => (btn.textContent = block.classList.contains("ag-folded")
        ? T("展开全部 " + lines + " 行", "Show all " + lines + " lines")
        : T("收起", "Collapse"));
      btn.addEventListener("click", () => { block.classList.toggle("ag-folded"); set(); });
      set();
      block.appendChild(btn);
    });
  }

  function wideTables() {
    const mark = () => document.querySelectorAll(".md-typeset__scrollwrap").forEach((w) => {
      const wide = w.scrollWidth > w.clientWidth + 2;
      w.classList.toggle("ag-wide", wide);
      if (wide && !w.dataset.hint) {
        w.dataset.hint = T("← 左右滑动查看 →", "← scroll sideways →");
        w.addEventListener("scroll", () => w.classList.toggle("ag-scrolled", w.scrollLeft > 4), { passive: true });
      }
    });
    mark();
    setTimeout(mark, 400);   // Material wraps the tables after its own bundle starts
    window.addEventListener("resize", mark);
  }

  function searchHint() {
    const input = document.querySelector(".md-search__input");
    if (input) input.setAttribute("placeholder", T("搜索（按 / 键）", "Search (press /)"));
  }

  function feedback() {
    const article = document.querySelector("article.md-content__inner");
    if (!article || document.querySelector(".ag-hero")) return;
    const title = (article.querySelector("h1") || {}).textContent || document.title;
    const page = location.protocol === "file:" ? location.pathname.split("/site-offline/").pop() || location.pathname : location.href;
    const body = T("页面：", "Page: ") + page + "\n\n" + T("问题或建议：\n", "What is wrong or could be better:\n");
    const url = ISSUES + "?labels=guide&title=" + encodeURIComponent("[guide] " + title.replace(/¶$/, "").trim()) + "&body=" + encodeURIComponent(body);
    const box = document.createElement("aside");
    box.className = "ag-feedback";
    box.innerHTML = '<span>' + T("这一页有错误，或可以写得更清楚？", "Found a mistake, or could this page be clearer?") + "</span>" +
      '<a target="_blank" rel="noopener" href="' + url + '">' + T("报告问题", "Report it") + "</a>";
    article.appendChild(box);
  }

  // <div class="ag-filter-table"></div> before a table: a box that hides rows not containing the typed text
  function tableFilters() {
    document.querySelectorAll(".ag-filter-table").forEach((holder) => {
      let next = holder.nextElementSibling;
      const table = next && (next.matches("table") ? next : next.querySelector("table"));
      if (!table) return;
      const input = document.createElement("input");
      input.type = "search";
      input.className = "ag-input";
      input.placeholder = holder.dataset.placeholder || T("筛选…", "Filter…");
      const count = document.createElement("span");
      count.className = "ag-filter-count";
      holder.append(input, count);
      const rows = [...table.tBodies[0].rows];
      input.addEventListener("input", () => {
        const q = input.value.trim().toLowerCase();
        let shown = 0;
        rows.forEach((r) => { const hit = !q || r.textContent.toLowerCase().includes(q); r.hidden = !hit; shown += hit; });
        count.textContent = q ? T(shown + " / " + rows.length + " 条", shown + " of " + rows.length) : "";
      });
    });
  }

  // <div class="ag-cardtable"> around a table: each cell learns its column name, so CSS can show rows as cards on a phone
  function cardTables() {
    document.querySelectorAll(".ag-cardtable table").forEach((t) => {
      const heads = [...t.querySelectorAll("thead th")].map((th) => th.textContent.trim());
      t.querySelectorAll("tbody tr").forEach((tr) => [...tr.cells].forEach((td, i) => td.setAttribute("data-label", heads[i] || "")));
    });
  }

  // Screenshots open full-size on click. One reusable overlay, Escape / click / backdrop closes it.
  function lightbox() {
    const targets = document.querySelectorAll('.ag-shot img, .md-typeset img[src*="pcsuite-ui/"]');
    if (!targets.length) return;
    const box = document.createElement("dialog");
    box.className = "ag-lightbox";
    box.setAttribute("aria-label", T("图片放大预览", "Image preview"));
    const img = document.createElement("img");
    const cap = document.createElement("p");
    const dismiss = document.createElement("button");
    dismiss.type = "button";
    dismiss.className = "ag-lightbox-close";
    dismiss.textContent = T("关闭", "Close");
    box.append(img, cap, dismiss);
    document.body.appendChild(box);
    let lastFocus = null;
    box.addEventListener("close", () => {
      document.body.classList.remove("ag-lightbox-open");
      img.removeAttribute("src");
      if (lastFocus) lastFocus.focus();
    });
    targets.forEach((t) => {
      t.setAttribute("tabindex", "0");
      t.setAttribute("role", "button");
      const label = T("放大图片", "Enlarge image");
      t.setAttribute("aria-label", t.alt ? `${label} · ${t.alt}` : label);
      const open = () => {
        lastFocus = t;
        img.src = t.currentSrc || t.src;
        img.alt = t.alt || "";
        cap.textContent = t.alt || "";
        box.showModal();
        document.body.classList.add("ag-lightbox-open");
      };
      t.addEventListener("click", open);
      t.addEventListener("keydown", (e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); open(); } });
    });
    dismiss.addEventListener("click", () => box.close());
    box.addEventListener("click", (event) => { if (event.target === box || event.target === img) box.close(); });
  }

  // Demo filter: show a friendly empty state when nothing matches.
  function demoEmptyState() {
    const input = document.querySelector(".ag-demo-filter");
    if (!input) return;
    const cards = [...document.querySelectorAll(".ag-demo-card")];
    if (!cards.length) return;
    const empty = document.createElement("div");
    empty.className = "ag-empty-state";
    empty.setAttribute("role", "status");
    empty.innerHTML = "<b>" + T("没有匹配的 Demo", "No matching demos") + "</b>" +
      T("换个关键词试试，例如 Connect、Homing 或 Record。", "Try another keyword, e.g. Connect, Homing or Record.");
    input.insertAdjacentElement("afterend", empty);
    const sync = () => {
      const any = cards.some((c) => !c.hidden);
      empty.style.display = any ? "none" : "block";
    };
    input.addEventListener("input", () => setTimeout(sync, 0));
    sync();
  }

  // Back-to-top: appears after one viewport of scrolling.
  function backToTop() {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "ag-totop";
    btn.textContent = "↑";
    btn.setAttribute("aria-label", T("返回顶部", "Back to top"));
    document.body.appendChild(btn);
    const sync = () => btn.classList.toggle("show", window.scrollY > window.innerHeight);
    window.addEventListener("scroll", sync, { passive: true });
    sync();
    btn.addEventListener("click", () => {
      const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
      window.scrollTo({ top: 0, behavior: reduced ? "auto" : "smooth" });
    });
  }

  function init() { cardTables(); tableFilters(); progress(); codeBlocks(); wideTables(); searchHint(); feedback(); lightbox(); demoEmptyState(); backToTop(); }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init); else init();
})();
