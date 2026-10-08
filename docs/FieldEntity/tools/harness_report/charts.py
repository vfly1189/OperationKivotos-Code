import io, re, math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, FancyArrowPatch
import numpy as np
from functools import lru_cache
from data import *

INK, MUTED, FAINT, LINE = '#1a1c22', '#5b6270', '#8a91a0', '#e3e6ec'
S1, S2, S3, S4 = '#2a78d6', '#eb6834', '#1baf7a', '#eda100'
plt.rcParams.update({
    'font.family': 'Malgun Gothic', 'svg.fonttype': 'none', 'font.size': 8,
    'axes.edgecolor': LINE, 'axes.labelcolor': MUTED, 'xtick.color': MUTED, 'ytick.color': MUTED,
    'axes.spines.top': False, 'axes.spines.right': False, 'axes.grid': True, 'grid.color': '#eef0f4', 'grid.linewidth': 0.8,
    'axes.axisbelow': True, 'xtick.major.size': 0, 'ytick.major.size': 0, 'axes.titlesize': 8.5, 'axes.titleweight': 'bold',
    'axes.titlecolor': INK, 'axes.titlelocation': 'left', 'legend.frameon': False, 'legend.fontsize': 7.5, 'lines.linewidth': 1.4,
    'axes.unicode_minus': False,
})

L = lru_cache(maxsize=None)(load)


def svg(fig):
    b = io.StringIO()
    fig.savefig(b, format='svg', bbox_inches='tight', pad_inches=0.04)
    plt.close(fig)
    s = b.getvalue()
    s = s[s.index('<svg'):]
    s = re.sub(r'<svg([^>]*?) width="[^"]*" height="[^"]*"', r'<svg\1 width="100%"', s, count=1)
    return s


SECT = {1: (-12.5, 78.7, 70.3, 101.6), 2: (-78.0, 14.3, 42.0, 71.7), 3: (-15.9, 75.3, 14.6, 44.1),
        4: (-89.0, 2.3, -13.8, 16.3), 5: (-25.2, 66.1, -43.6, -11.4)}
SWEEP = [(68, 93), (-5, 93), (-5, 55), (-70, 50), (-5, 48), (-5, 22), (66, 21), (-5, 21), (-5, 0), (-80, -3),
         (-5, -3), (-5, -22), (51, -22)]
RET = [(51, -22), (50, -22), (-5, -22), (-5, -11.4), (-5, 2), (-10, 2), (-5, 2), (-5, -22), (-5, 14.6), (-5, 42),
       (-5, 47), (-14, 47), (-5, 47), (-5, 18), (-5, 55.5), (-5, 71.7), (-5, 93), (10, 93), (50, 93)]
WIG = [('c1', 50, -22), ('c2', -5, -11.4), ('c3', -5, 14.6), ('c4', -5, 42), ('c5', -5, 55.5), ('c6', -5, 71.7),
       ('c7', 10, 93), ('c8', 50, 93)]


