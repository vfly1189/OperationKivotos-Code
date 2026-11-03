# -*- coding: utf-8 -*-
"""쿼터뷰 조준 보정 이론 정리 문서(AimFix_Theory.html) 생성기.

프로젝트 수치가 아니라 일반론이다. 그림 속 숫자는 전부 '예시값'으로 계산한 것.
  - 커서 레이가 바닥(y=0)만 맞으면, 높이 h의 커서 점은 d = h·D/(H−h) 만큼 카메라 반대쪽으로 밀린다
  - 조준 평면을 y=a로 올리면 d(a) = (h−a)·D/(H−h)
  - 플레이어 P, 몹 M(거리 R), 밀림 방향과 P→M 사이 각 α → ε = atan(d·sinα / (R + d·cosα)), e = R·sin ε
"""
import math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from aim_miss_gen import CSS  # 같은 폴더 문서와 스타일을 맞춘다

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "AimFix_Theory.html")

BLUE, AMBER, GREEN, RED = "#3b5bdb", "#e8590c", "#2b8a3e", "#c92a2a"
INK, MUTED, FAINT, LINE = "#1a1c22", "#5b6270", "#8a91a0", "#e3e6ec"
BLUE_S, AMBER_S, GREEN_S, SHADE = "#eef2ff", "#fff4ec", "#ebfbee", "#f7f8fa"

EXTRA_CSS = """
.f{font-size:12.5px;font-weight:800;fill:#1a1c22}
.fa{font-size:14px;font-weight:800}
.cov td{text-align:center}.cov td.k{text-align:left}
.cov .y{color:#2b8a3e;font-weight:800}.cov .p{color:#e8590c;font-weight:800}.cov .n{color:#c92a2a;font-weight:800}.cov .z{color:#8a91a0}
ol.steps{margin:4px 0 0;padding-left:18px}ol.steps li{margin:3px 0}
"""

DEFS = (f'<svg width="0" height="0" style="position:absolute"><defs>'
        + "".join(f'<marker id="{i}" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto">'
                  f'<path d="M0,0 L8,4 L0,8 z" fill="{c}"/></marker>'
                  for i, c in (("ah", FAINT), ("ahb", BLUE), ("aha", AMBER), ("ahg", GREEN), ("ahr", RED)))
        + '<clipPath id="capclip"><rect x="140" y="40" width="60" height="150" rx="30"/></clipPath>'
        + '</defs></svg>')


def T(x, y, s, cls="t", anchor="start", fill=None, extra=""):
    f = f' fill="{fill}"' if fill else ""
    return f'<text x="{x:.1f}" y="{y:.1f}" class="{cls}" text-anchor="{anchor}"{f} {extra}>{s}</text>'


def L(x1, y1, x2, y2, col=FAINT, w=1.4, dash=None, mk=None):
    d = f' stroke-dasharray="{dash}"' if dash else ""
    m = f' marker-end="url(#{mk})"' if mk else ""
    return f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{col}" stroke-width="{w}"{d}{m}/>'


def box(x, y, w, h, fill=SHADE, stroke="none", rx=12):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}" stroke="{stroke}"/>'


def lines(x, y, rows, step=18):
    """rows = [(text, cls, fill)] → 세로로 쌓은 text"""
    out = []
    for i, r in enumerate(rows):
        s, cls, fill = (r + (None, None))[:3] if isinstance(r, tuple) else (r, "s", None)
        out.append(T(x, y + i * step, s, cls or "s", fill=fill or INK))
    return "".join(out)


def poly(pts, col, w=2.0, dash=None):
    d = f' stroke-dasharray="{dash}"' if dash else ""
    p = " ".join(f"{x:.1f},{y:.1f}" for x, y in pts)
    return f'<polyline points="{p}" fill="none" stroke="{col}" stroke-width="{w}" stroke-linejoin="round"{d}/>'


# ---------------------------------------------------------------- 계산 모델
def miss(M, h, H, C, a=0.0):
    """플레이어=원점, 몹 M(x,z), 커서 높이 h, 카메라 높이 H·수평위치 C(x,z), 조준 평면 a → e(m), ε(deg)"""
    if h >= H:
        return float("inf"), 90.0
    f = (h - a) / (H - h)
    G = (M[0] + f * (M[0] - C[0]), M[1] + f * (M[1] - C[1]))
    L_ = math.hypot(*G)
    u = (G[0] / L_, G[1] / L_)
    e = abs(M[0] * u[1] - M[1] * u[0])
    eps = math.degrees(math.acos(max(-1, min(1, (M[0] * u[0] + M[1] * u[1]) / math.hypot(*M)))))
    return e, eps


