"""Build the UI layout mockups (Logs page and Tune page) as HTML, then render PNGs.

Uses the real sample logs in Tests/Trrimed logs and the real tunes in
Tests/tune settnigs so the mockups show believable values.

Run from the repo root:  python Docs/mockups/build_mockups.py
"""
import html
import os
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Docs", "mockups")
LOGS = os.path.join(ROOT, "Tests", "Trrimed logs")
TUNES = os.path.join(ROOT, "Tests", "tune settnigs")

RUN_LOGS = ["Session 55.csv", "Session 56.csv", "Session 53.csv"]
RUN_TUNES = ["yeti tune.dat", "boost by throttle .dat", "boost by rpm.dat"]
DASH = ["", "9 6", "2 5"]          # solid, dashed, dotted (Run 1, 2, 3)
DASH_NAME = ["solid", "dashed", "dotted"]

# Channel colours copied from Utils/ColorMap.cs
CHANNELS = [
    ("RPM", "#CD573F", "rpm", "{:,.0f}"),
    ("Throttle %", "#F0F0F0", "thr", "{:.0f} %"),
    ("Voltage", "#FF4D4D", "volt", "{:.2f} V"),
    ("Current", "#2ED957", "amp", "{:.0f} A"),
    ("Ripple", "#C855D6", "rip", "{:.2f} V"),
    ("PowerOut", "#6CB0E0", "pwr", "{:.0f} %"),
    ("MotorTemp", "#B18BF5", "mt", "{:.0f} °C"),
    ("ESC Temp", "#FF4AE0", "et", "{:.0f} °C"),
    ("MotorTiming", "#4A7AFF", "tim", "{:.0f} °"),
    ("Acceleration", "#6FA1FF", "acc", "{:.2f} g"),
]
VISIBLE = {"RPM", "Throttle %", "Current", "Voltage"}
GPS = [("RaceBox Speed", "#FFA033"), ("RaceBox Distance", "#8CCB35"), ("RaceBox G-Force X", "#33E6E6")]
LOGGER = [("Logger Throttle %", "#FFE066"), ("Logger RPM", "#F29E86"), ("Logger Wheel Speed", "#FF8FB1")]

T_MIN, T_MAX = -0.5, 2.5
CURSOR_T = 0.85


def num(s):
    s = s.strip().rstrip("b")
    try:
        return float(s)
    except ValueError:
        return 0.0


def load_log(name):
    rows = []
    with open(os.path.join(LOGS, name), encoding="latin1") as f:
        lines = [l for l in f if not l.startswith("#")]
    header = [h.strip() for h in lines[0].split(",")]
    for line in lines[2:]:
        parts = line.split(",")
        if len(parts) < len(header):
            continue
        v = {h: num(p) for h, p in zip(header, parts)}
        thr_ms = v["Throttle"]
        rows.append({
            "rpm": v["Speed"] / 2,  # 4-pole mode halves RPM
            "thr": max(0.0, min(100.0, 100 * (thr_ms - 1.5) / 0.41)),
            "volt": v["Voltage"], "amp": v["Current"], "rip": v["Ripple"],
            "pwr": v["Power-Out"], "mt": v["Motor Temp."], "et": v["Temperature"],
            "tim": v["Motor Timing."], "acc": v["Acceleration."],
            "_thr_ms": thr_ms,
        })
    # Launch: Throttle > 1.65 ms AND PowerOut > 10
    launch = next(i for i, r in enumerate(rows) if r["_thr_ms"] > 1.65 and r["pwr"] > 10)
    for i, r in enumerate(rows):
        r["t"] = (i - launch) * 0.05
    return [r for r in rows if T_MIN <= r["t"] <= T_MAX]


def load_tune(name):
    kv = {}
    with open(os.path.join(TUNES, name), encoding="latin1") as f:
        for line in f:
            line = line.strip()
            if line.startswith("#") or ":" not in line:
                continue
            k, v = line.split(":", 1)
            kv[k.strip()] = v.strip()
    return kv