def fig_map():
    m, slots = slots_map()
    fig, ax = plt.subplots(figsize=(7.2, 3.55))
    ax.grid(False)
    for sid, (x0, x1, z0, z1) in SECT.items():
        ax.add_patch(Rectangle((x0, z0), x1 - x0, z1 - z0, fill=True, fc='#f7f8fa', ec='#c9ced8', lw=0.8, zorder=0))
        right = sid in (1, 3, 5)
        ax.text(x1 - 2 if right else x0 + 2, z1 - 3, f'S{sid}', ha='right' if right else 'left', va='top',
                fontsize=8, color=FAINT, fontweight='bold')
    for sid, s in slots.items():
        xs, zs = zip(*s['kids'])
        ax.scatter(xs, zs, s=2.5, color='#b8bfcc', zorder=1, linewidths=0)
        off = {1005: (0, -6.2, 'center'), 1010: (-6.5, 0, 'right'), 1018: (0, 4.8, 'center')}
        if sid in off:
            dx, dz, ha = off[sid]
            ax.text(s['c'][0] + dx, s['c'][1] + dz, str(sid), ha=ha, va='center', fontsize=7, color=MUTED)
    xs, zs = zip(*SWEEP)
    ax.plot(xs, zs, color=S1, lw=1.6, zorder=3, label='ⓐ 팔 순회 (예측 경로 655m · 5m/s)')
    xs, zs = zip(*RET)
    ax.plot(xs, zs, color=FAINT, lw=1.0, ls=(0, (3, 2)), zorder=2, label='돌아오는 길 (이동 · ⓑ · ⓒ)')
    for a, b in (((-10, 2), (-5, -22)), ((-14, 47), (-5, 18))):
        ax.add_patch(FancyArrowPatch(a, b, arrowstyle='-|>', mutation_scale=9, color=S2, lw=1.8, zorder=4,
                                     connectionstyle='arc3,rad=-0.25'))
    ax.plot([], [], color=S2, lw=1.8, label='ⓑ 끌기 (1018 · 1005, 1.5m/s)')
    for name, x, z in WIG:
        ax.scatter([x], [z], s=26, marker='D', color=S3, edgecolors='white', linewidths=0.8, zorder=5)
        ax.text(x + 2.2, z + 1.2, name, fontsize=6.8, color=INK, zorder=6)
    ax.scatter([], [], s=26, marker='D', color=S3, label='ⓒ 1m 왕복 ×10')
    ax.scatter([68], [93], s=40, marker='o', color=INK, zorder=6)
    ax.text(70, 95.5, '시작', fontsize=7, color=INK)
    ax.set_xlim(-92, 82); ax.set_ylim(-47, 104); ax.set_aspect('equal')
    ax.set_xlabel('x (m)'); ax.set_ylabel('z (m)')
    ax.legend(loc='upper left', bbox_to_anchor=(1.01, 1.0), ncol=1, handlelength=2.2, fontsize=7.2)
    return svg(fig)


def fig_cdf():
    fig, ax = plt.subplots(figsize=(7.2, 2.1))
    for k, c, lbl in (('s1', S1, '1회'), ('s2', S2, '2회'), ('s3', S3, '3회')):
        v = np.sort([r['dtMs'] for r in L(k)['frames']])
        y = np.arange(1, len(v) + 1) / len(v)
        ax.plot(v, y, color=c, lw=1.4, label=f'{lbl}  p50 {np.percentile(v, 50):.2f} · p99 {np.percentile(v, 99):.2f}ms')
    ax.set_xlim(1, 6); ax.set_ylim(0, 1.01)
    ax.set_xlabel('프레임 시간 (ms)'); ax.set_ylabel('누적 비율')
    ax.legend(loc='lower right')
    return svg(fig)


def bands(ax, d, ymax, labels=True):
    segs = [(t, c) for t, c, f in d['segt'] if t is not None]
    end = d['frames'][-1]['t']
    for i, (t, c) in enumerate(segs):
        t1 = segs[i + 1][0] if i + 1 < len(segs) else end
        if c.startswith('b'):
            ax.axvspan(t, t1, color=S2, alpha=0.08, lw=0)
            if labels: ax.text((t + t1) / 2, ymax * 0.97, c.split('_')[0], ha='center', va='top', fontsize=6.6, color='#8a3708')
        elif c.startswith('c'):
            ax.axvspan(t, t1, color=S3, alpha=0.16, lw=0)
        elif c == 'a_sweep' and labels:
            ax.text(t1 / 2, ymax * 0.97, 'ⓐ 팔 순회', ha='center', va='top', fontsize=6.8, color=MUTED)


def binned(frames, key, w=0.5, how='mean'):
    out = defaultdict(list)
    for r in frames: out[int(r['t'] / w)].append(r[key])
    xs = sorted(out)
    return [x * w for x in xs], [(max(out[x]) if how == 'max' else sum(out[x]) / len(out[x])) for x in xs]


