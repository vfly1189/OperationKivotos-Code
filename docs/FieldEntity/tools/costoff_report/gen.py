# CostOff_Result.html 생성 — python -B gen.py (이 폴더에서) → docs/FieldEntity/CostOff_Result.html
#  갱신 LOD 0단계 끄기 실험: 밀도 ×4 · ⑤ Distance · 개발 빌드(2026-10-12) · 조건마다 1회.
#  회피(avoid) 기록이 b2_drag_1005 직전에 끊겨 다섯 조건 모두 a_sweep ~ c4 구간(약 214초)으로 맞춰 비교한다.
#  PDF: Chrome headless로 인쇄 (입출력 모두 Windows 절대경로)
import sys, os, io, re, statistics as st, importlib.util
sys.dont_write_bytecode = True
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..')).replace(os.sep, '/')
DEV = os.environ.get('HARNESS_DEV_LOGS', 'C:/Users/c/Desktop/테스트/MetricsLogs')
spec = importlib.util.spec_from_file_location('af', os.path.join(HERE, '..', 'analyze_field.py'))
af = importlib.util.module_from_spec(spec); spec.loader.exec_module(af)

RUNS = [  # (키, 이름, 파일)
    ('none', '끄기 없음', 'field_Distance_x4_20261012_232020'),
    ('bt', '행동 트리 끔', 'field_Distance_x4_off-bt_20261012_232534'),
    ('anim', 'Animator 끔', 'field_Distance_x4_off-anim_20261012_233047'),
    ('hpbar', 'HP바 끔', 'field_Distance_x4_off-hpbar_20261012_234150'),
    ('avoid', '회피 끔', 'field_Distance_x4_off-avoid_20261012_234703'),
]
STOP = 'b2_drag_1005'      # 회피 기록이 끊긴 구간 — 여기 전까지만
BIN = 20                   # 생존 몹 수를 20마리 단위로 묶는다
MIN_FRAMES = 200           # 묶음 하나에 이 이상 프레임이 있어야 점으로 쓴다
THRESHOLD = 10             # 절감 10% 미만 = 효과 작음 (같은 조건 반복 기울기 차이 약 5%)
BUDGET = 1000 / 144


