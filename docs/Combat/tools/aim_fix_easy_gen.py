# -*- coding: utf-8 -*-
"""쿼터뷰 조준 보정 — 쉬운 설명판(AimFix_Easy.html) 생성기.

AimFix_Theory 와 같은 내용을 식 없이 그림과 풀어 쓴 문장으로 다시 정리한다.
보정 방식 그림은 이론판 함수에서 오른쪽 수식 상자만 떼어 내고 라벨을 쉬운 말로 바꿔 재사용한다.
숫자는 전부 예시값.
"""
import math, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from aim_miss_gen import CSS
import aim_fix_theory_gen as th
from aim_fix_theory_gen import (T, L, box, poly, DEFS, EXTRA_CSS,
                                BLUE, AMBER, GREEN, RED, INK, MUTED, FAINT, LINE,
                                BLUE_S, AMBER_S, GREEN_S, SHADE)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "AimFix_Easy.html")

EASY_CSS = """
.card{border:1px solid #e3e6ec;border-radius:14px;padding:10px 14px 8px;margin:10px 0;break-inside:avoid;background:#fff}
.card h3{color:#c92a2a;font-size:11.6pt;margin:0 0 6px;font-weight:800}
.row{display:flex;gap:16px;align-items:center}
.row .pic{flex:0 0 54%}
.row .txt{flex:1;font-size:9.8pt;line-height:1.6}
.txt p{margin:0 0 5px}
.txt .good{color:#2b8a3e;font-weight:800}.txt .bad{color:#c92a2a;font-weight:800}
.big{font-size:10.6pt;line-height:1.7}
table.easy td,table.easy th{text-align:center}
"""