def curve(raw):
    parts = [p for p in raw.split(".") if p.strip()]
    pts = []
    for i, p in enumerate(parts):
        try:
            y = max(0.0, min(100.0, float(p) / 65535 * 100))
        except ValueError:
            continue
        pts.append((i / (len(parts) - 1) * 100, y))
    return pts


runs = [load_log(n) for n in RUN_LOGS]
tunes = [load_tune(n) for n in RUN_TUNES]


def value_at(run, key, t):
    best = min(run, key=lambda r: abs(r["t"] - t))
    return best[key]


# ---------------------------------------------------------------- shared css

CSS = """
* { box-sizing: border-box; margin: 0; padding: 0; }
body { width: 1920px; height: 1080px; background: #13171E; color: #E6E9EF;
       font: 13px 'Segoe UI', sans-serif; overflow: hidden; display: flex; flex-direction: column; }
.top { height: 44px; background: #1B212B; border-bottom: 1px solid #292F39; display: flex; align-items: center; padding: 0 14px; gap: 10px; }
.brand { font-weight: 600; font-size: 15px; margin-right: 18px; }
.tab { padding: 0 18px; height: 44px; line-height: 44px; color: #9AA3B2; font-size: 14px; border-bottom: 3px solid transparent; }
.tab.on { color: #E6E9EF; border-bottom-color: #4A90E2; font-weight: 600; }
.sp { flex: 1; }
.btn { background: #232A35; border: 1px solid #333B47; color: #E6E9EF; padding: 6px 14px; border-radius: 4px; font-size: 13px; }
.btn.pri { background: #2D6CB5; border-color: #2D6CB5; }
.pill { display: flex; background: #232A35; border-radius: 14px; overflow: hidden; border: 1px solid #333B47; }
.pill span { padding: 5px 14px; color: #9AA3B2; } .pill span.on { background: #2D6CB5; color: #fff; }
.strip { height: 40px; background: #161B23; border-bottom: 1px solid #292F39; display: flex; align-items: center; padding: 0 14px; gap: 8px; }
.chip { display: flex; align-items: center; gap: 8px; background: #1A1F27; border: 1px solid #292F39; border-radius: 4px; padding: 4px 10px; height: 30px; }
.chip .x { color: #5C6573; } .chip .eye { color: #9AC7F2; }
.chip.add { color: #9AA3B2; border-style: dashed; }
.lbl { color: #9AA3B2; font-size: 12px; }
.note { position: absolute; z-index: 10; background: #F5C542; color: #1b1b1b; font: 600 13px 'Segoe UI'; padding: 6px 10px; border-radius: 4px; box-shadow: 0 2px 8px rgba(0,0,0,.5); max-width: 300px; }
"""


def linesample(i, color="#E6E9EF", w=28):
    return (f'<svg width="{w}" height="10"><line x1="0" y1="5" x2="{w}" y2="5" stroke="{color}" '
            f'stroke-width="2" stroke-dasharray="{DASH[i]}"/></svg>')


def top_bar(active):
    tabs = "".join(f'<div class="tab{" on" if t == active else ""}">{t}</div>' for t in ["Logs", "Tune"])
    return f"""<div class="top"><div class="brand">DragOverlay</div>{tabs}<div class="sp"></div>
<div class="pill"><span class="on">Drag</span><span>Speed</span></div>
<div class="btn">⤓ Auto load</div><div class="btn">Open project</div><div class="btn pri">Save project</div><div class="btn">⚙</div></div>"""


def run_strip():
    chips = ""
    for i, n in enumerate(RUN_LOGS):
        chips += (f'<div class="chip">{linesample(i)}<b>Run {i + 1}</b><span>{n}</span>'
                  f'<span class="lbl">tune: {RUN_TUNES[i]}</span><span class="eye">●</span><span class="x">×</span></div>')
    chips += '<div class="chip add">+ Add log (Castle, RaceBox, Mini Logger)</div>'
    return f'<div class="strip">{chips}</div>'


