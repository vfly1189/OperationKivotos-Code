# 그림자 측정 로그 분석 — ActivationLog CSV(정책 7개가 한 파일) → 집합 지표 · 동등성 · 히스테리시스 · 떨림.
#  사용: python analyze_shadow.py <MetricsLogs/activation_*_shadow_*.csv> [original|balanced]
#        맵 배치를 생략하면 로그의 스포너 위치로 자동 판별한다 (변형 맵은 1009가 S3로 옮겨져 있음).
#  몬스터 수 = 스포너의 스폰 포인트 수 (maps/*.json, parse_map.py로 프리팹에서 생성) — 예측(Predictions.pdf)과 같은 기준.
#  예측과 정의가 다른 점: 평균은 프레임 가중(서 있던 시간 포함), 예측은 경로 0.5m 간격 평균.
import csv, json, math, os, sys
from collections import defaultdict

sys.stdout.reconfigure(encoding='utf-8')

ON_RADIUS, OFF_RADIUS = 20.0, 25.0   # 계획서 0-1
FLICKER_SEC = 2.0                    # 경계 떨림 = 끈 뒤 2초 안에 다시 켬 (계획서 3절)

NAMES = {1: '① Sector', 2: '② +인접', 3: '③ 3×3', 4: '④ 5×5', 5: '⑤ 거리', 6: '⑥ 창+거리', 7: '⑦ CG'}
# 예측 (03_Comparison_Plan 4절): 평균 활성 · 최대 활성 · 한 걸음 최대 변경 (몬스터 수)
PREDICTION = {
    'original': {1: (71, 187, 119), 2: (186, 295, 119), 3: (49, 102, 54), 4: (40, 102, 51), 5: (33, 102, 34), 6: (33, 102, 34), 7: (33, 102, 34)},
    'balanced': {1: (73, 153, 85), 2: (199, 295, 85), 3: (56, 85, 68), 4: (45, 68, 51), 5: (33, 68, 17), 6: (33, 68, 17), 7: (33, 68, 17)},
}


def load_map(layout):
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'maps', f'{layout}.json')
    m = json.load(open(path, encoding='utf-8'))
    slots, center = {}, {}
    for s in m['spawners']:
        sid = int([v for k, v in s['ids'] if k == '_spawnerId'][0])
        slots[sid] = len(s['kids'])
        center[sid] = (s['pos'][0], s['pos'][2])
    return slots, center


def load_log(path):
    lines = open(path, encoding='utf-8-sig').read().splitlines()
    header = [l for l in lines if l.startswith('#')]
    frames = next(l for l in header if 'frames:' in l).split('frames:')[1].split('·')[0].strip()
    start, end = map(int, frames.split('~'))
    rows = [dict(frame=int(r['frame']), time=float(r['time']), pol=int(r['policy']), id=int(r['spawnerId']),
                 on=r['state'] == 'on', x=float(r['playerX']), z=float(r['playerZ']), d=float(r['distance']))
            for r in csv.DictReader(l for l in lines if not l.startswith('#'))]
    return header, start, end, rows


def map_error(rows, center):
    return max(abs(math.dist((r['x'], r['z']), center[r['id']]) - r['d']) for r in rows if r['id'] in center)


def policy_stats(rows, slots, start, end):
    """정책별 집합 지표. series = [(frame, 활성 몬스터 수)] 계단 함수 — 그 프레임부터 그 값."""
    out = {}
    for p in range(1, 8):
        ev = [r for r in rows if r['pol'] == p]
        if not ev: continue
        byframe = defaultdict(list)
        for r in ev: byframe[r['frame']].append(r)
        first = min(byframe)   # 첫 스폰은 전환이 아니라 로딩 — ⑦은 1프레임 늦게 온다

        cur = 0; last_f = start; area = 0; peak = 0; max_change = 0; init = 0
        last_off = {}; flicker = 0; series = [(start, 0)]
        for fr in sorted(byframe):
            area += cur * (fr - last_f); last_f = fr
            change = 0
            for r in byframe[fr]:
                n = slots[r['id']]
                if r['on']:
                    cur += n
                    if r['id'] in last_off and r['time'] - last_off[r['id']] <= FLICKER_SEC: flicker += 1
                else:
                    cur -= n; last_off[r['id']] = r['time']
                change += n
            peak = max(peak, cur); series.append((fr, cur))
            if fr == first: init = change
            else: max_change = max(max_change, change)
        area += cur * (end - last_f)

        on = sum(r['on'] for r in ev)
        out[p] = dict(on=on, off=len(ev) - on, mean=area / (end - start), peak=peak, max_change=max_change,
                      flicker=flicker, init=init, series=series)
    return out


def main():
    log = sys.argv[1]
    header, start, end, rows = load_log(log)

    if len(sys.argv) > 2:
        layout = sys.argv[2]
    else:   # 로그 거리와 더 잘 맞는 배치
        layout = min(PREDICTION, key=lambda l: map_error(rows, load_map(l)[1]))
    slots, center = load_map(layout)

    total = end - start
    print(f"{os.path.basename(log)}")
    print(f"배치 {layout} · 프레임 {start}~{end} ({total}) · {rows[0]['time']:.0f}~{rows[-1]['time']:.0f}s · {len(rows)}줄")
    print(f"로그 거리 vs 맵 좌표 최대 오차 {map_error(rows, center):.3f} m")
    for l in header:
        if l.startswith('# check'): print(l[2:])

    pred = PREDICTION[layout]
    print()
    print(f"{'정책':10} {'켬':>4} {'끔':>4} {'평균활성':>8} {'최대':>5} {'최대변경':>8} {'떨림':>5} {'첫스폰':>6} | 예측 평균/최대/변경")
    for p, st in policy_stats(rows, slots, start, end).items():
        pa, pm, pc = pred[p]
        print(f"{NAMES[p]:10} {st['on']:>4} {st['off']:>4} {st['mean']:>8.1f} {st['peak']:>5} {st['max_change']:>8} {st['flicker']:>5} {st['init']:>6} | {pa}/{pm}/{pc}")

    print("\n히스테리시스 (켬 ≤ 20m · 끔 ≥ 25m)")
    for p in (5, 6, 7):
        ev = [r for r in rows if r['pol'] == p]
        ons = [r['d'] for r in ev if r['on']]; offs = [r['d'] for r in ev if not r['on']]
        bad = sum(d > ON_RADIUS + 1e-3 for d in ons) + sum(d < OFF_RADIUS - 1e-3 for d in offs)
        print(f"  {NAMES[p]}: 켬 최대 {max(ons, default=float('nan')):.2f} · 끔 최소 {min(offs, default=float('nan')):.2f} · 위반 {bad}")

    print("\n떨림 사례")
    last = {}
    for r in rows:
        k = (r['pol'], r['id'])
        if not r['on']: last[k] = r
        elif k in last and r['time'] - last[k]['time'] <= FLICKER_SEC:
            o = last[k]
            print(f"  {NAMES[r['pol']]} 스포너 {r['id']}: 끔 {o['time']:.1f}s ({o['x']:.2f}, {o['z']:.2f}) → 켬 {r['time']:.1f}s ({r['x']:.2f}, {r['z']:.2f})")

    reached = {r['id'] for r in rows if r['pol'] == 5 and r['on']}
    print(f"\n⑤가 켠 스포너 {len(reached)}/{len(slots)} · 안 닿은 것 {sorted(set(slots) - reached)}")


if __name__ == '__main__':
    main()
