# 계측 로그 분석 (계획서 0-4 · 0-5) — FieldMetrics CSV(frames · events) + 같은 시각의 ActivationLog CSV → 3절 측정 항목 · 탈락 조건.
#  사용: python analyze_field.py <MetricsLogs/field_*_frames.csv> [--skip 초]
#        python analyze_field.py <frames.csv> <frames.csv> ...      ← 여러 개면 실행 비교(하네스 3회 편차 검증)
#  분석 구간: 하네스 기록(segment 이벤트)이 있으면 첫 구간(a_sweep) 시작 ~ end. 없으면 앞 --skip초(기본 3)를 버린 나머지.
#  같은 폴더의 field_*_events.csv · activation_*.csv(같은 이름 · 시각)를 자동으로 찾는다. 없으면 그 항목만 건너뛴다.
import csv, math, os, re, sys
from collections import OrderedDict

sys.stdout.reconfigure(encoding='utf-8')

FLICKER_SEC = 2.0   # 경계 떨림 = 끈 뒤 2초 안에 다시 켬 (계획서 3절, analyze_shadow.py와 같음)
# 탈락 판정은 왕복 구간(c*)만 — 경로가 되돌아오는 곳(팔 끝 등)에선 히스테리시스가 지켜져도 2초 안에 다시 켜질 수 있다.
#  (첫 하네스 실행: ⑦이 25.02m에서 끄고 팔 끝에서 되돌아와 19.99m에서 1.3초 뒤 다시 켬 — 경로 탓이지 경계 떨림이 아님)
MARKERS = [('selectUs', '정책 선택(실제)'), ('shadowUs', '정책 선택(그림자)'), ('tickUs', '스포너 틱'),
           ('popUs', '스폰: 풀 꺼내기'), ('statUs', '스폰: SetStat'), ('onSpawnUs', '스폰: OnSpawn'),
           ('activateUs', '스폰: SetActive'), ('recallUs', '정책 회수'), ('metricsUs', '계측 자체')]
SPAWN_STEPS = ['popUs', 'statUs', 'onSpawnUs', 'activateUs']


def read(path):
    lines = open(path, encoding='utf-8-sig').read().splitlines()
    header = [l for l in lines if l.startswith('#')]
    rows = list(csv.DictReader(l for l in lines if not l.startswith('#')))
    return header, rows


def pct(values, q):
    """최근접 순위 백분위 (q = 0~100). 빈 목록이면 nan."""
    if not values: return math.nan
    s = sorted(values)
    return s[min(len(s) - 1, max(0, math.ceil(q / 100 * len(s)) - 1))]


def flicker_cases(activation_path):
    """실제 정책만. 끈 뒤 2초 안에 다시 켠 경우 [(frame, spawnerId, time, x, z)]."""
    header, rows = read(activation_path)
    real = next((int(l.split()[1]) for l in header if '실제' in l), None)
    last_off, cases = {}, []
    for r in rows:
        if real is not None and int(r['policy']) != real: continue
        sid, t = int(r['spawnerId']), float(r['time'])
        if r['state'] == 'off': last_off[sid] = t
        elif sid in last_off and t - last_off[sid] <= FLICKER_SEC:
            cases.append((int(r['frame']), sid, t, float(r['playerX']), float(r['playerZ'])))
    return real, cases


def segment_of(frame, segments):
    """segments = [(시작 프레임, 이름)] 정렬. 그 프레임이 속한 구간 이름."""
    name = None
    for f, label in segments:
        if f > frame: break
        name = label
    return name


def group(label):
    return 'move' if label in (None, 'move', 'settle') else label


def load(frames_path, skip):
    stem = re.sub(r'_frames\.csv$', '', frames_path)
    events_path = stem + '_events.csv'
    activation_path = os.path.join(os.path.dirname(frames_path), re.sub(r'^field_', 'activation_', os.path.basename(stem)) + '.csv')

    header, fr = read(frames_path)
    ev = read(events_path)[1] if os.path.exists(events_path) else None
    segments = sorted((int(e['frame']), e['cause']) for e in ev if e['kind'] == 'segment') if ev else []

    starts = [f for f, label in segments if label not in ('settle', 'move', 'end')]
    end = next((f for f, label in segments if label == 'end'), None)
    if starts and skip is None:
        lo, hi, how = starts[0], end if end is not None else 1 << 30, '하네스 구간 ' + ('a_sweep ~ end' if end else 'a_sweep ~ (end 없음 — 중단된 실행?)')
    else:
        skip = 3.0 if skip is None else skip
        t, lo = 0.0, None
        for r in fr:
            if t >= skip: lo = int(r['frame']); break
            t += float(r['dtMs']) / 1000
        lo, hi, how = (lo if lo is not None else 1 << 30), 1 << 30, f'앞 {skip}초 버림'

    fr = [r for r in fr if lo <= int(r['frame']) < hi]
    if ev is not None: ev = [e for e in ev if lo <= int(e['frame']) < hi]
    flick = None
    if os.path.exists(activation_path):
        real, cases = flicker_cases(activation_path)
        flick = (real, [c for c in cases if lo <= c[0] < hi])
    return dict(header=header, frames=fr, events=ev, segments=segments, flicker=flick, how=how,
                events_path=events_path, activation_path=activation_path, name=os.path.basename(stem))


