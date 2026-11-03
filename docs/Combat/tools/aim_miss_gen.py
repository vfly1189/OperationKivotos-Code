# -*- coding: utf-8 -*-
"""쿼터뷰 조준 오차 설명 문서(AimMiss_Explained.html) 생성기.

계산 모델 (전부 계산값 — 실측 아님)
  - 카메라 = 플레이어 + _delta, 플레이어를 LookAt (CameraController)
  - 커서 레이는 MapGround만 맞춘다 (CharacterMovement.RotateToMouse) → 몹을 통과해 바닥에 닿는다
  - 높이 h 지점(몹 위의 커서)을 지나는 레이의 바닥 점 G = M + h/(Cy-h) * (M - Cxz)
  - 탄은 플레이어 중심에서 G 방향으로 수평 직진 (총구 오프셋은 무시)
  - 명중 = 몹 중심과 탄 경로의 거리 < 몹 반지름 0.265 + 탄 반폭 0.057
"""
import math, os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "AimMiss_Explained.html")

R_MON = 0.265      # Droid_Helmet_AR 캡슐 반지름
R_BUL = 0.057      # BulletTrail 박스 반폭 (0.114 / 2)
R_HIT = R_MON + R_BUL
H_BODY, H_HEAD = 0.52, 1.0
CAM_FIELD = (0.0, 5.0, -3.0)   # Game / NormalDungeon 씬
CAM_BOSS = (0.0, 4.0, -5.0)    # BossDungeon 씬, 코드 기본값


def aim(M, h, C):
    """몹 위치 M(x,z), 커서 높이 h → (빗나간 거리, 바닥 조준점 G)"""
    f = h / (C[1] - h)
    G = (M[0] + f * (M[0] - C[0]), M[1] + f * (M[1] - C[2]))
    L = math.hypot(*G)
    d = (G[0] / L, G[1] / L)
    return abs(M[0] * d[1] - M[1] * d[0]), G


def deg(C):
    return math.degrees(math.atan2(C[1], -C[2]))