def fit(rows):
    """스폰 · 회수 없는 프레임만 → 생존 몹 20마리 단위 중앙값 → 프레임 수 가중 직선."""
    bins = {}
    for r in rows:
        if int(r['spawns']) or int(r['recalls']): continue
        bins.setdefault(int(r['live']) // BIN * BIN, []).append(float(r['dtMs']))
    pts = [(b + BIN / 2, st.median(v), len(v)) for b, v in sorted(bins.items()) if len(v) >= MIN_FRAMES]
    n = sum(w for *_, w in pts); mx = sum(x * w for x, _, w in pts) / n; my = sum(y * w for _, y, w in pts) / n
    k = sum(w * (x - mx) * (y - my) for x, y, w in pts) / sum(w * (x - mx) ** 2 for x, _, w in pts)
    return my - k * mx, k, pts


R = {}
for key, name, stem in RUNS:
    d = af.load(f'{DEV}/{stem}_frames.csv', None)
    segs = d['segments']
    a0 = next(f for f, s in segs if s == 'a_sweep')
    b2 = next(f for f, s in segs if s == STOP)
    rows = [r for r in d['frames'] if a0 <= int(r['frame']) < b2]
    c, k, pts = fit(rows)
    dts = sorted(float(r['dtMs']) for r in rows)
    secs = sum(dts) / 1000
    R[key] = dict(name=name, stem=stem, c=c, k=k * 1000, pts=pts, secs=secs,
                  p50=dts[len(dts) // 2], p99=dts[int(len(dts) * 0.99)],
                  over=sum(1 for x in dts if x > BUDGET),
                  live=sum(float(r['dtMs']) * int(r['live']) for r in rows) / 1000 / secs,
                  live_max=max(int(r['live']) for r in rows))
base = R['none']['k']
for key in R:
    R[key]['save'] = (base - R[key]['k']) / base * 100
    R[key]['at400'] = R[key]['c'] + R[key]['k'] / 1000 * 400

INK, MUTED, FAINT, LINE = '#1a1c22', '#5b6270', '#8a91a0', '#e3e6ec'
COL = {'none': '#1a1c22', 'bt': '#3b5bdb', 'anim': '#7048e8', 'hpbar': '#2b8a3e', 'avoid': '#e8590c'}
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


# ---- 그림 1: 생존 몹 수 vs 프레임 시간 (점 = 20마리 묶음 중앙값, 선 = 맞춘 직선)
def fig_lines():
    fig, ax = plt.subplots(figsize=(7.2, 3.1))
    for key, *_ in RUNS:
        r = R[key]
        xs = [x for x, _, _ in r['pts']]; ys = [y for _, y, _ in r['pts']]
        lw, z = (2.0, 5) if key == 'none' else (1.3, 3)
        ax.scatter(xs, ys, s=12, color=COL[key], zorder=z)
        ax.plot([0, 420], [r['c'], r['c'] + r['k'] / 1000 * 420], color=COL[key], lw=lw, zorder=z,
                ls='-' if key != 'avoid' else (0, (4, 2)),
                label=f"{r['name']} — 마리당 {r['k']:.1f}μs")
    ax.set_xlim(0, 420); ax.set_ylim(0, 6.5)
    ax.set_xlabel('살아 있는 몬스터 (마리)'); ax.set_ylabel('프레임 시간 (ms, 중앙값)')
    ax.legend(loc='upper left')
    ax.text(415, 0.3, '기울기가 낮을수록 = 몬스터 한 마리가 싸다', ha='right', fontsize=7.5, color=MUTED)
    return svg(fig)


# ---- 그림 2: 마리당 비용 막대 + 절감 %
def fig_bars():
    order = ['none', 'bt', 'anim', 'hpbar', 'avoid']
    fig, ax = plt.subplots(figsize=(7.2, 2.3))
    ys = list(range(len(order)))[::-1]
    for y, key in zip(ys, order):
        r = R[key]
        small = key != 'none' and r['save'] < THRESHOLD
        ax.barh(y, r['k'], height=0.6, color='#c9ced8' if small else COL[key])
        lab = f"{r['k']:.2f}μs" + ('' if key == 'none' else f"   −{r['save']:.0f}%")
        ax.text(r['k'] + 0.15, y, lab, va='center', fontsize=8.5, color=INK, fontweight='bold')
    ax.axvline(base * (1 - THRESHOLD / 100), color=FAINT, lw=1, ls=(0, (3, 2)))
    ax.text(base * (1 - THRESHOLD / 100) - 0.15, len(order) - 0.45, f'−{THRESHOLD}% 선', ha='right', fontsize=7.3, color=FAINT)
    ax.set_yticks(ys); ax.set_yticklabels([R[k]['name'] for k in order])
    ax.set_xlim(0, 14.5); ax.set_xlabel('몬스터 한 마리가 프레임마다 드는 시간 (μs)')
    ax.grid(axis='y', visible=False)
    return svg(fig)


lines_svg, bars_svg = fig_lines(), fig_bars()

style = open(REPO + '/docs/FieldEntity/Shadow_Result.html', encoding='utf-8').read()
style = style[style.index('<style>'):style.index('</style>') + 8]
style = style.replace('</style>', '''  body{ font-size:10.4pt; line-height:1.6; }
  .fig svg{ margin:2px 0; }
  .big{ display:grid; grid-template-columns:repeat(4,1fr); gap:8px; margin:10px 0 6px; }
  .big div{ border:1px solid var(--line); border-radius:12px; padding:9px 11px; }
  .big .q{ font-size:8.4pt; color:var(--muted); display:block; }
  .big .v{ font-size:15pt; font-weight:800; display:block; margin-top:2px; letter-spacing:-.02em; }
  .big .n{ font-size:8pt; font-weight:800; }
  .word{ display:grid; grid-template-columns:repeat(3,1fr); gap:8px; margin:6px 0; }
  .word div{ background:var(--shade); border-radius:10px; padding:7px 10px; font-size:8.8pt; line-height:1.45; }
  .word b{ display:block; font-size:9.2pt; margin-bottom:1px; }
  table td, table th{ font-size:9.2pt; } td.r{ text-align:right; white-space:nowrap; } td strong{ white-space:nowrap; }
</style>''')


def tile(key, note, color):
    r = R[key]
    return (f'<div><span class="q">{r["name"]}</span><span class="v" style="color:{color}">−{r["save"]:.0f}%</span>'
            f'<span class="n" style="color:{color}">{note}</span></div>')


def save_cell(k):
    return '—' if k == 'none' else f'−{R[k]["save"]:.0f}%'


rows_html = ''.join(
    f'<tr><td><strong>{R[k]["name"]}</strong></td><td class="r">{R[k]["k"]:.2f}μs</td>'
    f'<td class="r">{save_cell(k)}</td>'
    f'<td class="r">{R[k]["at400"]:.2f}ms</td><td class="r">{R[k]["p50"]:.2f}</td><td class="r">{R[k]["p99"]:.2f}</td>'
    f'<td class="r">{R[k]["over"]}</td><td class="r">{R[k]["live"]:.0f}</td></tr>'
    for k, *_ in RUNS)

sum3 = sum(base - R[k]['k'] for k in ('bt', 'anim', 'hpbar'))
html = f'''<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>끄기 실험</title>{style}</head><body>

<div class="kicker">FieldEntity · 갱신 LOD 0단계 · 끄기 실험 · 2026-10-12</div>
<h1>몬스터 한 마리의 비용은 어디에 쓰이나</h1>
<p class="lead">거리 방식으로 <strong>살아 있는 몬스터 수</strong>는 줄였다. 남은 몬스터의 약 88%는 화면 밖에 있는데도 비용을 다 낸다.
다음 단계(갱신 LOD)는 그 몬스터들의 <strong>한 마리당 비용</strong>을 줄이는 것이다. 무엇부터 줄일지 정하려고,
비용 항목 네 가지를 <strong>하나씩 통째로 꺼 보고</strong> 한 마리 비용이 최대 얼마나 내려가는지 쟀다.</p>

<div class="big">
  {tile('bt', '가장 큼 → 1순위', COL['bt'])}
  {tile('anim', '큼 → 2순위', COL['anim'])}
  {tile('hpbar', '중간 → 3순위', COL['hpbar'])}
  {tile('avoid', '거의 없음 → 생략', '#8a91a0')}
</div>
<p class="s" style="color:var(--muted);font-size:8.6pt;margin-top:0">각 칸 = 그 항목을 껐을 때 몬스터 한 마리 비용이 줄어든 비율 (기준: 아무것도 안 끈 상태 {base:.2f}μs/마리).</p>

<h2>읽기 전에 — 용어 세 개</h2>
<div class="word">
  <div><b>한 마리 비용 (μs/마리)</b>몬스터가 한 마리 늘 때 프레임 시간이 늘어나는 양. 1μs = 0.001ms.</div>
  <div><b>끄기 실험</b>실제 게임에 넣을 동작이 아니다. "이 항목이 0이면 최대 얼마나 빨라지나"(상한)를 재는 것.</div>
  <div><b>밀도 ×4</b>스폰 지점마다 몬스터를 4배로. 차이가 잘 보이게 부하를 키운 것 — 켜지는 스폰 지점은 그대로.</div>
</div>

<h2>1. 몬스터가 늘 때 프레임 시간이 얼마나 느나</h2>
<div class="fig"><div class="figt">살아 있는 몬스터 수에 따른 프레임 시간 — 다섯 조건</div>
{lines_svg}
<div class="figcap">점 = 생존 몬스터 수를 20마리씩 묶은 구간의 프레임 시간 중앙값, 선 = 그 점들에 맞춘 직선. 모든 선이 왼쪽 끝(몬스터 0)에서 약 {min(R[k]["c"] for k in R):.2f}~{max(R[k]["c"] for k in R):.2f}ms로 모인다 — 몬스터와 상관없는 고정 비용은 같고, <strong>기울기(한 마리 비용)만 달라진다</strong>. 회피 끔(점선)은 기준선과 거의 겹친다.</div></div>

<div class="fig"><div class="figt">한 마리 비용 — 막대가 짧을수록 그 항목이 비쌌다는 뜻</div>
{bars_svg}
<div class="figcap">점선 = 기준에서 −{THRESHOLD}%. 같은 조건을 두 번 돌렸을 때 기울기가 약 5% 차이 났으므로, 이보다 작게 줄어든 항목은 "효과 작음"으로 본다(회색).</div></div>

<table><thead><tr><th>조건</th><th>한 마리 비용</th><th>절감</th><th>400마리일 때</th><th>p50 (ms)</th><th>p99 (ms)</th><th>6.94ms 넘음</th><th>평균 생존</th></tr></thead><tbody>
{rows_html}
</tbody></table>
<p class="s" style="color:var(--muted);font-size:8.6pt">400마리일 때 = 맞춘 직선으로 계산한 프레임 시간. 평균 생존이 조건마다 비슷해(116~123마리) p50 · p99도 그대로 비교할 수 있다.</p>

<h2>2. 항목을 어떻게 껐나</h2>
<table><thead><tr><th style="width:15%">항목</th><th style="width:33%">끈 방법</th><th>같이 사라진 것 · 주의</th></tr></thead><tbody>
<tr><td><strong>행동 트리</strong></td><td>몬스터가 매 프레임 하는 "판단"(감지 · 추적 · 공격 결정)을 건너뜀</td><td>판단을 안 하니 움직이지도 쏘지도 않는다 → 이동 · 애니 전환 · 사격 비용도 같이 빠졌다. 그래서 −{R["bt"]["save"]:.0f}%는 "판단 + 판단이 일으키는 일"의 합.</td></tr>
<tr><td><strong>Animator</strong></td><td>애니메이션 컴포넌트를 끔 (몬스터가 한 자세로 굳음)</td><td>발사 · 공격 끝 · 장전 끝이 애니 신호라, 그냥 끄면 공격 상태에서 영영 못 빠져나온다. 그래서 신호 시각만 흉내 냈다(발사 0.58초 · 공격 끝 2.55초 · 장전 끝 2.83초) — 사격은 그대로 일어난다.</td></tr>
<tr><td><strong>HP바</strong></td><td>머리 위 HP바 오브젝트를 통째로 끔</td><td>스크립트(위치 계산)만 끄면 UI 캔버스 갱신 비용이 남아서 오브젝트째 껐다.</td></tr>
<tr><td><strong>회피</strong></td><td>길찾기 에이전트의 "서로 피하기"만 끔</td><td>에이전트 자체를 끄면 이동이 멈추므로 회피 계산만 껐다. 이동은 그대로.</td></tr>
</tbody></table>
<div class="callout c-blue"><span class="lbl">합이 100%가 아닌 이유</span>세 항목 절감을 더하면 {sum3:.1f}μs로 기준 {base:.2f}μs의 {sum3 / base * 100:.0f}%다. 행동 트리를 끄면 애니 전환 · 사격도 줄어드는 것처럼 항목끼리 겹치는 부분이 있어서, 이 합은 "셋 다 끄면 줄어드는 양"보다 크게 잡힌다. 그래도 순서(행동 트리 &gt; Animator &gt; HP바 ≫ 회피)는 분명하다.</div>

<h2>3. 결과 읽기 — 무엇을 줄일까</h2>
<div class="callout c-green"><span class="lbl">구현할 것 (순서대로)</span>
<ul>
<li><strong>① 행동 트리 — 화면 밖이면 덜 자주 판단</strong> (상한 −{R["bt"]["save"]:.0f}%). 매 프레임 대신 몇 프레임에 한 번만 판단한다. 시작 프레임을 몬스터마다 엇갈려야 한 프레임에 몰리지 않는다.</li>
<li><strong>② Animator — 화면 밖이면 뼈 계산 생략</strong> (상한 −{R["anim"]["save"]:.0f}%). 애니 신호(발사 등)는 계속 와야 하므로 완전히 끄지 못하고, 신호는 살리고 뼈 위치 계산만 건너뛰는 설정(CullUpdateTransforms)을 쓴다 → 실제 절감은 이 상한보다 작다.</li>
<li><strong>③ HP바 — 화면 밖이면 끔</strong> (상한 −{R["hpbar"]["save"]:.0f}%). 화면 밖 HP바는 어차피 안 보이므로 상한에 가깝게 얻을 수 있다. 셋 중 가장 쉽다.</li>
</ul></div>
<div class="callout c-amber"><span class="lbl">생략할 것 — 회피 (−{R["avoid"]["save"]:.0f}%)</span>회피 계산은 <strong>움직이는</strong> 몬스터만 한다. 화면 밖 몬스터는 대부분 제자리에 서 있고, 이 맵은 멀리서 쏘는 원거리 몬스터(RL)가 대부분이라 플레이어 주위로 몰려들지도 않는다. 그래서 꺼도 거의 안 줄었다. (근접 몬스터가 몰려드는 맵이라면 다시 볼 항목.)</div>

<h2>측정은 이렇게 했다</h2>
<table><tbody>
<tr><td style="width:22%"><strong>조건</strong></td><td>개발 빌드 · 같은 PC · 1920×1080 창 · 프레임 제한 없음 · 거리 방식(⑤) · 밀도 ×4 (스폰 지점 23개 · 슬롯 1,452개). 끄는 항목만 바꿔서 실행.</td></tr>
<tr><td><strong>같은 길</strong></td><td>자동 주행. 회피 끔 기록이 두 번째 "끌고 가기" 직전에 끊겨서, 다섯 조건 모두 <strong>맵 한 바퀴 ~ 경계 왕복 4곳 · 끌고 가기 1번(약 {R["none"]["secs"]:.0f}초)</strong>으로 맞춰 비교했다.</td></tr>
<tr><td><strong>한 마리 비용 계산</strong></td><td>몬스터를 만들거나 치운 프레임은 뺀다(그 순간 비용이 섞이므로) → 생존 몬스터 20마리 단위로 묶어 중앙값 → 직선을 맞춰 기울기를 읽는다.</td></tr>
<tr><td><strong>횟수</strong></td><td>조건마다 1번(시간상). 같은 조건 반복 시 기울기 차이 약 5% → 10% 넘는 차이만 의미 있게 본다. 행동 트리 · Animator · HP바는 이 기준을 크게 넘는다.</td></tr>
<tr><td><strong>정상 동작 확인</strong></td><td>다섯 조건 모두 화면 안 등장 0 · 끌고 온 몬스터 소실 0 · 몬스터 풀 확장 0. 교전 유예는 끝날 때 0(Animator 끔 포함 — 신호 흉내가 제대로 동작).</td></tr>
</tbody></table>
<div class="callout c-violet"><span class="lbl">주의 — 예전 숫자와 비교하지 말 것</span>예전 비용 식의 "마리당 7.58μs"는 정책 7개의 실행별 중앙값을 하나의 직선으로 맞춘 값이고, 여기 {base:.2f}μs는 한 실행 안에서 몬스터 수에 따라 맞춘 값이다. 계산 방법이 달라 직접 비교하지 않는다. 같은 방법으로 ×1 기록(10-10 빌드)을 계산하면 10.1~10.6μs다.</div>

<div class="callout c-blue"><span class="lbl">한 줄로</span>몬스터 한 마리 비용의 대부분은 <strong>행동 트리(−{R["bt"]["save"]:.0f}%) · Animator(−{R["anim"]["save"]:.0f}%) · HP바(−{R["hpbar"]["save"]:.0f}%)</strong>에서 나오고, 회피는 거의 0이다. 화면 밖 몬스터에 이 세 가지를 순서대로 줄인다.</div>

<div class="footer">데이터: 개발 빌드 MetricsLogs <code>{' · '.join(R[k]['stem'].replace('field_Distance_x4_', '') for k in R)}</code> · 생성 <code>docs/FieldEntity/tools/costoff_report/gen.py</code></div>
</body></html>'''

out = REPO + '/docs/FieldEntity/CostOff_Result.html'
open(out, 'w', encoding='utf-8').write(html)
print('written', out)
for k in R:
    print(f"{k:6s} {R[k]['c']:.3f} + {R[k]['k']:.2f}μs  save {R[k]['save']:.1f}%  400→{R[k]['at400']:.2f}")