def summarize(d):
    fr, ev = d['frames'], d['events']
    f = lambda k: [float(r[k]) for r in fr]
    n = len(fr)
    s = OrderedDict()
    s['frames'] = n
    s['sec'] = sum(f('dtMs')) / 1000
    s['dt_p50'], s['dt_p99'], s['dt_max'] = pct(f('dtMs'), 50), pct(f('dtMs'), 99), max(f('dtMs'))
    s['main_p50'], s['main_p99'] = pct(f('mainMs'), 50), pct(f('mainMs'), 99)
    s['spawns'], s['recalls'], s['defers'] = int(sum(f('spawns'))), int(sum(f('recalls'))), int(sum(f('defers')))
    s['max_spawn_frame'], s['max_recall_frame'] = int(max(f('spawns'))), int(max(f('recalls')))
    s['live_mean'], s['live_max'] = sum(f('live')) / n, int(max(f('live')))
    if ev is not None:
        s['on_spawn'] = sum(e['kind'] == 'spawn' and e['cause'] == 'activate' and e['onScreen'] == '1' for e in ev)
        s['on_recall'] = sum(e['kind'] == 'recall' and e['onScreen'] == '1' for e in ev)
        s['engaged_recall'] = sum(e['kind'] == 'recall' and e['engaged'] == '1' for e in ev)
    if d['flicker'] is not None:
        s['flicker'] = len(d['flicker'][1])
        if d['segments']:
            s['flicker_wiggle'] = sum(group(segment_of(c[0], d['segments'])).startswith('c') for c in d['flicker'][1])
    s['pool_grows'] = sum(int(re.search(r'확장 (\d+)', l).group(1)) for l in d['header'] if l.startswith('# pool '))
    return s