# ---------------------------------------------------------------- 그림 1: 지금의 조준 흐름
def fig_flow():
    boxes = [
        ("① 커서", "화면의 마우스 위치", "#3b5bdb"),
        ("② 레이", "카메라 → 커서 방향\nMapGround(바닥)만 맞춤", "#3b5bdb"),
        ("③ 몸 회전", "바닥 점을 향해\nLookAt (높이는 무시)", "#3b5bdb"),
        ("④ 발사", "총구에서 몸통 방향으로\n수평 직진", "#e8590c"),
    ]
    w, gap, x0 = 152, 24, 8
    s = ['<svg viewBox="0 0 700 92" width="100%">']
    for i, (t, sub, col) in enumerate(boxes):
        x = x0 + i * (w + gap)
        s.append(f'<rect x="{x}" y="8" width="{w}" height="76" rx="10" fill="#fff" stroke="{col}" stroke-width="1.6"/>')
        s.append(f'<text x="{x+w/2}" y="32" text-anchor="middle" class="tb" fill="{col}">{t}</text>')
        for j, line in enumerate(sub.split("\n")):
            s.append(f'<text x="{x+w/2}" y="{52+j*13}" text-anchor="middle" class="s">{line}</text>')
        if i < 3:
            ax = x + w + 3
            s.append(f'<path d="M{ax} 46 L{ax+gap-7} 46" stroke="#8a91a0" stroke-width="1.6" marker-end="url(#ar)"/>')
    s.append('<defs><marker id="ar" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto">'
             '<path d="M0,0 L8,4 L0,8 z" fill="#8a91a0"/></marker></defs>')
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 그림 2: 옆에서 본 그림
def fig_side():
    C = CAM_FIELD
    sc = 40.0
    gy = 210            # 바닥 y
    px = 190            # 플레이어 x (z=0)
    X = lambda z: px + z * sc
    Y = lambda y: gy - y * sc
    mz = 3.0
    s = ['<svg viewBox="0 0 700 250" width="100%">',
         '<defs><marker id="a2" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto"><path d="M0,0 L8,4 L0,8 z" fill="#e8590c"/></marker></defs>']
    # 바닥
    s.append(f'<line x1="20" y1="{gy}" x2="560" y2="{gy}" stroke="#c9ced8" stroke-width="2"/>')
    s.append(f'<text x="24" y="{gy+16}" class="xs">바닥 (MapGround)</text>')
    # 카메라
    cx, cy = X(C[2]), Y(C[1])
    s.append(f'<rect x="{cx-14}" y="{cy-9}" width="28" height="18" rx="4" fill="#1a1c22"/>')
    s.append(f'<circle cx="{cx+16}" cy="{cy}" r="5" fill="#1a1c22"/>')
    s.append(f'<text x="{cx-12}" y="{cy-16}" class="t">카메라</text>')
    s.append(f'<text x="{cx-12}" y="{cy+24}" class="xs">높이 5m · 뒤 3m</text>')
    # 플레이어
    s.append(f'<rect x="{X(0)-8}" y="{Y(1.0)}" width="16" height="{1.0*sc}" rx="7" fill="#3b5bdb"/>')
    s.append(f'<text x="{X(0)}" y="{gy+16}" text-anchor="middle" class="t" fill="#3b5bdb">플레이어</text>')
    # 몹
    s.append(f'<rect x="{X(mz)-R_MON*sc}" y="{Y(1.05)}" width="{2*R_MON*sc}" height="{1.05*sc}" rx="9" fill="#ced4da" stroke="#868e96"/>')
    s.append(f'<text x="{X(mz)}" y="{gy+16}" text-anchor="middle" class="t">몹 (키 1.05m)</text>')
    # 레이: 머리
    for h, col, lab in [(H_HEAD, "#e8590c", "머리"), (H_BODY, "#f59f00", "몸통")]:
        f = h / (C[1] - h)
        gz = mz + f * (mz - C[2])
        s.append(f'<line x1="{cx+16}" y1="{cy}" x2="{X(gz)}" y2="{gy}" stroke="{col}" stroke-width="1.6" stroke-dasharray="5 3"/>')
        s.append(f'<circle cx="{X(mz)}" cy="{Y(h)}" r="3.5" fill="{col}"/>')
        s.append(f'<circle cx="{X(gz)}" cy="{gy}" r="4.5" fill="#fff" stroke="{col}" stroke-width="2"/>')
        s.append(f'<text x="{X(mz)-R_MON*sc-6}" y="{Y(h)+3}" text-anchor="end" class="s" fill="{col}">커서({lab})</text>')
    gz_head = mz + H_HEAD / (C[1] - H_HEAD) * (mz - C[2])
    s.append(f'<path d="M{X(mz)} {gy+26} L{X(gz_head)} {gy+26}" stroke="#e8590c" stroke-width="1.6" marker-end="url(#a2)"/>')
    s.append(f'<text x="{(X(mz)+X(gz_head))/2}" y="{gy+38}" text-anchor="middle" class="t" fill="#e8590c">{gz_head-mz:.1f}m 뒤로</text>')
    # 오른쪽 설명
    bx = 575
    s.append(f'<rect x="{bx}" y="40" width="118" height="130" rx="10" fill="#fff4ec"/>')
    lines = [("tb", "#8a3708", "몹은 레이를"), ("tb", "#8a3708", "막지 않는다"),
             ("s", "#8a3708", "레이는 몹을 통과해"), ("s", "#8a3708", "그 뒤 바닥에 닿는다"),
             ("s", "#8a3708", ""), ("s", "#8a3708", "커서가 높을수록"), ("s", "#8a3708", "조준점이 멀리 밀린다")]
    for i, (c, col, t) in enumerate(lines):
        s.append(f'<text x="{bx+59}" y="{64+i*15}" text-anchor="middle" class="{c}" fill="{col}">{t}</text>')
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 그림 3: 위에서 본 그림 (옆 몹 / 앞 몹)
def fig_top():
    C = CAM_FIELD
    BOX_TOP, BOX_BOT = 8, 410
    s = ['<svg viewBox="0 0 700 460" width="100%">',
         '<defs><marker id="a3" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto"><path d="M0,0 L8,4 L0,8 z" fill="#3b5bdb"/></marker>'
         '<marker id="a4" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto"><path d="M0,0 L8,4 L0,8 z" fill="#2b8a3e"/></marker></defs>']

    def panel(bx, bw, px, py, sc, M, title, good, more):
        X = lambda x: px + x * sc
        Y = lambda z: py - z * sc
        m, G = aim(M, H_HEAD, C)
        s.append(f'<rect x="{bx}" y="{BOX_TOP}" width="{bw}" height="{BOX_BOT-BOX_TOP}" rx="12" fill="#fff" stroke="#e3e6ec"/>')
        s.append(f'<text x="{bx+14}" y="{BOX_TOP+22}" class="tb">{title}</text>')
        # 커서 레이의 바닥 투영: 카메라(화면 아래, 패널 밖) → 몹 → 조준점. 패널 아래 변에서 자른다.
        cx, cz = X(C[0]), Y(C[2])
        gx, gz = X(G[0]), Y(G[1])
        k = (cz - (BOX_BOT - 2)) / (cz - gz)
        sx, sy = cx + (gx - cx) * k, BOX_BOT - 2
        # 탄 경로: 플레이어 → 조준점 방향으로 0.9m 더
        L = math.hypot(*G)
        ext = (G[0] / L * (L + more), G[1] / L * (L + more))
        col = "#2b8a3e" if good else "#3b5bdb"
        mk = "a4" if good else "a3"
        s.append(f'<line x1="{X(0)}" y1="{Y(0)}" x2="{X(ext[0])}" y2="{Y(ext[1])}" stroke="{col}" stroke-width="2.6" marker-end="url(#{mk})"/>')
        s.append(f'<line x1="{sx:.1f}" y1="{sy:.1f}" x2="{gx:.1f}" y2="{gz:.1f}" stroke="#e8590c" stroke-width="1.5" stroke-dasharray="5 3"/>')
        # 몹 + 명중 범위
        s.append(f'<circle cx="{X(M[0])}" cy="{Y(M[1])}" r="{R_HIT*sc:.1f}" fill="none" stroke="#868e96" stroke-dasharray="2 2"/>')
        s.append(f'<circle cx="{X(M[0])}" cy="{Y(M[1])}" r="{R_MON*sc:.1f}" fill="#ced4da" stroke="#868e96"/>')
        s.append(f'<text x="{X(M[0])}" y="{Y(M[1])+3}" text-anchor="middle" class="xs" fill="#1a1c22">몹</text>')
        s.append(f'<circle cx="{gx:.1f}" cy="{gz:.1f}" r="5" fill="#fff" stroke="#e8590c" stroke-width="2"/>')
        s.append(f'<circle cx="{X(0)}" cy="{Y(0)}" r="9" fill="#3b5bdb"/>')
        s.append(f'<text x="{X(0)}" y="{Y(0)+25}" text-anchor="middle" class="t" fill="#3b5bdb">플레이어</text>')
        return m, G, X, Y, (sx, sy)

    # 왼쪽: 오른쪽 3m 몹
    m1, G1, X1, Y1, st1 = panel(8, 338, 48, 290, 62.0, (3.0, 0.0), "옆에 있는 몹 (오른쪽 3m) · 머리 조준", False, 0.5)
    L = math.hypot(*G1); d = (G1[0] / L, G1[1] / L)
    t = 3.0 * d[0]
    foot = (d[0] * t, d[1] * t)
    s.append(f'<line x1="{X1(3.0)}" y1="{Y1(0)}" x2="{X1(foot[0]):.1f}" y2="{Y1(foot[1]):.1f}" stroke="#e8590c" stroke-width="2.4"/>')
    s.append(f'<text x="{X1(0)+6}" y="{Y1(0)-78}" class="tb" fill="#e8590c">탄이 {m1:.2f}m 옆으로 지나감</text>')
    s.append(f'<text x="{X1(0)+6}" y="{Y1(0)-63}" class="s" fill="#8a3708">(맞으려면 {R_HIT:.2f}m 이내)</text>')
    s.append(f'<text x="{X1(G1[0])+6}" y="{Y1(G1[1])+20}" class="s" fill="#e8590c">조준점</text>')
    s.append(f'<text x="{st1[0]+8:.1f}" y="{st1[1]-6:.1f}" class="xs" fill="#e8590c">↙ 카메라 쪽에서 온 커서 레이</text>')

    # 오른쪽: 화면 위쪽 3m 몹
    m2, G2, X2, Y2, st2 = panel(354, 338, 520, 372, 52.0, (0.0, 3.0), "앞에 있는 몹 (화면 위쪽 3m) · 머리 조준", True, 0.4)
    s.append(f'<text x="{X2(0)+14}" y="{Y2(G2[1])+4}" class="s" fill="#e8590c">조준점 (1.5m 뒤로 밀림)</text>')
    s.append(f'<text x="{X2(0)+20}" y="{Y2(3.0)+4}" class="s" fill="#1f6b2e">밀려도 같은 줄 위 → 명중</text>')

    # 패널 아래 설명
    s.append(f'<text x="14" y="432" class="t" fill="#8a3708">밀리는 방향 = 카메라 반대쪽 → 쏘는 방향과 어긋남</text>')
    s.append(f'<text x="14" y="447" class="s">그래서 몹이 옆에 있으면 탄이 옆으로 빠진다</text>')
    s.append(f'<text x="360" y="432" class="t" fill="#1f6b2e">밀리는 방향 = 쏘는 방향과 같음</text>')
    s.append(f'<text x="360" y="447" class="s">거리만 멀어지고 방향은 그대로라 맞는다</text>')
    s.append('</svg>')
    return "\n".join(s), m1