# ---------------------------------------------------------------- 그림 1: 옆에서 본 바닥 투영 오차
def fig_side():
    k, gy, cx, H, D, hm, hc = 30, 215, 40, 5.0, 7.0, 1.8, 1.5
    camy = gy - H * k
    mx = cx + D * k
    d = hc * D / (H - hc)
    gx = mx + d * k
    cy = gy - hc * k
    s = ['<svg viewBox="0 0 700 262" width="100%">']
    s.append(f'<polygon points="{cx},{camy} {cx},{gy} {gx},{gy}" fill="{BLUE_S}"/>')
    s.append(L(18, gy, 395, gy, INK, 1.6))
    s.append(T(395, gy + 14, "바닥 (y = 0)", "xs", "end"))
    s.append(f'<rect x="{mx-13}" y="{gy-hm*k}" width="26" height="{hm*k}" rx="9" fill="#dfe4ee" stroke="{FAINT}"/>')
    s.append(f'<polygon points="{mx},{cy} {mx},{gy} {gx},{gy}" fill="{AMBER_S}" fill-opacity=".85" stroke="{AMBER}" stroke-width="1"/>')
    s.append(L(cx, camy, mx, cy, AMBER, 2.0))
    s.append(L(mx, cy, gx, gy, AMBER, 2.0, "5 4"))
    s.append(f'<circle cx="{cx}" cy="{camy}" r="8" fill="{BLUE}"/>')
    s.append(T(cx + 14, camy - 6, "카메라", "t", fill=BLUE))
    s.append(f'<circle cx="{mx}" cy="{cy}" r="4" fill="{AMBER}"/>')
    s.append(T(mx + 18, cy - 20, "커서가 가리킨 점", "t", fill=AMBER))
    s.append(T(mx + 18, cy - 8, "(몹 위, 높이 h)", "xs"))
    s.append(f'<circle cx="{gx}" cy="{gy}" r="5" fill="{AMBER}"/>')
    s.append(T(gx + 8, gy - 8, "조준점 G", "t", fill=AMBER))
    # 치수선
    s.append(L(22, camy, 22, gy, MUTED, 1.0))
    s.append(T(12, (camy + gy) / 2 + 4, "H", "tb", "middle"))
    s.append(L(mx - 24, cy, mx - 24, gy, MUTED, 1.0))
    s.append(T(mx - 33, (cy + gy) / 2 + 4, "h", "tb", "middle"))
    s.append(L(cx, 234, mx, 234, MUTED, 1.0))
    s.append(L(cx, 229, cx, 239, MUTED, 1.0) + L(mx, 229, mx, 239, MUTED, 1.0) + L(gx, 229, gx, 239, MUTED, 1.0))
    s.append(T((cx + mx) / 2, 252, "D  (카메라 → 몹 수평거리)", "t", "middle"))
    s.append(L(mx, 234, gx, 234, AMBER, 1.4))
    s.append(T((mx + gx) / 2, 252, "d  (밀림)", "t", "middle", AMBER))
    # 수식 상자
    s.append(box(412, 14, 278, 232))
    s.append(lines(428, 40, [
        ("닮은 삼각형 두 개", "tb", None),
        ("큰 삼각형 (파랑) : 높이 H, 밑변 D + d", "t", None),
        ("작은 삼각형 (주황) : 높이 h, 밑변 d", "t", None),
    ], 20))
    s.append(T(428, 112, "h / d  =  H / (D + d)", "f"))
    s.append(T(428, 142, "d  =  h · D / (H − h)", "fa", fill=AMBER))
    s.append(lines(428, 172, [
        "• h ↑ (몹이 클수록, 머리를 찍을수록) → d ↑",
        "• H ↓ (카메라가 낮을수록) → d ↑",
        "• h → H 이면 분모 → 0, d → ∞",
        "   레이가 수평이 되어 바닥에 닿지 않는다",
    ], 17))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 그림 2: 위에서 본 방향 오차
def fig_top():
    k = 50
    P, M = (110, 190), (260, 190)
    d = 1.5
    G = (M[0], M[1] - d * k)
    ux, uy = G[0] - P[0], G[1] - P[1]
    n = math.hypot(ux, uy); ux, uy = ux / n, uy / n
    t = (M[0] - P[0]) * ux + (M[1] - P[1]) * uy
    F = (P[0] + t * ux, P[1] + t * uy)
    end = (P[0] + 270 * ux, P[1] + 270 * uy)
    eps = math.degrees(math.atan2(d, 3.0))
    a_end = (P[0] + 46 * math.cos(math.radians(eps)), P[1] - 46 * math.sin(math.radians(eps)))
    s = ['<svg viewBox="0 0 700 262" width="100%">']
    s.append(L(P[0], P[1], M[0], M[1], FAINT, 1.2, "3 3"))
    s.append(T((P[0] + M[0]) / 2, P[1] + 16, "R", "tb", "middle"))
    s.append(f'<circle cx="{M[0]}" cy="{M[1]}" r="16" fill="none" stroke="{GREEN}" stroke-dasharray="3 3"/>')
    s.append(f'<circle cx="{M[0]}" cy="{M[1]}" r="12" fill="#dfe4ee" stroke="{FAINT}"/>')
    s.append(L(M[0], M[1] - 13, G[0], G[1] + 6, AMBER, 1.8, mk="aha"))
    s.append(T(M[0] + 10, (M[1] + G[1]) / 2, "d", "tb", fill=AMBER))
    s.append(T(M[0] + 22, (M[1] + G[1]) / 2, "카메라 반대쪽으로 밀림", "xs"))
    s.append(f'<circle cx="{G[0]}" cy="{G[1]}" r="4.5" fill="{AMBER}"/>')
    s.append(T(G[0] + 8, G[1] - 6, "G", "tb", fill=AMBER))
    s.append(L(P[0], P[1], end[0], end[1], BLUE, 2.2, mk="ahb"))
    s.append(T(end[0] + 4, end[1] - 4, "탄 경로", "t", fill=BLUE))
    s.append(L(M[0], M[1], F[0], F[1], RED, 1.6, "4 3"))
    s.append(T((M[0] + F[0]) / 2 - 12, (M[1] + F[1]) / 2 + 4, "e", "tb", "middle", RED))
    s.append(f'<path d="M{P[0]+46} {P[1]} A46 46 0 0 0 {a_end[0]:.1f} {a_end[1]:.1f}" fill="none" stroke="{INK}" stroke-width="1.2"/>')
    s.append(T(P[0] + 52, P[1] - 7, "ε", "tb"))
    s.append(f'<circle cx="{P[0]}" cy="{P[1]}" r="9" fill="{BLUE}"/>')
    s.append(T(P[0], P[1] + 26, "플레이어 P", "t", "middle", BLUE))
    s.append(T(M[0] + 22, M[1] + 4, "몹 M", "t"))
    s.append(T(M[0] + 22, M[1] + 18, "α = 90° (옆 몹)", "xs"))
    s.append(L(30, 250, 30, 196, FAINT, 1.4, mk="ah"))
    s.append(T(38, 246, "카메라 시선 방향", "xs"))
    s.append(box(412, 10, 278, 244))
    s.append(lines(428, 36, [("옆에 있는 몹일수록 방향이 틀어진다", "tb", None)], 20))
    s.append(T(428, 64, "ε = atan( d·sin α / (R + d·cos α) )", "f"))
    s.append(T(428, 88, "e = R · sin ε", "f"))
    s.append(T(428, 114, "명중  ⇔  e < r몹 + r탄", "fa", fill=GREEN))
    s.append(lines(428, 140, [
        "α = P→M 과 밀림 방향(카메라 시선) 사이 각",
        "• α = 0° (화면 위쪽 몹): ε = 0",
        "   거리만 밀리고 방향은 그대로 → 맞음",
        "• α = 90° (옆 몹): ε = atan(d / R) 로 최대",
        "• α = 180° (카메라 쪽 몹) 이고 d > R 이면",
        "   G가 플레이어 뒤로 넘어가 정반대로 쏜다",
    ], 17))
    s.append('</svg>')
    return "\n".join(s), eps


