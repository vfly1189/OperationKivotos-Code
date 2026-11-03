# -*- coding: utf-8 -*-
"""쿼터뷰 조준 보정 합본(AimFix_Complete.html) 생성기.

이론판(aim_fix_theory_gen) + 쉬운판(aim_fix_easy_gen) + 실무 사례를 한 문서로 합친다.
주제마다 [쉽게] → [자세히] 순서로 붙인다. 숫자는 전부 예시값, 실무 사례는 공개 자료 요약.
"""
import math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from aim_miss_gen import CSS
import aim_fix_theory_gen as th
import aim_fix_easy_gen as ez
from aim_fix_theory_gen import (T, L, box, lines, DEFS, EXTRA_CSS,
                                BLUE, AMBER, GREEN, RED, INK, MUTED, FAINT, LINE,
                                BLUE_S, AMBER_S, GREEN_S, SHADE)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "AimFix_Complete.html")

FULL_CSS = """
.lv{display:inline-block;font-size:8.4pt;font-weight:800;padding:2px 10px;border-radius:10px;margin:8px 0 2px;letter-spacing:.04em}
.lv-e{background:#eef2ff;color:#2b3a8f}.lv-d{background:#fff4ec;color:#8a3708}.lv-p{background:#ebfbee;color:#1f6b2e}
h2{color:#c92a2a}
.src{font-size:8.4pt;line-height:1.55}.src li{margin:2px 0}.src a{color:#2b3a8f;text-decoration:none}
.read{border:1px solid #e3e6ec;border-radius:12px;padding:8px 14px;margin:8px 0;font-size:9.6pt}
"""

EASY = '<div><span class="lv lv-e">쉽게</span></div>'
DETAIL = '<div><span class="lv lv-d">자세히</span></div>'
PRACT = '<div><span class="lv lv-p">실무에선</span></div>'

SIL = ('<ellipse cx="170" cy="120" rx="40" ry="55"/><circle cx="170" cy="52" r="22"/>'
       '<polygon points="138,92 66,148 78,162 146,116"/><polygon points="202,92 266,58 274,72 206,118"/>'
       '<polygon points="266,58 300,14 308,20 276,68"/>'
       '<rect x="146" y="160" width="14" height="38" rx="4"/><rect x="180" y="160" width="14" height="38" rx="4"/>')


# ---------------------------------------------------------------- 실무 1: 이중 프로브 (TLOU2 → 쿼터뷰)
def fig_dual_probe():
    k, gy, cx, H = 32, 215, 40, 4.25
    camy = gy - H * k
    px = cx + 3 * k
    mx = cx + 7 * k
    hm, hc = 1.8, 1.5
    cy = gy - hc * k
    muz = (px + 8, gy - 1.0 * k)
    slope = (cy - camy) / (mx - cx)
    hitx = mx - 14
    hit = (hitx, camy + slope * (hitx - cx))
    s = ['<svg viewBox="0 0 700 250" width="100%">']
    s.append(L(14, gy, 400, gy, INK, 1.6))
    s.append(f'<rect x="{px-7}" y="{gy-38}" width="14" height="38" rx="6" fill="{BLUE}"/>')
    s.append(T(px, gy + 15, "플레이어", "t", "middle", BLUE))
    s.append(f'<rect x="{mx-14}" y="{gy-hm*k}" width="28" height="{hm*k}" rx="11" fill="#dfe4ee" stroke="{FAINT}"/>')
    s.append(T(mx + 20, gy - 8, "몹", "t"))
    s.append(f'<line x1="{cx}" y1="{camy}" x2="{hit[0]:.1f}" y2="{hit[1]:.1f}" stroke="{GREEN}" stroke-opacity=".22" stroke-width="16" stroke-linecap="round"/>')
    s.append(L(cx, camy, hit[0], hit[1], GREEN, 1.6, "5 3"))
    s.append(f'<circle cx="{cx}" cy="{camy}" r="8" fill="{BLUE}"/>' + T(cx + 12, camy - 8, "카메라", "tb", fill=BLUE))
    s.append(L(muz[0], muz[1], hit[0] - 4, hit[1] + 1, BLUE, 2.4, mk="ahb"))
    s.append(f'<circle cx="{hit[0]:.1f}" cy="{hit[1]:.1f}" r="5" fill="{GREEN}"/>')
    s.append(ez.badge(150, 52, "①") + T(164, 56, "카메라 탄 (안 보임)", "tb", fill=GREEN))
    s.append(T(164, 72, "커서 레이를 두껍게 · 몹만 맞음", "s", fill=INK))
    s.append(ez.badge(hit[0] + 26, hit[1] - 30, "②") + T(hit[0] + 40, hit[1] - 26, "맞은 점 P*", "tb", fill=GREEN))
    s.append(ez.badge(186, 236, "③") + T(200, 240, "캐릭터 탄 (보임): 총구 → P*", "tb", fill=BLUE))
    s.append(box(412, 10, 278, 232))
    s.append(lines(428, 36, [("TLOU2 이중 프로브를 쿼터뷰로 옮기면", "tb", None)], 20))
    s.append(T(428, 64, "P* = 구 캐스트(커서 레이, r, 몹만)", "f", fill=GREEN))
    s.append(T(428, 88, "탄 방향 = (P* − 총구) 정규화", "f", fill=BLUE))
    s.append(lines(428, 114, [
        "• 판정은 '플레이어가 보는 화면' 기준",
        "• 캐릭터 탄은 결과를 따라가는 연출",
        "• 원작엔 벽만 검사하는 숨은 레이가 하나 더",
        "• 안 맞으면 → ① 허리 높이 판으로",
        ("한계", "tb", RED),
        "플레이어에게 유리 → 경쟁 게임엔 부적합",
        "(발표자가 직접 밝힌 단점)",
    ], 17))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 실무 2: 부위별 캡슐