# ---------------------------------------------------------------- 그림 4: 위치별 지도
def heat(C, h, title, ox, oy, size=300, rng=5.0, step=0.25):
    n = int(2 * rng / step)
    cs = size / n
    s = []
    s.append(f'<text x="{ox}" y="{oy-8}" class="tb">{title}</text>')
    misses = hits = 0
    for i in range(n):
        for j in range(n):
            x = -rng + (i + 0.5) * step
            z = rng - (j + 0.5) * step
            r = math.hypot(x, z)
            if r < 0.8 or r > rng:
                continue
            m, _ = aim((x, z), h, C)
            if m < R_HIT:
                col = "#d3f9d8"; hits += 1
            else:
                k = min(1.0, (m - R_HIT) / 0.9)
                # amber 농도
                a = 0.25 + 0.75 * k
                col = f"rgba(232,89,12,{a:.2f})"; misses += 1
            s.append(f'<rect x="{ox+i*cs:.1f}" y="{oy+j*cs:.1f}" width="{cs+0.3:.1f}" height="{cs+0.3:.1f}" fill="{col}"/>')
    c = ox + size / 2
    cy = oy + size / 2
    for rr in (2, 4):
        s.append(f'<circle cx="{c}" cy="{cy}" r="{rr/rng*size/2}" fill="none" stroke="#fff" stroke-width="1"/>')
        s.append(f'<text x="{c+rr/rng*size/2-2}" y="{cy-3}" text-anchor="end" class="xs" fill="#5b6270">{rr}m</text>')
    s.append(f'<circle cx="{c}" cy="{cy}" r="6" fill="#3b5bdb"/>')
    s.append(f'<text x="{c}" y="{oy+size+14}" text-anchor="middle" class="xs">▼ 카메라 쪽 (화면 아래)</text>')
    pct = misses / (hits + misses) * 100
    return "\n".join(s), pct


