# LOD_Result.html 생성 — python -B gen.py (이 폴더에서) → docs/FieldEntity/LOD_Result.html
#  갱신 LOD 결과: 밀도 ×1 · ×2 · ×4 · ×8 × LOD 끔/켬 (개발 빌드 2026-10-13 · 조건마다 1회) + ×4 첫 시도(뼈 계산 생략) 비교.
#  PDF: Chrome headless로 인쇄 (입출력 모두 Windows 절대경로)
import sys, os, io, re, bisect, contextlib, statistics as st, importlib.util
from collections import defaultdict
sys.dont_write_bytecode = True
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..')).replace(os.sep, '/')
DEV = os.environ.get('HARNESS_DEV_LOGS', 'C:/Users/c/Desktop/테스트/MetricsLogs')
spec = importlib.util.spec_from_file_location('af', os.path.join(HERE, '..', 'analyze_field.py'))
af = importlib.util.module_from_spec(spec); spec.loader.exec_module(af)

SWEEP = {  # 밀도: (LOD 끔, LOD 켬) — 같은 빌드 00:41
    1: ('field_Distance_20261013_005433', 'field_Distance_lod_20261013_005946'),
    2: ('field_Distance_x2_20261013_010459', 'field_Distance_x2_lod_20261013_011012'),
    4: ('field_Distance_x4_20261013_011525', 'field_Distance_x4_lod_20261013_012038'),
    8: ('field_Distance_x8_20261013_012551', 'field_Distance_x8_lod_20261013_013104'),
}
TRY1 = ('field_Distance_x4_20261013_002117', 'field_Distance_x4_lod_20261013_002630')   # 첫 시도: Animator 뼈 계산만 생략
TRY2 = ('field_Distance_x4_20261013_004110', 'field_Distance_x4_lod_20261013_004623')   # 바꾼 뒤: Animator 끔
BUDGET = 1000 / 144
DENS = list(SWEEP)


def load(stem):
    with contextlib.redirect_stdout(io.StringIO()):
        d = af.load(f'{DEV}/{stem}_frames.csv', None)
        s = af.summarize(d)
    return d, s