def polyline(points, color, dash, width=1.8, opacity=1.0):
    pts = " ".join(f"{x:.1f},{y:.1f}" for x, y in points)
    return (f'<polyline points="{pts}" fill="none" stroke="{color}" stroke-width="{width}" '
            f'stroke-dasharray="{dash}" stroke-linejoin="round" opacity="{opacity}"/>')


# ---------------------------------------------------------------- logs page

def logs_page():
    side_w = 380
    # Sidebar rows
    hdr = "".join(f'<div class="vc">{linesample(i, w=22)} R{i + 1}</div>' for i in range(3))
    rows = ""
    for name, color, key, fmt in CHANNELS:
        on = name in VISIBLE
        vals = "".join(f'<div class="vc">{fmt.format(value_at(r, key, CURSOR_T))}</div>' for r in runs)
        rows += (f'<div class="row{"" if on else " off"}"><span class="cb">{"✔" if on else ""}</span>'
                 f'<span class="sw" style="background:{color}"></span><span class="nm">{name}</span>{vals}</div>')
    gps_rows = "".join(f'<div class="row off dis"><span class="cb"></span><span class="sw" style="background:{c}"></span>'
                       f'<span class="nm">{n.replace("RaceBox ", "")}</span><div class="vc">-</div><div class="vc">-</div><div class="vc">-</div></div>'
                       for n, c in GPS)

    sidebar = f"""
<div class="side">
  <div class="cur"><span>Cursor</span><b>{CURSOR_T:.2f} s</b><span class="lbl">click the chart to move</span></div>
  <div class="hdr"><span class="nm"></span>{hdr}</div>
  <div class="grp">▾ ESC (Castle)<span class="lbl">10 channels</span></div>{rows}
  <div class="grp">▾ GPS (RaceBox)<span class="lbl">no log loaded</span></div>{gps_rows}
  <div class="grp">▸ Mini Logger<span class="lbl">no log loaded</span></div>
</div>"""

    # Chart geometry
    cx, cy = side_w + 60, 84 + 20
    cw = 1920 - cx - 30
    tune_h = 230
    ch = 1080 - cy - 40 - tune_h - 20

    def X(t):
        return cx + (t - T_MIN) / (T_MAX - T_MIN) * cw

    svg = [f'<rect x="{cx}" y="{cy}" width="{cw}" height="{ch}" fill="#0E1218" stroke="#292F39"/>']
    t = T_MIN
    while t <= T_MAX + 1e-9:
        x = X(t)
        svg.append(f'<line x1="{x}" y1="{cy}" x2="{x}" y2="{cy + ch}" stroke="#1E2530"/>')
        svg.append(f'<text x="{x}" y="{cy + ch + 18}" fill="#9AA3B2" font-size="12" text-anchor="middle">{t:.1f}</text>')
        t += 0.5
    svg.append(f'<text x="{cx + cw / 2}" y="{cy + ch + 36}" fill="#9AA3B2" font-size="12" text-anchor="middle">Time from launch (s)</text>')
    svg.append(f'<line x1="{X(0)}" y1="{cy}" x2="{X(0)}" y2="{cy + ch}" stroke="#5C6573" stroke-dasharray="4 4"/>')

    for name, color, key, fmt in CHANNELS:
        if name not in VISIBLE:
            continue
        lo = min(r[key] for run in runs for r in run)
        hi = max(r[key] for run in runs for r in run)
        if name == "Voltage":
            lo = 0
        span = (hi - lo) or 1
        for i, run in enumerate(runs):
            pts = [(X(r["t"]), cy + ch - 10 - (r[key] - lo) / span * (ch - 30)) for r in run]
            svg.append(polyline(pts, color, DASH[i]))
        if name == "RPM":  # the focused channel owns the visible axis
            for k in range(6):
                val = lo + span * k / 5
                y = cy + ch - 10 - k / 5 * (ch - 30)
                svg.append(f'<text x="{cx - 8}" y="{y + 4}" fill="{color}" font-size="12" text-anchor="end">{val:,.0f}</text>')
                svg.append(f'<line x1="{cx}" y1="{y}" x2="{cx + cw}" y2="{y}" stroke="#1E2530"/>')
    xc = X(CURSOR_T)
    svg.append(f'<line x1="{xc}" y1="{cy}" x2="{xc}" y2="{cy + ch}" stroke="#F5C542" stroke-width="1.5"/>')

    # Fold-away tune curves panel
    ty = cy + ch + 50
    svg.append(f'<rect x="{side_w + 10}" y="{ty}" width="{1920 - side_w - 20}" height="{tune_h}" fill="#161B23" stroke="#292F39" rx="4"/>')
    svg.append(f'<text x="{side_w + 24}" y="{ty + 24}" fill="#E6E9EF" font-size="14" font-weight="600">▾ Tune curves</text>')
    svg.append(f'<text x="{side_w + 140}" y="{ty + 24}" fill="#9AA3B2" font-size="12">fold away to give the chart the full height · full detail on the Tune tab</text>')
    pw = (1920 - side_w - 80) / 2
    for j, (title, key, col) in enumerate([("Throttle curve", "Forward Curve 65535", "#F0F0F0"),
                                            ("Boost by throttle", "Boost By Throttle Table 65535", "#FFA033")]):
        px = side_w + 30 + j * (pw + 20)
        py, ph = ty + 40, tune_h - 64
        svg.append(f'<rect x="{px}" y="{py}" width="{pw}" height="{ph}" fill="#0E1218" stroke="#292F39"/>')
        svg.append(f'<text x="{px + 8}" y="{py + 16}" fill="#9AA3B2" font-size="12">{title}</text>')
        svg.append(f'<text x="{px + pw / 2}" y="{py + ph + 16}" fill="#5C6573" font-size="11" text-anchor="middle">throttle %</text>')
        for i, tn in enumerate(tunes):
            pts = [(px + x / 100 * pw, py + ph - 6 - y / 100 * (ph - 26)) for x, y in curve(tn[key])]
            svg.append(polyline(pts, col, DASH[i], 1.6))
        if len({tn[key] for tn in tunes}) == 1:
            svg.append(f'<text x="{px + pw - 10}" y="{py + 16}" fill="#9AA3B2" font-size="12" text-anchor="end">same in all 3 tunes</text>')
        thr_now = value_at(runs[0], "thr", CURSOR_T)
        mx = px + thr_now / 100 * pw
        svg.append(f'<line x1="{mx}" y1="{py}" x2="{mx}" y2="{py + ph}" stroke="#F5C542" stroke-width="1.5"/>')

    css = CSS + f"""
.main {{ flex: 1; position: relative; }}
.side {{ position: absolute; left: 0; top: 0; bottom: 0; width: {side_w}px; background: #161B23; border-right: 1px solid #292F39; overflow: hidden; }}
.cur {{ display: flex; align-items: baseline; gap: 10px; padding: 12px 14px; border-bottom: 1px solid #292F39; font-size: 14px; }}
.cur b {{ color: #F5C542; font-size: 18px; }}
.hdr, .row {{ display: flex; align-items: center; height: 30px; padding: 0 10px; gap: 8px; }}
.hdr {{ color: #9AA3B2; font-size: 12px; height: 28px; }}
.grp {{ display: flex; gap: 10px; align-items: center; height: 32px; padding: 0 12px; background: #1B212B; font-weight: 600; border-top: 1px solid #292F39; border-bottom: 1px solid #292F39; }}
.row {{ border-bottom: 1px solid #1B212B; }}
.row.off .nm, .row.off .vc {{ color: #5C6573; }}
.row.dis {{ opacity: .55; }}
.cb {{ width: 16px; height: 16px; border: 1px solid #5C6573; border-radius: 3px; font-size: 11px; line-height: 14px; text-align: center; color: #9AC7F2; }}
.sw {{ width: 12px; height: 12px; border-radius: 2px; }}
.nm {{ flex: 1; }}
.vc {{ width: 70px; text-align: right; font-variant-numeric: tabular-nums; display: flex; justify-content: flex-end; align-items: center; gap: 4px; }}
"""
    notes = f"""
<div class="note" style="left:40px; top:600px">Channels move to a left list. Tick = on chart. Each run's value at the cursor sits on the row.</div>
<div class="note" style="left:40px; top:690px">Groups fold away. GPS and Mini Logger channels appear here when their log is loaded.</div>
<div class="note" style="left:1540px; top:560px">Chart gets the full width. No Tune panel on the right.</div>
<div class="note" style="left:1100px; top:{ty - 84 + 4}px; max-width:520px">Tune curves, all runs overlaid. Yellow line = throttle at the cursor.</div>
"""
    body = (top_bar("Logs") + run_strip() +
            f'<div class="main">{sidebar}{notes}</div>'
            f'<svg width="1920" height="1080" style="position:absolute;left:0;top:0;pointer-events:none">'
            + "".join(svg) + "</svg>")
    return f"<!doctype html><html><head><meta charset='utf-8'><style>{css}</style></head><body>{body}</body></html>"