# ---------------------------------------------------------------- 그림 3: 무차원 곡선 d/D = k/(1−k)
def fig_universal():
    x0, x1, y0, y1 = 60, 420, 22, 222
    kmax, ymax = 1.0, 6.0
    X = lambda k: x0 + k / kmax * (x1 - x0)
    Y = lambda v: y1 - min(v, ymax) / ymax * (y1 - y0)
    s = ['<svg viewBox="0 0 700 256" width="100%">']
    for v in range(0, 7):
        s.append(L(x0, Y(v), x1, Y(v), LINE, 1))
        s.append(T(x0 - 8, Y(v) + 3, f"{v}", "xs", "end"))
    for kk in (0, 0.2, 0.4, 0.6, 0.8, 1.0):
        s.append(T(X(kk), y1 + 14, f"{kk:.1f}", "xs", "middle"))
    s.append(L(x0, y1, x1, y1, INK, 1.2) + L(x0, y0, x0, y1, INK, 1.2))
    s.append(T((x0 + x1) / 2, y1 + 30, "k = h / H   (커서 높이 ÷ 카메라 높이)", "t", "middle"))
    s.append(T(16, y0 - 6, "d / D", "t"))
    pts = [(X(kk), Y(kk / (1 - kk))) for kk in [i / 400 for i in range(0, 400)] if kk / (1 - kk) <= ymax]
    s.append(poly(pts, AMBER, 2.6))
    s.append(L(X(1), y0, X(1), y1, RED, 1.2, "4 3"))
    s.append(T(X(1) - 6, y0 + 10, "k → 1 : 발산", "xs", "end", RED))
    marks = [(0.1, "작은 몹 몸통"), (0.2, "작은 몹 머리"), (0.5, "큰 몹 몸통"), (0.8, "큰 보스 머리 · 낮은 카메라")]
    for kk, lab in marks:
        v = kk / (1 - kk)
        s.append(f'<circle cx="{X(kk):.1f}" cy="{Y(v):.1f}" r="4" fill="{INK}"/>')
        if kk == 0.1:
            s.append(L(X(kk), Y(v) - 4, X(kk) + 4, Y(v) - 20, FAINT, 1))
            s.append(T(X(kk) + 6, Y(v) - 24, f"{lab} ({v:.2f})", "s", fill=INK))
        elif kk == 0.2:
            s.append(L(X(kk) + 2, Y(v) - 4, X(kk) + 26, Y(v) - 38, FAINT, 1))
            s.append(T(X(kk) + 28, Y(v) - 42, f"{lab} ({v:.2f})", "s", fill=INK))
        elif kk == 0.5:
            s.append(T(X(kk) + 10, Y(v) + 14, f"{lab} ({v:.2f})", "s", fill=INK))
        elif kk >= 0.8:
            s.append(T(X(kk) - 8, Y(v) - 6, f"{lab} ({v:.2f})", "s", "end", INK))
        else:
            s.append(T(X(kk) + 8, Y(v) - 6, f"{lab} ({v:.2f})", "s", fill=INK))
    s.append(box(440, 14, 250, 226))
    s.append(lines(456, 40, [("카메라·몹 크기와 상관없는 한 장", "tb", None)], 20))
    s.append(T(456, 70, "d / D  =  k / (1 − k)", "fa", fill=AMBER))
    s.append(lines(456, 98, [
        "앞 식을 H로 나누면 변수가 k 하나만 남는다.",
        "",
        "• k = 0.2 → 몹까지 거리의 25% 밀림",
        "• k = 0.5 → 100% (몹 거리만큼 더)",
        "• k = 0.8 → 400%",
        "",
        "선형이 아니라 쌍곡선이다.",
        "작을 때는 거의 평평하다가",
        "k가 1에 다가가면 수직으로 꺾인다.",
    ], 15))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 그림 4: 몹 크기 스윕 (예시값)
H0, R0, RB = 1.05, 0.265, 0.057
CAM_HI = (5.0, (0.0, -3.0))   # 높은 카메라 (약 59°)
CAM_LO = (4.0, (0.0, -5.0))   # 낮은 카메라 (약 39°)
MOB = (3.0, 0.0)              # 플레이어 오른쪽 3m


def sweep(H, C, frac, a=0.0):
    out = []
    for i in range(0, 451):
        sc = 0.5 + i * 0.01
        h = frac * sc * H0
        e, eps = miss(MOB, h, H, C, a)
        out.append((sc, e, eps))
    return out


def fig_scale():
    x0, x1, y0, y1 = 64, 470, 20, 222
    smin, smax, ymax = 0.5, 5.0, 3.0
    X = lambda v: x0 + (v - smin) / (smax - smin) * (x1 - x0)
    Y = lambda v: y1 - min(v, ymax) / ymax * (y1 - y0)
    s = ['<svg viewBox="0 0 700 258" width="100%">']
    for v in (0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0):
        s.append(L(x0, Y(v), x1, Y(v), LINE, 1))
        s.append(T(x0 - 8, Y(v) + 3, f"{v:.1f}", "xs", "end"))
    for v in (1, 2, 3, 4, 5):
        s.append(T(X(v), y1 + 14, f"×{v}", "xs", "middle"))
    s.append(L(x0, y1, x1, y1, INK, 1.2) + L(x0, y0, x0, y1, INK, 1.2))
    s.append(T((x0 + x1) / 2, y1 + 30, "몹 크기 배율 s  (키·반지름 모두 ×s, 거리 R = 3m 고정)", "t", "middle"))
    s.append(T(10, y0 - 6, "e (m)", "t"))
    curves = [
        ("머리 · 높은 카메라", CAM_HI, 0.95, 0.0, AMBER, None),
        ("머리 · 낮은 카메라", CAM_LO, 0.95, 0.0, RED, None),
        ("몸통 · 높은 카메라", CAM_HI, 0.50, 0.0, AMBER, "5 4"),
    ]
    data = {}
    for name, (H, C), frac, a, col, dash in curves:
        sw = sweep(H, C, frac, a)
        data[name] = sw
        pts = [(X(sc), Y(e)) for sc, e, _ in sw if e != float("inf")]
        s.append(poly(pts, col, 2.4, dash))
    tol = [(X(sc), Y(sc * R0 + RB)) for sc, _, _ in data["머리 · 높은 카메라"]]
    s.append(poly(tol, GREEN, 2.6))
    s_lo = CAM_LO[0] / (0.95 * H0)
    s.append(L(X(s_lo), y0, X(s_lo), y1, RED, 1.0, "3 3"))
    s.append(T(X(s_lo) - 5, y0 + 10, "낮은 카메라 발산", "xs", "end", RED))
    # 각도 주석 (s = 1, 4)
    for sc in (1.0, 4.0):
        i = int(round((sc - 0.5) / 0.01))
        e, eps = data["머리 · 높은 카메라"][i][1:]
        s.append(f'<circle cx="{X(sc):.1f}" cy="{Y(e):.1f}" r="3.6" fill="{AMBER}"/>')
        s.append(T(X(sc) + 6, Y(e) - 7, f"ε {eps:.0f}°", "xs", fill=AMBER))
    leg = [("허용치 r몹 + r탄  (s에 비례)", GREEN, None)] + [(n, c, dsh) for n, _, _, _, c, dsh in curves]
    for i, (n, c, dsh) in enumerate(leg):
        yy = 34 + i * 20
        s.append(L(486, yy, 512, yy, c, 2.6, dsh))
        s.append(T(518, yy + 4, n, "s"))
    s.append(box(482, 118, 208, 112, BLUE_S))
    s.append(lines(494, 138, [
        ("읽는 법", "tb", BLUE),
        ("주황·빨강이 초록 위 = 빗나감", "s", None),
        ("몸집도 같이 커져서 명중 여부는", "s", None),
        ("어느 정도 상쇄된다. 커지는 건", "s", None),
        ("빗나간 '거리'와 '각도', 그리고 발산.", "s", None),
    ], 17))
    s.append('</svg>')
    return "\n".join(s), data


