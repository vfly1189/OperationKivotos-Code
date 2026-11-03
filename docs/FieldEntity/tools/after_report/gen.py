# After_Result.html 생성 — python -B gen.py (이 폴더에서) → docs/FieldEntity/After_Result.html
#  Before = ① Sector 3회, After = ⑤ Distance 3회 (같은 빌드 2026-10-10 03:44). 쉽게 읽히는 요약판.
#  PDF: Chrome headless로 인쇄 (입출력 모두 Windows 절대경로)
import sys, os, io, re
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'policy_report'))
sys.dont_write_bytecode = True
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from data import *

BEFORE = ['Sector_20261010_040137', 'Sector_20261010_040650', 'Sector_20261010_041203']
AFTER = ['Distance_20261010_034504', 'Distance_20261010_035017', 'Distance_20261010_035530']

INK, MUTED, FAINT, LINE = '#1a1c22', '#5b6270', '#8a91a0', '#e3e6ec'
BEF, AFT, GREEN = '#e8590c', '#3b5bdb', '#2b8a3e'
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


B = [summarize_run(f'{DEV}/field_{s}_frames.csv') for s in BEFORE]
A = [summarize_run(f'{DEV}/field_{s}_frames.csv') for s in AFTER]
mb = lambda k: mean([s[k] for s in B])
ma = lambda k: mean([s[k] for s in A])


def timeline(stem):
    """하네스 시각(a_sweep = 0)별 살아 있는 몹 · 프레임 시간, 그리고 구간."""
    d = load(f'{DEV}/field_{stem}_frames.csv')
    t, ts, live, dt, ft = 0.0, [], [], [], {}
    for r in d['frames']:
        ft[int(r['frame'])] = t
        ts.append(t); live.append(float(r['live'])); dt.append(float(r['dtMs']))
        t += float(r['dtMs']) / 1000
    segs = [(ft[f], l) for f, l in d['segments'] if f in ft]
    return ts, live, dt, segs