def fig_maps():
    s = ['<svg viewBox="0 0 700 760" width="100%">']
    items = [
        (CAM_FIELD, H_BODY, "필드·일반 던전 · 몸통 조준", 30, 30),
        (CAM_FIELD, H_HEAD, "필드·일반 던전 · 머리 조준", 370, 30),
        (CAM_BOSS, H_BODY, "보스 던전 · 몸통 조준", 30, 410),
        (CAM_BOSS, H_HEAD, "보스 던전 · 머리 조준", 370, 410),
    ]
    pcts = []
    for C, h, t, ox, oy in items:
        svg, pct = heat(C, h, t, ox, oy)
        s.append(svg)
        pcts.append(pct)
        s.append(f'<text x="{ox+300}" y="{oy-8}" text-anchor="end" class="t" fill="#8a3708">빗나감 {pct:.0f}%</text>')
    s.append('</svg>')
    return "\n".join(s), pcts


# ---------------------------------------------------------------- 그림 5: 해결 방향 3가지
def fig_fix():
    s = ['<svg viewBox="0 0 700 250" width="100%">',
         '<defs><marker id="a5" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto"><path d="M0,0 L8,4 L0,8 z" fill="#2b8a3e"/></marker>'
         '<marker id="a6" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto"><path d="M0,0 L8,4 L0,8 z" fill="#adb5bd"/></marker></defs>']
    W = 222
    for k in range(3):
        x = 4 + k * (W + 11)
        s.append(f'<rect x="{x}" y="6" width="{W}" height="238" rx="12" fill="#fff" stroke="#e3e6ec"/>')
    # ① 커서 아래 몹 먼저
    x = 4
    s.append(f'<text x="{x+14}" y="30" class="tb" fill="#2b8a3e">① 커서 아래 몹을 먼저 본다</text>')
    s.append(f'<line x1="{x+20}" y1="200" x2="{x+210}" y2="200" stroke="#c9ced8" stroke-width="2"/>')
    s.append(f'<rect x="{x+22}" y="52" width="22" height="14" rx="3" fill="#1a1c22"/>')
    s.append(f'<rect x="{x+130}" y="158" width="22" height="42" rx="9" fill="#ced4da" stroke="#868e96"/>')
    s.append(f'<line x1="{x+44}" y1="62" x2="{x+141}" y2="166" stroke="#2b8a3e" stroke-width="1.8"/>')
    s.append(f'<line x1="{x+141}" y1="166" x2="{x+178}" y2="200" stroke="#adb5bd" stroke-width="1.4" stroke-dasharray="4 3"/>')
    s.append(f'<circle cx="{x+141}" cy="166" r="4" fill="#2b8a3e"/>')
    s.append(f'<circle cx="{x+141}" cy="200" r="4.5" fill="#fff" stroke="#2b8a3e" stroke-width="2"/>')
    s.append(f'<text x="{x+156}" y="150" class="s" fill="#2b8a3e">몹에 먼저 맞음</text>')
    s.append(f'<text x="{x+84}" y="216" class="s" fill="#2b8a3e">조준점 = 몹 발밑</text>')
    s.append(f'<text x="{x+14}" y="236" class="xs">레이 대상: 바닥만 → 몹 레이어 우선</text>')
    # ② 총구 → 조준점
    x = 4 + W + 11
    s.append(f'<text x="{x+14}" y="30" class="tb" fill="#2b8a3e">② 총구에서 조준점으로 쏜다</text>')
    bx, by = x + 60, 170
    s.append(f'<circle cx="{bx}" cy="{by}" r="16" fill="#3b5bdb"/>')
    s.append(f'<circle cx="{bx+22}" cy="{by-6}" r="4" fill="#e8590c"/>')
    s.append(f'<text x="{bx+28}" y="{by+12}" class="xs">총구</text>')
    tx, ty = x + 150, 62
    s.append(f'<circle cx="{tx}" cy="{ty}" r="11" fill="#ced4da" stroke="#868e96"/>')
    s.append(f'<line x1="{bx}" y1="{by}" x2="{tx}" y2="{ty}" stroke="#c9ced8" stroke-width="1" stroke-dasharray="2 3"/>')
    dx, dy = tx - bx, ty - by
    s.append(f'<line x1="{bx+22}" y1="{by-6}" x2="{bx+22+dx*1.05}" y2="{by-6+dy*1.05}" stroke="#adb5bd" stroke-width="1.6" marker-end="url(#a6)"/>')
    s.append(f'<line x1="{bx+22}" y1="{by-6}" x2="{tx}" y2="{ty}" stroke="#2b8a3e" stroke-width="2" marker-end="url(#a5)"/>')
    s.append(f'<text x="{x+14}" y="205" class="s" fill="#868e96">회색 = 지금: 몸통 방향과 평행</text>')
    s.append(f'<text x="{x+14}" y="218" class="s" fill="#868e96">→ 총구 간격만큼 비껴감 · 초록 = 고친 뒤</text>')
    s.append(f'<text x="{x+14}" y="236" class="xs">방향 = (조준점 − 총구), 몸통 방향 아님</text>')
    # ③ 판정 두께
    x = 4 + 2 * (W + 11)
    s.append(f'<text x="{x+14}" y="30" class="tb" fill="#2b8a3e">③ 판정에 두께를 준다</text>')
    mx, my = x + 150, 120
    s.append(f'<circle cx="{mx}" cy="{my}" r="20" fill="#ced4da" stroke="#868e96"/>')
    s.append(f'<line x1="{x+20}" y1="{my-28}" x2="{x+205}" y2="{my-28}" stroke="#3b5bdb" stroke-width="1.6"/>')
    s.append(f'<text x="{x+20}" y="{my-34}" class="s" fill="#3b5bdb">얇은 탄 (폭 0.11m): 스침 = 빗나감</text>')
    s.append(f'<rect x="{x+20}" y="{my+38}" width="185" height="26" rx="13" fill="rgba(43,138,62,.15)" stroke="#2b8a3e"/>')
    s.append(f'<line x1="{x+20}" y1="{my+51}" x2="{x+205}" y2="{my+51}" stroke="#2b8a3e" stroke-width="1.6"/>')
    s.append(f'<text x="{x+20}" y="{my+80}" class="s" fill="#2b8a3e">구 캐스트: 반지름만큼 관대하게</text>')
    s.append(f'<text x="{x+14}" y="236" class="xs">얼마나 관대할지 = 설계 값 (측정으로 정함)</text>')
    s.append('</svg>')
    return "\n".join(s)