def report(d):
    header, fr, ev = d['header'], d['frames'], d['events']
    print('\n'.join(header))
    if not fr:
        print('분석 구간에 프레임이 없음'); return
    f = lambda k: [float(r[k]) for r in fr]
    n = len(fr)
    total_sec = sum(f('dtMs')) / 1000
    print(f'\n분석 구간: 프레임 {fr[0]["frame"]}~{fr[-1]["frame"]} ({n}프레임 · {total_sec:.1f}초, {d["how"]})')

    head = ' '.join(header)
    m_fps, m_vs = re.search(r'targetFrameRate: (-?\d+)', head), re.search(r'vSyncCount: (\d+)', head)
    if (m_fps and int(m_fps.group(1)) > 0) or (m_vs and int(m_vs.group(1)) > 0):
        print('⚠ 프레임 상한(targetFrameRate · vSync)이 걸린 측정 — dt · mainMs가 상한에 붙는다. 비용 비교엔 마커 열만 본다')
    if 'build: editor' in head:
        print('⚠ 에디터 측정 — 프레임 시간 p99 · 최대는 에디터 잡음이 섞인다. 비용 비교는 개발 빌드로')

    # ---- 비용
    dt, main = f('dtMs'), f('mainMs')
    print('\n[비용] 프레임 시간 (ms)        p50      p99      최대')
    print(f'  프레임 길이 dtMs        {pct(dt, 50):8.2f} {pct(dt, 99):8.2f} {max(dt):8.2f}   평균 FPS {n / total_sec:.1f}')
    print(f'  메인 스레드 mainMs      {pct(main, 50):8.2f} {pct(main, 99):8.2f} {max(main):8.2f}')

    print('\n[비용] 마커 (μs/프레임)         평균      p99      최대')
    for k, label in MARKERS:
        v = f(k)
        print(f'  {label:18} {sum(v) / n:9.1f} {pct(v, 99):8.1f} {max(v):8.1f}')

    spawns, recalls, defers = f('spawns'), f('recalls'), f('defers')
    total_spawns = sum(spawns)
    if total_spawns:
        per = {k: sum(f(k)) / total_spawns for k in SPAWN_STEPS}
        print(f'  스폰 1회 평균: {sum(per.values()):.1f}μs = ' + ' + '.join(f'{k[:-2]} {v:.1f}' for k, v in per.items()))
    if sum(recalls):
        print(f'  회수 1회 평균: {sum(f("recallUs")) / sum(recalls):.1f}μs')

    print('\n[비용] 전환 스파이크')
    i_s = max(range(n), key=lambda i: spawns[i]); i_r = max(range(n), key=lambda i: recalls[i])
    print(f'  한 프레임 최대 스폰 {int(spawns[i_s])} (프레임 {fr[i_s]["frame"]} · dt {dt[i_s]:.2f}ms) · '
          f'최대 회수 {int(recalls[i_r])} (프레임 {fr[i_r]["frame"]} · dt {dt[i_r]:.2f}ms)')
    print(f'  합계: 스폰 {int(total_spawns)} · 정책 회수 {int(sum(recalls))} · 교전 유예 {int(sum(defers))}')

    print('\n[비용] 풀 · 메모리')
    for l in header:
        if l.startswith('# pool '): print('  ' + l[2:])
    mem = f('memMB')
    print(f'  할당 메모리 MB: 시작 {mem[0]:.1f} · 끝 {mem[-1]:.1f} · 최대 {max(mem):.1f}')

    # ---- 현상
    s = summarize(d)
    if ev is not None:
        sp = [e for e in ev if e['kind'] == 'spawn']
        rc = [e for e in ev if e['kind'] == 'recall']
        print('\n[현상]')
        print(f'  화면 안 등장(정책 때문, 리스폰 제외) {s["on_spawn"]} / 스폰 {len(sp)} '
              f'(리스폰 {sum(e["cause"] == "respawn" for e in sp)} 중 화면 안 {sum(e["cause"] == "respawn" and e["onScreen"] == "1" for e in sp)})')
        print(f'  화면 안 소멸(정책 회수) {s["on_recall"]} / 회수 {len(rc)} (유예 뒤 회수 {sum(e["cause"] == "deferred" for e in rc)})')
        print(f'  끌고 온 몬스터 소실(교전 중 회수) {s["engaged_recall"]}')
        for label, cond in (('화면 안 등장', lambda e: e['kind'] == 'spawn' and e['cause'] == 'activate' and e['onScreen'] == '1'),
                            ('화면 안 소멸', lambda e: e['kind'] == 'recall' and e['onScreen'] == '1'),
                            ('교전 중 회수', lambda e: e['kind'] == 'recall' and e['engaged'] == '1')):
            for e in [e for e in ev if cond(e)][:5]:
                print(f'    {label}: 프레임 {e["frame"]} · 스포너 {e["spawnerId"]} · ({e["x"]}, {e["z"]}) · 뷰포트 ({e["vx"]}, {e["vy"]})')
    else:
        print(f'\n[현상] 이벤트 파일 없음: {d["events_path"]}')

    deferred = f('deferred')
    print(f'  교전 유예 최대 {int(max(deferred))} · 마지막 {int(deferred[-1])} (누수 검증: 플레이어가 떠난 뒤 0이어야 함)')
    if d['flicker'] is not None:
        real, cases = d['flicker']
        print(f'  경계 떨림(실제 정책 {real}) {len(cases)}' + ''.join(
            f'\n    스포너 {c[1]} · {c[2]:.2f}s · 플레이어 ({c[3]:.1f}, {c[4]:.1f})' for c in cases[:5]))
    else:
        print(f'  경계 떨림: 켜고 끈 로그 없음 ({d["activation_path"]})')

    # ---- 구간별 (하네스)
    if d['segments'] and ev is not None:
        print('\n[구간별]                     초   dt p50   dt p99  스폰  회수  유예  화면안 등장/소멸  교전중회수  떨림  몹 최대')
        rows = OrderedDict()
        for r in fr:
            g = group(segment_of(int(r['frame']), d['segments']))
            rows.setdefault(g, dict(dt=[], spawns=0, recalls=0, defers=0, live=0, on_s=0, on_r=0, eng=0, flick=0))
            x = rows[g]
            x['dt'].append(float(r['dtMs'])); x['spawns'] += int(r['spawns']); x['recalls'] += int(r['recalls'])
            x['defers'] += int(r['defers']); x['live'] = max(x['live'], int(r['live']))
        for e in ev:
            g = group(segment_of(int(e['frame']), d['segments']))
            if g not in rows: continue
            if e['kind'] == 'spawn' and e['cause'] == 'activate' and e['onScreen'] == '1': rows[g]['on_s'] += 1
            if e['kind'] == 'recall' and e['onScreen'] == '1': rows[g]['on_r'] += 1
            if e['kind'] == 'recall' and e['engaged'] == '1': rows[g]['eng'] += 1
        if d['flicker'] is not None:
            for c in d['flicker'][1]:
                g = group(segment_of(c[0], d['segments']))
                if g in rows: rows[g]['flick'] += 1
        for g, x in rows.items():
            print(f'  {g:24} {sum(x["dt"]) / 1000:6.1f} {pct(x["dt"], 50):8.2f} {pct(x["dt"], 99):8.2f} {x["spawns"]:5} {x["recalls"]:5} {x["defers"]:5}'
                  f'   {x["on_s"]:5} / {x["on_r"]:<5}        {x["eng"]:5}  {x["flick"]:4}  {x["live"]:6}')

    # ---- 참고
    live, onscr = f('live'), f('onScreen')
    sum_live = sum(live)
    print('\n[참고]')
    print(f'  스포너 몹 평균 {sum_live / n:.1f} · 최대 {int(max(live))} · 화면 밖 비율 {(1 - sum(onscr) / sum_live) * 100 if sum_live else math.nan:.1f}%'
          f' · 켜진 스포너 평균 {sum(f("activeSpawners")) / n:.1f}')

    # ---- 탈락 조건 (계획서 2절)
    print('\n[탈락 조건]')
    def verdict(name, value, ok):
        print(f'  {"통과" if ok else "탈락"}  {name}: {value}')
    if ev is not None:
        verdict('화면 안 등장·소멸 0회', f'{s["on_spawn"]} / {s["on_recall"]}', s['on_spawn'] == 0 and s['on_recall'] == 0)
        verdict('끌고 온 몬스터 소실 0회', s['engaged_recall'], s['engaged_recall'] == 0)
    if d['flicker'] is not None:
        if d['segments']:
            verdict('경계 떨림 0회 (왕복 구간 c*)', f'{s["flicker_wiggle"]} (전체 {s["flicker"]} — 나머지는 경로가 되돌아온 곳)', s['flicker_wiggle'] == 0)
        else:
            verdict('경계 떨림 0회 (하네스가 아님 — 참고)', s['flicker'], s['flicker'] == 0)
    verdict('풀 확장 0회', s['pool_grows'], s['pool_grows'] == 0)
    print('  (p99 기준은 ① 기준선 측정 후 확정)')


