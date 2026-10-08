# 맵 프리팹(YAML) → 섹터 · 스포너 JSON. 시뮬레이션(예측)과 그림자 로그 분석이 같이 쓴다.
#  사용: python parse_map.py <BackStreetMap*.prefab> <출력.json>
#  spawners[].pos = 스포너 월드 위치, kids = 스폰 포인트 월드 위치(개수 = 몬스터 수), sector = 소속 섹터 ID

import re, json
import sys; p = sys.argv[1]
txt = open(p, encoding='utf-8').read()
parts = re.split(r'^--- !u!(\d+) &(-?\d+).*$', txt, flags=re.M)
objs = {}
for i in range(1, len(parts), 3):
    objs[parts[i + 1]] = (parts[i], parts[i + 2])
SECTOR = '4336bb93af908ca49998f0323b27f8bd'
SPAWN = '4f1f738160d7ded459b557f622569b7e'

def go_of(body):
    m = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
    return m and m.group(1)

def vec(body, key):
    m = re.search(key + r': \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)', body)
    return tuple(float(x) for x in m.groups()) if m else None

def quat(body):
    m = re.search(r'm_LocalRotation: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+), w: ([-\d.eE]+)', body)
    return tuple(float(x) for x in m.groups()) if m else (0, 0, 0, 1)

tr_of_go, father, trbody, children = {}, {}, {}, {}
names = {}
for fid, (cls, body) in objs.items():
    if cls in ('4', '224'):
        g = go_of(body); tr_of_go[g] = fid; trbody[fid] = body
        m = re.search(r'm_Father: \{fileID: (-?\d+)\}', body); father[fid] = m.group(1) if m else '0'
        seg = body.split('m_Children:')[1].split('m_Father')[0] if 'm_Children:' in body else ''
        children[fid] = re.findall(r'\{fileID: (-?\d+)\}', seg)
    elif cls == '1':
        m = re.search(r'm_Name: (.*)', body); names[fid] = m.group(1).strip()
go_of_tr = {v: k for k, v in tr_of_go.items()}

def qrot(q, v):
    x, y, z, w = q
    # rotate v by quaternion
    ix = w * v[0] + y * v[2] - z * v[1]
    iy = w * v[1] + z * v[0] - x * v[2]
    iz = w * v[2] + x * v[1] - y * v[0]
    iw = -x * v[0] - y * v[1] - z * v[2]
    return (ix * w + iw * -x + iy * -z - iz * -y,
            iy * w + iw * -y + iz * -x - ix * -z,
            iz * w + iw * -z + ix * -y - iy * -x)

def qmul(a, b):
    ax, ay, az, aw = a; bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)

def chain_of(tr):
    chain, seen, t = [], set(), tr
    while t and t != '0' and t in trbody and t not in seen:
        seen.add(t); chain.append(t); t = father.get(t)
    return chain

def world(tr):
    pos, rot, scl = (0, 0, 0), (0, 0, 0, 1), (1, 1, 1)
    for t in reversed(chain_of(tr)):
        b = trbody[t]
        lp = vec(b, 'm_LocalPosition') or (0, 0, 0); ls = vec(b, 'm_LocalScale') or (1, 1, 1)
        sp = (lp[0] * scl[0], lp[1] * scl[1], lp[2] * scl[2])
        rp = qrot(rot, sp)
        pos = (pos[0] + rp[0], pos[1] + rp[1], pos[2] + rp[2])
        rot = qmul(rot, quat(b)); scl = (scl[0] * ls[0], scl[1] * ls[1], scl[2] * ls[2])
    return pos, rot, scl

def path(tr):
    return '/'.join(names.get(go_of_tr.get(t), '?') for t in reversed(chain_of(tr)))

sectors, spawners, boxes = [], [], {}
for fid, (cls, body) in objs.items():
    if cls == '114' and SECTOR in body:
        sectors.append((go_of(body), int(re.search(r'_sectorID: (-?\d+)', body).group(1))))
    if cls == '114' and SPAWN in body:
        spawners.append((go_of(body), body))
    if cls == '65':
        boxes.setdefault(go_of(body), []).append((vec(body, 'm_Size'), vec(body, 'm_Center'), re.search(r'm_IsTrigger: (\d)', body).group(1)))

out = {'sectors': [], 'spawners': []}
print('sectors', len(sectors))
for g, sid in sorted(sectors, key=lambda s: s[1]):
    tr = tr_of_go[g]; w, r, s = world(tr)
    print(sid, path(tr), 'pos', tuple(round(x, 1) for x in w), 'rot', tuple(round(x, 3) for x in r), 'scale', s, 'box', boxes.get(g))
    out['sectors'].append({'id': sid, 'tr': tr, 'pos': w, 'rot': r, 'scale': s, 'box': boxes.get(g)})
print('spawners', len(spawners))
for g, body in spawners:
    tr = tr_of_go[g]; w, r, s = world(tr); ch = chain_of(tr)
    sid = re.findall(r'\n  (\w*[Ii][Dd]\w*): (-?\d+)', body)
    secs = [s2 for sg, s2 in sectors if tr_of_go[sg] in ch]
    kids = [world(c)[0] for c in children[tr]]
    print(sid, 'children', len(children[tr]), 'sector', secs, 'pos', tuple(round(x, 1) for x in w), path(tr))
    out['spawners'].append({'ids': sid, 'sector': secs, 'pos': w, 'kids': kids})
json.dump(out, open(sys.argv[2], 'w'))