# ---------------------------------------------------------------- 그림 5: 레이에 몹 포함 — 경계에서 튐
def fig_step():
    H, D = 5.0, 5.0
    s = ['<svg viewBox="0 0 700 236" width="100%">']
    for idx, (hm, title) in enumerate(((1.0, "작은 몹 (h = 1m)"), (3.0, "큰 몹 (h = 3m)"))):
        ox = idx * 350
        gmin, gmax = 3.0, 14.0
        X = lambda g: ox + 52 + (g - gmin) / (gmax - gmin) * 270
        Y = lambda v: 196 - (v - gmin) / (gmax - gmin) * 150
        gtop = D * H / (H - hm)
        s.append(f'<rect x="{X(D):.1f}" y="40" width="{X(gtop)-X(D):.1f}" height="156" fill="{BLUE_S}"/>')
        s.append(T((X(D) + X(gtop)) / 2, 54, "실루엣", "xs", "middle", BLUE))
        s.append(L(X(gmin), 196, X(gmax), 196, INK, 1.2) + L(X(gmin), 40, X(gmin), 196, INK, 1.2))
        s.append(T(ox + 52 + 135, 212, "커서 위치 (화면 아래 → 위)", "s", "middle"))
        s.append(T(ox + 44, 34, "조준점 깊이", "s", "start", INK))
        s.append(T(ox + 52 + 135, 230, title, "tb", "middle"))
        s.append(L(X(gmin), Y(gmin), X(gmax), Y(gmax), FAINT, 1.4, "4 3"))
        s.append(poly([(X(gmin), Y(gmin)), (X(D), Y(D)), (X(gtop), Y(D))], BLUE, 2.6))
        s.append(poly([(X(gtop), Y(gtop)), (X(gmax), Y(gmax))], BLUE, 2.6))
        s.append(L(X(gtop), Y(D), X(gtop), Y(gtop), AMBER, 2.0, "4 3"))
        jump = gtop - D
        s.append(T(X(gtop) + 6, (Y(D) + Y(gtop)) / 2 + 4, f"튐 {jump:.2f}m", "t", fill=AMBER))
    s.append(L(230, 10, 256, 10, BLUE, 2.6) + T(262, 14, "몹 포함 레이", "s", fill=INK))
    s.append(L(370, 10, 396, 10, FAINT, 1.4, "4 3") + T(402, 14, "바닥만 보는 레이", "s", fill=INK))
    s.append('</svg>')
    return "\n".join(s), D * H / (H - 1.0) - D, D * H / (H - 3.0) - D


# ---------------------------------------------------------------- 그림 6: 실루엣≠콜라이더 / 규모별 겨눌 곳
def fig_shape_scale():
    s = ['<svg viewBox="0 0 700 250" width="100%">']
    sil = ('<ellipse cx="170" cy="120" rx="40" ry="55"/><circle cx="170" cy="52" r="22"/>'
           '<polygon points="138,92 66,148 78,162 146,116"/><polygon points="202,92 266,58 274,72 206,118"/>'
           '<polygon points="266,58 300,14 308,20 276,68"/>'
           '<rect x="146" y="160" width="14" height="38" rx="4"/><rect x="180" y="160" width="14" height="38" rx="4"/>')
    s.append(f'<g fill="{AMBER_S}" stroke="{AMBER}" stroke-width="1.2">{sil}</g>')
    s.append(f'<g fill="#d7defc" stroke="{BLUE}" stroke-width="1.2" clip-path="url(#capclip)">{sil}</g>')
    s.append(f'<rect x="140" y="40" width="60" height="150" rx="30" fill="none" stroke="{BLUE}" stroke-width="1.6" stroke-dasharray="5 3"/>')
    s.append(T(214, 186, "콜라이더(캡슐)", "s", fill=BLUE))
    s.append(f'<rect x="22" y="214" width="12" height="12" rx="3" fill="#d7defc" stroke="{BLUE}"/>')
    s.append(T(40, 224, "콜라이더 안 → 레이가 몹을 맞춤", "s"))
    s.append(f'<rect x="22" y="232" width="12" height="12" rx="3" fill="{AMBER_S}" stroke="{AMBER}"/>')
    s.append(T(40, 242, "보이지만 콜라이더 밖 → 바닥으로 빠짐 (원래 문제 재발)", "s"))
    # 오른쪽: 규모별
    P = (392, 128)
    B, rB = (598, 128), 70
    cur = (570, 74)
    sm, rs = (478, 212), 10
    cs = (486, 206)
    def ang(a, b): return math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
    dB = abs(ang(P, cur) - ang(P, B))
    dS = abs(ang(P, cs) - ang(P, sm))
    s.append(f'<circle cx="{B[0]}" cy="{B[1]}" r="{rB}" fill="#dfe4ee" stroke="{FAINT}"/>')
    s.append(f'<circle cx="{sm[0]}" cy="{sm[1]}" r="{rs}" fill="#dfe4ee" stroke="{FAINT}"/>')
    s.append(L(P[0], P[1], cur[0], cur[1], GREEN, 2.0, mk="ahg"))
    s.append(L(P[0], P[1], B[0] - 4, B[1], AMBER, 2.0, "5 4", "aha"))
    s.append(L(P[0], P[1], cs[0], cs[1], GREEN, 1.6))
    s.append(L(P[0], P[1], sm[0], sm[1], AMBER, 1.6, "5 4"))
    s.append(f'<circle cx="{cur[0]}" cy="{cur[1]}" r="4" fill="{GREEN}"/><circle cx="{B[0]}" cy="{B[1]}" r="3.5" fill="{AMBER}"/>')
    s.append(f'<circle cx="{cs[0]}" cy="{cs[1]}" r="3" fill="{GREEN}"/>')
    s.append(T(cur[0], 50, "커서 (노린 부위)", "s", "middle", GREEN))
    s.append(T(B[0] + 8, B[1] + 18, "중심 흡착", "s", fill=AMBER))
    s.append(f'<circle cx="{P[0]}" cy="{P[1]}" r="8" fill="{BLUE}"/>')
    s.append(T(P[0], P[1] + 24, "P", "tb", "middle", BLUE))
    s.append(T(474, 110, f"Δθ ≈ {dB:.0f}°", "tb", fill=RED))
    s.append(T(sm[0] + 16, sm[1] + 4, f"작은 몹 Δθ ≈ {dS:.0f}°", "s", fill=INK))
    s.append(T(372, 22, "Δθ ≈ atan( 커서 오프셋 / 거리 )", "t"))
    s.append(T(372, 242, "큰 몹: 중심으로 끌면 노린 부위를 지운다", "s"))
    s.append('</svg>')
    return "\n".join(s), dB, dS


