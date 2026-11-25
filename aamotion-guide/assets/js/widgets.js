/* Interactive figures for the AAMotion Guide. A page places <div class="ag-widget" data-widget="NAME"></div>;
   this file renders it. Text follows the page language (zh / en). */
(function () {
  "use strict";
  const NS = "http://www.w3.org/2000/svg";
  const zh = () => (document.documentElement.lang || "zh").toLowerCase().startsWith("zh");
  const T = (a, b) => (zh() ? a : b);
  const reduced = () => window.matchMedia && matchMedia("(prefers-reduced-motion: reduce)").matches;
  const sleep = (ms) => new Promise((r) => setTimeout(r, reduced() ? 0 : ms));
  const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
  const sv = (tag, attrs, parent, text) => { const e = document.createElementNS(NS, tag); for (const k in attrs) e.setAttribute(k, attrs[k]); if (text != null) e.textContent = text; if (parent) parent.appendChild(e); return e; };
  const tween = (ms, fn) => new Promise((res) => {
    if (reduced()) { fn(1); res(); return; }
    const t0 = performance.now();
    const f = (now) => { const t = Math.min(1, (now - t0) / ms); fn(t); t < 1 ? requestAnimationFrame(f) : res(); };
    requestAnimationFrame(f);
  });
  const btn = (label, cls) => { const b = el("button", "ag-btn" + (cls ? " " + cls : ""), label); b.type = "button"; return b; };
  const log = (box, text, kind) => { const d = el("div", kind ? "ag-log-" + kind : "", null); d.textContent = text; box.appendChild(d); box.scrollTop = box.scrollHeight; };

  /* ---------- command architecture ---------- */
  function arch(root) {
    const N = {
      pcs: [20, 30, 130, 50, "PCSuite", T("自己的 AAComm", "its own AAComm")],
      app: [20, 160, 130, 60, T("你的程序", "Your program"), "C# · C++ · Python"],
      hook: [180, 30, 130, 50, "ErrorOccurred", T("错误钩子", "error hook")],
      am: [180, 160, 130, 60, "AAMotion", T("对象 · 单位 · 错误", "objects · units · errors")],
      ac: [340, 160, 120, 60, "AAComm", T("编码 · 队列 · 超时", "encode · queue · timeout")],
      lb: [490, 160, 100, 60, T("本机 TCP", "Local TCP"), "127.0.0.1:399"],
      srv: [620, 140, 130, 100, "AACommServer", T("通道 · FIFO", "channel · FIFO")],
      log: [620, 280, 130, 50, T("通信日志", "Comm log"), T("调试日志 · 抓包", "debug log · capture")],
      ctl: [780, 140, 110, 100, T("控制器", "Controller"), "TCP 50000"],
    };
    const S = {
      read: [T("读一个参数", "Read a parameter"), [
        ["app", "am", "axis.Pos", T("程序读 A 轴位置", "the program reads axis A position")],
        ["am", "ac", '"APos"', T("AAMotion 生成助记符", "AAMotion builds the mnemonic")],
        ["ac", "lb", "#42", T("查命令表编码，分配关联号", "encode via the message table, assign a correlation id")],
        ["lb", "srv", "IPC", T("经本机 TCP 交给服务器", "hand it to the server over local TCP")],
        ["srv", "ctl", "APos", T("排队、启动超时计时、发给控制器", "queue, start the timeout, send to the controller")],
        ["ctl", "srv", "24680>", T("控制器回复", "the controller replies")],
        ["srv", "ac", "#42", T("按关联号送回", "routed back by correlation id")],
        ["ac", "am", "24680", ""],
        ["am", "app", "24.68 mm", T("按用户单位换算后返回", "converted by User Units and returned")]]],
      move: [T("一次点位运动", "A point-to-point move"), [
        ["app", "am", "MoveAbs", T("调用绝对运动", "call MoveAbs")],
        ["am", "ac", "", T("拆成多条命令", "split into several commands")],
        ["ac", "srv", "", ""],
        ["srv", "ctl", "ASpeed…", T("写速度和加减速", "write speed and accel / decel")],
        ["ctl", "srv", "OK>", ""],
        ["srv", "ctl", "AMotionMode=1", T("点位模式", "PTP mode")],
        ["ctl", "srv", "OK>", ""],
        ["srv", "ctl", "AAbsTrgt", T("目标位置", "target")],
        ["ctl", "srv", "OK>", ""],
        ["srv", "ctl", "ABegin", T("开始运动", "start")],
        ["ctl", "srv", "OK>", ""],
        ["srv", "am", "", T("全部应答", "all answered")],
        ["am", "app", T("返回", "return"), T("要等到位用 MoveAbsAndWait", "use MoveAbsAndWait to wait for in-target")]]],
      err: [T("控制器报错", "A controller error"), [
        ["app", "am", "MotorOn", T("请求使能", "request servo on")],
        ["am", "ac", "", ""],
        ["ac", "srv", "AMotorOn=1", ""],
        ["srv", "ctl", "AMotorOn=1", ""],
        ["ctl", "srv", "ERR 31", T("拒绝：尚未执行自动换相", "refused: auto-phasing not done")],
        ["srv", "log", T("红色", "red"), T("通信日志里标红", "shown red in the comm log")],
        ["srv", "am", "ERR 31", ""],
        ["am", "hook", "(31, AMotorOn=1, …)", T("触发错误钩子", "the error hook fires")],
        ["am", "app", T("异常", "exception"), T("C# 抛 ControllerCommandException；C 返回 false", "C# throws ControllerCommandException; C returns false")]]],
      share: [T("两个程序共享", "Two programs share"), [
        ["pcs", "lb", "ALoggerUpload", T("PCSuite 在上传数据", "PCSuite uploads data")],
        ["app", "am", "APos", T("你的程序读位置", "your program reads position")],
        ["am", "lb", "", ""],
        ["lb", "srv", "FIFO", T("两条请求进同一个队列", "both requests join one queue")],
        ["srv", "ctl", T("先发 PCSuite 的", "PCSuite first"), T("一次只发一条", "one request in flight")],
        ["ctl", "srv", T("大块数据", "bulk data"), ""],
        ["srv", "pcs", "", ""],
        ["srv", "ctl", "APos", T("轮到你的请求", "now your request")],
        ["ctl", "srv", "50001>", ""],
        ["srv", "am", "", ""],
        ["am", "app", "", T("变慢是因为排队，不是控制器慢", "slower because of queueing, not the controller")]]],
    };
    const tabs = el("div", "ag-tabs"), stage = el("div", "ag-stage"), side = el("div", "ag-side");
    const cap = el("div", "ag-cap"), logbox = el("div", "ag-log"), ctrls = el("div", "ag-ctrls");
    const play = btn(T("▶ 播放", "▶ Play")), step = btn(T("下一步", "Step"), "ghost");
    ctrls.append(play, step); side.append(cap, ctrls, logbox);
    const svg = sv("svg", { viewBox: "0 0 900 350", class: "ag-svg", role: "img" }, null);
    stage.appendChild(svg);
    const ctr = (k) => { const n = N[k]; return [n[0] + n[2] / 2, n[1] + n[3] / 2]; };
    const line = (a, b, dash, label) => {
      const [x1, y1] = ctr(a), [x2, y2] = ctr(b);
      sv("line", { x1, y1, x2, y2, class: "ag-edge" + (dash ? " dash" : "") }, svg);
      if (label) sv("text", { x: (x1 + x2) / 2, y: (y1 + y2) / 2 - 8, class: "ag-small", "text-anchor": "middle" }, svg, label);
    };
    line("app", "am"); line("am", "ac"); line("ac", "lb"); line("lb", "srv"); line("srv", "ctl");
    line("pcs", "lb", 1); line("am", "hook", 1); line("srv", "log", 1);
    const G = {};
    for (const k in N) {
      const [x, y, w, h, t, d] = N[k];
      const g = sv("g", { class: "ag-node" + (k === "srv" ? " srv" : k === "ctl" ? " ctl" : "") }, svg);
      sv("rect", { x, y, width: w, height: h, rx: 12 }, g);
      sv("text", { x: x + w / 2, y: y + h / 2 - 3, class: "ag-title", "text-anchor": "middle" }, g, t);
      sv("text", { x: x + w / 2, y: y + h / 2 + 14, class: "ag-small", "text-anchor": "middle" }, g, d);
      G[k] = g;
    }
    const dot = sv("circle", { r: 9, cx: -50, cy: -50, class: "ag-dot" }, svg);
    const tag = sv("text", { x: -50, y: -50, class: "ag-tag", "text-anchor": "middle" }, svg, "");
    let cur = "read", idx = 0, busy = false;
    const reset = () => { idx = 0; logbox.innerHTML = ""; Object.values(G).forEach((g) => g.classList.remove("hot")); dot.setAttribute("cx", -50); tag.setAttribute("x", -50); cap.textContent = T("选一个场景，点“播放”或“下一步”。", "Pick a scenario, then Play or Step."); };
    const one = async () => {
      const s = S[cur][1][idx]; if (!s) return false;
      const [a, b, lab, txt] = s, [x1, y1] = ctr(a), [x2, y2] = ctr(b);
      Object.values(G).forEach((g) => g.classList.remove("hot")); G[a].classList.add("hot"); tag.textContent = lab;
      await tween(520, (t) => { const x = x1 + (x2 - x1) * t, y = y1 + (y2 - y1) * t; dot.setAttribute("cx", x); dot.setAttribute("cy", y); tag.setAttribute("x", x); tag.setAttribute("y", y - 14); });
      G[b].classList.add("hot");
      if (txt) { cap.textContent = (idx + 1) + "/" + S[cur][1].length + "  " + txt; }
      log(logbox, N[a][4] + " → " + N[b][4] + (lab ? "   " + lab : ""), /ERR|异常|exception/.test(lab) ? "err" : "");
      idx++; return true;
    };
    Object.keys(S).forEach((k, i) => {
      const b = btn(S[k][0], i ? "" : "on"); b.dataset.k = k;
      b.onclick = () => { if (busy) return; tabs.querySelectorAll("button").forEach((x) => x.classList.toggle("on", x === b)); cur = k; reset(); };
      tabs.appendChild(b);
    });
    play.onclick = async () => { if (busy) return; busy = true; reset(); while (await one()) await sleep(120); busy = false; };
    step.onclick = async () => { if (busy) return; busy = true; if (!(await one())) reset(); busy = false; };
    const body = el("div", "ag-row"); body.append(stage, side);
    side.prepend(ctrls);
    root.append(tabs, body); reset();
  }

  /* ---------- connect sequence ---------- */
  function connect(root) {
    const S = [
      [T("找到或启动服务器", "Find or start the server"), T("在程序目录、当前工作目录（以及其下的 aacommserver 子目录）找 AACommServer；Windows 上最后查 PCSuite 安装路径。最多等 15 秒端口就绪。", "Look for AACommServer in the application folder and the working directory (and their aacommserver subfolder); on Windows, finally the PCSuite install path. Waits up to 15 s for the port."), 1500],
      [T("和服务器握手", "Handshake with the server"), T("新开本机 TCP 连接，用二进制协议发 Open；2 秒内收到握手包（服务器版本和功能位）。收不到就改用旧的字符串协议。", "Open a local TCP connection and send a binary Open; a handshake (server version and features) arrives within 2 s, otherwise fall back to the string protocol."), 300],
      [T("拨号控制器", "Dial the controller"), T("服务器连接控制器：以太网 10 秒超时，仿真 20 秒，串口 / CAN 1 秒。", "The server connects: 10 s on Ethernet, 20 s for a simulator, 1 s on RS-232 / CAN."), 800],
      [T("读控制器身份", "Read identity"), T("型号、固件版本、轴数、功能位。型号必须和初始化时选的一致。", "Model, firmware version, axis count, feature bits. The model must match the one given to Initialize."), 200],
      [T("读命令表", "Read the message table"), T("这个固件支持的全部关键字、编码和类型。按固件版本缓存在“文档\\PCSuite\\ApplicationData\\产品编号\\固件版本”下，同一固件第二次连接直接读文件。", "Every keyword this firmware supports, with its encoding and type. Cached per firmware under Documents\\PCSuite\\ApplicationData\\<product>\\<firmware>, so a second connect reads the file."), 900],
      [T("读用户单位", "Read User Units"), T("各轴是否启用单位换算、系数和单位名。", "Per-axis unit conversion state, factors and names."), 200],
      [T("启动缓存", "Start the cache"), T("如果 AAMotionConfig.FastReadEnabled = true，自动注册默认参数并开始后台刷新。", "If AAMotionConfig.FastReadEnabled is true, register the defaults and start the background refresh."), 100],
    ];
    const list = el("div", "ag-steps"), detail = el("div", "ag-cap"), ctrls = el("div", "ag-ctrls");
    const next = btn(T("下一步", "Next")), auto = btn(T("▶ 自动播放", "▶ Auto"), "ghost"), rst = btn(T("重置", "Reset"), "ghost");
    ctrls.append(next, auto, rst);
    let i = -1, t = 0, busy = false;
    const render = () => {
      list.innerHTML = "";
      S.forEach((s, k) => { const d = el("div", "ag-step" + (k < i ? " done" : k === i ? " cur" : ""), "<b>" + (k + 1) + ". " + s[0] + "</b><span>" + (k < i ? "✓" : k === i ? "…" : "") + "</span>"); list.appendChild(d); });
      detail.innerHTML = i < 0 ? T("点“下一步”开始连接。", "Press Next to start connecting.") : i >= S.length ? "<b>" + T("连接成功", "Connected") + "</b> · " + T("示意耗时", "illustrative time") + " ≈ " + (t / 1000).toFixed(1) + " s" : "<b>" + S[i][0] + "</b><br>" + S[i][1];
    };
    next.onclick = () => { if (i < S.length) { if (i >= 0) t += S[i][2]; i++; } render(); };
    rst.onclick = () => { i = -1; t = 0; render(); };
    auto.onclick = async () => { if (busy) return; busy = true; rst.onclick(); for (let k = 0; k <= S.length; k++) { next.onclick(); await sleep(900); } busy = false; };
    root.append(list, ctrls, detail); render();
  }

  /* ---------- where the SDK looks for the server ---------- */
  function serverLookup(root) {
    const P = [
      [T("应用程序目录", "Application folder"), T("可执行文件所在目录（C# 程序、C++ 程序）", "the executable's folder (C# or C++ program)")],
      [T("应用程序目录\\aacommserver", "Application folder\\aacommserver"), T("NuGet 给 .NET Framework 项目放服务器的位置", "where NuGet puts the server for .NET Framework projects")],
      [T("当前工作目录", "Working directory"), T("Python 进程靠它：python.exe 的目录里没有服务器", "what a Python process relies on: python.exe's folder has no server")],
      [T("当前工作目录\\aacommserver", "Working directory\\aacommserver"), ""],
      [T("注册表：PCSuite 安装路径", "Registry: PCSuite install path"), T("只在 Windows；装过 PCSuite 的电脑能找到", "Windows only; found on a PC with PCSuite installed")],
    ];
    const opts = [
      [T("C# 程序（NuGet，.NET Framework）", "C# program (NuGet, .NET Framework)"), 1],
      [T("C++ 程序（服务器拷到 exe 旁边）", "C++ program (server copied beside the exe)"), 0],
      [T("Python（aamotion.py 切换工作目录）", "Python (aamotion.py switches the working directory)"), 2],
      [T("什么都没放，但装过 PCSuite", "Nothing shipped, PCSuite installed"), 4],
      [T("什么都没放，也没装 PCSuite", "Nothing shipped, no PCSuite"), -1],
    ];
    const sel = el("select", "ag-select");
    opts.forEach((o, k) => { const op = el("option", null, o[0]); op.value = k; sel.appendChild(op); });
    const run = btn(T("▶ 查找", "▶ Look up"));
    const list = el("div", "ag-steps"), out = el("div", "ag-cap");
    const go = async () => {
      const hit = opts[+sel.value][1];
      list.innerHTML = ""; out.textContent = "";
      const rows = P.map((p, k) => { const d = el("div", "ag-step", "<b>" + (k + 1) + ". " + p[0] + "</b><span></span>"); if (p[1]) d.title = p[1]; list.appendChild(d); return d; });
      for (let k = 0; k < rows.length; k++) {
        rows[k].classList.add("cur"); await sleep(450); rows[k].classList.remove("cur");
        if (k === hit) { rows[k].classList.add("done"); rows[k].querySelector("span").textContent = T("找到 ✓", "found ✓"); out.innerHTML = T("启动 <code>", "Start <code>") + "AACommServer" + "</code>" + T("，初始化成功。", "; initialization succeeds."); return; }
        rows[k].querySelector("span").textContent = T("没有", "no");
      }
      out.innerHTML = T("<b>没找到服务器：</b>C# 初始化失败，C 接口 <code>aamotion_initialize</code> 返回 NULL。", "<b>No server found:</b> C# initialization fails and <code>aamotion_initialize</code> returns NULL.");
    };
    run.onclick = go;
    const ctrls = el("div", "ag-ctrls"); ctrls.append(sel, run);
    root.append(ctrls, list, out);
  }

  /* ---------- simulator session lifecycle ---------- */
  function simLife(root) {
    const files = el("div", "ag-folder"), logbox = el("div", "ag-log"), ctrls = el("div", "ag-ctrls");
    const con = btn(T("ConnectSimulation", "ConnectSimulation")), save = btn("ASave", "ghost"), dis = btn(T("Disconnect", "Disconnect"), "ghost");
    ctrls.append(con, save, dis);
    let state = 0, id = "";
    const draw = () => {
      if (!state) { files.innerHTML = "<span class='ag-muted'>%TEMP%\\  " + T("（没有仿真目录）", "(no simulator folder)") + "</span>"; return; }
      files.innerHTML = "<div><b>%TEMP%\\aasim-" + id + "\\</b></div><div class='ag-file'>agito.par" + (state === 2 ? " <em>" + T("已保存", "saved") + "</em>" : "") + "</div><div class='ag-file'>motor.txt</div>";
    };
    con.onclick = () => {
      if (state) { log(logbox, T("已经连着：要重连先 Disconnect", "Already open: Disconnect first to reconnect"), "warn"); return; }
      id = Math.random().toString(16).slice(2, 10); state = 1;
      log(logbox, T("新建临时目录 aasim-", "create temp folder aasim-") + id + T("，从 starter.par 生成 agito.par，启动 firmware_sim.exe", ", stage agito.par from starter.par, start firmware_sim.exe"));
      draw();
    };
    save.onclick = () => { if (!state) return; state = 2; log(logbox, T("ASave：参数写进这个临时目录里的 agito.par", "ASave: parameters written to agito.par in this temp folder")); draw(); };
    dis.onclick = () => { if (!state) return; log(logbox, T("断开：仿真进程结束，整个临时目录删除——保存的参数也一起没了", "Disconnect: the simulator exits and the whole temp folder is deleted, saved parameters included"), "err"); state = 0; draw(); };
    root.append(ctrls, files, logbox); draw();
  }

  /* A status explanation, with no device connection or motion calculation. */
  function motionStates(root) {
    root.append(el("p", "", T("状态示意：不连接设备；点击阶段查看 API 与判据。阶段宽度不代表实际耗时。", "State illustration: no device connection. Select a stage for its API and criterion. Width is not elapsed time.")));
    const stages = [
      [T("换相", "Commutate"), "AutoPhase → IsAutoPhase", "0", T("不作判据", "not the criterion"), T("先等待换相完成，失败或超时不进入使能；仿真器也遵循此顺序。", "Wait for commutation before enabling, including in the simulator. Do not enable after failure or timeout.")],
      [T("使能", "Motor on"), "MotorOn", "0", T("不作判据", "not the criterion"), T("已确认换相后使能，读取 MotorOn == 1，并处理命令失败。", "Enable after confirmed commutation; verify MotorOn == 1 and handle command failures.")],
      [T("运动", "Move"), "MoveAbs / Begin", "≠ 0", "2", T("命令返回与整定到位是两个时刻。当前分支的缓存状态机处理启动后的旧状态。", "Command return and settling occur at different times. The current branch cache handles old startup states.")],
      [T("运动结束", "Motion ended"), "MotionStat == 0", "0", "≠ 4", T("此处运动已结束，但尚未整定到位。还需结合 MotionReason 排查异常停止。", "Here motion has ended but settling is incomplete. Inspect MotionReason for abnormal stops.")],
      [T("整定到位", "Settled"), "InTargetStat == 4", "0", "4", T("满足 InTargetTol 与 InTargetTime；不要求 Pos 浮点数完全等于目标。", "InTargetTol and InTargetTime are satisfied; exact floating-point Pos equality is not required.")]
    ];
    const controls = el("div", "ag-controls");
    const svg = sv("svg", {viewBox:"0 0 800 110", role:"img", "aria-label":T("运动状态阶段图", "Motion state stages")});
    svg.style.width = "100%";
    const boxes = stages.map((s, i) => {
      const g = sv("g", {}, svg);
      const r = sv("rect", {x:i*160+4,y:20,width:150,height:65,rx:8,stroke:"currentColor","stroke-width":1}, g);
      sv("text", {x:i*160+79,y:58,"text-anchor":"middle",fill:"currentColor","font-size":16}, g, s[0]);
      return r;
    });
    const details = el("div", "ag-log");
    details.setAttribute("aria-live", "polite");
    let active = 0;
    function show(index) {
      active = index;
      buttons.forEach((b,i) => b.setAttribute("aria-pressed", String(i===index)));
      boxes.forEach((b,i) => b.setAttribute("fill", i===index ? "var(--md-accent-fg-color--transparent, #c8e6f5)" : "transparent"));
      details.replaceChildren();
      const s = stages[index];
      log(details, s[1]);
      log(details, "MotionStat: " + s[2] + " · InTargetStat: " + s[3]);
      log(details, s[4]);
    }
    const buttons = stages.map((s,i) => {const b=btn(s[0]); b.onclick=()=>show(i); controls.append(b); return b;});
    const timeout = btn(T("等待超时会怎样", "What if waiting times out?"));
    timeout.onclick=()=>{show(active); log(details,T("*AndWait 返回 false 不会自动停止。按需请求 Stop（Decel），再核对实际状态；Abort 使用 EmrgDec。", "A false *AndWait return does not stop motion. Request Stop (Decel) as needed, then inspect actual state. Abort uses EmrgDec."), "err");};
    controls.append(timeout);
    root.append(controls, svg, details); show(0);
  }

  function singleMotion(root) {
    const kind = root.dataset.example;
    const example = (window.AG_SINGLE_DEMOS || {})[kind];
    if (!example) return;
    const stopping = kind === "stop" || kind === "abort";
    const method = {"move-abs":"MoveAbs", "move-rel":"MoveRel", "move-abs-wait":"MoveAbsAndWait", stop:"Stop", abort:"Abort"}[kind];
    root.append(el("p", "", T("教学示意，不连接设备。动画节奏和曲线不代表实际运动时间或固件轨迹。代码来自本页独立工程。", "Teaching illustration with no device connection. Animation timing and curves do not predict actual motion or firmware trajectories. Code comes from this standalone project.")));
    const controls = el("div", "ag-ctrls");
    const play = btn("▶ "+T("播放", "Play")), pause = btn("❚❚ "+T("暂停", "Pause")), step = btn("⏭ "+T("单步", "Step")), reset = btn("↺ "+T("重置", "Reset"));
    reset.classList.add("ghost");
    controls.append(play, pause, step, reset);
    const rate = el("select", "ag-select");
    rate.setAttribute("aria-label", T("减速度关系", "Deceleration relationship"));
    [["equal","EmrgDec = Decel"],["higher","EmrgDec > Decel"],["lower","EmrgDec < Decel"]].forEach(([v,t])=>{const o=el("option",null,t);o.value=v;rate.append(o);});
    if (stopping) {controls.append(rate); root.append(el("p", "", T("相同初速度下比较减速趋势。示例读取实际设置，不修改 Decel 或 EmrgDec；曲线只展示所选关系。", "Compare deceleration trends from the same initial velocity. The sample reads actual settings without changing Decel or EmrgDec; curves illustrate only the selected relationship.")));}
    root.append(controls);
    const track = el("div", "ag-track");
    root.append(track);
    const svg = sv("svg", {viewBox: stopping ? "0 0 720 180" : "0 0 720 112", role:"img", "aria-label":T("运动与目标示意", "Motion and target illustration")});
    svg.classList.add("ag-svg");
    const start = kind === "move-rel" ? 7000 : 2000;
    const goal = kind === "move-rel" ? 8000 : 10000;
    const x = v => 30 + v / 12000 * 660;
    sv("line",{x1:30,y1:55,x2:690,y2:55,stroke:"currentColor"},svg);
    const targetMark = sv("line",{x1:x(goal),x2:x(goal),y1:30,y2:80,stroke:"var(--ag-warn)","stroke-dasharray":"4 4"},svg);
    const targetText = sv("text",{x:x(goal),y:22,"text-anchor":"middle",fill:"currentColor","font-size":13},svg, T("目标 ","Target ")+goal);
    targetMark.style.display = targetText.style.display = stopping ? "none" : "";
    const dot = sv("circle",{cx:x(start),cy:55,r:9,fill:"var(--ag-hot)"},svg);
    const position = sv("text",{x:30,y:100,fill:"currentColor","font-size":14},svg);
    const normal = sv("path",{d:"M 40 125 L 190 125 L 380 170",fill:"none",stroke:"var(--ag-c1)","stroke-width":3},svg);
    const emergency = sv("path",{fill:"none",stroke:"var(--ag-c2)","stroke-width":3},svg);
    normal.style.display = emergency.style.display = stopping ? "" : "none";
    const legend = sv("text",{x:400,y:145,fill:"currentColor","font-size":13},svg, "Stop: Decel / Abort: EmrgDec");
    legend.style.display = stopping ? "" : "none";
    root.append(svg);
    const status=el("p","ag-cap"); status.setAttribute("aria-live","polite");
    const codeBox=el("pre","ag-single-code"), codeEl=el("code");codeBox.append(codeEl);
    const consoleBox=el("div","ag-console"), consoleHead=el("div","ag-console-head"), message=el("ol");
    consoleHead.append(el("span",null,T("步骤日志","Step log")), el("span",null,T("示意，不是程序实测输出","Illustration, not captured program output")));
    message.setAttribute("aria-live","polite");
    consoleBox.append(consoleHead,message);
    root.append(status,codeBox,consoleBox);
    const KEYWORDS=/^(if|else|while|for|return|var|bool|double|int|new|try|catch|finally|true|false|null|using|string)$/;
    const TYPES=/^(AAMotionAPI|AxisRef|ISingleAxis|Stopwatch|Thread|Console|StringComparison|eKeyword|ControllerType|MotionController|Exception|ControllerCommandException)$/;
    function highlight(line, target){
      const re=/(\/\/.*$)|(\$?"(?:[^"\\]|\\.)*")|\b(\d+(?:\.\d+)?)\b|\b([A-Za-z_]\w*)\b/g;
      let last=0, m;
      while((m=re.exec(line))){
        if(m.index>last) target.append(document.createTextNode(line.slice(last,m.index)));
        const kind=m[1]?"c":m[2]?"s":m[3]?"n":KEYWORDS.test(m[4])?"k":TYPES.test(m[4])?"t":"";
        if(kind){const t=el("span","ag-tok-"+kind);t.textContent=m[0];target.append(t);}
        else target.append(document.createTextNode(m[0]));
        last=re.lastIndex;
      }
      if(last<line.length) target.append(document.createTextNode(line.slice(last)));
    }
    const stages = [
      [T("连接", "Connect"), "Connect / ConnectSimulation", T("显式指定 IP 或仿真器路径；失败时不运动。", "Specify IP or simulator path; do not move on connection failure.")],
      [T("换相", "Commutate"), "AutoPhase → IsAutoPhase", T("先检查换相，等待有超时；仿真器也要换相。", "Check commutation with a bounded wait, including simulation.")],
      [T("使能", "Motor on"), "MotorOn", "MotorOn == 1"],
      [T("发出命令", "Issue command"), stopping ? "Jog(1000)" : method, T("命令请求与运动完成是不同阶段。", "Requesting motion and completing it are different stages.")],
      [T("运动中", "Moving"), stopping ? method : method === "MoveAbsAndWait" ? "MoveAbsAndWait (waiting)" : "InTargetStat", "MotionStat ≠ 0 · InTargetStat = 2"],
      [T("运动结束", "Motion ended"), "MotionStat", T("MotionStat = 0；尚未证明整定到位。", "MotionStat = 0; settling has not been established.")],
      [stopping ? T("检查停止结果", "Check stop result") : T("整定到位", "Settled"), stopping ? "MotionStat == 0" : "InTargetStat == 4", stopping ? T("停止判据是 MotionStat = 0；InTargetStat 不作到位承诺。", "The stop criterion is MotionStat = 0, without promising an in-target value.") : T("InTargetStat = 4，满足配置的容差和整定时间。", "InTargetStat = 4 meets configured tolerance and settling time.")],
      [T("清理与断开", "Cleanup and disconnect"), "Stop → MotionStat == 0 → MotorOff → Disconnect", T("确认停止再断使能；清理失败必须核对设备实际状态。", "Confirm stop before motor-off; inspect actual state on cleanup failure.")]
    ];
    let index=0, progress=0, playing=false, frame=0, previous=0;
    function draw() {
      const s=stages[index];
      status.textContent=(index+1)+" / "+stages.length+" · "+s[0]+" · "+s[1];
      track.style.setProperty("--n", stages.length);
      track.replaceChildren(...stages.map((_,i)=>el("span", i<index?"done":i===index?"cur":"")));
      message.replaceChildren(...stages.slice(0,index+1).map((st,i)=>{
        const li=el("li", i===index?"cur":"");
        li.append(el("b",null,"["+(i+1)+"/"+stages.length+"]"), document.createTextNode(st[0]+" · "+st[1]), el("em",null,st[2]));
        return li;
      }));
      message.scrollTop=message.scrollHeight;
      const moved=index<4?0:index===4?progress:1;
      const pos=stopping?start+1200*moved:start+(goal-start)*moved;
      dot.setAttribute("cx",x(pos));
      position.textContent=T("示意位置：", "Illustrated position: ")+Math.round(pos)+(stopping?"":" · "+T("绝对目标：", "Absolute target: ")+goal);
      const end={equal:380,higher:285,lower:540}[rate.value];
      emergency.setAttribute("d","M 40 125 L 190 125 L "+end+" 170");
      codeEl.replaceChildren();
      const code=index===0?example.connect:index<=2?example.prepare:index===7?example.cleanup:index>=5?example.observe:example.operation;
      code.split("\n").forEach(line=>{
        const span=el("span");if(line)highlight(line,span);else span.textContent=" ";
        const active=index>=3&&index<=6;
        if(active && (line.includes("."+method+"(") || index===3&&stopping&&line.includes(".Jog(") || index>=5&&(line.includes("MotionStat")||line.includes("InTargetStat")||line.includes("settled"))))span.classList.add("ag-code-active");
        codeEl.append(span);
      });
      pause.disabled=!playing;
      play.disabled=playing||index===stages.length-1;
      step.disabled=index===stages.length-1;
      root.dataset.stage=String(index);
    }
    function halt(){playing=false;cancelAnimationFrame(frame);previous=0;}
    function tick(now){
      if(!playing||!root.isConnected){halt();return;}
      if(previous)progress+=(now-previous)/1100;
      previous=now;
      if(progress>=1){progress=0;index++;if(index>=stages.length-1){index=stages.length-1;halt();}}
      draw();if(playing)frame=requestAnimationFrame(tick);
    }
    play.onclick=()=>{if(reduced()){halt();index=stages.length-1;progress=0;draw();return;}playing=true;previous=0;draw();frame=requestAnimationFrame(tick);};
    pause.onclick=()=>{halt();draw();};
    step.onclick=()=>{halt();index=Math.min(index+1,stages.length-1);progress=0;draw();};
    reset.onclick=()=>{halt();index=0;progress=0;draw();};
    rate.onchange=()=>{halt();draw();};
    draw();
  }

  function homing(root) {
    const stages = {
      ready: [55, T("起点：轴已使能，等待回零命令。", "Start: the axis is enabled and ready for the homing command.")],
      limit: [310, T("寻找限位：轴沿搜索方向移动，直到限位开关输入触发。", "Seek the limit: the axis moves in the search direction until the limit-switch input triggers.")],
      stop: [375, T("位置误差检测：机构碰到硬限位后，实际位置停止变化，指令位置仍在推进。位置误差达到阈值时，控制器确认触及硬限位。", "Detect the hard stop: feedback position stops at the mechanical stop while the command position advances. The controller detects the stop when following error reaches the threshold.")],
      stuck: [375, T("堵转检测：轴抵住硬限位后，实际速度低于阈值，驱动电流超过阈值。这两个条件持续达到设定时间时，控制器确认堵转。", "Detect motor stall: at the mechanical stop, actual velocity is below its threshold and motor current is above its threshold. The controller detects a stall when both conditions persist for the configured time.")],
      leaveLimit: [295, T("反向离开限位开关，释放限位输入，然后低速搜索 Index。", "Back away from the switch to release the limit input, then search slowly for the index.")],
      leaveStop: [345, T("反向退离硬限位，然后低速搜索 Index。", "Back away from the mechanical stop, then search slowly for the index.")],
      index: [210, T("锁存 Index：编码器 Z 相脉冲到达时，控制器锁存当时的位置。主编码器作为锁存源时需要固件支持。", "Latch the index: the controller captures the position at the encoder Z pulse. Using the main encoder as lock source requires firmware support.")],
      done: [null, T("建立坐标零点：控制器使用找到的参考位置和配置的偏移。HomingStat = 100 表示成功，负值表示失败。", "Set the coordinate origin from the reference and any configured offset. HomingStat = 100 means success. A negative value means failure.")],
    };
    const methods = {
      limit: [T("限位开关", "Limit switch"), ["limit"]],
      index: ["Index", ["index"]],
      "limit-index": [T("限位开关 + Index", "Limit switch + index"), ["limit", "leaveLimit", "index"]],
      "hard-stop": [T("硬限位（位置误差）", "Hard stop by position error"), ["stop"]],
      "motor-stall": [T("硬限位（堵转）", "Hard stop by motor stall"), ["stuck"]],
      "hard-stop-index": [T("硬限位 + Index", "Mechanical hard stop + index"), ["stop", "leaveStop", "index"]],
    };
    const select = el("select", "ag-select");
    select.setAttribute("aria-label", T("回零方法", "Homing method"));
    for (const [key, [label]] of Object.entries(methods)) {
      const option = el("option", null, label);
      option.value = key;
      select.append(option);
    }
    select.value = root.dataset.example || "limit";
    const play = btn(T("播放", "Play"));
    const step = btn(T("下一步", "Next step"));
    const reset = btn(T("重置", "Reset"));
    const controls = el("div", "ag-ctrls");
    controls.append(select, play, step, reset);
    const svg = sv("svg", {viewBox: "0 0 420 112", role: "img",
      "aria-label": T("回零信号和轴位置示意", "Homing signals and illustrated axis position")});
    svg.classList.add("ag-svg");
    sv("line", {x1: 35, y1: 58, x2: 395, y2: 58, stroke: "currentColor"}, svg);
    for (const [x, y, label] of [[210, 29, "Index (Z)"], [310, 98, T("限位开关", "Limit switch")],
                                 [375, 29, T("硬限位", "Hard stop")]]) {
      sv("line", {x1: x, x2: x, y1: 40, y2: 76, stroke: "currentColor",
        "stroke-dasharray": x === 375 ? "" : "3 3"}, svg);
      sv("text", {x, y, fill: "currentColor", "text-anchor": "middle", "font-size": 12}, svg, label);
    }
    const dot = sv("circle", {cx: 55, cy: 58, r: 8, fill: "var(--ag-hot)"}, svg);
    const status = el("p", "ag-cap");
    status.setAttribute("aria-live", "polite");
    root.append(el("p", null, T("教学示意。播放或单步查看触发信号与参考点。", "Play or step through the illustration to see what triggers each reference event.")),
      controls, svg, status);
    let position = 55, index = 0, revision = 0;
    const sequence = () => ["ready", ...methods[select.value][1], "done"];
    function show() {
      const keys = sequence();
      status.textContent = (index + 1) + "/" + keys.length + " · " + stages[keys[index]][1];
      play.disabled = step.disabled = index === keys.length - 1;
    }
    function restart() {
      revision++;
      index = 0;
      position = 55;
      dot.setAttribute("cx", position);
      show();
    }
    async function advance(token) {
      const keys = sequence();
      if (index >= keys.length - 1) return;
      const destination = stages[keys[++index]][0] ?? position;
      const start = position;
      play.disabled = step.disabled = true;
      await tween(750, t => {
        if (token === revision) dot.setAttribute("cx", start + (destination - start) * t);
      });
      if (token !== revision || !root.isConnected) return;
      position = destination;
      show();
    }
    step.onclick = () => advance(++revision);
    play.onclick = async () => {
      const token = ++revision;
      while (token === revision && root.isConnected && index < sequence().length - 1) {
        await advance(token);
        play.disabled = step.disabled = true;
        await new Promise(resolve => setTimeout(resolve, 1800));
      }
      if (token === revision && root.isConnected) show();
    };
    select.onchange = reset.onclick = restart;
    restart();
  }

  const W = { arch, connect, "server-lookup": serverLookup, "sim-life": simLife, "motion-states": motionStates, "single-motion": singleMotion, homing };
  function demoFilter(input) {
    const cards = [...document.querySelectorAll(".ag-demo-card")];
    input.addEventListener("input", () => {
      const q = input.value.trim().toLowerCase();
      cards.forEach(c => { c.hidden = q !== "" && !((c.dataset.text || "") + " " + c.textContent).toLowerCase().includes(q); });
      document.querySelectorAll(".ag-demo-chapter").forEach(sec => {
        sec.hidden = q !== "" && !sec.querySelector(".ag-demo-card:not([hidden])");
      });
    });
  }
  function init() {
    document.querySelectorAll(".ag-demo-filter:not([data-ready])").forEach(input => { input.dataset.ready = "1"; demoFilter(input); });
    document.querySelectorAll(".ag-widget[data-widget]").forEach((root) => {
      if (root.dataset.ready) return;
      const f = W[root.dataset.widget]; if (!f) return;
      root.dataset.ready = "1"; root.innerHTML = ""; f(root);
    });
  }
  if (window.document$ && typeof window.document$.subscribe === "function") window.document$.subscribe(init);
  else if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();