def fit1(fr):
    """스폰 · 회수 없는 프레임 → 생존 몹 20마리 묶음 중앙값 → 프레임 수 가중 직선. (고정 ms, 마리당 μs)"""
    bins = {}
    for r in fr:
        if r['spawns'] != '0' or r['recalls'] != '0': continue
        bins.setdefault(int(r['live']) // 20 * 20, []).append(float(r['dtMs']))
    pts = [(b + 10, st.median(v), len(v)) for b, v in sorted(bins.items()) if len(v) >= 200]
    n = sum(w for *_, w in pts); mx = sum(x * w for x, _, w in pts) / n; my = sum(y * w for _, y, w in pts) / n
    k = sum(w * (x - mx) * (y - my) for x, y, w in pts) / sum(w * (x - mx) ** 2 for x, _, w in pts)
    return my - k * mx, k * 1000


def time_pct(fr):
    """프레임이 화면에 머문 시간으로 가중한 백분위 + 6.94ms 넘는 프레임에 쓴 시간 비율."""
    dts = sorted(float(r['dtMs']) for r in fr)
    T, acc, p = sum(dts), 0.0, {}
    for x in dts:
        acc += x
        for q in (50, 99):
            if q not in p and acc >= T * q / 100: p[q] = x
    return p[50], p[99], sum(x for x in dts if x > BUDGET) / T * 100


def series(fr):
    t, out = 0.0, []
    for r in fr:
        out.append((t, r)); t += float(r['dtMs']) / 1000
    return out


def reg2(rows):
    """dt = 고정 + a × 나머지 몹 + b × 줄임 대상 몹 (두 몹 수 10마리 칸 중앙값, 칸 프레임 수 가중)"""
    b = defaultdict(list)
    for f, rd, dd in rows: b[(f // 10, rd // 10)].append((f, rd, dd))
    X, Y, W = [], [], []
    for v in b.values():
        if len(v) < 100: continue
        a = np.array(v); X.append([1, np.median(a[:, 0]), np.median(a[:, 1])]); Y.append(np.median(a[:, 2])); W.append(len(v))
    X, Y, W = np.array(X), np.array(Y), np.sqrt(np.array(W))
    c = np.linalg.lstsq(X * W[:, None], Y * W, rcond=None)[0]
    return c[1] * 1000, c[2] * 1000


def pair(off_stem, on_stem):
    out = {}
    for key, stem in (('off', off_stem), ('on', on_stem)):
        d, s = load(stem)
        fr = d['frames']
        c, k = fit1(fr)
        p50, p99, over = time_pct(fr)
        secs = sum(float(r['dtMs']) for r in fr) / 1000
        out[key] = dict(stem=stem, c=c, k=k, p50=p50, p99=p99, over=over, s=s, fr=fr,
                        live=sum(float(r['dtMs']) * int(r['live']) for r in fr) / 1000 / secs,
                        red=(sum(int(r['reduced']) for r in fr) / len(fr)) if key == 'on' else 0,
                        lod=(sum(float(r['lodUs']) for r in fr) / len(fr)) if key == 'on' else 0,
                        cap=(BUDGET - c) / (k / 1000))
    # 줄임 대상 1마리 비용: 켬 기록은 그대로, 끔 기록엔 같은 시각(같은 경로 · 같은 생존 수) 켬 기록의 줄임 수를 대입
    on_s = series(out['on']['fr']); ton = [t for t, _ in on_s]
    calm = lambda r: r['spawns'] == '0' and r['recalls'] == '0' and r['defers'] == '0'
    rows_on = [(int(r['live']) - int(r['reduced']), int(r['reduced']), float(r['dtMs'])) for _, r in on_s if calm(r)]
    rows_off = []
    for t, r in series(out['off']['fr']):
        if not calm(r): continue
        ro = on_s[min(len(on_s) - 1, bisect.bisect_left(ton, t))][1]
        if ro['live'] == r['live']:
            rows_off.append((int(r['live']) - int(ro['reduced']), int(ro['reduced']), float(r['dtMs'])))
    out['rest_off'], out['red_off'] = reg2(rows_off)
    out['rest_on'], out['red_on'] = reg2(rows_on)
    out['save'] = (out['off']['k'] - out['on']['k']) / out['off']['k'] * 100
    for key in ('off', 'on'): del out[key]['fr']
    return out


R = {dn: pair(*SWEEP[dn]) for dn in DENS}
T1, T2 = pair(*TRY1), pair(*TRY2)

INK, MUTED, FAINT, LINE = '#1a1c22', '#5b6270', '#8a91a0', '#e3e6ec'
OFF, ON, GREEN = '#e8590c', '#3b5bdb', '#2b8a3e'
plt.rcParams.update({
    'font.family': 'Malgun Gothic', 'svg.fonttype': 'none', 'font.size': 8.5,
    'axes.edgecolor': LINE, 'axes.labelcolor': MUTED, 'xtick.color': MUTED, 'ytick.color': MUTED,
    'axes.spines.top': False, 'axes.spines.right': False, 'axes.grid': True, 'grid.color': '#eef0f4', 'grid.linewidth': 0.8,
    'axes.axisbelow': True, 'xtick.major.size': 0, 'ytick.major.size': 0, 'axes.titlesize': 9, 'axes.titleweight': 'bold',
    'axes.titlecolor': INK, 'axes.titlelocation': 'left', 'legend.frameon': False, 'legend.fontsize': 8, 'axes.unicode_minus': False,
})


def svg(fig):
    b = io.StringIO()
    fig.savefig(b, format='svg', bbox_inches='tight', pad_inches=0.04)
    plt.close(fig)
    s = b.getvalue(); s = s[s.index('<svg'):]
    return re.sub(r'<svg([^>]*?) width="[^"]*" height="[^"]*"', r'<svg\1 width="100%"', s, count=1)


XL = [f'×{d}\n(평균 {R[d]["off"]["live"]:.0f}마리)' for d in DENS]


# ---- 그림 1: 밀도별 한 마리 비용 (끔 · 켬) + 줄임 대상 1마리 비용 (끔 · 켬)
def fig_cost():
    fig, axs = plt.subplots(1, 2, figsize=(7.2, 2.25))
    x = np.arange(len(DENS))
    for ax, (ko, kn, title, ymax) in zip(axs, (
            (lambda d: R[d]['off']['k'], lambda d: R[d]['on']['k'], '몬스터 한 마리 비용 (전체 평균)', 16),
            (lambda d: R[d]['red_off'], lambda d: R[d]['red_on'], '줄임 대상 몬스터 한 마리 비용', 16))):
        yo = [ko(d) for d in DENS]; yn = [kn(d) for d in DENS]
        ax.plot(x, yo, 'o-', color=OFF, lw=1.8, label='LOD 끔')
        ax.plot(x, yn, 'o-', color=ON, lw=1.8, label='LOD 켬')
        for i in range(len(DENS)):
            ax.text(x[i], yo[i] + 0.55, f'{yo[i]:.1f}', ha='center', fontsize=7.5, color=OFF)
            ax.text(x[i], yn[i] - 1.25, f'{yn[i]:.1f}', ha='center', fontsize=7.5, color=ON)
        ax.set_xticks(x); ax.set_xticklabels(XL, fontsize=7.6)
        ax.set_ylim(0, ymax); ax.set_ylabel('μs / 마리 / 프레임'); ax.set_title(title)
    axs[0].legend(loc='lower right')
    fig.tight_layout(w_pad=2)
    return svg(fig)


# ---- 그림 2: 시간 가중 p50 · p99 막대 (밀도별 끔 · 켬)
def fig_frame():
    fig, axs = plt.subplots(1, 3, figsize=(7.2, 2.35))
    x = np.arange(len(DENS)); w = 0.36
    for ax, (key, title, fmt) in zip(axs, (('p50', '보통 프레임 (p50, ms)', '{:.2f}'), ('p99', '느린 프레임 (p99, ms)', '{:.1f}'),
                                            ('over', '6.94ms 넘는 프레임에 쓴 시간 (%)', '{:.1f}'))):
        yo = [R[d]['off'][key] for d in DENS]; yn = [R[d]['on'][key] for d in DENS]
        ax.bar(x - w / 2, yo, w, color=OFF, label='LOD 끔'); ax.bar(x + w / 2, yn, w, color=ON, label='LOD 켬')
        top = max(yo + yn)
        for i in range(len(DENS)):
            ax.text(x[i] + w / 2, yn[i] + top * 0.02, fmt.format(yn[i]), ha='center', fontsize=6.8, color=ON)
        ax.set_xticks(x); ax.set_xticklabels([f'×{d}' for d in DENS]); ax.set_ylim(0, top * 1.15)
        ax.set_title(title, fontsize=8.3); ax.grid(axis='x', visible=False)
    axs[0].legend(loc='upper left', fontsize=7.3)
    fig.tight_layout(w_pad=1.2)
    return svg(fig)


# ---- 그림 0: 규칙 도식 (손으로 그린 SVG)
def scheme():
    import random
    random.seed(11)
    cx, cy, sc = 430, 122, 4.4          # 플레이어 위치, 1m = 4.4px (화면 비율 그대로)
    halo = 'paint-order="stroke" stroke="#ffffff" stroke-width="3"'
    s = '<svg viewBox="0 0 700 248" width="100%">'
    s += f'<circle cx="{cx}" cy="{cy}" r="{25 * sc:.0f}" fill="#f7f8fa" stroke="#c9ced8" stroke-dasharray="4 3"/>'
    s += f'<circle cx="{cx}" cy="{cy}" r="{20 * sc:.0f}" fill="none" stroke="#c9ced8"/>'
    s += f'<circle cx="{cx}" cy="{cy}" r="{15 * sc:.0f}" fill="#fff4ec" stroke="#e8590c" stroke-width="1.2"/>'
    # 화면(앞 6 · 뒤 3 · 좌우 9m)과 15% 여유 띠
    s += f'<rect x="{cx - 9 * sc:.0f}" y="{cy - 6 * sc:.0f}" width="{18 * sc:.0f}" height="{9 * sc:.0f}" fill="#e6ecff" stroke="#3b5bdb" stroke-width="1.6"/>'
    s += f'<rect x="{cx - 11.7 * sc:.0f}" y="{cy - 7.35 * sc:.0f}" width="{23.4 * sc:.0f}" height="{11.7 * sc:.0f}" fill="none" stroke="#3b5bdb" stroke-dasharray="3 3"/>'
    pts = []
    while len(pts) < 34:
        a = random.uniform(0, 6.283); rr = 25 * random.random() ** 0.5
        if rr < 3: continue
        x, y = cx + rr * sc * np.cos(a), cy + rr * sc * np.sin(a)
        if any((x - px) ** 2 + (y - py) ** 2 < 11 ** 2 for px, py in pts): continue
        pts.append((x, y))
    for x, y in pts:
        dx, dy = (x - cx) / sc, (cy - y) / sc
        full = (-11.7 <= dx <= 11.7 and -4.35 <= dy <= 7.35) or (dx * dx + dy * dy <= 15 * 15)
        s += (f'<circle cx="{x:.0f}" cy="{y:.0f}" r="{4.6 if full else 3.8}" fill="{"#1a1c22" if full else "#ffffff"}" '
              f'stroke="{"#1a1c22" if full else "#8a91a0"}" stroke-width="1.4"/>')
    s += f'<circle cx="{cx}" cy="{cy}" r="6.5" fill="#3b5bdb" stroke="#fff" stroke-width="2"/>'
    s += f'<text x="{cx}" y="{cy + 3 * sc + 13:.0f}" text-anchor="middle" class="xs" {halo}>플레이어</text>'
    s += f'<text x="{cx - 9 * sc + 4:.0f}" y="{cy - 6 * sc + 12:.0f}" class="xs" fill="#3b5bdb" {halo}>화면</text>'
    s += f'<text x="{cx + 15 * sc * 0.71 + 4:.0f}" y="{cy + 15 * sc * 0.71 + 4:.0f}" class="xs" fill="#c2410c" {halo}>감지 15m 안 = 교전</text>'
    s += f'<text x="{cx + 25 * sc * 0.62:.0f}" y="{cy - 25 * sc * 0.80 - 2:.0f}" class="xs" fill="#8a91a0" {halo}>켜기 20m · 끄기 25m</text>'
    # 범례 (왼쪽)
    s += '<circle cx="22" cy="40" r="4.6" fill="#1a1c22"/><text x="34" y="44" class="s"><tspan font-weight="700">원래대로</tspan></text>'
    s += '<text x="34" y="63" class="s" fill="#5b6270">화면 안(점선 = 15% 여유 포함)</text><text x="34" y="80" class="s" fill="#5b6270">또는 교전 중(감지 15m 안 · 쫓는 중)</text>'
    s += '<circle cx="22" cy="112" r="3.8" fill="#fff" stroke="#8a91a0" stroke-width="1.4"/><text x="34" y="116" class="s"><tspan font-weight="700">줄임</tspan> — 화면 밖 · 가만히 있음</text>'
    s += '<text x="34" y="135" class="s" fill="#5b6270">HP바 끔</text><text x="34" y="152" class="s" fill="#5b6270">판단(행동 트리) 8프레임에 한 번</text><text x="34" y="169" class="s" fill="#5b6270">애니메이션(Animator) 끔</text>'
    return s + '</svg>'


cost_svg, frame_svg = fig_cost(), fig_frame()

style = open(REPO + '/docs/FieldEntity/Shadow_Result.html', encoding='utf-8').read()
style = style[style.index('<style>'):style.index('</style>') + 8]
style = style.replace('</style>', '''  body{ font-size:10.4pt; line-height:1.6; }
  .fig svg{ margin:2px 0; }
  .big{ display:grid; grid-template-columns:repeat(4,1fr); gap:8px; margin:10px 0 6px; }
  .big div{ border:1px solid var(--line); border-radius:12px; padding:9px 11px; }
  .big .q{ font-size:8.4pt; color:var(--muted); display:block; min-height:2.6em; }
  .big .v{ font-size:13.5pt; font-weight:800; display:block; margin-top:2px; letter-spacing:-.02em; }
  .big .v .b{ color:#e8590c; } .big .v .a{ color:#3b5bdb; } .big .v .ar{ color:var(--faint); font-weight:400; }
  .big .n{ font-size:8pt; color:var(--green-ink); font-weight:800; }
  .word{ display:grid; grid-template-columns:repeat(3,1fr); gap:8px; margin:6px 0; }
  .word div{ background:var(--shade); border-radius:10px; padding:7px 10px; font-size:8.8pt; line-height:1.45; }
  .word b{ display:block; font-size:9.2pt; margin-bottom:1px; }
  table td, table th{ font-size:9pt; } td.r{ text-align:right; white-space:nowrap; } td strong{ white-space:nowrap; }
  td.o{ color:#c2410c; font-weight:700; text-align:right; } td.n{ color:#2f4ac0; font-weight:800; text-align:right; }
</style>''')

r8 = R[8]
rows = ''.join(
    f'<tr><td><strong>×{d}</strong></td><td class="r">{R[d]["off"]["live"]:.0f}</td>'
    f'<td class="o">{R[d]["off"]["k"]:.2f}</td><td class="n">{R[d]["on"]["k"]:.2f}</td><td class="r">−{R[d]["save"]:.0f}%</td>'
    f'<td class="o">{R[d]["off"]["p50"]:.2f}</td><td class="n">{R[d]["on"]["p50"]:.2f}</td>'
    f'<td class="o">{R[d]["off"]["over"]:.1f}%</td><td class="n">{R[d]["on"]["over"]:.1f}%</td>'
    f'<td class="o">{R[d]["off"]["cap"]:.0f}</td><td class="n">{R[d]["on"]["cap"]:.0f}</td>'
    f'<td class="r">{R[d]["on"]["red"] / R[d]["on"]["live"] * 100:.0f}%</td></tr>' for d in DENS)


def zero_row(label, key):
    vals = [R[d][k]['s'].get(key, 0) or 0 for d in DENS for k in ('off', 'on')]
    return f'<tr><td>{label}</td>' + ''.join(f'<td class="r">{v}</td>' for v in vals) + '</tr>'


checks = (zero_row('화면 안에서 갑자기 등장', 'on_spawn') + zero_row('화면 안에서 사라짐', 'on_recall') +
          zero_row('끌고 온 몬스터 소실', 'engaged_recall') + zero_row('경계 왕복 깜빡임', 'flicker_wiggle') +
          zero_row('몬스터 · HP바 풀 확장', 'pool_grows') +
          '<tr><td>LOD 늦은 복원 (화면 안에서야 원래대로)</td>' +
          ''.join(f'<td class="r">{"—" if k == "off" else R[d][k]["s"].get("lod_late", 0)}</td>' for d in DENS for k in ('off', 'on')) + '</tr>')

html = f'''<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>갱신 LOD 결과</title>{style}</head><body>

<div class="kicker">FieldEntity · 갱신 LOD · 2026-10-13</div>
<h1>화면 밖 몬스터를 덜 돌렸더니</h1>
<p class="lead">거리 방식으로 <strong>살아 있는 몬스터 수</strong>를 줄인 다음 단계다. 살아 있는 몬스터 중
<strong>화면 밖에서 가만히 있는</strong> 몬스터는 HP바를 끄고, 판단을 8프레임에 한 번만 하고, 애니메이션을 끈다.
몬스터 밀도를 1 · 2 · 4 · 8배로 올려 가며 LOD를 끄고 켜고 같은 경로를 자동으로 달려 비교했다.</p>

<div class="big">
  <div><span class="q">보통 프레임 시간 (×8, 시간 기준 p50)</span><span class="v"><span class="b">{r8["off"]["p50"]:.2f}</span><span class="ar"> → </span><span class="a">{r8["on"]["p50"]:.2f}ms</span></span><span class="n">{(1 - r8["on"]["p50"] / r8["off"]["p50"]) * 100:.0f}% 감소</span></div>
  <div><span class="q">줄임 대상 몬스터 한 마리 비용 (×8)</span><span class="v"><span class="b">{r8["red_off"]:.1f}</span><span class="ar"> → </span><span class="a">{r8["red_on"]:.1f}μs</span></span><span class="n">밀도와 무관하게 약 4μs</span></div>
  <div><span class="q">144fps 예산 안에 돌릴 수 있는 몬스터 (×8)</span><span class="v"><span class="b">{r8["off"]["cap"]:.0f}</span><span class="ar"> → </span><span class="a">{r8["on"]["cap"]:.0f}마리</span></span><span class="n">약 {r8["on"]["cap"] / r8["off"]["cap"]:.2f}배</span></div>
  <div><span class="q">눈에 띄는 이상 (튐 · 등장 · 소실)</span><span class="v"><span class="a">0번</span></span><span class="n">8번 실행 모두</span></div>
</div>

<div class="callout c-violet"><span class="lbl">한 줄로</span>거리 방식이 <strong>살아 있는 몬스터 수</strong>를 줄였다면, 갱신 LOD는 <strong>화면 밖에서 가만히 있는 몬스터의 비용</strong>을 혼잡도와 상관없이 약 4μs로 묶었다. 그래서 몬스터가 많을수록 효과가 커져 ×8에서 보통 프레임 −{(1 - r8["on"]["p50"] / r8["off"]["p50"]) * 100:.0f}%, 같은 예산에 약 {r8["on"]["cap"] / r8["off"]["cap"]:.1f}배를 돌린다 — 눈에 보이는 이상 없이.</div>

<div class="fig"><div class="figt">무엇을 줄이나 — 위에서 본 플레이어 주변</div>
{scheme()}
<div class="figcap">검은 점 = 원래대로 돈다: 화면 안(파란 사각형, 점선 = 15% 여유)에 있거나, 감지 거리 15m 안이라 플레이어를 쫓거나 쏘는 중(교전). 흰 점 = 줄인다: 화면 밖이면서 가만히 있는 몬스터. 교전 중인 몬스터는 화면 밖이어도 줄이지 않는다 — 공격 타이밍이 늦어지면 안 되므로. 화면에 들어오기 전(여유 띠)에 원래대로 돌려 굳은 자세가 보이지 않게 한다.</div></div>

<h2>읽기 전에 — 용어 세 개</h2>
<div class="word">
  <div><b>한 마리 비용 (μs)</b>몬스터가 한 마리 늘 때 프레임 시간이 늘어나는 양. 1μs = 0.001ms.</div>
  <div><b>밀도 ×N</b>스폰 지점마다 몬스터를 N배로. 켜지는 스폰 지점은 그대로라 마릿수만 N배가 된다.</div>
  <div><b>시간 기준 p50 · p99</b>프레임이 화면에 머문 시간으로 센 가운데 값 · 가장 느린 1%. 빠른 프레임이 개수로 많이 잡히는 치우침을 뺀 값.</div>
</div>

<h2>1. 몬스터가 많을수록 효과가 커진다</h2>
<div class="fig"><div class="figt">밀도별 한 마리 비용 — LOD 끔 · 켬</div>
{cost_svg}
<div class="figcap">왼쪽: LOD가 없으면 몬스터가 많아질수록 한 마리 비용이 오른다(×1 {R[1]["off"]["k"]:.1f} → ×8 {R[8]["off"]["k"]:.1f}μs, +{(R[8]["off"]["k"] / R[1]["off"]["k"] - 1) * 100:.0f}%) — 몰릴수록 서로 부딪히고 피하는 비용이 붙는다. LOD를 켜면 그 오름이 완만해져 절감이 ×1 −{R[1]["save"]:.0f}% → ×8 −{R[8]["save"]:.0f}%로 커진다. 오른쪽: 줄임 대상 몬스터만 보면 LOD 끔은 밀도를 따라 오르지만({R[1]["red_off"]:.1f} → {R[8]["red_off"]:.1f}μs) LOD 켬은 밀도와 상관없이 약 4μs로 묶인다.</div></div>

<div class="fig"><div class="figt">프레임 시간 — 밀도별 LOD 끔 · 켬 (시간 기준)</div>
{frame_svg}
<div class="figcap">×1에서는 차이가 없다(몬스터가 적어 줄일 게 없다). ×8에서는 보통 프레임이 {r8["off"]["p50"]:.2f} → {r8["on"]["p50"]:.2f}ms, 6.94ms(144fps 한 장 예산)를 넘는 프레임에 쓴 시간이 {r8["off"]["over"]:.1f}% → {r8["on"]["over"]:.1f}%로 줄었다.</div></div>

<table><thead><tr><th rowspan="2">밀도</th><th rowspan="2">평균 생존</th><th colspan="3">한 마리 비용 (μs)</th><th colspan="2">p50 (ms)</th><th colspan="2">6.94ms 넘는 시간</th><th colspan="2">예산 안 마릿수</th><th rowspan="2">줄인 비율</th></tr>
<tr><th>끔</th><th>켬</th><th>절감</th><th>끔</th><th>켬</th><th>끔</th><th>켬</th><th>끔</th><th>켬</th></tr></thead><tbody>
{rows}
</tbody></table>
<p class="s" style="color:var(--muted);font-size:8.4pt">예산 안 마릿수 = 그 실행에서 맞춘 직선(고정 + 한 마리 비용 × 마릿수)이 6.94ms에 닿는 마릿수. 한 마리 비용이 밀도에 따라 오르므로 몬스터가 많은 ×8 값이 실제 혼잡 상황에 가장 가깝다. 줄인 비율 = 생존 몬스터 중 줄임 상태였던 비율 — 밀도가 오를수록 교전 중인 몬스터가 늘어 줄일 수 있는 몬스터가 줄어든다.</p>

<h2>2. 첫 시도는 효과가 없었다 — 측정으로 바로잡은 것</h2>
<p>처음엔 애니메이션을 끄지 않고 <strong>뼈 위치 계산만 생략</strong>(CullUpdateTransforms)했다. 공격 판정이 애니메이션 신호로 오기 때문에, 신호는 살려 두는 안전한 설정을 고른 것이다. 결과는 효과 없음이었다.</p>
<table><thead><tr><th style="width:34%">×4 · 같은 빌드 끔/켬</th><th>첫 시도 — 뼈 계산만 생략</th><th>바꾼 뒤 — Animator 끔</th></tr></thead><tbody>
<tr><td>한 마리 비용 (전체)</td><td class="r">{T1["off"]["k"]:.2f} → {T1["on"]["k"]:.2f}μs ({(T1["on"]["k"] / T1["off"]["k"] - 1) * 100:+.0f}%)</td><td class="r"><strong>{T2["off"]["k"]:.2f} → {T2["on"]["k"]:.2f}μs ({(T2["on"]["k"] / T2["off"]["k"] - 1) * 100:+.0f}%)</strong></td></tr>
<tr><td>줄임 대상 한 마리 비용</td><td class="r">{T1["red_off"]:.1f} → {T1["red_on"]:.1f}μs</td><td class="r"><strong>{T2["red_off"]:.1f} → {T2["red_on"]:.1f}μs</strong></td></tr>
<tr><td>LOD 판정 비용 (프레임당)</td><td class="r">{T1["on"]["lod"]:.0f}μs</td><td class="r">{T2["on"]["lod"]:.0f}μs</td></tr>
</tbody></table>
<div class="callout c-blue"><span class="lbl">왜 첫 시도가 안 됐고, 왜 꺼도 되나</span>
<ul>
<li><strong>뼈 계산 생략은 거의 안 아꼈다.</strong> 줄임 대상 한 마리당 약 1μs — 프레임당 판정 비용에 묻혔다. 이 설정은 상태 머신은 계속 돌리고, 렌더러가 "안 보일 때"만 생략한다(그림자를 드리우는 몬스터는 화면 밖이어도 보이는 것으로 칠 수 있다 — 추정).</li>
<li><strong>줄임 대상은 애니메이션 신호가 필요 없다.</strong> 신호(발사 · 공격 끝 · 장전 끝)가 필요한 건 공격 중인 몬스터인데, 공격 중이면 교전이라 애초에 줄이지 않는다. 그래서 통째로 꺼도 게임이 깨지지 않는다 — 플레이어가 15m 안으로 오면 공격을 시작하기 전에 원래대로 돌아온다.</li>
<li>다시 켤 때 등장 연출이 반복되지 않게 상태를 유지하는 설정(keepAnimatorStateOnDisable)을 같이 켰다.</li>
</ul></div>

<h2>3. 눈에 보이는 문제 — 8번 모두 0</h2>
<table><thead><tr><th rowspan="2" style="width:30%">문제</th>{''.join(f'<th colspan="2">×{d}</th>' for d in DENS)}</tr>
<tr>{''.join('<th>끔</th><th>켬</th>' for _ in DENS)}</tr></thead><tbody>
{checks}
</tbody></table>
<p class="s" style="color:var(--muted);font-size:8.4pt">늦은 복원 = 화면 안에 들어온 뒤에야 원래대로 돌아간 경우(굳은 자세 · 늦게 뜨는 HP바가 보일 수 있는 순간). 여유 띠 15%에서 미리 돌려 0. 교전 유예도 8번 모두 끝날 때 0.</p>

<h2>측정은 이렇게 했다</h2>
<table><tbody>
<tr><td style="width:22%"><strong>조건</strong></td><td>개발 빌드 하나 · 같은 PC · 1920×1080 창 · 프레임 제한 없음 · 거리 방식(⑤). 밀도(×1 · ×2 · ×4 · ×8)와 LOD(끔 · 켬)만 바꿔 8번. 기록 <code>field_Distance*_20261013_*</code> · 생성 <code>tools/lod_report/gen.py</code> · 무엇을 줄일지 고른 근거 <code>CostOff_Result.pdf</code></td></tr>
<tr><td><strong>같은 길</strong></td><td>자동 주행 약 5분 — 맵 한 바퀴 → 경계 8곳 1m 왕복 → 몬스터 끌고 가기 2번. 몬스터가 생긴 수가 끔 · 켬에서 거의 같아(×8: {R[8]["off"]["s"]["spawns"]} / {R[8]["on"]["s"]["spawns"]}) 같은 경로가 재현됨을 확인.</td></tr>
<tr><td><strong>한 마리 비용</strong></td><td>몬스터를 만들거나 치운 프레임은 뺀다 → 생존 수 20마리 묶음 중앙값 → 직선의 기울기. 줄임 대상 한 마리 비용은 "줄임 대상 수"와 "나머지 수" 두 가지로 나눈 직선에서 읽었다(LOD 끔 기록에는 같은 시각 켬 기록의 줄임 수를 대입).</td></tr>
<tr><td><strong>횟수 · 잡음</strong></td><td>조건마다 1번(시간상). 같은 조건 반복 시 한 마리 비용이 약 5% 흔들린다 → ×1의 −{R[1]["save"]:.0f}%는 판단 보류, ×4 · ×8의 −{R[4]["save"]:.0f}~{R[8]["save"]:.0f}%는 그보다 확실히 크다.</td></tr>
<tr><td><strong>판정 비용</strong></td><td>LOD 판정(누가 화면 밖 · 비교전인가)은 프레임당 ×1 {R[1]["on"]["lod"]:.0f}μs ~ ×8 {R[8]["on"]["lod"]:.0f}μs (한 마리당 약 0.2μs). 위 수치는 이 비용을 이미 포함한다.</td></tr>
</tbody></table>



</body></html>'''

out = REPO + '/docs/FieldEntity/LOD_Result.html'
open(out, 'w', encoding='utf-8').write(html)
print('written', out)
for d in DENS:
    print(d, f"{R[d]['off']['k']:.2f}->{R[d]['on']['k']:.2f} red {R[d]['red_off']:.2f}->{R[d]['red_on']:.2f} p50 {R[d]['off']['p50']:.2f}->{R[d]['on']['p50']:.2f} cap {R[d]['off']['cap']:.0f}->{R[d]['on']['cap']:.0f}")
print('try1', T1['red_off'], T1['red_on'], 'try2', T2['red_off'], T2['red_on'])