# ---------------------------------------------------------------- 그림 7: 의도가 모호한 경우
def fig_ambig():
    s = ['<svg viewBox="0 0 700 170" width="100%">']
    s.append(box(10, 8, 330, 154, "#fff", LINE))
    s.append(f'<rect x="178" y="30" width="44" height="62" rx="14" fill="#e7eaf1" stroke="{FAINT}"/>')
    s.append(f'<rect x="132" y="66" width="70" height="70" rx="20" fill="#dfe4ee" stroke="{INK}"/>')
    s.append(T(124, 108, "A", "tb", "end") + T(124, 122, "가까움", "s", "end", INK))
    s.append(T(230, 46, "B", "tb") + T(230, 60, "멀리", "s", fill=INK))
    s.append(f'<path d="M188 78 l10 10 M198 78 l-10 10" stroke="{RED}" stroke-width="2.2"/>')
    s.append(T(212, 80, "?", "tb", "middle", RED))
    s.append(T(24, 152, "레이가 먼저 맞는 건 A. 노린 건 B일 수도.", "s"))
    s.append(box(360, 8, 330, 154, "#fff", LINE))
    s.append(f'<ellipse cx="520" cy="62" rx="74" ry="26" fill="{GREEN_S}" stroke="{GREEN}" stroke-dasharray="5 3"/>')
    s.append(T(596, 30, "깔고 싶은 장판", "s", "middle", GREEN))
    s.append(f'<rect x="498" y="58" width="44" height="76" rx="16" fill="#dfe4ee" stroke="{INK}"/>')
    s.append(f'<path d="M514 66 l10 10 M524 66 l-10 10" stroke="{RED}" stroke-width="2.2"/>')
    s.append(T(374, 152, "몹 뒤 바닥을 찍었는데 몹 몸에 걸려 흡착.", "s"))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 방법 ①: 조준 평면 높이기
def fig_plane():
    k, gy, cx, H, D, hm, hc, a = 30, 200, 40, 5.0, 7.0, 1.8, 1.5, 1.0
    camy = gy - H * k
    mx = cx + D * k
    cy = gy - hc * k
    ay = gy - a * k
    gx = mx + hc * D / (H - hc) * k
    px = cx + D * (H - a) / (H - hc) * k
    s = ['<svg viewBox="0 0 700 232" width="100%">']
    s.append(L(18, gy, 395, gy, INK, 1.6))
    s.append(L(18, ay, 395, ay, BLUE, 1.4, "6 4"))
    s.append(T(392, ay - 6, "조준 평면 y = a", "xs", "end", BLUE))
    s.append(f'<rect x="{mx-13}" y="{gy-hm*k}" width="26" height="{hm*k}" rx="9" fill="#dfe4ee" stroke="{FAINT}"/>')
    s.append(L(cx, camy, mx, cy, AMBER, 2.0))
    s.append(L(mx, cy, gx, gy, FAINT, 1.6, "5 4"))
    s.append(f'<circle cx="{cx}" cy="{camy}" r="8" fill="{BLUE}"/>' + T(cx + 14, camy - 6, "카메라", "t", fill=BLUE))
    s.append(f'<circle cx="{mx}" cy="{cy}" r="4" fill="{AMBER}"/>')
    s.append(f'<circle cx="{px:.1f}" cy="{ay}" r="5" fill="{BLUE}"/>')
    s.append(f'<circle cx="{gx:.1f}" cy="{gy}" r="5" fill="{FAINT}"/>')
    s.append(L(mx, 216, px, 216, BLUE, 2.0) + T((mx + px) / 2, 230, f"{(px-mx)/k:.1f}m", "t", "middle", BLUE))
    s.append(L(px, 216, gx, 216, FAINT, 1.4, "3 3") + T((px + gx) / 2 + 8, 230, f"바닥이면 {(gx-mx)/k:.1f}m", "xs", "middle"))
    s.append(L(22, ay, 22, gy, MUTED, 1.0) + T(12, (ay + gy) / 2 + 4, "a", "tb", "middle"))
    s.append(box(412, 10, 278, 214))
    s.append(lines(428, 36, [("바닥 대신 '쏘는 높이'와 만나는 점", "tb", None)], 20))
    s.append(T(428, 66, "d(a) = (h − a) · D / (H − h)", "fa", fill=BLUE))
    s.append(lines(428, 94, [
        "• a = 0 이면 원래 식 그대로",
        "• 커서 높이 h = a 에서 오차 0",
        "• 몹이 없어도 동작하고 연속적 (튐 없음)",
        "• 콜라이더를 안 보니 실루엣 문제와 무관",
        ("한계", "tb", RED),
        "a는 하나뿐. 큰 몹 머리처럼 h가 a에서",
        "멀어지면 (h − a) 가 다시 커진다.",
    ], 17))
    s.append('</svg>')
    return "\n".join(s), (px - mx) / k, (gx - mx) / k