def fig_multi_capsule():
    caps = [((150, 95), (190, 95), 26), ((148, 124), (192, 124), 30), ((152, 152), (188, 152), 24),
            ((140, 104), (76, 152), 16), ((202, 104), (266, 68), 16), ((272, 62), (300, 22), 10),
            ((153, 168), (153, 194), 14), ((187, 168), (187, 194), 14)]
    s = ['<svg viewBox="0 0 700 250" width="100%">']
    for i, title in enumerate(("캡슐 하나", "부위별 캡슐 여러 개")):
        dx = -10 + i * 350
        s.append(f'<g transform="translate({dx},0)">')
        s.append(f'<g fill="#eef0f4" stroke="{FAINT}" stroke-width="1">{SIL}</g>')
        if i == 0:
            s.append(f'<rect x="140" y="40" width="60" height="150" rx="30" fill="{BLUE}" fill-opacity=".28" stroke="{BLUE}" stroke-dasharray="5 3"/>')
        else:
            s.append(f'<circle cx="170" cy="52" r="21" fill="{BLUE}" fill-opacity=".28" stroke="{BLUE}"/>')
            for (a, b, w) in caps:
                s.append(f'<line x1="{a[0]}" y1="{a[1]}" x2="{b[0]}" y2="{b[1]}" stroke="{BLUE}" stroke-opacity=".32" stroke-width="{w}" stroke-linecap="round"/>')
        s.append('</g>')
        s.append(T(dx + 185, 222, title, "tb", "middle", BLUE))
        s.append(T(dx + 185, 240, "팔·무기는 판정 밖" if i == 0 else "보이는 모양을 따라간다", "t", "middle", RED if i == 0 else GREEN))
    s.append(L(330, 120, 362, 120, FAINT, 2, mk="ah"))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 실무 3: 무기의 동심원 (Witchfire)
def fig_rings():
    c = (170, 118)
    s = ['<svg viewBox="0 0 700 236" width="100%">']
    s.append(f'<ellipse cx="206" cy="150" rx="24" ry="44" fill="#dfe4ee" stroke="{FAINT}"/>')
    s.append(f'<circle cx="182" cy="102" r="12" fill="#f6d5d5" stroke="{RED}"/>')
    s.append(T(150, 98, "머리", "s", "end", RED))
    s.append(f'<circle cx="{c[0]}" cy="{c[1]}" r="84" fill="none" stroke="{BLUE}" stroke-width="1.4" stroke-dasharray="6 4"/>')
    s.append(f'<circle cx="{c[0]}" cy="{c[1]}" r="48" fill="{BLUE}" fill-opacity=".08" stroke="{BLUE}" stroke-width="1.4"/>')
    s.append(f'<circle cx="{c[0]}" cy="{c[1]}" r="20" fill="none" stroke="{RED}" stroke-width="1.6"/>')
    s.append(f'<path d="M{c[0]-6} {c[1]-6} l12 12 M{c[0]+6} {c[1]-6} l-12 12" stroke="{INK}" stroke-width="2.2"/>')
    s.append(L(c[0] + 3, c[1] - 3, 178, 108, RED, 1.6, mk="ahr"))
    s.append(T(c[0] - 86, 30, "바깥 원 : 적이 닿으면 탄착점을 그쪽으로 당김", "s", fill=BLUE))
    s.append(T(c[0] - 86, 222, "안쪽 원 : 적이 조금이라도 들어오면 명중", "s", fill=BLUE))
    s.append(T(268, 70, "약점 원 : 머리가 들어오면", "s", fill=RED))
    s.append(T(268, 85, "몸을 조준해도 머리로", "s", fill=RED))
    # 거리별 세기
    x0, x1, y0, y1 = 420, 680, 40, 190
    rng = 0.72
    s.append(L(x0, y1, x1, y1, INK, 1.2) + L(x0, y0, x0, y1, INK, 1.2))
    pts = [(x0, y1 - 30), (x0 + rng * (x1 - x0), y0 + 10), (x0 + (rng + 0.08) * (x1 - x0), y1 - 8), (x1, y1 - 4)]
    s.append(th.poly(pts, GREEN, 2.6))
    s.append(L(pts[1][0], y0, pts[1][0], y1, FAINT, 1, "3 3"))
    s.append(T(pts[1][0], y1 + 14, "무기 사거리", "s", "middle", INK))
    s.append(T(x1, y1 + 14, "거리 →", "xs", "end"))
    s.append(T(x0 - 6, y0 + 4, "보정 세기", "xs", "end"))
    s.append(T(x0 + 8, y0 - 12, "멀수록 세지다가 사거리 밖에선 뚝 떨어짐", "s", fill=GREEN))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 실무 4: 픽셀로 고르기 (ID 버퍼)
