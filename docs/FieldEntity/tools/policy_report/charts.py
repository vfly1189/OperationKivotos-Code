# Policy_Result 그림 — matplotlib → 인라인 SVG (글자는 텍스트로 남김: svg.fonttype none)
import io, re
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from data import *

INK, MUTED, FAINT, LINE = '#1a1c22', '#5b6270', '#8a91a0', '#e3e6ec'
AMBER, AMBER_L, BLUE, BLUE_L, VIOLET, GRAY, GRAY_L, GREEN = '#e8590c', '#f6b48c', '#3b5bdb', '#a9b8f2', '#7048e8', '#9aa1ae', '#d5d9e0', '#2b8a3e'
FAM_COLOR = {'sector': AMBER, 'spot': GRAY, 'dist': BLUE}
FAM_LABEL = {'sector': '섹터형 (①②)', 'spot': '칸형 (③④)', 'dist': '거리형 (⑤⑥⑦)'}
plt.rcParams.update({
    'font.family': 'Malgun Gothic', 'svg.fonttype': 'none', 'font.size': 8,
    'axes.edgecolor': LINE, 'axes.labelcolor': MUTED, 'xtick.color': MUTED, 'ytick.color': MUTED,
    'axes.spines.top': False, 'axes.spines.right': False, 'axes.grid': True, 'grid.color': '#eef0f4', 'grid.linewidth': 0.8,
    'axes.axisbelow': True, 'xtick.major.size': 0, 'ytick.major.size': 0, 'axes.titlesize': 8.5, 'axes.titleweight': 'bold',
    'axes.titlecolor': INK, 'axes.titlelocation': 'left', 'legend.frameon': False, 'legend.fontsize': 7.5, 'lines.linewidth': 1.4,
    'axes.unicode_minus': False,
})


def svg(fig):
    b = io.StringIO()
    fig.savefig(b, format='svg', bbox_inches='tight', pad_inches=0.04)
    plt.close(fig)
    s = b.getvalue()
    s = s[s.index('<svg'):]
    return re.sub(r'<svg([^>]*?) width="[^"]*" height="[^"]*"', r'<svg\1 width="100%"', s, count=1)


def label(p): return f'{NUM[p]} {SHORT[p]}'


# ---- 1. 스폰 1회 분해 — 수정 전후
def fig_breakdown():
    ba = before_after()
    rows = [('에디터 · 수정 전', ba['ed_before']), ('에디터 · 수정 후', ba['ed_after']),
            ('개발 빌드 · 수정 전 (새벽 ①)', ba['dev_before']), ('개발 빌드 · 수정 후 (① 이번)', ba['dev_after'])]
    segs = [('씬 찾기 (FindAnyObjectByType)', AMBER), ('꺼내기 나머지 (SetParent 등)', AMBER_L),
            ('SetStat', GRAY_L), ('OnSpawn', GRAY), ('SetActive', BLUE)]
    fig, ax = plt.subplots(figsize=(7.2, 2.25))
    ax.grid(axis='y', visible=False)
    for i, (name, d) in enumerate(rows):
        y = len(rows) - 1 - i
        if 'popFindUs' in d:
            vals = [d['popFindUs'], d['popUs'] - d['popFindUs'], d['statUs'], d['onSpawnUs'], d['activateUs']]
        else:
            vals = [None, d['popUs'], d['statUs'], d['onSpawnUs'], d['activateUs']]
        x = 0
        for j, v in enumerate(vals):
            if v is None: continue
            if j == 1 and vals[0] is None:   # 분해 전 pop — 빗금
                ax.barh(y, v, left=x, height=0.56, color='white', edgecolor=AMBER, hatch='////', lw=0.8)
                ax.text(x + v / 2, y, f'꺼내기 {v:.0f}', ha='center', va='center', fontsize=7, color=INK,
                        bbox=dict(fc='white', ec='none', pad=0.6))
            else:
                ax.barh(y, v, left=x, height=0.56, color=segs[j][1], edgecolor='white', lw=1.2)
                if j == 0 and v > 60:
                    ax.text(x + v / 2, y, f'씬 찾기 {v:.0f}', ha='center', va='center', fontsize=7, color='white', fontweight='bold')
            x += v
        ax.text(x + 6, y, f'{x:.0f}μs', va='center', fontsize=8, color=INK, fontweight='bold')
    ax.set_yticks(range(len(rows))); ax.set_yticklabels([r[0] for r in rows][::-1])
    ax.set_xlim(0, 580); ax.set_xlabel('스폰 1회 (μs) — 마커 합 ÷ 스폰 수')
    hs = [plt.Rectangle((0, 0), 1, 1, color=c) for _, c in segs] + [plt.Rectangle((0, 0), 1, 1, fc='white', ec=AMBER, hatch='////')]
    ax.legend(hs, [n for n, _ in segs] + ['꺼내기 (분해 계측 전)'], loc='upper center', bbox_to_anchor=(0.45, -0.3), ncol=3, fontsize=7)
    return svg(fig)