CSS = """
:root{--ink:#1a1c22;--muted:#5b6270;--faint:#8a91a0;--line:#e3e6ec;--line2:#eef0f4;--shade:#f7f8fa;
--blue:#3b5bdb;--blue-soft:#eef2ff;--blue-ink:#2b3a8f;--amber:#e8590c;--amber-soft:#fff4ec;--amber-ink:#8a3708;
--green:#2b8a3e;--green-soft:#ebfbee;--green-ink:#1f6b2e;}
*{box-sizing:border-box}
@page{size:A4;margin:12mm 13mm}
html{-webkit-print-color-adjust:exact;print-color-adjust:exact}
body{font-family:"Malgun Gothic","맑은 고딕","Segoe UI",sans-serif;color:var(--ink);margin:0;line-height:1.55;font-size:10.2pt}
h1{font-size:19pt;margin:0 0 4px;font-weight:800;letter-spacing:-.02em}
.kicker{font-size:8pt;letter-spacing:.24em;color:var(--blue);font-weight:700;margin-bottom:7px}
h2{font-size:12.5pt;margin:12px 0 4px;font-weight:800}
.lead{font-size:10.4pt;color:var(--muted);margin:0 0 6px}
.fig{border:1px solid var(--line);border-radius:12px;padding:9px 11px 6px;margin:9px 0;background:#fff;break-inside:avoid}
.figt{font-size:9.8pt;font-weight:800;margin:0 0 3px}
.figcap{font-size:8.2pt;color:var(--faint);margin:3px 0 2px}
svg{display:block} svg text{font-family:"Malgun Gothic","맑은 고딕","Segoe UI",sans-serif}
.t{font-size:9.6px;font-weight:700}.t:not([fill]){fill:#1a1c22}
.tb{font-size:10.6px;font-weight:800}.tb:not([fill]){fill:#1a1c22}
.s{font-size:8.4px}.s:not([fill]){fill:#5b6270}
.xs{font-size:7.4px}.xs:not([fill]){fill:#8a91a0}
.callout{border-radius:11px;padding:9px 13px;margin:9px 0;font-size:9.2pt;line-height:1.55;break-inside:avoid}
.c-amber{background:var(--amber-soft);color:var(--amber-ink)} .c-blue{background:var(--blue-soft);color:var(--blue-ink)}
.c-green{background:var(--green-soft);color:var(--green-ink)}
.callout .lbl{font-weight:800;font-size:8.2pt;letter-spacing:.05em;display:block;margin-bottom:3px}
.callout ul{margin:2px 0 0;padding-left:16px}.callout li{margin:2px 0}
.stat{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin:10px 0}
.stat div{border:1px solid var(--line);border-radius:10px;padding:8px 11px}
.stat .n{display:block;font-size:16pt;font-weight:800;letter-spacing:-.02em;color:var(--amber-ink)}
.stat span b{font-weight:800;color:var(--ink)}
.stat span{font-size:8pt;color:var(--muted)}
table{width:100%;border-collapse:collapse;margin:8px 0;font-size:8.8pt;break-inside:avoid}
th{text-align:left;padding:5px 8px;background:var(--ink);color:#fff;font-weight:700;font-size:8.4pt}
td{padding:5px 8px;border-bottom:1px solid var(--line2);vertical-align:top}
tbody tr:nth-child(even){background:var(--shade)}
td.k{font-weight:800;color:var(--blue-ink);white-space:nowrap}
.pb{break-before:page}
.footer{margin-top:10px;padding-top:6px;border-top:1px solid var(--line);font-size:7.6pt;color:var(--faint)}
"""