def compare(ds):
    """실행 여러 개 — 같은 정책 3회면 편차 검증(0-5): 이벤트 수는 같아야 하고, 시간 지표는 편차를 본다."""
    sums = [summarize(d) for d in ds]
    keys = list(sums[0].keys())
    width = max(len(d['name']) for d in ds)
    print('실행 비교 — 분석 구간: ' + ' / '.join(sorted({d['how'] for d in ds})))
    for i, d in enumerate(ds): print(f'  [{i + 1}] {d["name"]}')
    print(f'\n{"지표":18}' + ''.join(f'{"[" + str(i + 1) + "]":>11}' for i in range(len(ds))) + f'{"범위":>11}{"범위/평균":>10}')
    for k in keys:
        vals = [s.get(k, math.nan) for s in sums]
        lo, hi = min(vals), max(vals)
        mean = sum(vals) / len(vals)
        rel = (hi - lo) / mean * 100 if mean else 0.0
        fmt = (lambda v: f'{v:11.2f}') if isinstance(vals[0], float) else (lambda v: f'{v:11}')
        print(f'{k:18}' + ''.join(fmt(v) for v in vals) + fmt(hi - lo) + f'{rel:9.1f}%')
    print('\n이벤트 수(spawns · recalls · defers · on_* · engaged_recall · flicker)가 실행마다 다르면 하네스 쪽 재현성 문제부터 본다.')


def main():
    args = sys.argv[1:]
    skip = None
    if '--skip' in args:
        i = args.index('--skip'); skip = float(args[i + 1]); del args[i:i + 2]
    ds = [load(p, skip) for p in args]
    if len(ds) == 1: report(ds[0])
    else: compare(ds)


if __name__ == '__main__':
    main()