def fmt(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


def badge(x, y, n):
    return (f'<circle cx="{x}" cy="{y}" r="10" fill="{RED}"/>'
            f'<text x="{x}" y="{y+4}" class="t" text-anchor="middle" fill="#fff">{n}</text>')


def strip_box(svg, x, width):
    """이론판 그림에서 오른쪽 수식 상자(x 위치의 회색 상자부터 끝까지)를 떼고 폭을 줄인다."""
    svg = re.sub(rf'<rect x="{x}" y="\d+" width="\d+" height="\d+" rx="12" fill="{SHADE}".*(?=</svg>)', "", svg, flags=re.S)
    return re.sub(r'viewBox="0 0 700 (\d+)"', rf'viewBox="0 0 {width} \1"', svg, count=1)


# ---------------------------------------------------------------- 옆에서 본 레이저 (mode: now / fixed / edge)
def fig_laser(mode="now"):
    k, gy, cx = 40, 205, 50
    H, D = 4.25, 7.0
    camy = gy - H * k
    mx = cx + D * k
    hm = 1.8
    def ground(h): return mx + h * D / (H - h) * k
    s = [f'<svg viewBox="0 0 700 {230 if mode == "fixed" else 250}" width="100%">']
    s.append(L(18, gy, 690, gy, INK, 1.6))
    s.append(f'<rect x="{cx+3*k-8}" y="{gy-40}" width="16" height="40" rx="7" fill="{BLUE}"/>')
    s.append(T(cx + 3 * k, gy + 16, "플레이어", "t", "middle", BLUE))
    s.append(f'<rect x="{mx-16}" y="{gy-hm*k}" width="32" height="{hm*k}" rx="12" fill="#dfe4ee" stroke="{FAINT}"/>')
    s.append(T(mx, gy + 16, "몹", "t", "middle"))
    s.append(f'<circle cx="{cx}" cy="{camy}" r="9" fill="{BLUE}"/>')
    s.append(T(cx + 16, camy - 4, "카메라", "tb", fill=BLUE))
    if mode == "now":
        h = 1.5
        cy, gx = gy - h * k, ground(h)
        s.append(L(cx, camy, mx, cy, AMBER, 2.4))
        s.append(L(mx, cy, gx, gy, AMBER, 2.4, "6 4"))
        s.append(f'<circle cx="{mx}" cy="{cy}" r="5" fill="{AMBER}"/>')
        s.append(f'<circle cx="{gx:.1f}" cy="{gy}" r="7" fill="{AMBER}"/>')
        s.append(badge(mx + 30, cy - 34, "①") + T(mx + 46, cy - 30, "커서가 몹을 가리킨다", "tb"))
        s.append(badge(mx + 92, cy + 2, "②") + T(mx + 108, cy + 6, "레이저는 몹을 그냥 통과한다", "tb"))
        s.append(badge(gx + 18, gy - 28, "③") + T(gx + 34, gy - 24, "바닥에 찍힌 이 점을 향해 쏜다", "tb"))
        s.append(L(mx, 224, gx - 4, 224, AMBER, 2.2, mk="aha"))
        s.append(T((mx + gx) / 2, 240, f"몹보다 {(gx-mx)/k:.1f}m 뒤로 밀림", "tb", "middle", AMBER))
    elif mode == "fixed":
        h = 1.5
        cy, gx = gy - h * k, ground(h)
        s.append(L(cx, camy, mx, cy, GREEN, 2.6))
        s.append(L(mx, cy, gx, gy, FAINT, 1.4, "3 4"))
        s.append(f'<circle cx="{gx:.1f}" cy="{gy}" r="5" fill="none" stroke="{FAINT}" stroke-width="1.5"/>')
        s.append(T(gx, gy - 12, "예전 조준점", "t", "middle", MUTED))
        s.append(f'<circle cx="{mx}" cy="{cy}" r="7" fill="{GREEN}"/>')
        s.append(T(mx + 24, cy - 26, "레이저가 몹에 닿으면 멈춘다", "tb", fill=GREEN))
        s.append(T(mx + 24, cy - 10, "→ 몹을 그대로 조준", "tb", fill=GREEN))
    else:  # edge
        h1, h2 = 1.7, 1.95
        y1, y2, g2 = gy - h1 * k, gy - h2 * k, ground(h2)
        s.append(L(cx, camy, mx, y1, GREEN, 2.4))
        s.append(f'<circle cx="{mx}" cy="{y1}" r="6" fill="{GREEN}"/>')
        s.append(L(cx, camy, g2, gy, AMBER, 2.4))
        s.append(f'<circle cx="{g2:.1f}" cy="{gy}" r="7" fill="{AMBER}"/>')
        s.append(T(mx - 24, y1 + 30, "머리를 찍으면 → 몹", "tb", "end", GREEN))
        s.append(T(690, 148, "머리 살짝 위를 찍으면 → 저 멀리 바닥", "tb", "end", AMBER))
        s.append(L(mx, 230, g2 - 4, 230, AMBER, 2.2, mk="aha"))
        s.append(T((mx + g2) / 2, 246, f"커서는 조금 움직였는데 조준점은 {(g2-mx)/k:.1f}m 튄다", "tb", "middle", AMBER))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 위에서 본: 위쪽 몹 vs 옆 몹
def fig_topdown():
    s = ['<svg viewBox="0 0 700 236" width="100%">']
    for i, (title, ok) in enumerate((("화면 위쪽에 있는 몹", True), ("옆에 있는 몹", False))):
        ox = 8 + i * 348
        s.append(box(ox, 8, 336, 220, "#fff", LINE, 14))
        s.append(T(ox + 168, 30, title, "tb", "middle"))
        s.append(T(ox + 168, 220, "▼ 카메라는 이쪽 (화면 아래)", "s", "middle", INK))
        if ok:
            P, M, G = (ox + 168, 186), (ox + 168, 110), (ox + 168, 62)
            end = (ox + 168, 46)
        else:
            P, M, G = (ox + 70, 160), (ox + 230, 160), (ox + 230, 92)
            ux, uy = G[0] - P[0], G[1] - P[1]; n = math.hypot(ux, uy)
            end = (P[0] + 250 * ux / n, P[1] + 250 * uy / n)
        s.append(f'<circle cx="{M[0]}" cy="{M[1]}" r="16" fill="#dfe4ee" stroke="{FAINT}"/>')
        s.append(T(M[0] + (0 if ok else 0), M[1] + 32 if not ok else M[1] + 4, "몹", "t", "middle") if not ok else T(M[0] - 24, M[1] + 4, "몹", "t", "end"))
        s.append(L(M[0], M[1] - 17, G[0], G[1] + 7, AMBER, 2.0, mk="aha"))
        s.append(T(G[0] + 12, (M[1] + G[1]) / 2 + 4, "밀림", "t", fill=AMBER))
        s.append(f'<circle cx="{G[0]}" cy="{G[1]}" r="5" fill="{AMBER}"/>')
        s.append(L(P[0], P[1], end[0], end[1], GREEN if ok else RED, 2.4, mk="ahg" if ok else "ahr"))
        s.append(f'<circle cx="{P[0]}" cy="{P[1]}" r="10" fill="{BLUE}"/>')
        s.append(T(P[0], P[1] + 24, "나", "tb", "middle", BLUE) if not ok else T(P[0] + 16, P[1] + 4, "나", "tb", fill=BLUE))
        if ok:
            s.append(T(ox + 30, 120, "밀려도 같은 줄 위", "tb", fill=GREEN))
            s.append(T(ox + 30, 138, "→ 맞는다 ✓", "tb", fill=GREEN))
        else:
            s.append(T(ox + 200, 60, "줄이 틀어져", "tb", "end", RED))
            s.append(T(ox + 200, 78, "→ 몹 위로 지나감 ✕", "tb", "end", RED))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 몹 키 1·2·3m
def fig_sizes():
    k, gy, H, D = 16, 176, 5.0, 5.0
    s = ['<svg viewBox="0 0 700 214" width="100%">']
    for i, hm in enumerate((1.0, 2.0, 3.0)):
        ox = 8 + i * 232
        cx = ox + 14
        camy = gy - H * k
        mx = cx + D * k
        d = hm * D / (H - hm)
        gx = mx + d * k
        s.append(box(ox, 8, 224, 200, "#fff", LINE, 14))
        s.append(T(ox + 112, 30, f"몹 키 {hm:.0f}m", "tb", "middle"))
        s.append(L(ox + 6, gy, ox + 218, gy, INK, 1.4))
        s.append(f'<rect x="{mx-8}" y="{gy-hm*k}" width="16" height="{hm*k}" rx="6" fill="#dfe4ee" stroke="{FAINT}"/>')
        s.append(f'<circle cx="{cx}" cy="{camy}" r="6" fill="{BLUE}"/>')
        s.append(L(cx, camy, gx, gy, AMBER, 2.0))
        s.append(f'<circle cx="{mx}" cy="{gy-hm*k}" r="3.5" fill="{AMBER}"/>')
        s.append(f'<circle cx="{gx:.1f}" cy="{gy}" r="5" fill="{AMBER}"/>')
        s.append(L(mx, 188, gx - 3, 188, AMBER, 2.2, mk="aha"))
        s.append(T(ox + 112, 203, f"밀림 {fmt(d)}m", "tb", "middle", AMBER))
    s.append(T(30, 100, "카메라", "xs", fill=BLUE))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 정리: 질문 세 개
def fig_questions():
    cols = [("누구를 노렸나?", BLUE, ["레이저가 몹에도 닿게", "② 화면에서 가까운 몹", "③ 자석처럼 붙잡기", "④ 스킬마다 다르게"]),
            ("어디로 쏘나?", AMBER, ["① 허리 높이 판", "작은 몹은 중심, 큰 몹은 찍은 곳"]),
            ("맞았나?", GREEN, ["⑤ 과녁 살짝 크게"])]
    s = ['<svg viewBox="0 0 700 168" width="100%">']
    w, gap = 204, 32
    for i, (q, col, chips) in enumerate(cols):
        x = 8 + i * (w + gap)
        s.append(box(x, 8, w, 154, "#fff", col, 14))
        s.append(T(x + w / 2, 36, q, "tb", "middle", col))
        for j, c in enumerate(chips):
            yy = 52 + j * 26
            s.append(box(x + 12, yy, w - 24, 21, SHADE, "none", 10))
            s.append(T(x + w / 2, yy + 15, c, "t", "middle"))
        if i < 2:
            s.append(L(x + w + 4, 84, x + w + gap - 6, 84, FAINT, 1.8, mk="ah"))
    s.append('</svg>')
    return "\n".join(s)


def easy_shape_scale():
    svg, dB, dS = th.fig_shape_scale()
    svg = svg.replace("Δθ ≈ atan( 커서 오프셋 / 거리 )", "초록 = 노린 방향 · 주황 = 중심으로 쏜 방향")
    svg = svg.replace(f"Δθ ≈ {dB:.0f}°", f"{dB:.0f}° 차이")
    svg = svg.replace(f"작은 몹 Δθ ≈ {dS:.0f}°", f"작은 몹은 {dS:.0f}° (거의 같음)")
    svg = svg.replace("큰 몹: 중심으로 끌면 노린 부위를 지운다", "큰 몹은 중심으로 쏘면 노린 곳을 놓친다")
    svg = svg.replace("보이지만 콜라이더 밖 → 바닥으로 빠짐 (원래 문제 재발)", "보이지만 판정 밖 → 레이저가 바닥으로 빠짐")
    svg = svg.replace("콜라이더 안 → 레이가 몹을 맞춤", "판정 안 → 레이저가 몹에 닿음")
    svg = svg.replace("콜라이더(캡슐)", "판정 캡슐")
    return svg, dB, dS


def main():
    plane, d_plane, d_ground = th.fig_plane()
    plane = strip_box(plane, 412, 400).replace("조준 평면 y = a", "허리 높이 투명 판").replace('"tb" text-anchor="middle" >a<', '"tb" text-anchor="middle" ><')
    plane = plane.replace(">a</text>", "></text>")
    screen = strip_box(th.fig_screen(), 412, 400).replace(">점수 ", ">거리 ").replace("선택 반경 ρ", "고르는 범위").replace(">화면<", ">플레이어가 보는 화면<")
    hyst, tg1, tg2 = th.fig_hyst()
    hyst = strip_box(hyst, 462, 452)
    for a, b in ((">점수<", ">거리<"), (">ρ_in<", ">잡기<"), (">ρ_out<", ">놓기<"), (">ρ<", ">기준<"),
                 (">임계값 1개<", ">기준 1개<"), (">임계값 2개<", ">기준 2개<"),
                 ("칠해진 칸 = 타겟을 잡고 있는 상태", "칠해진 칸 = 몹을 붙잡고 있는 시간")):
        hyst = hyst.replace(a, b)
    judge = strip_box(th.fig_judge(), 412, 400)
    for a, b in ((">r몹 + r탄 + δ<", ">넉넉한 과녁<"), (">r몹 + r탄<", ">원래 과녁<"), (">e<", "><"),
                 ("얇은 판정이면 빗나감, δ만큼 두꺼우면 명중", "원래 과녁이면 빗나감 · 넉넉하면 맞음")):
        judge = judge.replace(a, b)
    shape, dB, dS = easy_shape_scale()
    rows = "".join(f"<tr><td>{h:.0f}m</td><td>{fmt(h*5/(5-h))}m</td><td>{fmt((h*5/(5-h))/1.25)}배</td></tr>" for h in (1, 2, 3, 4))

    pic = lambda svg: f'<div class="pic">{svg}</div>'
    html = f"""<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>조준 보정 쉬운 설명</title><style>{CSS}{EXTRA_CSS}{EASY_CSS}</style></head><body>
{DEFS}
<div class="kicker">COMBAT · AIM · 쉬운 설명판</div>
<h1>커서로 몹을 찍었는데 왜 빗나갈까</h1>
<p class="lead" style="color:#1a1c22">식 없이 그림으로 풀었다. 숫자는 이해를 돕기 위한 예시값이다.</p>

<h2>1. 지금 무슨 일이 일어나나</h2>
<div class="callout c-blue big"><span class="lbl">레이저 포인터로 생각하자</span>
카메라에서 커서 쪽으로 레이저를 쏜다고 생각하면 된다. 게임은 이 레이저가 <b>바닥에 찍힌 점</b>을 조준점으로 쓰고, 캐릭터는 그 점을 향해 돌아서 쏜다.</div>

<div class="fig">{fig_laser("now")}</div>

<div class="callout c-amber big">문제는 <b>②번</b>이다. 레이저가 몹을 무시하고 몹 뒤의 바닥까지 가 버린다. 그래서 조준점이 몹보다 <b>뒤로 밀린다</b>.</div>

<div class="fig"><div class="figt">그럼 왜 어떤 몹은 맞고 어떤 몹은 빗나갈까 (위에서 본 그림)</div>{fig_topdown()}</div>

<div class="callout c-amber big">밀리는 방향은 언제나 <b>카메라 반대쪽(화면 위쪽)</b>이다.
화면 위쪽에 있는 몹은 밀려도 같은 줄 위라서 맞는다. 옆에 있는 몹은 밀린 만큼 쏘는 방향이 틀어져서 몹 옆으로 지나간다.</div>

<h2 class="pb">2. 몹이 크면 왜 더 심해질까</h2>
<div class="fig"><div class="figt">카메라 높이 5m, 몹까지 5m. 몹의 머리 끝을 찍었을 때</div>{fig_sizes()}</div>

<div class="callout c-blue big">커서가 높은 곳을 찍을수록(= 큰 몹의 머리) 레이저가 <b>옆으로 눕는다</b>.
누운 레이저는 바닥에 닿기까지 훨씬 멀리 간다. 그래서 몹 키가 조금만 커져도 밀림은 <b>확</b> 커진다.</div>

<table class="easy"><thead><tr><th>몹 키</th><th>조준점이 밀리는 거리</th><th>1m 몹과 비교</th></tr></thead>
<tbody>{rows}<tr><td>5m</td><td colspan="2">레이저가 바닥에 닿지 않음 → 조준 실패</td></tr></tbody></table>

<div class="callout c-amber big"><span class="lbl">정리</span>
<ol class="steps">
<li>키가 2배면 밀림은 2배가 아니라 <b>2.7배</b>, 3배면 <b>6배</b>로 커진다.</li>
<li>몹 키가 카메라 높이에 가까워지면 레이저가 바닥에 아예 닿지 않는다. 큰 보스 + 낮은 카메라가 딱 이 경우다.</li>
<li>다만 큰 몹은 몸도 커서 "완전히 빗나간다"기보다 <b>"엉뚱한 데로 쏜다"</b>로 느껴진다.</li>
</ol></div>

<div style="break-inside:avoid"><h2>3. 가장 쉬운 해결: 레이저가 몹에도 닿게 하기</h2>
<div class="fig">{fig_laser("fixed")}</div>
<div class="callout c-green big">커서가 몹 위에 있으면 바닥까지 가지 않고 <b>몹을 바로 조준</b>한다. 이것만으로 대부분 좋아지고, 실제 게임에서도 쓰는 출발점이다. 하지만 아래 네 가지 문제가 남는다.</div></div>

<div class="card"><h3>남는 문제 1 · 몹 머리 바로 위를 찍으면 확 튄다</h3>
{fig_laser("edge")}
<div class="txt"><p>몹 위면 몹을 조준하고, 몹을 조금이라도 벗어나면 다시 저 뒤 바닥을 조준한다. <b>원래 문제가 없어진 게 아니라 몹 테두리로 모였을 뿐</b>이다. 몹이 움직이면 조준이 깜빡거린다.</p></div></div>

<div class="card"><h3>남는 문제 2 · 보이는 모양 ≠ 판정 모양 &nbsp;/&nbsp; 남는 문제 3 · 큰 몹은 "어디를" 찍었나</h3>
{shape}
<div class="txt"><p><b>왼쪽:</b> 게임은 몹을 단순한 캡슐로 판정한다. 팔이나 무기처럼 캡슐 밖으로 나온 곳을 찍으면 레이저가 다시 바닥으로 빠진다.</p>
<p><b>오른쪽:</b> 작은 몹은 중심을 쏴도 상관없다. 하지만 큰 보스의 어깨를 찍었는데 중심으로 쏘면 노린 곳에서 {dB:.0f}°나 벗어난다.</p></div></div>

<div class="card"><h3>남는 문제 4 · 몹 위를 찍었다고 그 몹을 노린 건 아니다</h3>
{th.fig_ambig()}
<div class="txt"><p>뒤에 있는 몹을 노렸을 수도 있고, 몹 뒤 바닥에 장판을 깔고 싶었을 수도 있다. 커서 아래 몹을 무조건 고르면 <b>"조준을 뺏겼다"</b>는 느낌이 든다.</p></div></div>

<h2 class="pb">4. 남은 문제를 메우는 다섯 가지 방법</h2>

<div class="card"><h3>① 바닥 대신 허리 높이의 투명한 판에 찍기</h3>
<div class="row">{pic(plane)}<div class="txt">
<p>레이저가 바닥이 아니라 <b>총을 쏘는 높이의 판</b>에 찍히게 한다. 밀림이 {d_ground:.1f}m에서 {d_plane:.1f}m로 줄어든다.</p>
<p><span class="good">좋은 점</span> 몹이 없어도 되고, 테두리에서 튀지 않는다.</p>
<p><span class="bad">한계</span> 판 높이는 하나뿐이라 큰 몹 머리처럼 판에서 먼 곳은 여전히 어긋난다.</p></div></div></div>

<div class="card"><h3>② 3D 공간 말고 "화면"에서 커서에 가까운 몹 고르기</h3>
<div class="row">{pic(screen)}<div class="txt">
<p>플레이어가 보는 <b>화면 위에서</b> 커서 근처 범위 안에 있는 몹 중 가장 가까운 몹을 고른다.</p>
<p><span class="good">좋은 점</span> 카메라 각도나 몹 키와 상관없다. 큰 몹은 화면에서도 크게 보여서 자연히 잘 잡힌다.</p>
<p><span class="bad">한계</span> 고르는 범위의 경계는 여전히 있다 → ③으로 보완.</p></div></div></div>

<div class="card"><h3>③ 자석처럼: 붙잡을 땐 깐깐하게, 놓을 땐 느슨하게</h3>
<div class="row">{pic(hyst)}<div class="txt">
<p>몹을 잡는 기준과 놓는 기준을 <b>다르게</b> 둔다. 한 번 잡은 몹은 커서가 조금 흔들려도 놓지 않는다.</p>
<p><span class="good">좋은 점</span> 같은 움직임에서 조준 바뀜이 {tg1}번 → {tg2}번으로 줄었다. 테두리 깜빡임이 사라진다.</p></div></div></div>

<div class="card"><h3>④ 스킬마다 커서를 다르게 읽기</h3>
{th.fig_skill()}
<div class="txt"><p>커서 하나에 원하는 게 여러 개다. 총은 몹을 노리고, 장판은 바닥 그 자리를 원하고, 돌진은 방향만 필요하다. <b>스킬 종류로 나눠 읽으면</b> 문제 4(조준 뺏김)가 풀린다.</p></div></div>

<div class="card"><h3>⑤ 조준은 그대로, 과녁을 살짝 크게</h3>
<div class="row">{pic(judge)}<div class="txt">
<p>조준을 고치는 대신 <b>맞았는지 판정을 넉넉하게</b> 본다. "쏘는 순간 커서가 적 위에 있었으면 맞은 걸로" 하는 방식도 있다.</p>
<p><span class="good">좋은 점</span> 작은 오차는 플레이어가 아예 못 느낀다.</p>
<p><span class="bad">한계</span> 너무 크게 하면 안 맞을 걸 맞힌다. 공정함과 손맛 사이의 줄타기.</p></div></div></div>

<h2>5. 한 장 요약: 결국 질문은 세 개</h2>
<div class="fig">{fig_questions()}</div>

<div class="callout c-green big"><span class="lbl">보통은 이렇게 섞어 쓴다</span>
<ol class="steps">
<li>화면에서 커서 근처 몹을 고른다 <b>(②)</b></li>
<li>한 번 잡은 몹은 쉽게 놓지 않는다 <b>(③)</b></li>
<li>근처에 몹이 없으면 허리 높이 판으로 조준한다 <b>(①)</b></li>
<li>맞았는지는 살짝 넉넉하게 본다 <b>(⑤)</b></li>
<li>장판 스킬은 바닥 그대로 쓴다 <b>(④)</b></li>
</ol>
"레이저가 몹에도 닿게"는 1번의 가장 단순한 버전이다. 좋은 출발점이지만 이것만으로는 테두리 튐 · 모양 차이 · 큰 몹 · 의도 문제가 남는다.</div>

<div class="footer">조준 보정 쉬운 설명판 · 자세한 식은 AimFix_Theory.pdf · 생성: docs/Combat/tools/aim_fix_easy_gen.py</div>
</body></html>"""
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(html)
    print("wrote", os.path.abspath(OUT), "plane", round(d_plane, 2), round(d_ground, 2), "toggles", tg1, tg2, "dB", round(dB, 1))


if __name__ == "__main__":
    main()