def fig_idpick():
    s = ['<svg viewBox="0 0 700 176" width="100%">']
    for i, title in enumerate(("플레이어가 보는 화면", "몰래 그리는 ID 화면")):
        ox = 20 + i * 380
        s.append(box(ox, 22, 300, 132, "#fff", LINE, 12))
        s.append(T(ox + 150, 14, title, "tb", "middle"))
        cA = "#dfe4ee" if i == 0 else "#ffd43b"
        cB = "#c9d1e3" if i == 0 else "#74c0fc"
        s.append(f'<rect x="{ox+150}" y="40" width="46" height="66" rx="16" fill="{cB}" stroke="{FAINT if i == 0 else "none"}"/>')
        s.append(f'<rect x="{ox+100}" y="70" width="70" height="76" rx="22" fill="{cA}" stroke="{FAINT if i == 0 else "none"}"/>')
        s.append(f'<polygon points="{ox+168},96 {ox+230},70 {ox+234},80 {ox+170},110" fill="{cA}" stroke="{FAINT if i == 0 else "none"}"/>')
        s.append(f'<path d="M{ox+214} {ox*0+72} l9 9 M{ox+223} 72 l-9 9" stroke="{RED}" stroke-width="2.2"/>')
        if i == 1:
            s.append(T(ox + 135, 128, "ID 1", "tb", "middle"))
            s.append(T(ox + 173, 58, "ID 2", "tb", "middle"))
    s.append(L(326, 88, 394, 88, FAINT, 2, mk="ah"))
    s.append(T(360, 108, "커서 밑", "xs", "middle") + T(360, 120, "한 픽셀 읽기", "xs", "middle"))
    s.append(T(520, 172, "커서 밑 색 = ID 1의 팔 → 실루엣 그대로 정확", "s", "middle", GREEN))
    s.append('</svg>')
    return "\n".join(s)


# ---------------------------------------------------------------- 쿼터뷰로 옮긴 추천 흐름
def fig_pipeline():
    s = ['<svg viewBox="0 0 700 214" width="100%">']
    def node(x, y, w, h, t1, t2, col):
        s.append(box(x, y, w, h, "#fff", col, 11))
        s.append(T(x + w / 2, y + (h / 2 if not t2 else h / 2 - 4), t1, "tb", "middle", col))
        if t2:
            s.append(T(x + w / 2, y + h / 2 + 12, t2, "s", "middle", INK))
    node(8, 20, 92, 44, "커서", None, INK)
    node(128, 14, 170, 56, "카메라 탄", "커서 레이 구 캐스트 · 몹만", GREEN)
    node(332, 14, 170, 56, "맞은 점으로 탄 방향 역산", "(TLOU2 방식)", GREEN)
    node(536, 14, 156, 56, "한 번 잡으면 유지", "③ 자석", BLUE)
    node(128, 128, 170, 56, "화면 반경 안 가까운 몹", "② 화면 좌표", BLUE)
    node(332, 128, 170, 56, "허리 높이 판으로 조준", "① 몹이 없을 때", AMBER)
    node(536, 128, 156, 56, "장판 스킬은 바닥 그대로", "④ 스킬별", AMBER)
    s.append(L(100, 42, 124, 42, FAINT, 1.8, mk="ah"))
    s.append(L(298, 42, 328, 42, GREEN, 1.8, mk="ahg") + T(313, 36, "맞음", "xs", "middle", GREEN))
    s.append(L(502, 42, 532, 42, FAINT, 1.8, mk="ah"))
    s.append(L(213, 70, 213, 124, RED, 1.8, mk="ahr") + T(220, 102, "안 맞음", "xs", fill=RED))
    s.append(f'<path d="M298 156 C312 156 312 60 330 52" fill="none" stroke="{BLUE}" stroke-width="1.6" marker-end="url(#ahb)"/>')
    s.append(T(316, 118, "있음", "xs", "middle", BLUE))
    s.append(L(298, 170, 328, 170, RED, 1.8, mk="ahr") + T(313, 182, "없음", "xs", "middle", RED))
    s.append(T(350, 206, "판정은 마지막에 ⑤ 살짝 넉넉하게 · 큰 몹은 부위별 캡슐로", "s", "middle", INK))
    s.append('</svg>')
    return "\n".join(s)