def fig_live():
    s1, c2 = L('s1'), L('c2')
    fig, ax = plt.subplots(figsize=(7.2, 2.4))
    bands(ax, s1, 200)
    x, y = binned(s1['frames'], 'live'); ax.plot(x, y, color=S2, lw=1.2, label='① Sector (개발 빌드)')
    x, y = binned(c2['frames'], 'live'); ax.plot(x, y, color=S1, lw=1.2, label='⑦ CullingGroup (에디터)')
    ax.set_xlim(0, 296.5); ax.set_ylim(0, 200)
    ax.set_xlabel('하네스 시각 (초, ⓐ 시작 = 0) · 초록 띠 = ⓒ 왕복, 주황 띠 = ⓑ 끌기'); ax.set_ylabel('살아 있는 몹 (0.5초 평균)')
    ax.legend(loc='upper left', bbox_to_anchor=(0.09, 0.86), fontsize=7)
    return svg(fig)


def fig_dt():
    s1 = L('s1')
    fig, ax = plt.subplots(figsize=(7.2, 2.1))
    bands(ax, s1, 75, labels=False)
    x, y = binned(s1['frames'], 'dtMs', 0.25, 'max'); ax.plot(x, y, color=S2, lw=0.9)
    fr = s1['frames']; i = max(range(len(fr)), key=lambda i: fr[i]['spawns'])
    ax.annotate(f"한 프레임 스폰 {int(fr[i]['spawns'])} → {fr[i]['dtMs']:.1f}ms", (fr[i]['t'], fr[i]['dtMs']),
                xytext=(fr[i]['t'] + 18, 60), fontsize=7, color=INK, arrowprops=dict(arrowstyle='-', color=FAINT, lw=0.8))
    ax.set_xlim(0, 296.5); ax.set_ylim(0, 75)
    ax.set_xlabel('하네스 시각 (초)'); ax.set_ylabel('프레임 시간 (ms, 0.25초 최대)')
    return svg(fig)


def fig_policy():
    m, slots = slots_map()
    st, _ = shadow_sweep(L('c2'), slots)
    fig, axs = plt.subplots(1, 2, figsize=(7.2, 2.4), sharey=True)
    ys = np.arange(7)[::-1]
    for ax, key, idx, title, xmax in ((axs[0], 'mean', 0, '평균 활성 (몹, 시간 가중)', 215),
                                      (axs[1], 'peak', 1, '최대 활성 (몹)', 340)):
        vals = [st[p][key] for p in range(1, 8)]; pred = [PRED[p][idx] for p in range(1, 8)]
        ax.barh(ys, vals, height=0.55, color=S1, zorder=2)
        ax.scatter(pred, ys, marker='|', s=170, color=S2, linewidths=2.2, zorder=3)
        for y, v in zip(ys, vals):
            ax.text(v + xmax * 0.03, y, f'{v:.1f}' if key == 'mean' else f'{v:.0f}', va='center', fontsize=7, color=INK)
        ax.set_title(title); ax.set_xlim(0, xmax); ax.grid(axis='y', visible=False)
    axs[0].set_yticks(ys); axs[0].set_yticklabels([NAMES[p] for p in range(1, 8)])
    axs[1].scatter([], [], marker='|', s=120, color=S2, linewidths=2.2, label='예측 (Predictions.pdf)')
    axs[1].barh([], [], color=S1, label='하네스 실측 (그림자)')
    axs[1].legend(loc='lower right')
    return svg(fig), st