# ---------------------------------------------------------------- tune page

TUNE_ROWS = [
    ("Boost", ["Boost Mode Enable", "Boost Timing Mode", "Boost Timing Amount",
               "Boost RPM Activation Range - Start RPM", "Boost RPM Activation Range - End RPM",
               "Boost Throttle Activation Range - Start RPM", "Boost Throttle Activation Range - End RPM"]),
    ("Turbo", ["Turbo Enable", "Turbo Timing Amount", "Activation Throttle Percent",
               "Acceleration Degrees Per Second", "Deceleration Degrees Per Second"]),
    ("Power and throttle", ["Max Power", "Punch Control", "Start Power", "Throttle Dead Band", "Current Limiting"]),
    ("Brake", ["Drag Brake", "Drag Brake Ramp", "Brake Amount"]),
    ("Device", ["Device Name", "Current Firmware", "Sample Frequency", "Motor Direction"]),
]
NICE = {
    "Boost RPM Activation Range - Start RPM": "Boost start RPM",
    "Boost RPM Activation Range - End RPM": "Boost end RPM",
    "Boost Throttle Activation Range - Start RPM": "Boost start throttle",
    "Boost Throttle Activation Range - End RPM": "Boost end throttle",
    "Activation Throttle Percent": "Turbo activation throttle",
    "Acceleration Degrees Per Second": "Turbo ramp up (deg/s)",
    "Deceleration Degrees Per Second": "Turbo ramp down (deg/s)",
}