def binned(ts, vs, w, fn):
    out_t, out_v, cur, buf = [], [], 0.0, []
    for t, v in zip(ts, vs):
        if t >= cur + w:
            if buf: out_t.append(cur + w / 2); out_v.append(fn(buf))
            cur += w * int((t - cur) // w); buf = []
        buf.append(v)
    if buf: out_t.append(cur + w / 2); out_v.append(fn(buf))
    return out_t, out_v


TB, TA = timeline(BEFORE[0]), timeline(AFTER[0])
SEGS = TB[3]


def shade(ax, ymax, labels=True):
    spans = []
    for i, (t0, l) in enumerate(SEGS):
        t1 = SEGS[i + 1][0] if i + 1 < len(SEGS) else 296.3
        if l.startswith('c'): spans.append((t0, t1, '#e6f4ea'))
        elif l.startswith('b'): spans.append((t0, t1, '#efeafd'))
    for t0, t1, c in spans:
        ax.axvspan(t0, t1, color=c, zorder=0, lw=0)
    if labels:
        ax.text(65, ymax * 0.93, '맵 한 바퀴', ha='center', fontsize=7.5, color=MUTED)
        ax.text(173, ymax * 0.93, '끌고 가기', ha='center', fontsize=7.5, color='#5028c0')
        ax.text(234, ymax * 0.93, '끌고 가기', ha='center', fontsize=7.5, color='#5028c0')


# ---- 그림 1: 살아 있는 몬스터 수 (시간)
def fig_live():
    fig, ax = plt.subplots(figsize=(7.2, 2.5))
    shade(ax, 200)
    for (ts, live, _, _), c, n in ((TB, BEF, 'Before (섹터 방식)'), (TA, AFT, 'After (거리 방식)')):
        x, y = binned(ts, live, 0.5, max)
        ax.plot(x, y, color=c, lw=1.3, label=n)
    ax.set_xlim(0, 296); ax.set_ylim(0, 200)
    ax.set_xlabel('시간 (초) — 자동 주행 5분'); ax.set_ylabel('살아 있는 몬스터 (마리)')
    from matplotlib.patches import Patch
    h, l = ax.get_legend_handles_labels()
    h += [Patch(color='#e6f4ea'), Patch(color='#efeafd')]; l += ['경계 1m 왕복 구간', '몬스터 끌고 가기 구간']
    ax.legend(h, l, loc='upper left', bbox_to_anchor=(1.01, 1.0))
    return svg(fig)


# ---- 그림 2: 프레임 시간 (시간) — 위아래 두 칸, 같은 축
def fig_dt():
    fig, axs = plt.subplots(2, 1, figsize=(7.2, 3.6), sharex=True, sharey=True)
    for ax, (ts, _, dt, _), c, n in ((axs[0], TB, BEF, 'Before (섹터 방식)'), (axs[1], TA, AFT, 'After (거리 방식)')):
        shade(ax, 28, labels=False)
        x, y = binned(ts, dt, 0.25, max)
        ax.plot(x, y, color=c, lw=0.9)
        ax.axhline(BUDGET, color=GREEN, lw=1.1, ls=(0, (4, 2)))
        ax.text(294, BUDGET + 2.6, '6.94ms 선 (이 위 = 끊길 수 있는 프레임)', ha='right', fontsize=7.3, color=GREEN)
        ax.set_title(n, color=c)
        ax.set_ylim(0, 28)
        ax.set_ylabel('프레임 시간 (ms)')
    axs[1].set_xlim(0, 296); axs[1].set_xlabel('시간 (초) — 0.25초마다 가장 느린 프레임')
    return svg(fig)


# ---- 그림 3: 막대 비교 (작은 그림 여러 개, 같은 모양)
def fig_bars():
    items = [('보통 프레임 (p50)', mb('dt_p50'), ma('dt_p50'), 'ms', '{:.2f}'),
             ('느린 프레임 (p99)', mb('dt_p99'), ma('dt_p99'), 'ms', '{:.2f}'),
             ('6.94ms 넘은 프레임', mb('over'), ma('over'), '개', '{:.0f}'),
             ('살아 있는 몬스터 (평균)', mb('live_mean'), ma('live_mean'), '마리', '{:.0f}')]
    fig, axs = plt.subplots(1, 4, figsize=(7.2, 1.95))
    for ax, (t, b, a, u, f) in zip(axs, items):
        ax.grid(False); ax.spines['left'].set_visible(False); ax.set_yticks([])
        ax.bar([0, 1], [b, a], width=0.62, color=[BEF, AFT])
        for x, v in ((0, b), (1, a)):
            ax.text(x, v + max(b, a) * 0.03, f.format(v) + u, ha='center', va='bottom', fontsize=8.5, color=INK, fontweight='bold')
        ax.set_xticks([0, 1]); ax.set_xticklabels(['Before', 'After'])
        ax.set_ylim(0, max(b, a) * 1.25)
        pct = (a - b) / b * 100
        ax.set_title(f'{t}\n{pct:+.0f}%', fontsize=8.3)
    fig.tight_layout(w_pad=1.2)
    return svg(fig)


live_svg, dt_svg, bars_svg = fig_live(), fig_dt(), fig_bars()

# ---- 그림 0: 무엇을 바꿨나 (손으로 그린 도식)
def scheme():
    import random
    random.seed(3)
    dots = [(random.uniform(14, 316), random.uniform(34, 176)) for _ in range(26)]
    def panel(ox, title, mode):
        s = f'<g transform="translate({ox},0)">'
        s += f'<text x="165" y="16" text-anchor="middle" class="tb">{title}</text>'
        # 섹터 3개 (세로로 나란한 큰 상자)
        for i in range(3):
            x = 10 + i * 104
            lit = mode == 'sector' and i == 1
            s += (f'<rect x="{x}" y="30" width="100" height="150" rx="6" fill="{"#fff4ec" if lit else "#f7f8fa"}" '
                  f'stroke="{"#e8590c" if lit else "#c9ced8"}" stroke-width="{1.6 if lit else 1}"/>')
            s += f'<text x="{x + 6}" y="44" class="xs">섹터 {i + 1}</text>'
        px, py = 172, 112
        if mode == 'dist':
            s += f'<circle cx="{px}" cy="{py}" r="62" fill="#eef2ff" stroke="#3b5bdb" stroke-width="1.6"/>'
            s += f'<circle cx="{px}" cy="{py}" r="78" fill="none" stroke="#3b5bdb" stroke-width="1" stroke-dasharray="4 3"/>'
        for x, y in dots:
            on = (mode == 'sector' and 114 <= x <= 214) or (mode == 'dist' and (x - px) ** 2 + (y - py) ** 2 <= 62 ** 2)
            c = ('#e8590c' if mode == 'sector' else '#3b5bdb') if on else '#c9ced8'
            s += f'<circle cx="{x:.0f}" cy="{y:.0f}" r="{4.2 if on else 3}" fill="{c}"/>'
        s += f'<circle cx="{px}" cy="{py}" r="6.5" fill="#1a1c22"/><text x="{px}" y="{py + 20}" text-anchor="middle" class="xs" fill="#1a1c22">플레이어</text>'
        on_n = sum(1 for x, y in dots if (mode == 'sector' and 114 <= x <= 214) or (mode == 'dist' and (x - px) ** 2 + (y - py) ** 2 <= 62 ** 2))
        cap = '색칠된 점 = 켜진 몬스터 자리' if mode == 'sector' else '실선 원(20m) 안이면 켬 · 점선 원(25m) 밖이면 끔'
        s += f'<text x="165" y="198" text-anchor="middle" class="s">{cap}</text>'
        return s + '</g>'
    return (f'<svg viewBox="0 0 700 206" width="100%">' + panel(0, 'Before — 섹터 방식', 'sector') +
            panel(360, 'After — 거리 방식', 'dist') + '</svg>')


style = open(REPO + '/docs/FieldEntity/Shadow_Result.html', encoding='utf-8').read()
style = style[style.index('<style>'):style.index('</style>') + 8]
style = style.replace('</style>', '''  body{ font-size:10.4pt; line-height:1.6; }
  .fig svg{ margin:2px 0; }
  .big{ display:grid; grid-template-columns:repeat(4,1fr); gap:8px; margin:10px 0 6px; }
  .big div{ border:1px solid var(--line); border-radius:12px; padding:9px 11px; }
  .big .q{ font-size:8.4pt; color:var(--muted); display:block; min-height:2.6em; }
  .big .v{ font-size:13.5pt; font-weight:800; display:block; margin-top:2px; letter-spacing:-.02em; }
  .big .v .b{ color:var(--amber); } .big .v .a{ color:var(--blue); } .big .v .ar{ color:var(--faint); font-weight:400; }
  .big .n{ font-size:8pt; color:var(--green-ink); font-weight:800; }
  .word{ display:grid; grid-template-columns:repeat(3,1fr); gap:8px; margin:6px 0; }
  .word div{ background:var(--shade); border-radius:10px; padding:7px 10px; font-size:8.8pt; line-height:1.45; }
  .word b{ display:block; font-size:9.2pt; margin-bottom:1px; }
  table td, table th{ font-size:9.2pt; } td strong{ white-space:nowrap; } td.b{ color:var(--amber-ink); font-weight:800; text-align:center; } td.a{ color:var(--blue-ink); font-weight:800; text-align:center; }
</style>''')

OVER60 = [sum(sum(1 for r in load(f'{DEV}/field_{st}_frames.csv')['frames'] if float(r['dtMs']) > 1000 / 60) for st in runs) for runs in (BEFORE, AFTER)]
dB = [x for s in B for x in s['bigspawn_dt']]; dA = [x for s in A for x in s['bigspawn_dt']]
html = f'''<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>Before After</title>{style}</head><body>

<div class="kicker">FieldEntity · Before / After · 2026-10-10</div>
<h1>몬스터를 켜고 끄는 방식을 바꿨더니</h1>
<p class="lead">필드의 몬스터는 플레이어 근처에 왔을 때만 만들고(켜고), 멀어지면 치운다(끈다). 이 "근처"를 정하는 방식을
<strong>섹터(맵을 큰 상자로 나눈 구역)</strong>에서 <strong>거리(플레이어 주변 원)</strong>로 바꾸고, 같은 경로를 자동으로 5분씩 3번 달려 비교했다.</p>

<div class="big">
  <div><span class="q">동시에 살아 있는 몬스터 (평균)</span><span class="v"><span class="b">{mb("live_mean"):.0f}</span><span class="ar"> → </span><span class="a">{ma("live_mean"):.0f}마리</span></span><span class="n">{(1 - ma("live_mean") / mb("live_mean")) * 100:.0f}% 감소</span></div>
  <div><span class="q">한 번에 새로 생기는 몬스터 (최대)</span><span class="v"><span class="b">{B[0]["max_spawn_frame"]}</span><span class="ar"> → </span><span class="a">{A[0]["max_spawn_frame"]}마리</span></span><span class="n">한꺼번에 몰리지 않음</span></div>
  <div><span class="q">끊길 수 있는 프레임 (5분 · 3번 합)</span><span class="v"><span class="b">{sum(s["over"] for s in B)}</span><span class="ar"> → </span><span class="a">{sum(s["over"] for s in A)}번</span></span><span class="n">{(1 - sum(s["over"] for s in A) / sum(s["over"] for s in B)) * 100:.0f}% 감소</span></div>
  <div><span class="q">눈에 띄는 이상 (갑자기 등장 · 깜빡임)</span><span class="v"><span class="b">{B[0]["on_spawn"] + B[0]["flicker_wiggle"]}</span><span class="ar"> → </span><span class="a">{A[0]["on_spawn"] + A[0]["flicker_wiggle"]}번</span></span><span class="n">한 번도 안 보임</span></div>
</div>

<div class="fig"><div class="figt">무엇을 바꿨나</div>
{scheme()}
<div class="figcap">Before는 플레이어가 들어간 섹터 안의 몬스터를 <strong>전부</strong> 켠다 — 섹터가 크면 멀리 있는 몬스터까지 켜지고, 섹터를 넘는 순간 한 섹터(최대 119마리)가 한꺼번에 생긴다. After는 플레이어에서 20m 안의 스폰 지점만 켜고 25m 밖으로 멀어지면 끈다(켜는 선과 끄는 선을 5m 띄워 경계에서 깜빡이지 않게).</div></div>

<h2>읽기 전에 — 용어 세 개</h2>
<div class="word">
  <div><b>프레임 시간</b>화면 한 장을 만드는 데 걸린 시간. 짧을수록 부드럽다.</div>
  <div><b>6.94ms 선</b>144fps 모니터에서 한 장에 쓸 수 있는 시간. 이보다 길면 그 순간 끊겨 보일 수 있다.</div>
  <div><b>p50 · p99</b>p50 = 보통 프레임(가운데 값). p99 = 100장 중 가장 느린 1장 — "가끔 느린 순간"이 얼마나 느린지.</div>
</div>

<h2 class="pb">1. 평소 — 살아 있는 몬스터가 줄었다</h2>
<div class="fig"><div class="figt">5분 동안 살아 있는 몬스터 수</div>
{live_svg}
<div class="figcap">처음 131초는 맵 한 바퀴, 초록 띠는 경계를 1m씩 왔다 갔다 하는 구간, 보라 띠는 몬스터를 끌고 가는 구간. Before는 섹터를 넘을 때마다 계단처럼 뛰고(최대 {B[0]["live_max"]}마리), 초록 띠에서 섹터 하나가 통째로 켜졌다 꺼지기를 반복한다. After는 주변 몇 무리만 켜져 낮고 고르다(최대 {A[0]["live_max"]}마리).</div></div>
<div class="fig"><div class="figt">한눈에 — 3번 평균</div>
{bars_svg}</div>
<div class="callout c-blue"><span class="lbl">왜 빨라졌나</span>몬스터 한 마리가 살아 있으면 매 프레임 약 7.6μs(0.0076ms)가 든다(AI · 길찾기 · 애니메이션 · HP바). 평균 {mb("live_mean") - ma("live_mean"):.0f}마리가 줄어 보통 프레임이 {mb("dt_p50"):.2f} → {ma("dt_p50"):.2f}ms가 됐다. 판정 방식 자체(거리 계산)의 비용은 프레임당 약 0.001ms라 무시해도 된다 — <strong>빨라진 이유는 "덜 켜 두기" 하나다.</strong></div>

<h2 class="pb">2. 순간 — 한꺼번에 몰리던 끊김이 사라졌다</h2>
<div class="fig"><div class="figt">5분 동안의 프레임 시간 — 위 Before, 아래 After (같은 눈금)</div>
{dt_svg}
<div class="figcap">솟은 바늘 하나하나가 "그 순간 무거웠던 프레임"이다. Before의 큰 바늘은 전부 섹터를 넘는 순간 — 몬스터 수십~119마리를 한 프레임에 만든다. 초록 띠(경계 왕복)에서는 바늘이 빽빽하다. After는 바늘이 거의 6.94ms 선 아래에 있다.</div></div>
<table><thead><tr><th>가장 무거운 순간</th><th>Before</th><th>After</th></tr></thead><tbody>
<tr><td>한 프레임에 새로 만든 몬스터</td><td class="b">{B[0]["max_spawn_frame"]}마리</td><td class="a">{A[0]["max_spawn_frame"]}마리</td></tr>
<tr><td>그 프레임의 시간</td><td class="b">{min(dB):.0f} ~ {max(dB):.0f}ms</td><td class="a">{min(dA):.0f} ~ {max(dA):.1f}ms</td></tr>
<tr><td>60fps 선(16.7ms)도 넘은 프레임 (3번 합)</td><td class="b">{OVER60[0]}개</td><td class="a">{OVER60[1]}개</td></tr>
</tbody></table>
<div class="callout c-amber"><span class="lbl">왜 사라졌나</span>Before는 "섹터 하나 = 몬스터 119마리"가 켜는 단위라, 섹터를 넘는 한 순간에 119마리 만들기가 몰린다. After는 켜는 단위가 스폰 지점 하나(최대 17마리)이고, 플레이어가 걸어가는 동안 원 안으로 하나씩 들어오므로 자연스럽게 흩어진다.</div>

<h2 class="pb">3. 눈에 보이는 문제 — 전부 0</h2>
<table><thead><tr><th>문제</th><th>무슨 일인가</th><th>Before</th><th>After</th></tr></thead><tbody>
<tr><td><strong>화면 안에서 갑자기 등장</strong></td><td>플레이어 눈앞 화면 안에서 몬스터가 툭 생겨남</td><td class="b">{B[0]["on_spawn"]}번</td><td class="a">0번</td></tr>
<tr><td><strong>경계에서 깜빡임</strong></td><td>경계 근처를 왔다 갔다 하면 몬스터 무리가 생겼다 사라졌다 반복 (2초 안에 다시 켜진 횟수)</td><td class="b">{B[0]["flicker_wiggle"]}번</td><td class="a">0번</td></tr>
<tr><td><strong>끌고 가던 몬스터가 사라짐</strong></td><td>싸우며 끌고 나온 몬스터가 구역을 벗어나자 사라짐</td><td>0번</td><td>0번</td></tr>
</tbody></table>
<div class="callout c-green"><span class="lbl">어떻게</span>
<ul>
<li><strong>갑자기 등장</strong> — After는 20m 밖(화면 바깥)에서 미리 켜 두므로 플레이어가 보기 전에 이미 있다.</li>
<li><strong>깜빡임</strong> — 켜는 선(20m)과 끄는 선(25m)을 띄워 두어, 경계에서 1m 왔다 갔다 해도 상태가 바뀌지 않는다. 섹터는 켜는 선 = 끄는 선이라 넘을 때마다 바뀐다.</li>
<li><strong>끌고 가던 몬스터</strong> — 두 방식 모두 "싸우는 중인 몬스터는 끝날 때까지 남겨 둔다"는 규칙을 같이 쓴다. 그래서 둘 다 0.</li>
</ul></div>

<h2>측정은 이렇게 했다</h2>
<table><tbody>
<tr><td style="width:24%"><strong>같은 조건</strong></td><td>같은 빌드(개발 빌드) · 같은 PC · 1920×1080 창 · 프레임 제한 없음. 정책만 바꿔서 실행.</td></tr>
<tr><td><strong>같은 길</strong></td><td>사람 대신 자동 주행 — 맵 한 바퀴 → 경계 8곳에서 1m씩 10번 왕복 → 몬스터 끌고 가기 2번. 약 5분.</td></tr>
<tr><td><strong>세 번씩</strong></td><td>Before · After 각 3번. 몬스터가 생기고 사라진 횟수가 3번 모두 같아(차이 0.5% 이내) 결과가 우연이 아님을 확인.</td></tr>
<tr><td><strong>맵</strong></td><td>스폰 지점 23개 · 몬스터 363마리 · 섹터 5개.</td></tr>
</tbody></table>
<div class="callout c-violet"><span class="lbl">한 줄로</span>켜 두는 범위를 "섹터 전체"에서 "주변 20m"로 좁혔더니, 살아 있는 몬스터가 {(1 - ma("live_mean") / mb("live_mean")) * 100:.0f}% 줄고 끊길 수 있는 프레임이 {(1 - sum(s["over"] for s in A) / sum(s["over"] for s in B)) * 100:.0f}% 줄었으며, 플레이어 눈에 보이던 이상은 모두 사라졌다.</div>

<div class="footer">자세한 수치 · 정책 7개 비교: <code>Policy_Result.pdf</code> · 기록: <code>Worklog_2026-10-09.md</code> 11절 · 데이터: 개발 빌드 MetricsLogs <code>field_Sector_20261010_040137 · 040650 · 041203</code> / <code>field_Distance_20261010_034504 · 035017 · 035530</code> · 생성 <code>docs/FieldEntity/tools/after_report/gen.py</code></div>
</body></html>'''

out = REPO + '/docs/FieldEntity/After_Result.html'
open(out, 'w', encoding='utf-8').write(html)
print('written', out)
