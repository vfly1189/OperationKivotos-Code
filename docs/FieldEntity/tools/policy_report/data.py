# 정책 7개 × 3회 측정 + 풀 꺼내기 수정 전후 — Policy_Result.html/pdf 생성용 집계 (gen.py가 부른다)
#  로그 위치: 개발 빌드는 빌드 폴더의 MetricsLogs, 에디터는 프로젝트 루트 MetricsLogs (둘 다 git 밖).
#  다른 측정으로 다시 만들려면 DEV · ED · RUNS · BEFORE_* 만 바꾼다.
import csv, glob, math, os, importlib.util
from collections import OrderedDict

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', '..')).replace(os.sep, '/')
DEV = os.environ.get('HARNESS_DEV_LOGS', 'C:/Users/c/Desktop/테스트/MetricsLogs')
ED = REPO + '/MetricsLogs'

spec = importlib.util.spec_from_file_location('af', REPO + '/docs/FieldEntity/tools/analyze_field.py')
af = importlib.util.module_from_spec(spec); spec.loader.exec_module(af)

POLICIES = ['Sector', 'AdjacentSector', 'Spotlight3x3', 'Spotlight5x5', 'Distance', 'SpotlightDistance', 'CullingGroup']
NUM = {p: '①②③④⑤⑥⑦'[i] for i, p in enumerate(POLICIES)}
SHORT = {'Sector': 'Sector', 'AdjacentSector': 'Adjacent', 'Spotlight3x3': 'Spot 3×3', 'Spotlight5x5': 'Spot 5×5',
         'Distance': 'Distance', 'SpotlightDistance': 'Spot+Dist', 'CullingGroup': 'Culling'}
FAMILY = {'Sector': 'sector', 'AdjacentSector': 'sector', 'Spotlight3x3': 'spot', 'Spotlight5x5': 'spot',
          'Distance': 'dist', 'SpotlightDistance': 'dist', 'CullingGroup': 'dist'}

# 이번 측정 = 빌드 22:24(풀 꺼내기 수정 · 무인 하네스) 이후 실행 21개
RUN_FROM = ('20261009', '222450')
# 수정 전후 — 에디터 하네스 ⑦ 그림자(같은 경로) · 개발 빌드 ① 새벽(수정 전, pop 분해 없음)
BEFORE_ED, AFTER_ED = 'CullingGroup_shadow_20261009_215814', 'CullingGroup_shadow_20261009_220950'
BEFORE_DEV = ['Sector_20261009_020552', 'Sector_20261009_021108', 'Sector_20261009_021623']
BUDGET = 1000 / 144   # 6.94ms


def runs_of(policy):
    fs = sorted(glob.glob(f'{DEV}/field_{policy}_2026*_frames.csv'))
    return [f for f in fs if tuple(os.path.basename(f).split('_')[-3:-1]) >= RUN_FROM]


def load(path):
    d = af.load(path, None)
    assert 'a_sweep ~ end' in d['how'], (path, d['how'])
    return d


def per_spawn(fr):
    """스폰 1회 분해 (μs) — 마커 합 ÷ 스폰 수."""
    g = lambda k: sum(float(r[k]) for r in fr if k in r)
    n = g('spawns')
    out = OrderedDict((k, g(k) / n) for k in ('popUs', 'statUs', 'onSpawnUs', 'activateUs'))
    if 'popFindUs' in fr[0]:
        out['popFindUs'], out['popParentUs'], out['popPoseUs'] = (g(k) / n for k in ('popFindUs', 'popParentUs', 'popPoseUs'))
    out['n'] = n
    return out


def classify_over(r):
    if float(r['spawns']) > 0 or float(r['recalls']) > 0: return 'churn'
    if float(r['metricsUs']) > 1000: return 'metrics'
    return 'other'