def tune_page():
    ndiff = sum(1 for _, keys in TUNE_ROWS for k in keys if len({t.get(k, "-") for t in tunes}) > 1)
    head = "".join(f'<th>{linesample(i)} Run {i + 1}<div class="lbl">{RUN_TUNES[i]}</div></th>' for i in range(3))
    body = ""
    for group, keys in TUNE_ROWS:
        body += f'<tr class="g"><td colspan="4">{group}</td></tr>'
        for k in keys:
            vals = [tunes[i].get(k, "-") for i in range(3)]
            diff = len(set(vals)) > 1
            cells = ""
            for i, v in enumerate(vals):
                changed = diff and v != vals[0]
                cells += f'<td class="{"chg" if changed else ""}">{html.escape(v)}</td>'
            body += f'<tr class="{"diff" if diff else ""}"><td class="k">{"● " if diff else ""}{html.escape(NICE.get(k, k))}</td>{cells}</tr>'

    # Curve charts on the right
    svg = []
    px, pw = 1240, 650
    for j, (title, key, col) in enumerate([("Throttle curve", "Forward Curve 65535", "#F0F0F0"),
                                            ("Boost by throttle", "Boost By Throttle Table 65535", "#FFA033")]):
        py, ph = 150 + j * 430, 350
        svg.append(f'<rect x="{px}" y="{py}" width="{pw}" height="{ph}" fill="#0E1218" stroke="#292F39"/>')
        svg.append(f'<text x="{px}" y="{py - 10}" fill="#E6E9EF" font-size="15" font-weight="600">{title}</text>')
        for k in range(0, 101, 25):
            y = py + ph - 10 - k / 100 * (ph - 30)
            x = px + k / 100 * pw
            svg.append(f'<line x1="{px}" y1="{y}" x2="{px + pw}" y2="{y}" stroke="#1E2530"/>')
            svg.append(f'<text x="{px - 6}" y="{y + 4}" fill="#9AA3B2" font-size="11" text-anchor="end">{k}</text>')
            svg.append(f'<text x="{x}" y="{py + ph + 16}" fill="#9AA3B2" font-size="11" text-anchor="middle">{k}</text>')
        for i, tn in enumerate(tunes):
            pts = [(px + x / 100 * pw, py + ph - 10 - y / 100 * (ph - 30)) for x, y in curve(tn[key])]
            svg.append(polyline(pts, col, DASH[i], 2))
        if len({tn[key] for tn in tunes}) == 1:
            svg.append(f'<text x="{px + pw - 10}" y="{py + ph - 24}" fill="#9AA3B2" font-size="12" text-anchor="end">same in all 3 tunes</text>')
        svg.append(f'<text x="{px + pw / 2}" y="{py + ph + 32}" fill="#5C6573" font-size="11" text-anchor="middle">throttle %</text>')

    css = CSS + """
.main { flex: 1; position: relative; }
.left { position: absolute; left: 20px; top: 16px; width: 1150px; bottom: 16px; }
.bar { display: flex; align-items: center; gap: 14px; margin-bottom: 12px; }
.badge { background: #4A3A12; color: #F5C542; border: 1px solid #7A5F1C; padding: 4px 10px; border-radius: 12px; font-weight: 600; }
.tog { display: flex; align-items: center; gap: 8px; color: #9AA3B2; }
.sw2 { width: 34px; height: 18px; border-radius: 9px; background: #333B47; position: relative; }
.sw2:after { content: ''; position: absolute; left: 2px; top: 2px; width: 14px; height: 14px; border-radius: 7px; background: #9AA3B2; }
table { width: 100%; border-collapse: collapse; background: #161B23; border: 1px solid #292F39; }
th { text-align: left; padding: 8px 12px; background: #1B212B; font-weight: 600; border-bottom: 1px solid #292F39; }
th:first-child { width: 300px; }
td { padding: 0 12px; height: 27px; border-bottom: 1px solid #1B212B; font-variant-numeric: tabular-nums; }
td.k { color: #9AA3B2; }
tr.g td { background: #1B212B; font-weight: 600; color: #E6E9EF; height: 30px; }
tr.diff td { background: #221D10; }
tr.diff td.k { color: #F5C542; }
td.chg { color: #F5C542; font-weight: 600; }
"""
    notes = """
<div class="note" style="left:1380px; top:16px">Curves from every run's tune on one chart, same line styles as the logs.</div>
<div class="note" style="left:490px; top:8px; max-width:420px">Rows that differ are highlighted. Values that changed from Run 1 are yellow.</div>
"""
    page = f"""
<div class="main">
 <div class="left">
  <div class="bar"><span style="font-size:16px;font-weight:600">Tune comparison</span>
   <span class="badge">{ndiff} settings differ</span>
   <span class="tog"><span class="sw2"></span>Show only differences</span>
   <div class="sp"></div><div class="btn">Attach tune to a run…</div></div>
  <table><tr><th>Setting</th>{head}</tr>{body}</table>
 </div>{notes}
</div>
<svg width="1920" height="1080" style="position:absolute;left:0;top:84px;pointer-events:none">{"".join(svg)}</svg>"""
    return (f"<!doctype html><html><head><meta charset='utf-8'><style>{css}</style></head><body>"
            f"{top_bar('Tune')}{run_strip()}{page}</body></html>")


EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"

for name, builder in [("logs-page", logs_page), ("tune-page", tune_page)]:
    html_path = os.path.join(OUT, name + ".html")
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(builder())
    png_path = os.path.join(OUT, name + ".png")
    subprocess.run([EDGE, "--headless", "--disable-gpu", "--hide-scrollbars", "--window-size=1920,1080",
                    f"--screenshot={png_path}", "file:///" + html_path.replace("\\", "/")],
                   check=True, capture_output=True, timeout=60)
    print("wrote", png_path)