# ---------------------------------------------------------------- 방법 ②: 화면 좌표로 고르기
def fig_screen():
    c = (205, 105)
    mobs = [((118, 128), 14), ((262, 78), 40), ((312, 156), 12)]
    scores = [math.hypot(m[0] - c[0], m[1] - c[1]) - r for m, r in mobs]
    best = min(range(3), key=lambda i: scores[i])
    rho = 52
    s = ['<svg viewBox="0 0 700 210" width="100%">']
    s.append(box(10, 8, 380, 194, "#fff", LINE))
    s.append(T(22, 26, "화면", "xs"))
    for i, ((x, y), r) in enumerate(mobs):
        good = i == best
        s.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{GREEN_S if good else "#eef0f4"}" stroke="{GREEN if good else FAINT}" stroke-width="{2 if good else 1}"/>')
        s.append(f'<circle cx="{x}" cy="{y}" r="2.5" fill="{INK}"/>')
        s.append(L(c[0], c[1], x, y, GREEN if good else FAINT, 1.2, None if good else "3 3"))
        if good:
            s.append(T(x + r + 6, y + 4, f"점수 {scores[i]:.0f}", "s", fill=GREEN))
        else:
            s.append(T(x, y + r + 13, f"점수 {scores[i]:.0f}", "s", "middle", INK))
    s.append(f'<circle cx="{c[0]}" cy="{c[1]}" r="{rho}" fill="none" stroke="{BLUE}" stroke-dasharray="5 3"/>')
    s.append(f'<path d="M{c[0]-6} {c[1]-6} l12 12 M{c[0]+6} {c[1]-6} l-12 12" stroke="{RED}" stroke-width="2.2"/>')
    s.append(T(c[0] - rho + 2, c[1] + rho + 12, "선택 반경 ρ", "s", fill=BLUE))
    s.append(box(412, 8, 278, 194))
    s.append(lines(428, 34, [("3D 레이 대신 플레이어가 보는 화면에서", "tb", None)], 20))
    s.append(T(428, 62, "점수ᵢ = ‖π(Mᵢ) − c‖ − ρᵢ", "fa", fill=GREEN))
    s.append(lines(428, 88, [
        "π(M) : 몹 위치를 화면에 투영한 점",
        "c : 커서,  ρᵢ : 화면에 비친 몹 반지름",
        "선택 = 점수 최소 · 단, 점수 < ρ",
        "• 카메라 각도·몹 키와 무관 → 원인을 우회",
        "• 큰 몹은 ρᵢ가 커서 자연히 잘 잡힌다",
        "• 실루엣을 조금 벗어나도 잡힌다",
    ], 17))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 방법 ③: 히스테리시스
def fig_hyst():
    x0, x1, y0, y1 = 70, 390, 18, 128
    def sig(t):
        if t < 2.5: base = 85 - (85 - 38) * t / 2.5
        elif t < 7.5: base = 38
        else: base = 38 + (90 - 38) * (t - 7.5) / 2.5
        return base + 10 * math.sin(3.3 * t) + 4 * math.sin(7.7 * t + 1)
    ts = [i * 0.02 for i in range(501)]
    vs = [sig(t) for t in ts]
    X = lambda t: x0 + t / 10 * (x1 - x0)
    Y = lambda v: y1 - v / 100 * (y1 - y0)
    single = [v < 40 for v in vs]
    dual, st = [], False
    for v in vs:
        if not st and v < 28: st = True
        elif st and v > 55: st = False
        dual.append(st)
    def toggles(arr): return sum(1 for i in range(1, len(arr)) if arr[i] != arr[i - 1])
    def bar(arr, y, col):
        out, i = [], 0
        while i < len(arr):
            if arr[i]:
                j = i
                while j < len(arr) and arr[j]: j += 1
                out.append(f'<rect x="{X(ts[i]):.1f}" y="{y}" width="{max(1.2, X(ts[j-1])-X(ts[i])):.1f}" height="12" fill="{col}"/>')
                i = j
            else:
                i += 1
        return "".join(out)
    s = ['<svg viewBox="0 0 700 214" width="100%">']
    s.append(L(x0, y1, x1, y1, INK, 1.2) + L(x0, y0, x0, y1, INK, 1.2))
    s.append(T(x0 - 6, y0 + 4, "점수", "xs", "end"))
    s.append(T(x1, y1 + 12, "시간 →", "xs", "end"))
    s.append(L(x0, Y(40), x1, Y(40), FAINT, 1.2, "4 3") + T(x0 - 6, Y(40) + 3, "ρ", "xs", "end"))
    s.append(L(x0, Y(28), x1, Y(28), GREEN, 1.2, "4 3") + T(x0 - 6, Y(28) + 3, "ρ_in", "xs", "end", GREEN))
    s.append(L(x0, Y(55), x1, Y(55), RED, 1.2, "4 3") + T(x0 - 6, Y(55) + 3, "ρ_out", "xs", "end", RED))
    s.append(poly([(X(t), Y(v)) for t, v in zip(ts, vs)], INK, 1.6))
    for yy, arr, col, lab in ((150, single, AMBER, "임계값 1개"), (176, dual, GREEN, "임계값 2개")):
        s.append(f'<rect x="{x0}" y="{yy}" width="{x1-x0}" height="12" fill="#eef0f4"/>')
        s.append(bar(arr, yy, col))
        s.append(T(x0 - 6, yy + 10, lab, "xs", "end"))
        s.append(T(x1 + 6, yy + 10, f"전환 {toggles(arr)}회", "xs", fill=col))
    s.append(T(x0, 204, "칠해진 칸 = 타겟을 잡고 있는 상태", "xs"))
    s.append(box(462, 8, 228, 196))
    s.append(lines(476, 34, [("잡을 때와 놓을 때 기준을 다르게", "tb", None)], 20))
    s.append(lines(476, 62, [
        ("잡기 : 점수 < ρ_in", "f", GREEN),
        ("놓기 : 점수 > ρ_out", "f", RED),
        ("(ρ_out > ρ_in)", "s", None),
    ], 21))
    s.append(lines(476, 136, [
        "• 경계에서 깜빡임이 사라진다",
        "• = 이미 잡은 타겟에 '끈끈함'",
        "• 레이 방식의 튐 문제도 이걸로 완화",
    ], 17))
    s.append('</svg>')
    return "\n".join(s), toggles(single), toggles(dual)