def main():
    side = th.fig_side()
    top, _ = th.fig_top()
    scale, data = th.fig_scale()
    step, jump_s, jump_b = th.fig_step()
    shape, dB, dS = ez.easy_shape_scale()
    plane, d_plane, d_ground = th.fig_plane()
    hyst, tg1, tg2 = th.fig_hyst()
    at = lambda n, sc: data[n][int(round((sc - 0.5) / 0.01))]
    hi1, hi4 = at("머리 · 높은 카메라", 1.0), at("머리 · 높은 카메라", 4.0)
    rows = "".join(f"<tr><td>{h:.0f}m</td><td>{ez.fmt(h*5/(5-h))}m</td><td>{ez.fmt((h*5/(5-h))/1.25)}배</td></tr>" for h in (1, 2, 3, 4))

    html = f"""<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>조준 보정 합본</title><style>{CSS}{EXTRA_CSS}{ez.EASY_CSS}{FULL_CSS}</style></head><body>
{DEFS}
<div class="kicker">COMBAT · AIM · 합본</div>
<h1 style="color:#1a1c22">커서로 몹을 찍었는데 왜 빗나갈까</h1>
<p class="lead" style="color:#1a1c22">쿼터뷰에서 커서로 조준할 때 생기는 문제, 해결 방법, 실무 사례를 한 문서로 정리했다.</p>
<div class="read"><b>읽는 법</b> — 주제마다 <span class="lv lv-e">쉽게</span>를 먼저 읽고, 식과 정확한 그림이 궁금하면 <span class="lv lv-d">자세히</span>를 본다. 자세히는 건너뛰어도 흐름이 이어진다. 그림 속 숫자는 이해를 돕는 예시값이고, <span class="lv lv-p">실무에선</span>은 공개 자료(GDC 발표·개발사 블로그) 요약이다.</div>

<h2>1. 지금 무슨 일이 일어나나</h2>
{EASY}
<div class="callout c-blue big">카메라에서 커서 쪽으로 <b>레이저</b>를 쏜다고 생각하자. 게임은 이 레이저가 <b>바닥에 찍힌 점</b>을 조준점으로 쓰고, 캐릭터는 그 점을 향해 돌아서 쏜다. 문제는 레이저가 몹을 무시하고 <b>몹 뒤 바닥까지</b> 간다는 것이다.</div>
<div class="fig">{ez.fig_laser("now")}</div>
{DETAIL}
<div class="fig"><div class="figt">밀리는 거리는 닮은 삼각형으로 구한다</div>{side}</div>

<div class="pb"></div>
{EASY}
<div class="callout c-blue big">밀리는 방향은 언제나 <b>카메라 반대쪽(화면 위쪽)</b>이다. 화면 위쪽 몹은 밀려도 같은 줄 위라 맞고, <b>옆에 있는 몹</b>은 쏘는 방향이 틀어져서 빗나간다.</div>
<div class="fig">{ez.fig_topdown()}</div>
{DETAIL}
<div class="fig"><div class="figt">밀린 거리 d가 방향 오차 ε로 바뀐다</div>{top}</div>

<h2 class="pb">2. 몹이 크면 왜 더 심해질까</h2>
{EASY}
<div class="callout c-blue big">큰 몹의 머리처럼 <b>높은 곳</b>을 찍을수록 레이저가 옆으로 눕는다. 누운 레이저는 바닥에 닿기까지 훨씬 멀리 간다. 그래서 키가 조금만 커져도 밀림은 <b>확</b> 커진다.</div>
<div class="fig"><div class="figt">카메라 높이 5m, 몹까지 5m. 몹의 머리 끝을 찍었을 때</div>{ez.fig_sizes()}</div>
<table class="easy"><thead><tr><th>몹 키</th><th>조준점이 밀리는 거리</th><th>1m 몹과 비교</th></tr></thead>
<tbody>{rows}<tr><td>5m</td><td colspan="2">레이저가 바닥에 닿지 않음 → 조준 실패</td></tr></tbody></table>
{DETAIL}
<div class="fig"><div class="figt">밀림 비율은 k = (커서 높이 ÷ 카메라 높이) 하나로 정해진다</div>{th.fig_universal()}</div>
<div class="fig"><div class="figt">몹 크기를 키우면 (옆 몹, 예시값)</div>{scale}</div>
<div class="callout c-amber big"><span class="lbl">정리</span>
<ol class="steps">
<li>키가 2배면 밀림은 <b>2.7배</b>, 3배면 <b>6배</b>. 직선이 아니라 쌍곡선으로 커진다.</li>
<li>큰 몹은 몸도 커서 "완전히 빗나감"보다 <b>"엉뚱한 데로 쏨"</b>으로 느껴진다. 머리 조준 ×1에서 {hi1[2]:.0f}°, ×4에서 {hi4[2]:.0f}° 틀어진다.</li>
<li>몹 키가 카메라 높이에 가까워지면 레이저가 바닥에 닿지 않아 <b>조준 자체가 실패</b>한다. 큰 보스 + 낮은 카메라가 이 경우다.</li>
</ol></div>

<h2 class="pb">3. 가장 쉬운 해결: 레이저가 몹에도 닿게</h2>
{EASY}
<div class="fig">{ez.fig_laser("fixed")}</div>
<div class="callout c-green big">커서가 몹 위에 있으면 바닥까지 가지 않고 <b>몹을 바로 조준</b>한다. 이것만으로 대부분 좋아지고 실제 게임도 쓰는 출발점이다. 하지만 네 가지 문제가 남는다.</div>

<div class="card"><h3>남는 문제 1 · 몹 머리 바로 위를 찍으면 확 튄다</h3>
{EASY}
{ez.fig_laser("edge")}
<div class="txt"><p>몹 위면 몹, 조금만 벗어나면 다시 저 뒤 바닥. <b>원래 문제가 없어진 게 아니라 몹 테두리로 모였을 뿐</b>이다.</p></div>
{DETAIL}
{step}
<div class="txt"><p>튀는 크기 = 몹 꼭대기 높이에서의 원래 밀림 = h·D/(H−h). 작은 몹 {jump_s:.2f}m, 큰 몹 {jump_b:.2f}m. 몹이 클수록 더 크게 튄다.</p></div></div>

<div class="card"><h3>남는 문제 2 · 보이는 모양 ≠ 판정 모양 &nbsp;/&nbsp; 남는 문제 3 · 큰 몹은 "어디를" 찍었나</h3>
{shape}
<div class="txt"><p><b>왼쪽:</b> 게임은 몹을 단순한 캡슐로 판정한다. 팔·무기처럼 캡슐 밖으로 나온 곳을 찍으면 레이저가 다시 바닥으로 빠진다.</p>
<p><b>오른쪽:</b> 작은 몹은 중심을 쏴도 {dS:.0f}° 차이라 상관없다. 큰 보스의 어깨를 찍었는데 중심으로 쏘면 {dB:.0f}° 벗어난다. 차이 = atan(커서가 중심에서 떨어진 거리 ÷ 몹까지 거리).</p></div></div>

<div class="card"><h3>남는 문제 4 · 몹 위를 찍었다고 그 몹을 노린 건 아니다</h3>
{th.fig_ambig()}
<div class="txt"><p>뒤의 몹을 노렸거나 몹 뒤 바닥에 장판을 깔고 싶었을 수도 있다. 커서 아래 몹을 무조건 고르면 <b>"조준을 뺏겼다"</b>는 느낌이 든다.</p></div></div>

<h2 class="pb">4. 남은 문제를 메우는 다섯 가지 방법</h2>

<div class="card"><h3>① 바닥 대신 허리 높이의 투명한 판에 찍기</h3>
{EASY}
<div class="txt"><p>레이저가 바닥이 아니라 <b>총을 쏘는 높이의 판</b>에 찍히게 한다. 밀림이 {d_ground:.1f}m에서 {d_plane:.1f}m로 줄어든다. <span class="good">좋은 점</span> 몹이 없어도 되고 튀지 않는다. <span class="bad">한계</span> 판 높이는 하나라 큰 몹 머리는 여전히 어긋난다.</p></div>
{DETAIL}
{plane}
{PRACT}
<div class="txt"><p>Unity 포럼의 탑다운 슈터 조준 질문에 가장 흔한 답이 "바닥 대신 총구 높이에 Plane을 두고 레이캐스트"다.</p></div></div>

<div class="card"><h3>② 3D 공간 말고 "화면"에서 커서에 가까운 몹 고르기</h3>
{EASY}
<div class="txt"><p>플레이어가 보는 <b>화면 위에서</b> 커서 근처 범위 안의 가장 가까운 몹을 고른다. <span class="good">좋은 점</span> 카메라 각도·몹 키와 무관하고, 큰 몹은 화면에서도 커서 잘 잡힌다. <span class="bad">한계</span> 범위 경계는 남는다 → ③.</p></div>
{DETAIL}
{th.fig_screen()}
{PRACT}
<div class="txt"><p>커서 방향과 적 방향의 각도를 내적으로 비교하는 콘 판정이 흔하다(gamedev.net). Hades는 맞히고 싶을 만한 적을 추린 뒤 조준 방향에 가장 가까운 적을 고르는 것으로 알려져 있다(비공식 역분석). 큰 각도로 꺾여 "조종을 뺏긴다"·"소환물을 보스보다 먼저 고른다"는 불만도 함께 나온다. <b>우선순위 규칙</b>이 품질을 가른다.</p></div></div>

<div class="card"><h3>③ 자석처럼: 붙잡을 땐 깐깐하게, 놓을 땐 느슨하게</h3>
{EASY}
<div class="txt"><p>몹을 잡는 기준과 놓는 기준을 <b>다르게</b> 둔다. 한 번 잡은 몹은 커서가 조금 흔들려도 놓지 않는다. 같은 움직임에서 조준 바뀜이 {tg1}번 → {tg2}번으로 줄었다.</p></div>
{DETAIL}
{hyst}
{PRACT}
<div class="txt"><p>Path of Exile은 마우스를 누르고 있으면 그 대상이 고정돼서 커서를 움직여도 유지된다(포럼). 기준을 두 개 두는 것의 극단적인 형태가 "락온"이다.</p></div></div>

<div class="card"><h3>④ 스킬마다 커서를 다르게 읽기</h3>
{EASY}
{th.fig_skill()}
<div class="txt"><p>총은 몹을, 장판은 바닥 그 자리를, 돌진은 방향만 원한다. <b>스킬 종류로 나눠 읽으면</b> 문제 4(조준 뺏김)가 풀린다.</p></div>
{PRACT}
<div class="txt"><p>Wolcen은 "커서 밑에 뭐가 있나"를 담당하는 타겟팅 시스템을 입력 처리와 따로 둔다. 같은 커서 정보를 스킬마다 다르게 쓰기 위한 구조다.</p></div></div>

<div class="card"><h3>⑤ 조준은 그대로, 과녁을 살짝 크게</h3>
{EASY}
<div class="txt"><p>조준을 고치는 대신 <b>맞았는지 판정을 넉넉하게</b> 본다. <span class="good">좋은 점</span> 작은 오차는 아예 못 느낀다. <span class="bad">한계</span> 너무 크면 안 맞을 걸 맞힌다.</p></div>
{DETAIL}
{th.fig_judge()}
{PRACT}
<div class="txt"><p>TLOU2는 화살이 아깝게 빗나가는 문제를 레이캐스트 → <b>구 캐스트</b>로 바꾸고 반경을 조금 키워 풀었다. Halo·Witchfire의 bullet magnetism은 쏜 순간 탄 방향을 근처 적 쪽으로 살짝 휜다. Witchfire는 "판정을 키우는 것"과는 다르다고 강조한다. 적의 판정은 그대로 두고 보정을 <b>무기</b>에 붙인다.</p></div></div>

<h2 class="pb">5. 실무에서 더 쓰는 방법</h2>
<div class="callout c-blue big">위 다섯 가지는 실제로 쓰인다. 실무에서는 여기에 네 가지를 더 겹친다. 마우스 쿼터뷰 조준만 다룬 공식 자료는 찾지 못했다. 그래서 가장 가까운 사례를 우리 상황에 옮겨 설명한다.</div>

<div class="card"><h3>실무 1 · 판정은 카메라가, 캐릭터 탄은 연출만 (TLOU2 이중 프로브)</h3>
{EASY}
<div class="txt"><p>TLOU2 발표는 3인칭 슈팅의 진짜 문제를 <b>"카메라와 캐릭터가 따로 논다"</b>로 정의한다. 우리 문제도 똑같다. 커서(카메라)는 몹을 보는데 탄은 캐릭터에서 나간다.</p>
<p>해법: <b>보는 쪽(카메라)이 맞았는지 정하고</b>, 캐릭터 탄은 그 결과를 향해 날아가는 연출로 만든다. 쿼터뷰에서는 <b>커서 레이가 곧 카메라 탄</b>이라 그대로 옮길 수 있다.</p></div>
{DETAIL}
{fig_dual_probe()}
<div class="txt"><p>원작 구성: ① 카메라 탄 = 조준점 방향 구 캐스트, 벽 무시 · ② 숨은 레이캐스트 = 카메라 탄을 따라가며 벽만 검사 · ③ 캐릭터 탄 = 카메라 탄의 예상 충돌점에서 역산, 적에게만 충돌. 결론 슬라이드는 <b>"Reticle is King"</b>(화면에 보이는 조준이 곧 결과)이다. 우리 "몹 포함 레이"와 "판정 두께"를 하나로 합친 형태이고, 두께 덕분에 테두리에서 튀는 문제도 줄어든다.</p></div></div>

<div class="card"><h3>실무 2 · 판정을 넓히지 말고 모양을 맞추기</h3>
{EASY}
{fig_multi_capsule()}
<div class="txt"><p>보이는 모양과 판정이 다르면 판정을 키워서 덮지 않는다. <b>판정 모양 자체를 몸에 맞게</b> 여러 개로 쪼갠다.</p></div>
{PRACT}
<div class="txt"><p>TLOU2는 상체 박스 하나를 <b>척추 관절마다 붙인 가로 캡슐 여러 개</b>로 바꿔서 숙이거나 비튼 자세의 이상한 명중·빗나감을 없앴다. Witchfire는 반대로 보정 계산에는 <b>척추 같은 핵심 뼈만</b> 쓴다. 잠깐 든 손 때문에 조준이 끌려가지 않게 하려는 것이다. 맞았는지 볼 땐 정밀하게, 끌어당길 땐 단순하게.</p></div></div>

<div class="card"><h3>실무 3 · 무기에 붙은 원 세 개와 약점 우선 (Witchfire)</h3>
{EASY}
{fig_rings()}
<div class="txt"><p>커서 둘레에 원 세 개를 둔다. 바깥 원에 닿으면 끌어당기고, 안쪽 원에 들어오면 명중이고, <b>약점 원에 머리가 들어오면 머리로</b> 보낸다. 보정 세기는 무기 사거리까지 거리에 따라 커지다가 사거리 밖에선 급히 줄어든다.</p></div>
{DETAIL}
<div class="txt"><p>문제 3("큰 몹은 어디를 노렸나")에 대한 실무 답에 가깝다. 중심으로 끌지도, 찍은 점만 쓰지도 않고 <b>부위에 우선순위</b>를 둔다. 무기마다 원 크기와 세기가 따로 있고, 옆에서 보면 거리 감쇠 때문에 완벽하지 않은 원뿔 모양이 된다.</p></div></div>

<div class="card"><h3>실무 4 · 화면 픽셀로 고르기 (ID 버퍼 피킹)</h3>
{EASY}
{fig_idpick()}
<div class="txt"><p>몹마다 고유한 색으로 <b>보이지 않는 화면을 한 장 더</b> 그리고, 커서 밑 한 픽셀의 색으로 무엇인지 알아낸다. 실루엣 그대로 정확하다. <span class="bad">한계</span> GPU에서 값을 읽어오는 비용과 지연 때문에 에디터(Blender)·RTS 선택에 주로 쓰고, 슈팅 판정에는 잘 쓰지 않는다. 테두리 튐 문제는 그대로 남는다.</p></div></div>

<div class="callout c-amber big"><span class="lbl">우리와는 무관한 것</span>
<b>마찰(friction, 커서 감속)</b> — 적 근처에서 조준 속도를 늦추는 보정. Halo와 Witchfire 모두 <b>컨트롤러 전용</b>이다. 마우스 쿼터뷰에는 해당하지 않는다.</div>

<h2>6. 쿼터뷰에 옮기면: 추천 흐름</h2>
<div class="fig">{fig_pipeline()}</div>
<div class="callout c-green big"><span class="lbl">순서대로 읽기</span>
<ol class="steps">
<li>커서 레이를 <b>몹만 맞는 두꺼운 구 캐스트</b>로 쏜다 (실무 1).</li>
<li>맞으면 그 점을 향해 총구에서 탄 방향을 역산한다. 한 번 잡은 몹은 쉽게 놓지 않는다 (③).</li>
<li>안 맞으면 화면 반경 안의 가까운 몹을 찾는다 (②). 그것도 없으면 허리 높이 판으로 조준한다 (①).</li>
<li>장판 스킬은 이 흐름을 타지 않고 바닥 점을 그대로 쓴다 (④).</li>
<li>판정은 살짝 넉넉하게 (⑤). 큰 보스는 부위별 캡슐(실무 2)과 약점 우선(실무 3)을 더한다.</li>
</ol></div>

<h2 class="pb">7. 한 장 요약</h2>
<div class="fig">{ez.fig_questions()}</div>
<table class="cov">
<thead><tr><th>방법</th><th>바닥 밀림·발산</th><th>테두리 튐</th><th>모양 차이</th><th>큰 몹 어디를</th><th>의도 모호</th></tr></thead>
<tbody>
<tr><td class="k">레이저가 몹에도 닿게</td><td class="p">◐ 몹 위만</td><td class="n">✕ 새로 생김</td><td class="n">✕</td><td class="n">✕ 중심 고정</td><td class="n">✕ 흡착</td></tr>
<tr><td class="k">① 허리 높이 판</td><td class="p">◐ 판 근처만</td><td class="y">● 연속</td><td class="y">● 무관</td><td class="n">✕</td><td class="z">—</td></tr>
<tr><td class="k">② 화면에서 가까운 몹</td><td class="y">●</td><td class="p">◐ 범위 경계</td><td class="y">●</td><td class="p">◐</td><td class="p">◐ 우선순위</td></tr>
<tr><td class="k">③ 자석 (잡기·놓기)</td><td class="z">—</td><td class="y">●</td><td class="z">—</td><td class="z">—</td><td class="z">—</td></tr>
<tr><td class="k">④ 스킬별 해석</td><td class="z">—</td><td class="z">—</td><td class="z">—</td><td class="z">—</td><td class="y">●</td></tr>
<tr><td class="k">⑤ 과녁 살짝 크게</td><td class="p">◐ 작은 오차만</td><td class="z">—</td><td class="p">◐</td><td class="z">—</td><td class="z">—</td></tr>
<tr><td class="k">실무 1 · 카메라 판정 + 역산</td><td class="y">●</td><td class="p">◐ 두께만큼</td><td class="p">◐ 두께만큼</td><td class="y">● 맞은 점</td><td class="n">✕</td></tr>
<tr><td class="k">실무 2 · 부위별 캡슐</td><td class="z">—</td><td class="z">—</td><td class="y">●</td><td class="p">◐</td><td class="z">—</td></tr>
<tr><td class="k">실무 3 · 원 세 개 + 약점</td><td class="z">—</td><td class="p">◐</td><td class="z">—</td><td class="y">●</td><td class="z">—</td></tr>
<tr><td class="k">실무 4 · 픽셀 피킹</td><td class="z">—</td><td class="n">✕</td><td class="y">●</td><td class="z">—</td><td class="z">—</td></tr>
</tbody></table>
<p class="lead" style="font-size:8.6pt;color:#1a1c22">● 해결 &nbsp; ◐ 일부 &nbsp; ✕ 안 됨·악화 &nbsp; — 해당 없음</p>

<div class="callout c-green big"><span class="lbl">결론</span>
<ol class="steps">
<li>"레이저가 몹에도 닿게"는 좋은 출발점이지만 문제를 <b>몹 테두리로 모을 뿐</b>이다.</li>
<li>한 방법으로 다 막히는 칸은 없다. 실무는 <b>여러 개를 겹친다</b>.</li>
<li>쿼터뷰에서 가장 설득력 있는 축은 <b>"판정은 커서(카메라)가, 탄은 연출만"</b>(TLOU2)이다. 커서 레이가 곧 카메라 탄이라 구조를 그대로 옮길 수 있다.</li>
</ol></div>

<h2 class="pb">출처</h2>
<ol class="src">
<li>Derek Mattson, "A Second Shot: Improving Ranged Combat in The Last of Us Part II", GDC 2024 — <a href="https://www.gdcvault.com/play/1034215/A-Second-Shot-Improving-Ranged">GDC Vault</a> · <a href="https://media.gdcvault.com/gdc2024/Slides/GDC+slide+presentations/Mattson_Derek_ASecondShot_Notes.pdf">발표 노트 PDF</a> (이중 프로브, 구 캐스트, 척추 캡슐, Reticle is King)</li>
<li>The Astronauts, "Witchfire aim assist" — <a href="https://www.theastronauts.com/2019/02/witchfire-aim-assist">theastronauts.com</a> (원 세 개, 약점 우선, 거리 감쇠, 핵심 뼈만 검사, 마찰은 컨트롤러 전용)</li>
<li>Halopedia, "Aim assist" · "Magnetism" — <a href="https://www.halopedia.org/Aim_assist">Aim assist</a> · <a href="https://halopedia.org/Magnetism">Magnetism</a> (탄 방향 보정, 감속은 컨트롤러 전용)</li>
<li>Unity Discussions — <a href="https://discussions.unity.com/t/mouse-aim-offset-correction-in-a-top-perspective-3d-game-with-2-dimensional-xz-aim/729892">Mouse aim offset correction in a top-perspective 3D game</a> · <a href="https://discussions.unity.com/t/3d-top-down-shooter-aiming-issue/860531">3D Top Down Shooter Aiming Issue</a> (총구 높이 평면)</li>
<li>gamedev.net — <a href="https://gamedev.net/forums/topic/705028-aim-assist-and-bullets-correction">aim assist and bullets correction</a> (콘 판정, 내적)</li>
<li>Steam 커뮤니티 — <a href="https://steamcommunity.com/app/1145360/discussions/0/2261312148314172223">Hades targeting 토론</a> (비공식, 우선순위 불만)</li>
<li>Path of Exile 포럼 — <a href="https://pathofexile.com/forum/view-thread/1118912">Click-and-hold monster targeting</a> (누르고 있으면 대상 고정)</li>
<li>Wolcen Update #58 — <a href="https://gamebanshee.com/kd89x">gamebanshee.com</a> (커서 밑 감지를 입력과 분리)</li>
<li>Blender GPU select — <a href="https://projects.blender.org/archive/blender-archive/src/commit/1bbf1ed03c081e61f5c76688a46d12e92da2856f/source/blender/gpu/GPU_select.h">GPU_select.h</a> · Apple Developer Forums <a href="https://developer.apple.com/forums/thread/68587">Picking with Metal</a> (ID·깊이 버퍼 피킹)</li>
</ol>

<div class="footer">쿼터뷰 조준 보정 합본 · 이론판(AimFix_Theory) + 쉬운판(AimFix_Easy) + 실무 사례 · 그림 숫자는 예시값(프로젝트 실측 아님) · 생성: docs/Combat/tools/aim_fix_full_gen.py</div>
</body></html>"""
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(html)
    print("wrote", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