# ---- 2. 수정 전 씬 찾기 — 살아 있는 몹 수와 무관
def fig_find_vs_live():
    pts = find_vs_live()
    fig, ax = plt.subplots(figsize=(3.5, 2.2))
    xs, ys = zip(*pts)
    ax.scatter(xs, ys, s=16, color=AMBER, edgecolors='white', linewidths=0.6, zorder=3)
    med = sorted(ys)[len(ys) // 2]
    ax.axhline(med, color=AMBER, lw=1, ls=(0, (3, 2)))
    ax.text(max(xs) * 0.98, med + 12, f'중앙값 {med:.0f}μs', ha='right', fontsize=7, color=MUTED)
    ax.set_ylim(0, 450); ax.set_xlim(0, max(xs) * 1.08)
    ax.set_xlabel('그 순간 살아 있는 몹 (마리)'); ax.set_ylabel('스폰 1회의 씬 찾기 (μs)')
    ax.set_title('수정 전 · 스폰 프레임 42개')
    return svg(fig)


# ---- 3. 정책별 프레임 시간 (p50 · p99) — 3회 점 + 평균 막대
def fig_dt_policy():
    pol = policy_runs()
    fig, axs = plt.subplots(1, 2, figsize=(7.2, 2.5), sharey=True)
    for ax, key, title, xmax in ((axs[0], 'dt_p50', '프레임 시간 p50 (ms)', 3.2), (axs[1], 'dt_p99', '프레임 시간 p99 (ms)', 5.6)):
        ax.grid(axis='y', visible=False)
        for i, p in enumerate(POLICIES):
            y = len(POLICIES) - 1 - i
            v = [s[key] for s in pol[p]]; m = mean(v); c = FAM_COLOR[FAMILY[p]]
            ax.barh(y, m, height=0.5, color=c, alpha=0.28, edgecolor='none')
            ax.scatter(v, [y] * 3, s=14, color=c, edgecolors='white', linewidths=0.6, zorder=3)
            ax.text(m + xmax * 0.02, y, f'{m:.2f}', va='center', fontsize=7.2, color=INK)
        ax.set_xlim(0, xmax); ax.set_title(title)
    axs[0].set_yticks(range(len(POLICIES))); axs[0].set_yticklabels([label(p) for p in POLICIES][::-1])
    hs = [plt.Rectangle((0, 0), 1, 1, color=FAM_COLOR[f]) for f in ('sector', 'spot', 'dist')]
    fig.legend(hs, [FAM_LABEL[f] for f in ('sector', 'spot', 'dist')], loc='lower center', bbox_to_anchor=(0.55, -0.08), ncol=3)
    return svg(fig)


# ---- 4. 프레임 시간 = 기본 + 몹 수 × 기울기
def fig_live_fit():
    a, b, r, xs, ys = fit_live()
    P = dict(zip(POLICIES, zip(xs, ys)))
    fig, ax = plt.subplots(figsize=(7.2, 2.9))
    gx = [0, 200]
    ax.plot(gx, [b + a * x for x in gx], color=FAINT, lw=1, ls=(0, (4, 2)), zorder=1)
    for p, (x, y) in P.items():
        ax.scatter([x], [y], s=46, color=FAM_COLOR[FAMILY[p]], edgecolors='white', linewidths=1.2, zorder=3)
    kw = dict(fontsize=7.3, color=INK, arrowprops=dict(arrowstyle='-', color=FAINT, lw=0.7, shrinkA=1, shrinkB=4))
    dx = [P[p][0] for p in ('Distance', 'SpotlightDistance', 'CullingGroup')]
    ax.annotate(f'⑤ ⑥ ⑦ 거리형  {min(dx):.1f}~{max(dx):.1f}마리 · {P["Distance"][1]:.2f}ms', xy=P['Distance'], xytext=(40, 1.33), **kw)
    ax.annotate(f'④ Spot 5×5  {P["Spotlight5x5"][0]:.1f}마리 · {P["Spotlight5x5"][1]:.2f}ms', xy=P['Spotlight5x5'], xytext=(62, 1.46), **kw)
    ax.annotate(f'③ Spot 3×3  {P["Spotlight3x3"][0]:.1f}마리 · {P["Spotlight3x3"][1]:.2f}ms', xy=P['Spotlight3x3'], xytext=(80, 1.6), **kw)
    ax.annotate(f'① Sector  {P["Sector"][0]:.1f}마리 · {P["Sector"][1]:.2f}ms', xy=P['Sector'], xytext=(8, 2.02), **kw)
    ax.annotate(f'② Adjacent  {P["AdjacentSector"][0]:.1f}마리 · {P["AdjacentSector"][1]:.2f}ms', xy=P['AdjacentSector'],
                xytext=(105, 2.78), **kw)
    ax.text(6, 2.62, f'dt p50 = {b:.3f}ms + {a * 1000:.2f}μs × 살아 있는 몹', fontsize=9, color=INK, fontweight='bold')
    ax.text(6, 2.48, f'정책 7개 평균점 최소제곱 · r = {r:.4f}', fontsize=7.5, color=MUTED)
    ax.set_xlim(0, 200); ax.set_ylim(1.2, 2.95)
    ax.set_xlabel('평균 살아 있는 몹 (마리, 3회 평균)'); ax.set_ylabel('프레임 시간 p50 (ms)', labelpad=6)
    return svg(fig)


# ---- 5. 순간 비용 — 그 프레임의 스폰 수 vs 프레임 시간
def fig_spawn_scatter():
    pol = policy_runs()
    a, b, n = fit_spawn()
    fig, axs = plt.subplots(1, 2, figsize=(7.2, 2.75), gridspec_kw=dict(width_ratios=[1.5, 1]))
    for ax, ymax, xmax in ((axs[0], 110, 125), (axs[1], 12, 40)):
        for fam in ('spot', 'sector', 'dist'):
            for p in POLICIES:
                if FAMILY[p] != fam: continue
                pts = [pt for s in pol[p] for pt in s['spawn_frames']]
                if p == 'AdjacentSector':
                    grow = [pt for pt in pts if pt[1] > 60]; rest = [pt for pt in pts if pt[1] <= 60]
                    if grow:
                        ax.scatter(*zip(*grow), s=26, facecolors='white', edgecolors=AMBER, linewidths=1.2, zorder=4)
                    pts = rest
                if pts:
                    ax.scatter(*zip(*pts), s=12, color=FAM_COLOR[fam], alpha=0.75, edgecolors='white', linewidths=0.4, zorder=3)
        ax.axhline(BUDGET, color=GREEN, lw=1.1, ls=(0, (4, 2)), zorder=2)
        ax.plot([0, xmax], [b, b + a * xmax], color=INK, lw=0.9, zorder=2)
        ax.set_xlim(0, xmax); ax.set_ylim(0, ymax)
        ax.set_xlabel('그 프레임에 스폰한 몹 (마리)')
    axs[0].set_ylabel('프레임 시간 (ms)')
    axs[0].text(121, BUDGET + 3, '144fps 예산 6.94ms', ha='right', fontsize=7, color=GREEN)
    axs[0].text(117, 92, '② 풀 확장 프레임\n(런타임 Instantiate)', ha='right', fontsize=7, color=AMBER)
    axs[0].text(58, 48, f'직선: {b:.2f}ms + {a:.3f}ms × 마리\n(② 제외 {n}프레임)', fontsize=7, color=INK)
    axs[0].set_title('전체 (21회 · 스폰만 있는 프레임)')
    axs[1].set_title('확대 — 40마리 이하')
    axs[1].text(1, BUDGET + 0.3, '6.94ms', ha='left', fontsize=7, color=GREEN)
    axs[1].annotate('17마리 = 스포너 하나\n(거리형의 한 번 최대)', xy=(17, 6.5), xytext=(21, 9.6), fontsize=7, color=INK,
                    arrowprops=dict(arrowstyle='-', color=FAINT, lw=0.8))
    hs = [plt.Line2D([], [], marker='o', ls='', color=FAM_COLOR[f], markersize=5) for f in ('sector', 'spot', 'dist')]
    hs.append(plt.Line2D([], [], marker='o', ls='', markerfacecolor='white', markeredgecolor=AMBER, markersize=5))
    fig.legend(hs, [FAM_LABEL[f] for f in ('sector', 'spot', 'dist')] + ['② 풀 확장'], loc='lower center',
               bbox_to_anchor=(0.5, -0.09), ncol=4)
    return svg(fig)


# ---- 6. 예산 초과 프레임 — 원인별
def fig_over():
    pol = policy_runs()
    cats = [('over_churn', '스폰 · 회수가 있는 프레임', AMBER), ('over_metrics', '계측 자체 (기록 목록 확장)', VIOLET),
            ('over_other', '그 밖 (원인 미상)', GRAY_L)]
    fig, ax = plt.subplots(figsize=(7.2, 2.3))
    ax.grid(axis='y', visible=False)
    for i, p in enumerate(POLICIES):
        y = len(POLICIES) - 1 - i; x = 0
        for k, _, c in cats:
            v = mean([s[k] for s in pol[p]])
            if v > 0:
                ax.barh(y, v, left=x, height=0.56, color=c, edgecolor='white', lw=1.2)
            x += v
        ax.text(x + 1, y, f'{x:.0f}', va='center', fontsize=7.5, color=INK, fontweight='bold')
    ax.set_yticks(range(len(POLICIES))); ax.set_yticklabels([label(p) for p in POLICIES][::-1])
    ax.set_xlabel('6.94ms 넘은 프레임 수 (296초 · 3회 평균)'); ax.set_xlim(0, 85)
    hs = [plt.Rectangle((0, 0), 1, 1, color=c) for _, _, c in cats]
    ax.legend(hs, [n for _, n, _ in cats], loc='lower right', fontsize=7)
    return svg(fig)