# ---------------------------------------------------------------- 방법 ④: 판정 쪽 보정
def fig_judge():
    P, M = (60, 120), (300, 104)
    s = ['<svg viewBox="0 0 700 196" width="100%">']
    s.append(f'<circle cx="{M[0]}" cy="{M[1]}" r="40" fill="{GREEN_S}" stroke="{GREEN}" stroke-width="1.2"/>')
    s.append(f'<circle cx="{M[0]}" cy="{M[1]}" r="20" fill="none" stroke="{GREEN}" stroke-dasharray="3 3"/>')
    s.append(f'<circle cx="{M[0]}" cy="{M[1]}" r="15" fill="#dfe4ee" stroke="{FAINT}"/>')
    yl = P[1] + 10 + (M[0] - P[0]) / (380 - P[0]) * 12
    s.append(L(P[0], P[1] + 10, 380, P[1] + 22, BLUE, 2.2, mk="ahb"))
    s.append(f'<circle cx="{P[0]}" cy="{P[1]}" r="8" fill="{BLUE}"/>' + T(P[0], P[1] + 26, "P", "tb", "middle", BLUE))
    s.append(L(M[0], M[1], M[0], yl, RED, 1.6, "3 3") + T(M[0] - 8, yl + 14, "e", "tb", fill=RED))
    s.append(T(M[0], M[1] - 46, "r몹 + r탄 + δ", "s", "middle", GREEN))
    s.append(T(M[0] + 46, M[1] - 18, "r몹 + r탄", "s", fill=GREEN))
    s.append(T(200, 176, "얇은 판정이면 빗나감, δ만큼 두꺼우면 명중", "s", "middle", INK))
    s.append(box(412, 8, 278, 180))
    s.append(lines(428, 34, [("조준 대신 결과를 관대하게", "tb", None)], 20))
    s.append(T(428, 62, "명중 ⇔ dist(탄 경로, M) < r몹 + r탄 + δ", "f", fill=GREEN))
    s.append(lines(428, 88, [
        "• 레이 → 구(sphere) 캐스트 = 두꺼운 탄",
        "• Reticle is King : 발사 순간 커서가 적 위면",
        "   명중으로 처리하거나 탄을 살짝 휘게",
        ("한계", "tb", RED),
        "δ가 크면 '안 맞을 걸 맞힌다'.",
        "공정성(특히 PvP)과 손맛의 경계.",
    ], 17))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 방법 ⑤: 스킬별 해석
def fig_skill():
    s = ['<svg viewBox="0 0 700 132" width="100%">']
    s.append(box(10, 44, 120, 44, "#fff", BLUE, 10) + T(70, 71, "커서 위치", "tb", "middle", BLUE))
    rows = [("투사체 스킬", "타겟을 고려한 조준점 (②·③ 사용)", BLUE, 22),
            ("장판 스킬", "바닥 점 그대로 (몹은 무시)", GREEN, 66),
            ("돌진·회피", "방향만 쓰고 높이는 버림", AMBER, 110)]
    for name, desc, col, y in rows:
        s.append(f'<path d="M130 66 C175 66 175 {y} 210 {y}" fill="none" stroke="{FAINT}" stroke-width="1.4" marker-end="url(#ah)"/>')
        s.append(box(214, y - 17, 140, 34, "#fff", col, 9) + T(284, y + 5, name, "tb", "middle", col))
        s.append(L(356, y, 384, y, FAINT, 1.4, mk="ah"))
        s.append(T(392, y + 4, desc, "t"))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 정리: 세 층
def fig_layers():
    cols = [("① 해석", "누구를, 어디를 노렸나", BLUE, ["몹 포함 레이", "② 화면 좌표 선택", "③ 히스테리시스", "④ 스킬별 해석"]),
            ("② 복원", "그 의도를 3D 점으로", AMBER, ["① 조준 평면 높이기", "몹 크기별 중심 / 찍은 지점"]),
            ("③ 판정", "탄이 맞았나", GREEN, ["⑤ 판정 두께 δ", "Reticle is King"])]
    s = ['<svg viewBox="0 0 700 178" width="100%">']
    w, gap = 204, 32
    for i, (t, q, col, chips) in enumerate(cols):
        x = 8 + i * (w + gap)
        s.append(box(x, 8, w, 164, "#fff", col, 14))
        s.append(T(x + w / 2, 34, t, "tb", "middle", col))
        s.append(T(x + w / 2, 52, q, "s", "middle"))
        for j, c in enumerate(chips):
            yy = 64 + j * 25
            s.append(box(x + 14, yy, w - 28, 21, SHADE, "none", 10))
            s.append(T(x + w / 2, yy + 15, c, "t", "middle"))
        if i < 2:
            s.append(L(x + w + 4, 90, x + w + gap - 6, 90, FAINT, 1.8, mk="ah"))
    s.append('</svg>')
    return "\n".join(s)