def summarize_run(path):
    d = load(path)
    s = af.summarize(d)
    fr = d['frames']
    s.update(per_spawn(fr))
    rc = sum(float(r['recalls']) for r in fr)
    s['recall_us'] = sum(float(r['recallUs']) for r in fr) / rc if rc else 0
    s['select_us'] = sum(float(r['selectUs']) for r in fr) / len(fr)
    s['mem_end'] = float(fr[-1]['memMB'])
    over = [r for r in fr if float(r['dtMs']) > BUDGET]
    s['over'] = len(over)
    for c in ('churn', 'metrics', 'other'):
        s['over_' + c] = sum(classify_over(r) == c for r in over)
    # 순간 비용 — 스폰 · 회수가 있는 프레임
    s['spawn_frames'] = [(int(float(r['spawns'])), float(r['dtMs'])) for r in fr if float(r['spawns']) > 0 and float(r['recalls']) == 0]
    s['recall_frames'] = [(int(float(r['recalls'])), float(r['dtMs'])) for r in fr if float(r['recalls']) > 0 and float(r['spawns']) == 0]
    worst = max(fr, key=lambda r: float(r['dtMs']))
    s['worst'] = {k: float(worst[k]) for k in ('dtMs', 'spawns', 'recalls', 'tickUs', 'popUs', 'onSpawnUs', 'activateUs', 'metricsUs')}
    big = [r for r in fr if float(r['spawns']) == s['max_spawn_frame']]
    s['bigspawn_dt'] = [float(r['dtMs']) for r in big]
    s['bigspawn_pop'] = [float(r['popUs']) / 1000 for r in big]
    # 순간이동(settle) 직후 화면 안 스폰 — 분석 구간 밖, 별도
    ev = af.read(path.replace('_frames.csv', '_events.csv'))[1]
    seg, c = None, 0
    for e in ev:
        if e['kind'] == 'segment': seg = e['cause']
        if e['kind'] == 'spawn' and e['onScreen'] == '1' and seg == 'settle': c += 1
    s['settle_onscreen'] = c
    s['pools'] = [l for l in d['header'] if l.startswith('# pool ')]
    s['path'] = os.path.basename(path)
    return s


def mean(v): return sum(v) / len(v)


def spread(v):
    m = mean(v); return (max(v) - min(v)) / m * 100 if m else 0.0


_cache = {}
def policy_runs():
    if 'pol' not in _cache:
        _cache['pol'] = OrderedDict((p, [summarize_run(f) for f in runs_of(p)]) for p in POLICIES)
    return _cache['pol']


def fit_live():
    """dt p50 ~ 살아 있는 몹 평균 — 정책 7개 평균점 최소제곱."""
    pol = policy_runs()
    xs = [mean([s['live_mean'] for s in pol[p]]) for p in POLICIES]
    ys = [mean([s['dt_p50'] for s in pol[p]]) for p in POLICIES]
    n = len(xs); mx, my = mean(xs), mean(ys)
    sxx = sum((x - mx) ** 2 for x in xs); sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    syy = sum((y - my) ** 2 for y in ys)
    a = sxy / sxx; b = my - a * mx; r = sxy / math.sqrt(sxx * syy)
    return a, b, r, xs, ys


def fit_spawn():
    """스폰만 있는 프레임: dt ~ 스폰 수 (21회 전부, 풀 확장한 ② 제외)."""
    pts = [pt for p, ss in policy_runs().items() if p != 'AdjacentSector' for s in ss for pt in s['spawn_frames']]
    xs, ys = [x for x, _ in pts], [y for _, y in pts]
    mx, my = mean(xs), mean(ys)
    a = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / sum((x - mx) ** 2 for x in xs)
    return a, my - a * mx, len(pts)


def before_after():
    """스폰 1회 분해 — 에디터 전/후(분해 있음), 개발 빌드 전(새벽 ①×3) · 후(① 이번 3회)."""
    out = OrderedDict()
    out['ed_before'] = per_spawn(load(f'{ED}/field_{BEFORE_ED}_frames.csv')['frames'])
    out['ed_after'] = per_spawn(load(f'{ED}/field_{AFTER_ED}_frames.csv')['frames'])
    def avg(dicts):
        return OrderedDict((k, mean([d[k] for d in dicts])) for k in dicts[0])
    out['dev_before'] = avg([per_spawn(load(f'{DEV}/field_{s}_frames.csv')['frames']) for s in BEFORE_DEV])
    out['dev_after'] = avg([per_spawn(load(f)['frames']) for f in runs_of('Sector')])
    return out


def find_vs_live():
    """수정 전 에디터 실행 — 스폰 프레임마다 (살아 있는 몹, 스폰당 씬 찾기 μs)."""
    fr = load(f'{ED}/field_{BEFORE_ED}_frames.csv')['frames']
    return [(float(r['live']), float(r['popFindUs']) / float(r['spawns'])) for r in fr if float(r['spawns']) > 0]


if __name__ == '__main__':
    import sys; sys.stdout.reconfigure(encoding='utf-8')
    for p, ss in policy_runs().items():
        print(p, [s['path'][-22:-11] for s in ss])
        for k in ('dt_p50', 'dt_p99', 'live_mean', 'spawns', 'over', 'over_churn', 'over_metrics', 'over_other', 'settle_onscreen', 'select_us', 'mem_end'):
            v = [s[k] for s in ss]; print(f'   {k:16} {v}  ({spread(v):.1f}%)')
        print('   worst', ss[0]['worst'])
        print('   bigspawn', ss[0]['max_spawn_frame'], [round(x, 1) for s in ss for x in s['bigspawn_dt']][:12])
    print('fit_live', fit_live()[:3]); print('fit_spawn', fit_spawn())
    for k, v in before_after().items(): print(k, {a: round(b, 1) for a, b in v.items()})