def fig_scatter():
    fig, ax = plt.subplots(figsize=(7.2, 2.4))
    xs, ys, xr, yr = [], [], [], []
    for k in ('s1', 's2', 's3'):
        for r in L(k)['frames']:
            if r['spawns'] > 0 and r['recalls'] == 0: xs.append(r['spawns']); ys.append(r['dtMs'])
            if r['recalls'] > 0 and r['spawns'] == 0: xr.append(r['recalls']); yr.append(r['dtMs'])
    ax.scatter(xs, ys, s=9, color=S1, alpha=0.55, linewidths=0, label=f'스폰만 있는 프레임 ({len(xs)})')
    ax.scatter(xr, yr, s=9, color=S2, alpha=0.55, linewidths=0, label=f'회수만 있는 프레임 ({len(xr)})')
    a, b = np.polyfit(xs, ys, 1); ar, br = np.polyfit(xr, yr, 1)
    gx = np.array([0, 125])
    ax.plot(gx, a * gx + b, color=S1, lw=1); ax.plot(gx, ar * gx + br, color=S2, lw=1)
    ax.text(122, a * 122 + b + 2.5, f'스폰 1마리당 +{a:.2f}ms', ha='right', fontsize=7.2, color=INK)
    ax.text(122, ar * 122 + br + 2.5, f'회수 1마리당 +{ar:.2f}ms', ha='right', fontsize=7.2, color=INK)
    ax.set_xlim(0, 125); ax.set_ylim(0, 75)
    ax.set_xlabel('그 프레임의 스폰 / 회수 수'); ax.set_ylabel('프레임 시간 (ms)')
    ax.legend(loc='upper left')
    return svg(fig), (a, b, ar, br)


def spawn_breakdown(keys):
    tot = defaultdict(float); n = 0
    for k in keys:
        for r in L(k)['frames']:
            for c in ('popUs', 'statUs', 'onSpawnUs', 'activateUs'): tot[c] += r[c]
            n += r['spawns']
    return {c: tot[c] / n for c in ('popUs', 'statUs', 'onSpawnUs', 'activateUs')}, n


def fig_breakdown():
    dev, n1 = spawn_breakdown(('s1', 's2', 's3')); ed, n2 = spawn_breakdown(('c1', 'c2'))
    fig, ax = plt.subplots(figsize=(7.2, 1.45))
    cols = [('popUs', '풀 꺼내기', S1), ('statUs', 'SetStat', S2), ('onSpawnUs', 'OnSpawn', S3), ('activateUs', 'SetActive', S4)]
    for y, d in ((1, dev), (0, ed)):
        left = 0
        for c, name, col in cols:
            ax.barh(y, d[c], left=left, height=0.55, color=col, edgecolor='white', linewidth=1.5)
            if d[c] > 40:
                ax.text(left + d[c] / 2, y, f'{name} {d[c]:.0f}', ha='center', va='center', fontsize=6.8,
                        color='white' if col in (S1,) else INK)
            left += d[c]
        ax.text(left + 6, y, f'{left:.0f}μs  (SetStat {d["statUs"]:.0f})', va='center', fontsize=7.3, color=INK)
    ax.set_yticks([1, 0]); ax.set_yticklabels([f'① 개발 빌드 · {n1:.0f}회', f'⑦ 에디터 · {n2:.0f}회'])
    ax.set_xlim(0, 680); ax.set_ylim(-0.5, 1.5)
    ax.set_xlabel('스폰 1회 평균 (μs)'); ax.grid(axis='y', visible=False)
    return svg(fig), dev, ed, n1, n2


def fig_deferred():
    fig, axs = plt.subplots(1, 2, figsize=(7.2, 1.9), sharey=True)
    for ax, seg, title in ((axs[0], 'b1_drag_1018', 'ⓑ1 · 1018 (S4 → S5 쪽)'), (axs[1], 'b2_drag_1005', 'ⓑ2 · 1005 (S2 → S3 쪽)')):
        for k, col, lbl in (('s1', S2, '① 개발 빌드'), ('c2', S1, '⑦ 에디터')):
            d = L(k)
            t0 = next(t for t, c, f in d['segt'] if c == seg)
            t1 = next(t for t, c, f in d['segt'] if t is not None and t > t0)
            pts = [(r['t'] - t0, r['deferred']) for r in d['frames'] if t0 - 2 <= r['t'] <= t1 + 6]
            ax.step(*zip(*pts), where='post', color=col, lw=1.3, label=lbl)
            ax.axvline(t1 - t0, color=FAINT, lw=0.8, ls=':')
        ax.set_title(title); ax.set_xlabel('구간 시작 후 (초) · 점선 = 구간 끝')
    axs[0].set_ylabel('유예 중인 몹'); axs[1].legend(loc='upper right')
    return svg(fig)