def main():
    top_svg, eps_top = fig_top()
    scale_svg, data = fig_scale()
    step_svg, jump_s, jump_b = fig_step()
    shape_svg, dB, dS = fig_shape_scale()
    plane_svg, d_plane, d_ground = fig_plane()
    hyst_svg, tg1, tg2 = fig_hyst()

    def at(name, sc):
        return data[name][int(round((sc - 0.5) / 0.01))]
    hi1, hi4 = at("머리 · 높은 카메라", 1.0), at("머리 · 높은 카메라", 4.0)

    html = f"""<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>쿼터뷰 조준 보정 이론</title><style>{CSS}{EXTRA_CSS}</style></head><body>
{DEFS}
<div class="kicker">COMBAT · AIM THEORY</div>
<h1>몬스터에 레이를 맞추면 끝일까</h1>
<p class="lead">쿼터뷰에서 커서로 방향을 정할 때 생기는 오차를 식으로 세우고, "레이에 몬스터 포함"이 어디까지 해결하는지, 그 뒤에 남는 것을 무엇으로 메우는지 정리했다. 그림 속 숫자는 모두 예시값이다.</p>

<h2>1. 문제: 커서는 몹 위인데 조준점은 몹 뒤 바닥</h2>
<div class="fig"><div class="figt">옆에서 보면 — 레이는 몹을 통과해 바닥에 닿는다</div>
{fig_side()}
</div>

<div class="fig"><div class="figt">위에서 보면 — 밀린 거리 d가 방향 오차 ε로 바뀐다</div>
{top_svg}
</div>

<h2 class="pb">2. 왜 몹이 클수록 도드라지나</h2>
<div class="fig"><div class="figt">밀림 비율 d/D는 k = h/H 하나로 정해진다</div>
{fig_universal()}
</div>

<div class="fig"><div class="figt">몹 크기를 키우면 (옆 몹, 예시값)</div>
{scale_svg}
</div>

<div class="callout c-amber"><span class="lbl">정리 — "클수록 도드라진다"의 정확한 뜻</span>
<ol class="steps">
<li><b>밀림 d는 쌍곡선으로 커진다.</b> 허용치(몸집)는 크기에 정비례인데 d = h·D/(H−h)는 분모가 줄어든다.</li>
<li><b>명중 여부만 보면 몸집이 일부 상쇄한다.</b> 그래서 체감은 "빗나감"보다 "엉뚱한 데로 쏜다"로 온다. 높은 카메라 머리 조준 기준 ×1에서 ε {hi1[2]:.0f}° · e {hi1[1]:.2f}m, ×4에서 ε {hi4[2]:.0f}° · e {hi4[1]:.2f}m.</li>
<li><b>h가 H에 다가가면 질적으로 깨진다.</b> 레이가 바닥에 안 닿거나 방 밖까지 날아가 조준 자체가 실패한다. 큰 몹 + 낮은 카메라가 정확히 이 조합이다.</li>
</ol></div>

<h2 class="pb">3. 가장 간단한 해법: 레이에 몬스터 포함</h2>
<div class="callout c-blue"><span class="lbl">하는 일</span>
레이가 몹 콜라이더에 먼저 맞으면 바닥 대신 그 몹을 조준점으로 쓴다. 실루엣 안에서는 오차가 0이 된다. 출발점으로 맞는 방법이고 실제 게임에서도 쓴다. 다만 아래 네 가지가 남는다.</div>

<div class="fig"><div class="figt">문제 1 · 맞음/안 맞음 둘로 갈린다 — 실루엣 위쪽 경계에서 조준점이 튄다</div>
{step_svg}
</div>
<div class="callout c-amber">튀는 크기 = 실루엣 꼭대기 높이에서의 원래 오차 = h·D/(H−h). 작은 몹 {jump_s:.2f}m, 큰 몹 {jump_b:.2f}m (H = 5m, D = 5m). <b>원래 문제가 사라진 게 아니라 경계로 모였을 뿐</b>이고, 그 크기도 몹이 클수록 커진다.</div>

<div class="fig"><div class="figt">문제 2 · 보이는 모양 ≠ 콜라이더 &nbsp;|&nbsp; 문제 3 · 큰 몹은 "어디를" 노렸는지가 중요</div>
{shape_svg}
</div>

<div class="fig"><div class="figt">문제 4 · 커서가 몹 위에 있어도 그 몹을 노린 게 아닐 수 있다</div>
{fig_ambig()}
</div>

<h2 class="pb">4. 남는 것을 메우는 보정 방식</h2>
<div class="fig"><div class="figt">① 조준 평면 높이기 — 원인(바닥에 투영)을 직접 줄인다</div>
{plane_svg}
</div>

<div class="fig"><div class="figt">② 화면 좌표로 고르기 — 원인을 우회한다</div>
{fig_screen()}
</div>

<div class="fig"><div class="figt">③ 히스테리시스 — 경계 깜빡임을 없앤다</div>
{hyst_svg}
</div>

<div class="fig"><div class="figt">④ 스킬별 해석 — 커서 하나에 의도가 여러 개</div>
{fig_skill()}
</div>

<div class="fig"><div class="figt">⑤ 판정 쪽 보정 — 조준은 그대로 두고 결과를 관대하게</div>
{fig_judge()}
</div>

<h2>5. 한 장 정리: 문제는 세 층이다</h2>
<div class="fig">
{fig_layers()}
</div>

<table class="cov">
<thead><tr><th>방법</th><th>바닥 투영·발산</th><th>경계 튐</th><th>실루엣≠콜라이더</th><th>규모별 겨눌 곳</th><th>의도 모호</th></tr></thead>
<tbody>
<tr><td class="k">몹 포함 레이</td><td class="p">◐ 실루엣 안만</td><td class="n">✕ 새로 생김</td><td class="n">✕</td><td class="n">✕ 중심 고정</td><td class="n">✕ 흡착</td></tr>
<tr><td class="k">① 조준 평면 높이기</td><td class="p">◐ a 근처만</td><td class="y">● 연속</td><td class="y">● 무관</td><td class="n">✕</td><td class="z">—</td></tr>
<tr><td class="k">② 화면 좌표 선택</td><td class="y">●</td><td class="p">◐ 반경 경계</td><td class="y">● 투영 반지름</td><td class="p">◐</td><td class="p">◐ 점수로 우선순위</td></tr>
<tr><td class="k">③ 히스테리시스</td><td class="z">—</td><td class="y">●</td><td class="z">—</td><td class="z">—</td><td class="z">—</td></tr>
<tr><td class="k">④ 스킬별 해석</td><td class="z">—</td><td class="z">—</td><td class="z">—</td><td class="z">—</td><td class="y">●</td></tr>
<tr><td class="k">⑤ 판정 두께 δ</td><td class="p">◐ 작은 오차만</td><td class="z">—</td><td class="p">◐</td><td class="z">—</td><td class="z">—</td></tr>
<tr><td class="k">몹 크기별 중심 / 찍은 지점</td><td class="z">—</td><td class="z">—</td><td class="z">—</td><td class="y">●</td><td class="z">—</td></tr>
</tbody></table>
<p class="lead" style="font-size:8.6pt">● 해결 &nbsp; ◐ 일부 &nbsp; ✕ 안 됨·악화 &nbsp; — 해당 없음</p>

<div class="callout c-green"><span class="lbl">결론</span>
<ol class="steps">
<li><b>몹 포함 레이는 ① 해석 층의 가장 단순한 해법</b>이다. 실루엣 안의 오차는 지우지만 오차를 경계로 모을 뿐이다.</li>
<li>한 방법으로 다 막히는 칸은 없다. <b>보통 ②(또는 몹 포함 레이) + ③ + ① 평면을 대체값으로</b> 겹치고, 판정은 ⑤로 마감한다.</li>
<li>몹 크기가 바뀌는 게임이면 <b>규모별 해석</b>이 따로 필요하다. 작은 몹은 중심, 큰 몹은 찍은 지점.</li>
</ol></div>

<div class="footer">쿼터뷰 조준 보정 이론 · 일반론 정리 (프로젝트 실측 아님) · 생성: docs/Combat/tools/aim_fix_theory_gen.py · 참고: Naughty Dog, "A Second Shot" (GDC 2024)</div>
</body></html>"""
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(html)
    print("wrote", os.path.abspath(OUT))
    print("eps_top", round(eps_top, 1), "jump", round(jump_s, 2), round(jump_b, 2), "dB dS", round(dB, 1), round(dS, 1),
          "plane", round(d_plane, 2), round(d_ground, 2), "toggles", tg1, tg2, "hi1", hi1, "hi4", hi4)


if __name__ == "__main__":
    main()