def main():
    top_svg, m_side = fig_top()
    maps_svg, pcts = fig_maps()
    _, G_boss_body = aim((3.0, 0.0), H_BODY, CAM_BOSS)
    m_boss_body, _ = aim((3.0, 0.0), H_BODY, CAM_BOSS)
    m_field_body, _ = aim((3.0, 0.0), H_BODY, CAM_FIELD)
    html = f"""<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>쿼터뷰 조준 오차</title><style>{CSS}</style></head><body>

<div class="kicker">COMBAT · PROJECTILE AIM</div>
<h1>커서는 몹 위에 있는데, 왜 빗나갈까</h1>
<p class="lead">쿼터뷰 카메라는 비스듬히 내려다본다. 그런데 조준은 "바닥"만 본다. 이 둘이 어긋나는 지점을 그림으로 정리했다.</p>

<div class="fig"><div class="figt">지금의 조준 흐름</div>
{fig_flow()}
<div class="figcap">CharacterMovement.RotateToMouse() → SpawnProjectiles. 문제는 ②: 레이가 몹을 무시하고 바닥까지 간다.</div></div>

<div class="stat">
<div><span class="n">{m_side:.2f}m</span><span>옆에 있는 몹의 <b>머리</b>를 찍었을 때 탄이 비껴가는 거리 (필드 카메라)</span></div>
<div><span class="n">{R_HIT:.2f}m</span><span>맞으려면 이 안쪽으로 지나가야 함 (몹 반지름 0.265 + 탄 반폭 0.057)</span></div>
<div><span class="n">{m_boss_body:.2f}m</span><span>보스 던전 카메라에선 <b>몸통</b>만 찍어도 이만큼 비껴감</span></div>
</div>

<div class="callout c-amber"><span class="lbl">한 줄 요약</span>
몹 위에 커서를 올리면 레이는 몹을 <b>통과해 뒤쪽 바닥</b>에 닿는다. 조준점이 <b>카메라 반대쪽으로 밀리고</b>,
몹이 <b>옆에</b> 있으면 그 밀림이 쏘는 방향을 틀어서 탄이 옆으로 지나간다. 결국 플레이어는 "몹 <b>발밑</b>을 찍어야 맞는다"를 배워야 한다.</div>

<h2>1. 옆에서 보면: 조준점이 몹 뒤로 밀린다</h2>
<div class="fig"><div class="figt">필드 카메라 (높이 5m · 뒤 3m · 약 {deg(CAM_FIELD):.0f}°), 앞쪽 3m의 몹</div>
{fig_side()}
<div class="figcap">밀리는 거리 = 커서 높이 ÷ (카메라 높이 − 커서 높이) × 카메라에서 몹까지의 수평 거리. 머리(1m)면 1.5m, 몸통(0.52m)이면 0.7m.</div></div>

<h2 class="pb">2. 위에서 보면: 몹이 어디 있느냐에 따라 갈린다</h2>
<div class="fig"><div class="figt">같은 "머리 조준"인데 옆 몹은 빗나가고, 앞 몹은 맞는다</div>
{top_svg}
<div class="figcap">주황 점선 = 커서 레이를 바닥에 내려 그린 선. 파란/초록 화살표 = 실제 탄 경로. 점선 원 = 탄이 이 안을 지나야 명중. 카메라는 두 그림 모두 화면 아래쪽(패널 밖).</div></div>

<h2 class="pb">3. 플레이어 주변 어디에 있는 몹이 빗나가나</h2>
<p class="lead">가운데 파란 점이 플레이어, 아래쪽이 카메라. 칸마다 "그 자리의 몹을 커서로 찍고 쐈을 때"를 계산했다.
<span style="color:#2b8a3e;font-weight:800">초록 = 명중</span>, <span style="color:#e8590c;font-weight:800">주황 = 빗나감</span> (진할수록 많이 빗나감). 반경 5m까지.</p>
<div class="fig">
{maps_svg}
<div class="figcap">세로(화면 위아래) 줄의 몹은 항상 맞고, 옆으로 갈수록 빗나간다. 거리와는 거의 상관없다 — 옆 몹의 빗나간 거리는 커서 높이와 카메라 위치만으로 정해진다.</div></div>

<div class="callout c-blue"><span class="lbl">읽는 법</span><ul>
<li><b>필드·일반 던전</b>: 몸통 조준은 옆 몹도 {m_field_body:.2f}m로 아슬아슬하게 맞는다. <b>머리</b>를 찍으면 옆쪽 대부분이 빗나간다.</li>
<li><b>보스 던전</b>: 카메라가 더 낮고 멀어서(약 {deg(CAM_BOSS):.0f}°) 밀림이 커진다. <b>몸통만 찍어도</b> 옆쪽이 빗나간다.</li>
<li>모든 계산은 <b>탄이 플레이어 중심에서 나간다</b>고 가정했다. 실제로는 총구가 옆에 붙어 있어 오차가 더 커지거나 줄어들 수 있다.</li>
</ul></div>

<h2 class="pb">4. 고치는 방향 세 가지</h2>
<div class="fig">
{fig_fix()}
</div>

<table><thead><tr><th style="width:30%">우리 쪽 해법</th><th style="width:34%">Naughty Dog TLOU2 (GDC 2024)</th><th>무엇을 막나</th></tr></thead><tbody>
<tr><td class="k">① 커서 아래 몹 우선</td><td>"Reticle is King" — 조준점이 적 위면 맞아야 한다</td><td>카메라 레이와 바닥 조준점의 어긋남</td></tr>
<tr><td class="k">② 총구 → 조준점</td><td>카메라 판정 결과로 캐릭터 탄 궤적을 역산</td><td>총구와 몸 중심의 간격</td></tr>
<tr><td class="k">③ 판정 두께</td><td>레이캐스트 → 구 캐스트, 반지름을 살짝 키움</td><td>스치듯 지나가는 아까운 빗나감</td></tr>
</tbody></table>

<div class="callout c-green"><span class="lbl">다음: 계산이 맞는지 재보기</span><ul>
<li>사격 봇이 몹의 <b>발 · 몸통 · 머리</b>에 커서를 올리고 쏜다. 몹 위치는 플레이어 주변 8방향.</li>
<li>탄마다 <b>명중 여부</b>와 <b>빗나간 거리</b>(몹 콜라이더까지 가장 가까웠던 거리)를 기록 → 위 지도의 실측판.</li>
<li>①②③을 하나씩 켜고 같은 조건으로 다시 재서 비교한다.</li>
</ul></div>

<div class="callout c-amber"><span class="lbl">이 문서의 숫자는 계산값</span>
카메라 오프셋(씬 값), AR 몹 캡슐(반지름 0.265m · 키 1.05m), 탄 박스(폭 0.114m)는 프로젝트에서 읽은 값이다.
총구 위치·몸 회전 보간·캐릭터마다 다른 무기는 반영하지 않았다. 실제 체감은 측정으로 확인해야 한다.</div>

<div class="footer">생성: docs/Combat/tools/aim_miss_gen.py · 근거 코드: CameraController(_delta) · CharacterMovement.RotateToMouse · SpawnProjectiles · BulletController
· 참고: Derek Mattson, "A Second Shot", GDC 2024</div>
</body></html>"""
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(html)
    print("wrote", os.path.abspath(OUT), "maps miss%", [round(p) for p in pcts])


if __name__ == "__main__":
    main()
