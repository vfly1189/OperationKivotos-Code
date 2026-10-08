# 측정 기록 로드 · 집계 — Harness_Result.html/pdf 생성용 (gen.py가 부른다)
#  RUNS = 문서에 쓴 실행 5개. 로그 위치: 개발 빌드는 빌드 폴더의 MetricsLogs, 에디터는 프로젝트 루트 MetricsLogs (둘 다 git 밖).
#  다른 측정으로 문서를 다시 만들려면 DEV · ED · RUNS만 바꾼다.
import csv, json, math, os
from collections import defaultdict, OrderedDict
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', '..')).replace(os.sep, '/')
DEV = os.environ.get('HARNESS_DEV_LOGS', 'C:/Users/c/Desktop/테스트/MetricsLogs')
ED = REPO + '/MetricsLogs'
RUNS = OrderedDict([
    ('s1', (DEV, 'Sector_20261009_020552')), ('s2', (DEV, 'Sector_20261009_021108')), ('s3', (DEV, 'Sector_20261009_021623')),
    ('c1', (ED, 'CullingGroup_shadow_20261009_014246')), ('c2', (ED, 'CullingGroup_shadow_20261009_015436')),
])

def read(path):
    lines = open(path, encoding='utf-8-sig').read().splitlines()
    return [l for l in lines if l.startswith('#')], list(csv.DictReader(l for l in lines if not l.startswith('#')))

def pct(v, q):
    s = sorted(v); return s[min(len(s)-1, max(0, math.ceil(q/100*len(s))-1))]

def load(key):
    d, stem = RUNS[key]
    hf, fr = read(f'{d}/field_{stem}_frames.csv')
    _, ev = read(f'{d}/field_{stem}_events.csv')
    _, act = read(f'{d}/activation_{stem}.csv')
    seg = [(int(e['frame']), float(e['time']), e['cause']) for e in ev if e['kind'] == 'segment']
    a0 = next(f for f, t, c in seg if c == 'a_sweep'); a1 = next(f for f, t, c in seg if c == 'end')
    # 프레임 → 하네스 시각(a_sweep = 0)
    t = 0.0; rows = []
    for r in fr:
        f = int(r['frame'])
        if f < a0 or f >= a1: continue
        rows.append(dict(frame=f, t=t, **{k: float(v) for k, v in r.items() if k != 'frame'}))
        t += float(r['dtMs']) / 1000
    ft = {r['frame']: r['t'] for r in rows}
    segs = [(ft.get(f, None), c, f) for f, tt, c in seg if a0 <= f <= a1]
    return dict(key=key, header=hf, frames=rows, events=[e for e in ev if a0 <= int(e['frame']) < a1], allev=ev,
                act=act, seg=seg, segt=segs, a0=a0, a1=a1, ft=ft)

def seg_of(frame, seg):
    name = None
    for f, t, c in seg:
        if f > frame: break
        name = c
    return name

def slots_map(layout='original'):
    m = json.load(open(f'{REPO}/docs/FieldEntity/tools/maps/{layout}.json', encoding='utf-8'))
    out = {}
    for s in m['spawners']:
        sid = int([v for k, v in s['ids'] if k == '_spawnerId'][0])
        out[sid] = dict(n=len(s['kids']), c=(s['pos'][0], s['pos'][2]), kids=[(k[0], k[2]) for k in s['kids']], sec=s['sector'][0])
    return m, out

PRED = {1: (71, 187, 119), 2: (186, 295, 119), 3: (49, 102, 54), 4: (40, 102, 51), 5: (33, 102, 34), 6: (33, 102, 34), 7: (33, 102, 34)}
NAMES = {1: '① Sector', 2: '② +인접', 3: '③ 3×3', 4: '④ 5×5', 5: '⑤ 거리', 6: '⑥ 창+거리', 7: '⑦ CullingGroup'}

def shadow_sweep(d, slots):
    """그림자 기록 → 경로 ⓐ(a_sweep) 구간의 정책별 평균(시간 가중) · 최대 · 한 프레임 최대 변경 · 떨림."""
    t0 = next(t for f, t, c in d['seg'] if c == 'a_sweep')
    t1 = next(t for f, t, c in d['seg'] if f > d['a0'] and c != 'a_sweep')
    out = {}
    for p in range(1, 8):
        ev = [r for r in d['act'] if int(r['policy']) == p]
        on = set(); cur = 0; area = 0.0; last = t0; peak = 0; maxch = 0
        byf = defaultdict(list)
        for r in ev: byf[int(r['frame'])].append(r)
        series = []
        for f in sorted(byf):
            tt = float(byf[f][0]['time'])
            if tt >= t1: break
            if tt > t0:
                area += cur * (tt - last); last = tt
            ch = 0
            for r in byf[f]:
                n = slots[int(r['spawnerId'])]['n']
                if r['state'] == 'on': cur += n
                else: cur -= n
                ch += n
            if tt >= t0:
                peak = max(peak, cur); maxch = max(maxch, ch); series.append((tt - t0, cur))
            else:
                series = [(0.0, cur)]
        area += cur * (t1 - max(last, t0))
        out[p] = dict(mean=area / (t1 - t0), peak=max(peak, series[0][1] if series else 0), maxch=maxch)
    return out, t1 - t0
