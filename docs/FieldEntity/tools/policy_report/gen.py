# Policy_Result.html 생성 — python -B gen.py (이 폴더에서) → docs/FieldEntity/Policy_Result.html
#  PDF: Chrome headless로 인쇄 (입출력 모두 Windows 절대경로 — docs PDF 방식)
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.dont_write_bytecode = True
import charts
from data import *

pol = policy_runs()
ba = before_after()
A_LIVE, B_LIVE, R_LIVE, XS, YS = fit_live()
A_SP, B_SP, N_SP = fit_spawn()
M = lambda p, k: mean([s[k] for s in pol[p]])

bd_svg = charts.fig_breakdown()
fv_svg = charts.fig_find_vs_live()
dtp_svg = charts.fig_dt_policy()
fit_svg = charts.fig_live_fit()
sc_svg = charts.fig_spawn_scatter()
ov_svg = charts.fig_over()

style = open(REPO + '/docs/FieldEntity/Shadow_Result.html', encoding='utf-8').read()
style = style[style.index('<style>'):style.index('</style>') + 8]
style = style.replace('</style>', '''  .fig svg{ margin:2px 0; }
  .stat b{ font-size:12.5pt !important; } .stat b.o{ color:var(--amber-ink); } .stat b.g{ color:var(--green-ink); } .stat b.b{ color:var(--blue-ink); }
  .row2{ display:grid; grid-template-columns:1fr 1.05fr; gap:9px; align-items:start; }
  .pill{ display:inline-block; border-radius:9px; padding:0 7px; font-size:7.6pt; font-weight:800; }
  .pill.ok{ background:var(--green-soft); } .pill.ng{ background:var(--amber-soft); }
  .pill{ white-space:nowrap; } th{ font-size:7.8pt; }
  td.r{ text-align:right; font-variant-numeric:tabular-nums; }
</style>''')

ed0, ed1, dv0, dv1 = ba['ed_before'], ba['ed_after'], ba['dev_before'], ba['dev_after']
tot = lambda d: d['popUs'] + d['statUs'] + d['onSpawnUs'] + d['activateUs']

# ---------------------------------------------------------------- 도식 (인라인 SVG)
def box(x, y, w, h, t1, t2='', fill='#f7f8fa', stroke='#c9ced8', ink=None, ink2=None):
    a = f' fill="{ink}"' if ink else ''
    b = f' fill="{ink2}"' if ink2 else ''
    s = f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="9" fill="{fill}" stroke="{stroke}" stroke-width="1.2"/>'
    if t2:
        s += f'<text x="{x + w / 2}" y="{y + h / 2 - 3}" text-anchor="middle" class="t"{a}>{t1}</text>'
        s += f'<text x="{x + w / 2}" y="{y + h / 2 + 10}" text-anchor="middle" class="s"{b}>{t2}</text>'
    else:
        s += f'<text x="{x + w / 2}" y="{y + h / 2 + 3.5}" text-anchor="middle" class="t"{a}>{t1}</text>'
    return s

def arrow(x1, y1, x2, y2, color='#8a91a0'):
    return (f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{color}" stroke-width="1.4" marker-end="url(#ah)"/>')

DEFS = ('<defs><marker id="ah" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">'
        '<path d="M0,0 L10,5 L0,10 z" fill="#8a91a0"/></marker></defs>')

# D0 — 오늘 한 일
steps = [('문제', '스폰 1회의 63% = 풀 꺼내기', '#fff4ec', '#e8590c'),
         ('분해', '꺼내기 안에 마커 3개', '#f7f8fa', '#c9ced8'),
         ('원인', '씬 찾기가 꺼내기의 94%', '#fff4ec', '#e8590c'),
         ('수정', 'CurrentScene 씬당 1회 캐시', '#ebfbee', '#2b8a3e'),
         ('측정', '정책 7개 × 3회 무인 실행', '#eef2ff', '#3b5bdb')]
d0 = f'<svg viewBox="0 0 700 62" width="100%">{DEFS}'
for i, (t1, t2, f, st) in enumerate(steps):
    x = 4 + i * 139
    d0 += box(x, 6, 122, 48, t1, t2, fill=f, stroke=st)
    if i < 4: d0 += arrow(x + 123, 30, x + 137, 30)
d0 += '</svg>'

# D1 — 비용이 생기던 길
d1 = f'<svg viewBox="0 0 700 214" width="100%">{DEFS}'
chain = [('MonsterFactory', 'CreateAsync'), ('Resource.Instantiate', '위치 · 회전, 부모 없음'), ('Pool.Pop(parent)', 'parent ?? CurrentScene'),
         ('SceneEx.CurrentScene', '프로퍼티 — 매 호출'), ('FindAnyObjectByType', '&lt;BaseScene&gt;() 전체 순회')]
for i, (t1, t2) in enumerate(chain):
    x = 4 + i * 139
    hot = i == 4
    d1 += box(x, 34, 122, 46, t1, t2, fill='#e8590c' if hot else '#f7f8fa', stroke='#e8590c' if hot else '#c9ced8',
              ink='#fff' if hot else None, ink2='#fff' if hot else None)
    if i < 4: d1 += arrow(x + 123, 57, x + 137, 57)
# 마커 괄호 (Instantiate ~ 끝)
d1 += ('<path d="M143,26 L143,18 L695,18 L695,26" fill="none" stroke="#e8590c" stroke-width="1.2"/>'
       f'<text x="419" y="12" text-anchor="middle" class="s" fill="#8a3708">Field.Spawn.PoolPop 마커 = 스폰 1회 {ed0["popUs"]:.0f}μs · 그중 씬 찾기 {ed0["popFindUs"]:.0f}μs (에디터 · 수정 전)</text>')
# 아래 세 상자
d1 += ('<rect x="4" y="104" width="214" height="104" rx="10" fill="#fff" stroke="#e3e6ec"/>'
       '<text x="16" y="122" class="t">같은 길을 지나는 다른 호출</text>'
       '<text x="16" y="139" class="s">· 총알 — SpawnProjectiles · SpreadProjectiles</text>'
       '<text x="16" y="154" class="s">· 히트 VFX · 장판(SpawnDamageField)</text>'
       '<text x="16" y="169" class="s">· 부모 없이 Instantiate → 교전 중 매 발</text>'
       '<text x="16" y="188" class="xs">계측은 몬스터 스폰 구간 안에서만 세도록 막았다</text>'
       '<text x="16" y="199" class="xs">(MonsterPop · PopSub) — 섞이면 원인이 흐려진다</text>')
d1 += ('<rect x="236" y="104" width="214" height="104" rx="10" fill="#fff" stroke="#e3e6ec"/>'
       '<text x="248" y="122" class="t">훑는 대상 = 로드된 객체 전부</text>'
       '<text x="248" y="139" class="s">· 풀 프리웜 200개 × (RL · AR · HP바 · 총알 궤적)</text>'
       '<text x="248" y="154" class="s">· 맵 · UI · 파티 · 꺼져 있는 것까지</text>'
       '<text x="248" y="169" class="s">· 활성 몹 수와 무관 → 오른쪽 그림처럼 평평</text>'
       '<text x="248" y="188" class="xs" fill="#8a3708">풀을 30 → 200으로 키운 것이 비용을 키웠다</text>'
       '<text x="248" y="199" class="xs" fill="#8a3708">(풀을 키울수록 스폰이 느려지는 구조)</text>')
d1 += ('<rect x="468" y="104" width="228" height="104" rx="10" fill="#ebfbee" stroke="#2b8a3e"/>'
       '<text x="480" y="122" class="t" fill="#1f6b2e">수정 — CurrentScene 지연 캐시</text>'
       '<text x="480" y="139" class="s" fill="#1f6b2e">· 비어 있을 때만 FindAnyObjectByType</text>'
       '<text x="480" y="154" class="s" fill="#1f6b2e">· 씬이 바뀌면 이전 BaseScene 파괴 → null → 재검색</text>'
       '<text x="480" y="169" class="s" fill="#1f6b2e">· sceneLoaded에서도 비움 (새 씬 Awake 대비)</text>'
       '<text x="480" y="188" class="xs" fill="#1f6b2e">호출부 9곳 그대로 — 총알 · VFX도 같이 빨라짐</text>'
       '<text x="480" y="199" class="xs" fill="#1f6b2e">씬 전환 Start → Select → Game → 던전 플레이 확인</text>')
d1 += '</svg>'

# D2 — 순간이동 직후 (⑤ vs ⑦) 타임라인
def lane(y, title, sub, cells):
    s = f'<text x="6" y="{y + 20}" class="t">{title}</text><text x="6" y="{y + 33}" class="xs">{sub}</text>'
    for x, w, t1, t2, hot in cells:
        f, st = ('#fff4ec', '#e8590c') if hot == 'a' else (('#eef2ff', '#3b5bdb') if hot == 'b' else ('#f7f8fa', '#c9ced8'))
        s += box(x, y + 4, w, 38, t1, t2, fill=f, stroke=st)
    return s
d2 = f'<svg viewBox="0 0 700 186" width="100%">{DEFS}'
cols = [(118, 'Update'), (262, 'LateUpdate'), (406, '렌더링'), (550, '다음 프레임 Update')]
for x, t in cols:
    d2 += f'<text x="{x + 68}" y="12" text-anchor="middle" class="xs">{t}</text>'
d2 += ('<line x1="112" y1="18" x2="112" y2="180" stroke="#e3e6ec"/><line x1="544" y1="18" x2="544" y2="180" stroke="#e3e6ec" stroke-dasharray="3 3"/>'
       '<text x="328" y="180" text-anchor="middle" class="xs">프레임 N (하네스가 플레이어를 순간이동)</text>'
       '<text x="620" y="180" text-anchor="middle" class="xs">프레임 N+1</text>')
d2 += lane(20, '⑤ 거리형', '바로 판정',
           [(118, 136, '20m 안 → 바로 스폰', '화면 판정: 카메라는 아직 옛 자리', 'a'), (262, 136, '카메라 따라옴', '', ''),
            (406, 136, '스폰된 몹이 보임', '', ''), (550, 136, '—', '', '')])
d2 += lane(86, '⑦ CullingGroup', '렌더링 때 갱신',
           [(118, 136, '상태는 이전 프레임 것', '아직 스폰 안 함', ''), (262, 136, '카메라 따라옴', '', ''),
            (406, 136, '컬링 · 상태 갱신', '새 카메라 기준', ''), (550, 136, '이제 스폰', '화면 판정: 새 카메라 → 화면 안', 'b')])
d2 += '<text x="6" y="150" class="xs" fill="#8a3708">기록: ⑤ "화면 밖" · ⑦ "화면 안 10"</text>'
d2 += '<text x="6" y="162" class="xs" fill="#8a3708">실제로는 둘 다 다음 렌더에서 보인다 (추정)</text>'
d2 += '</svg>'

# ---------------------------------------------------------------- 표
def rng(p, k, f='{:.0f}'): return ' / '.join(f.format(s[k]) for s in pol[p])
rep_rows = ''.join(
    f'<tr><td class="k">{NUM[p]} {p}</td><td class="c">{rng(p, "spawns")}</td><td class="c">{rng(p, "recalls")}</td>'
    f'<td class="c">{rng(p, "flicker_wiggle")}</td><td class="r">{M(p, "dt_p50"):.2f}</td><td class="c">{spread([s["dt_p50"] for s in pol[p]]):.1f}%</td>'
    f'<td class="r">{M(p, "dt_p99"):.2f}</td><td class="c">{spread([s["dt_p99"] for s in pol[p]]):.1f}%</td></tr>' for p in POLICIES)

def pill(ok, v): return f'<span class="pill {"ok" if ok else "ng"}">{v}</span>'
def fail_row(p):
    s = pol[p][0]
    cells = [(s['on_spawn'] == 0, s['on_spawn']), (s['on_recall'] == 0, s['on_recall']), (s['engaged_recall'] == 0, s['engaged_recall']),
             (s['flicker_wiggle'] == 0, s['flicker_wiggle']), (s['pool_grows'] == 0, s['pool_grows'])]
    ok = all(c[0] for c in cells)
    return (f'<tr><td class="k">{NUM[p]} {p}</td>' + ''.join(f'<td class="c">{pill(o, v)}</td>' for o, v in cells) +
            f'<td class="c">{s["settle_onscreen"]}</td><td class="c">{pill(ok, "통과" if ok else "탈락")}</td></tr>')
fail_rows = ''.join(fail_row(p) for p in POLICIES)

def burst_row(p):
    ss = pol[p]
    dts = [x for s in ss for x in s['bigspawn_dt']]
    return (f'<tr><td class="k">{NUM[p]} {p}</td><td class="c">{ss[0]["max_spawn_frame"]}</td><td class="c">{min(dts):.1f} ~ {max(dts):.1f}</td>'
            f'<td class="r">{M(p, "popUs") + M(p, "statUs") + M(p, "onSpawnUs") + M(p, "activateUs"):.0f}μs</td><td class="r">{M(p, "recall_us"):.0f}μs</td>'
            f'<td class="r">{M(p, "select_us"):.1f}μs</td><td class="r">{M(p, "live_max"):.0f}</td><td class="r">{M(p, "mem_end"):.0f}MB</td></tr>')
burst_rows = ''.join(burst_row(p) for p in POLICIES)

budget_n = (BUDGET - B_SP) / A_SP
spot34 = [x for p in ('Spotlight3x3', 'Spotlight5x5') for s in pol[p] for x in s['bigspawn_dt']]
dist17 = [x for p in ('Distance', 'SpotlightDistance', 'CullingGroup') for s in pol[p] for x in s['bigspawn_dt']]
adj = pol['AdjacentSector'][0]['worst']
dist_over = mean([M(p, 'over') for p in ('Distance', 'SpotlightDistance', 'CullingGroup')])

# ---------------------------------------------------------------- 본문
html = f'''<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>Policy Result</title>{style}</head><body>

<div class="kicker">FieldEntity · Phase 1-3 스폰 분해 · Phase 3 정책 7개 × 3회</div>
<h1>측정 결과 — 스폰 비용 수정 · 활성화 정책 7개 비교</h1>
<p class="lead">2026-10-09 ~ 10. 정책을 재기 전에 스폰 비용의 가장 큰 몫(풀 꺼내기)을 먼저 잡았고, 그 빌드로 정책 7개를 같은 하네스에서 3번씩 무인으로 돌렸다.
결론은 둘 — <strong>스폰 비용은 씬 검색 한 줄이었고</strong>, <strong>정책의 평소 비용은 "몹을 몇 마리 살려 두나" 하나로 거의 완벽히 설명된다.</strong></p>

<div class="stat">
  <div><b class="g">{dv0["popUs"]:.0f} → {dv1["popUs"]:.0f}μs</b><span>풀 꺼내기 (개발 빌드) · 원인 = 매 호출 씬 전체 검색</span></div>
  <div><b class="b">+{A_LIVE * 1000:.2f}μs / 마리</b><span>프레임 시간 = {B_LIVE:.2f}ms + 살아 있는 몹 × 기울기 (r = {R_LIVE:.4f})</span></div>
  <div><b class="b">⑤ ⑥ ⑦ = {M("Distance", "dt_p50"):.2f}ms</b><span>거리형 셋은 시간으로 구별 안 됨 (3회 편차 안)</span></div>
  <div><b class="o">17마리 ≤ {max(dist17):.1f}ms</b><span>거리형의 한 프레임 최대 스폰 · 144fps 예산 6.94ms 언저리 (대부분 ~5ms)</span></div>
</div>

<div class="fig"><div class="figt">오늘의 흐름</div>
{d0}</div>

<h2>1. 스폰 비용의 원인 — 풀 꺼내기의 94%는 씬 검색이었다</h2>
<div class="h2sub">가설은 "풀 루트 → 씬 루트로 옮기는 SetParent(계층 이동)"였다. 꺼내기 안을 마커 3개(씬 찾기 · SetParent · 위치)로 쪼개 같은 하네스를 돌렸더니 SetParent는 {ed0["popParentUs"]:.0f}μs, 씬 찾기가 {ed0["popFindUs"]:.0f}μs였다.</div>
<div class="fig"><div class="figt">비용이 생기던 길 — 몬스터 스폰 1회</div>
{d1}
<div class="figcap">몬스터 팩토리는 부모를 넘기지 않는다 → <code>Pool.Pop</code>이 씬 오브젝트를 부모로 쓰려고 <code>CurrentScene</code>을 부르고, 그 프로퍼티가 호출마다 <code>FindAnyObjectByType</code>으로 씬을 뒤졌다. <code>HarnessRunner</code>에는 이미 "호출마다 씬을 검색하므로 매 프레임 부르지 않는다"는 주석이 있었는데, 정작 스폰 길에 남아 있었다.</div></div>

<h2>1-1. 수정 전후 — 스폰 1회 분해</h2>
<div class="fig"><div class="figt">스폰 1회 = 꺼내기 + SetStat + OnSpawn + SetActive (μs)</div>
{bd_svg}
<div class="figcap">에디터 두 줄은 같은 하네스(⑦ 그림자) · 같은 설정이라 깨끗한 전후 비교다. 개발 빌드 두 줄은 빌드 설정이 다르다(새벽 빌드는 Autoconnect Profiler 등이 켜져 있었다) — 꺼내기 말고 다른 단계가 줄어든 몫엔 그 차이가 섞여 있다. 수정 효과로 보는 건 꺼내기 {dv0["popUs"]:.0f} → {dv1["popUs"]:.0f}μs.</div></div>
<div class="row2">
<div class="fig" style="margin-top:0"><div class="figt">왜 "객체 수"인가 — 활성 몹과 무관</div>
{fv_svg}
<div class="figcap">수정 전 스폰 프레임마다 한 마리당 씬 찾기. 몹이 17마리든 68마리든 280~390μs로 평평 — 활성 수가 아니라 꺼진 것까지 포함한 로드된 객체 전부를 훑는 비용이다.</div></div>
<div>
<table><thead><tr><th>스폰 1회 (μs)</th><th>에디터 전</th><th>에디터 후</th></tr></thead><tbody>
<tr><td class="k">씬 찾기</td><td class="r"><span class="ng">{ed0["popFindUs"]:.1f}</span></td><td class="r"><span class="ok">{ed1["popFindUs"]:.1f}</span></td></tr>
<tr><td class="k">SetParent</td><td class="r">{ed0["popParentUs"]:.1f}</td><td class="r">{ed1["popParentUs"]:.1f}</td></tr>
<tr><td class="k">위치 · 나머지</td><td class="r">{ed0["popUs"] - ed0["popFindUs"] - ed0["popParentUs"]:.1f}</td><td class="r">{ed1["popUs"] - ed1["popFindUs"] - ed1["popParentUs"]:.1f}</td></tr>
<tr><td class="k">꺼내기 합</td><td class="r"><strong>{ed0["popUs"]:.1f}</strong></td><td class="r"><strong>{ed1["popUs"]:.1f}</strong></td></tr>
<tr><td class="k">스폰 1회 합</td><td class="r"><strong>{tot(ed0):.1f}</strong></td><td class="r"><strong>{tot(ed1):.1f}</strong> (−{(1 - tot(ed1) / tot(ed0)) * 100:.0f}%)</td></tr>
</tbody></table>
<div class="callout c-green"><span class="lbl">확인</span>동작은 그대로 — 스폰 653 → 652, 회수 619 → 618, 탈락 조건 동일. 비용만 줄었다. 남은 스폰 비용의 중심은 <strong>SetActive</strong>(애니메이터 Rebind · NavMeshAgent 켜기, 개발 빌드 {dv1["activateUs"]:.0f}μs)와 OnSpawn.</div>
</div></div>

<h2>2. 측정 — 정책 7개 × 3회</h2>
<div class="h2sub">개발 빌드(수정 반영) · 그림자 끔 · 1920×1080 창 · 프레임 상한 없음 · 하네스 v2 고정 경로(분석 구간 296초). <code>-harness</code>면 Start → Select(새로하기 = 매번 깨끗한 세이브)를 자동으로 넘겨 21회를 사람 손 없이 돌렸다. 순서는 정책 7개를 한 바퀴씩 세 번 — 같은 정책을 연달아 돌리지 않는다.</div>
<table><thead><tr><th>정책</th><th>스폰 (1/2/3회)</th><th>회수</th><th>왕복 떨림</th><th>dt p50</th><th>편차</th><th>dt p99</th><th>편차</th></tr></thead><tbody>{rep_rows}</tbody></table>
<div class="callout c-green"><span class="lbl">재현성</span>이벤트 수는 3회가 같거나 ±0.3%(유예된 몹의 회수 시점이 몹 AI 타이밍에 따라 조금 다름). 시간 지표 편차는 p50 0.1~3.2% · p99 0.2~3.4% — 아래 비교에서 이 폭보다 작은 차이는 "같다"로 읽는다. 새벽 빌드와는 빌드 설정이 달라 프레임 시간을 섞지 않는다 — 모든 비교는 이 빌드 안에서.</div>

<h2>3. 평소 비용 — 정책이 아니라 "살려 둔 몹 수"</h2>
<div class="fig"><div class="figt">정책별 프레임 시간 — 막대 = 3회 평균, 점 = 각 회</div>
{dtp_svg}</div>
<div class="fig"><div class="figt">프레임 시간 p50 vs 평균 살아 있는 몹 — 정책 7개</div>
{fit_svg}
<div class="figcap">점선 = 최소제곱 직선. 일곱 점이 한 직선 위에 있다(r = {R_LIVE:.4f}). 판정(선택) 비용은 프레임당 1~9μs라 이 그림에 보이지 않는다.</div></div>
<div class="callout c-blue"><span class="lbl">읽는 법</span>
<ul>
<li><strong>몬스터 1마리 = 프레임마다 약 {A_LIVE * 1000:.1f}μs</strong> (BT · NavMesh · 애니메이터 · HP바 합). 정책 차이는 이 단가 × 평균 생존 수로 거의 다 설명된다 — ① Sector는 거리형보다 {M("Sector", "live_mean") - M("Distance", "live_mean"):.0f}마리 더 살려 두어 +{(M("Sector", "dt_p50") - M("Distance", "dt_p50")):.2f}ms, ②는 +{M("AdjacentSector", "live_mean") - M("Distance", "live_mean"):.0f}마리로 +{(M("AdjacentSector", "dt_p50") - M("Distance", "dt_p50")):.2f}ms.</li>
<li>그러니 "어떤 판정이 싸냐"보다 <strong>"필요 없는 몹을 얼마나 덜 살려 두냐"</strong>가 비용이다. 판정 방식은 그 수를 정하는 수단.</li>
<li><strong>⑤ ⑥ ⑦은 모든 시간 지표가 3회 편차 안</strong> — 살려 두는 몹 수(24.4~24.7)가 같으니 당연하다. 셋 중 선택은 시간 밖의 기준으로.</li>
</ul></div>

<h2>4. 순간 비용 — 한 프레임에 몇 마리를 바꾸나</h2>
<div class="h2sub">평소 비용이 "몇 마리 살려 두나"라면, 순간 비용은 "한 프레임에 몇 마리를 한꺼번에 켜나"다. 스폰만 있는 프레임만 골라 그 프레임의 스폰 수와 프레임 시간을 찍었다.</div>
<div class="fig"><div class="figt">그 프레임의 스폰 수 vs 프레임 시간 — 21회 전부</div>
{sc_svg}
<div class="figcap">직선 기울기 {A_SP:.3f}ms/마리 — 마커로 잰 스폰 1회(약 130μs)보다 크다. 마커 밖의 몫(새로 켜진 몹의 첫 애니메이션 평가 · 렌더링 등)이 있다는 뜻. 이 기울기로 144fps 예산 안에 한 프레임 약 <strong>{budget_n:.0f}마리</strong>까지 켤 수 있다.</div></div>
<table><thead><tr><th>정책</th><th>최대 스폰/프레임</th><th>그 프레임 dt (ms)</th><th>스폰 1회</th><th>회수 1회</th><th>판정/프레임</th><th>최대 생존</th><th>끝 메모리</th></tr></thead><tbody>{burst_rows}</tbody></table>
<div class="fig"><div class="figt">144fps 예산(6.94ms)을 넘은 프레임 — 무엇과 겹쳤나</div>
{ov_svg}
<div class="figcap">거리형의 초과 프레임(평균 {dist_over:.0f}개 / 296초)은 스폰 · 회수와 거의 겹치지 않는다(⑦ 3회 중 2개). 최악 프레임은 <strong>계측 자체</strong> — 프레임 기록 목록(List)이 용량을 두 배로 늘리며 복사하는 4.8~6.7ms. 측정 도구가 측정을 오염시키는 중이라 After 전에 고친다.</div></div>
<div class="two">
<div class="card a"><div class="hd"><b>① ② — 섹터 하나 = 119마리</b><span>23 ~ 103ms</span></div>
<p>섹터를 넘는 순간 한 섹터 전체를 한 프레임에 켠다. ①은 22~25ms. ②는 생존이 295마리까지 올라 <strong>풀 200을 넘고</strong>, 그 프레임에 런타임 Instantiate가 섞여 {adj["dtMs"]:.0f}ms(꺼내기만 {adj["popUs"] / 1000:.0f}ms). ②를 쓰려면 풀 약 300이 비용에 같이 들어간다.</p></div>
<div class="card"><div class="hd"><b>③④ 34마리 · ⑤⑥⑦ 17마리</b><span>예산 언저리</span></div>
<p>거리형의 한 번 최대는 스포너 하나(17마리) — 그 프레임 {min(dist17):.1f}~{max(dist17):.1f}ms(대부분 5ms 안팎)로 예산 안. 칸형의 34마리는 {min(spot34):.1f}~{max(spot34):.1f}ms로 넘는다 → <strong>밀도가 두 배가 되면 거리형도 넘는다</strong>는 실측 근거.</p></div>
</div>

<h2>5. 탈락 조건</h2>
<div class="h2sub">분석 구간(a_sweep ~ end) 기준, 1회차 값. 3회 모두 같다. 풀 확장은 스폰 경로 풀(몹 · HP바)만 판정한다 — 데미지 토스트처럼 교전량에 따라 쓰이는 풀은 정책과 무관.</div>
<table><thead><tr><th>정책</th><th>화면 안 등장</th><th>화면 안 소멸</th><th>끌고 온 몹 소실</th><th>왕복 떨림</th><th>풀 확장</th><th>순간이동 직후 (참고)</th><th>판정</th></tr></thead><tbody>{fail_rows}</tbody></table>
<div class="callout c-amber"><span class="lbl">탈락 넷</span>① 화면 안 등장 9 · 떨림 136 / ② 떨림 114 · 풀 확장 187 / ③ 떨림 77 / ④ 떨림 19 — 칸 · 섹터 경계를 1m씩 왕복하는 구간(ⓒ)에서 경계가 흔들린다. 히스테리시스(켜기 20m · 끄기 25m)가 있는 거리형 셋만 0.</div>

<h3>⑦만 순간이동 직후 "화면 안 10" — 판정 시점 차이 (추정)</h3>
<div class="fig"><div class="figt">하네스 시작 순간이동(settle, 분석 구간 밖) — 프레임 안에서 무슨 일이 일어나나</div>
{d2}
<div class="figcap">3회 모두 ⑦만 10, ⑤⑥은 0. CullingGroup은 상태를 렌더링(컬링) 때 갱신하므로 한 프레임 늦게 반응하고, 그때는 카메라가 이미 새 자리에 와 있어 "화면 안"으로 기록된다. ⑤⑥은 순간이동한 그 Update에 바로 스폰해, 아직 옛 자리인 카메라로 판정돼 "화면 밖"으로 찍힌다 — 하지만 같은 프레임 렌더에서 보일 것이다. 즉 <strong>정책 결함이 아니라 계측의 판정 시점 문제</strong>로 본다. 확인 방법: 스폰 순간 대신 다음 프레임 카메라로 판정. 걸어서 이동하는 구간에선 셋 다 0.</div></div>

<h2>6. 정리 — 다음 결정(③ 조합)으로 가져갈 것</h2>
<table><thead><tr><th>항목</th><th>측정이 말하는 것</th><th>제안</th></tr></thead><tbody>
<tr><td class="k">판정 방식</td><td>탈락 조건 통과는 ⑤⑥⑦뿐. 시간은 셋이 같다(p50 {M("Distance", "dt_p50"):.2f} · p99 {M("Distance", "dt_p99"):.2f}ms).</td><td>시간 밖 기준으로 고른다 — 구현 · 디버깅 단순성, 스포너 수가 늘 때의 판정 비용(Phase 4 선택 비용 경계), 순간이동 거동(위 추정 확인 후).</td></tr>
<tr><td class="k">교전 유지</td><td>7개 정책 · 21회 모두 끌고 온 몹 소실 0, 실행 끝 유예 0(누수 없음).</td><td>공통 규칙으로 확정.</td></tr>
<tr><td class="k">프레임 스폰 예산</td><td>거리형 17마리 프레임 최대 {max(dist17):.1f}ms(대부분 ~5ms). 한 프레임 약 {budget_n:.0f}마리까지 예산 안. 34마리(칸형)는 {max(spot34):.1f}ms까지.</td><td>지금 맵은 필수 아님. <strong>밀도 ×2 변형에서는 필요</strong> — 상한 약 {int(budget_n // 5 * 5)}마리/프레임으로 넣고 변형 맵에서 효과를 잰다.</td></tr>
<tr><td class="k">풀 크기</td><td>거리형 최대 생존 102 / 풀 200 — 여유. ②는 295로 초과.</td><td>채택 정책의 최대 생존 × 여유로 산정(거리형이면 현재 200 유지 또는 축소 검토).</td></tr>
<tr><td class="k">계측 오염</td><td>최악 프레임이 계측 자체(기록 목록 두 배 확장, 4.8~6.7ms).</td><td>After 측정 전에 묶음 단위 저장으로 수정.</td></tr>
<tr><td class="k">스폰 단가</td><td>남은 스폰 비용의 중심은 SetActive({dv1["activateUs"]:.0f}μs) · OnSpawn({dv1["onSpawnUs"]:.0f}μs), 마커 밖 몫까지 프레임 기준 약 {A_SP * 1000:.0f}μs/마리.</td><td>예산을 넣으면 우선순위는 낮다. 밀도를 더 올릴 때 다시.</td></tr>
</tbody></table>
<div class="callout c-violet"><span class="lbl">다음 순서</span>계측 기록 목록 수정 → ⑤ / ⑥ / ⑦ 중 채택 결정(+ 순간이동 판정 확인) → 조합 구현(판정 + 교전 유지 + 필요하면 프레임 스폰 예산) → 같은 하네스로 After(Before = ① Sector 기준선) → 결과 문서 · 포트폴리오.</div>
<div class="callout c-blue"><span class="lbl">포트폴리오 문장 후보</span>"풀링을 했는데도 스폰이 비쌌다 → 계측을 쪼개 보니 풀이 아니라 매 호출 씬 검색이었다(94%) → 한 줄 캐시로 스폰 1회 −{(1 - tot(ed1) / tot(ed0)) * 100:.0f}%(같은 조건 전후)." 그리고 "정책 7개를 같은 경로로 3번씩 재 보니, 프레임 비용은 판정 방식이 아니라 살려 둔 몹 수 하나로 설명됐다(r = {R_LIVE:.4f}) — 그래서 문제를 '어떻게 판정하나'에서 '몇 마리를 살려 두나'로 다시 잡았다."</div>

<div class="footer">데이터: 개발 빌드(2026-10-09 22:24) MetricsLogs <code>field_{{정책}}_*</code> 21개 (+ 같은 시각의 events · activation), 수정 전후 에디터 <code>field_{BEFORE_ED}</code> · <code>field_{AFTER_ED}</code>, 수정 전 개발 빌드 <code>field_Sector_20261009_020552 · 021108 · 021623</code>. 분석 <code>docs/FieldEntity/tools/analyze_field.py</code>, 생성 <code>docs/FieldEntity/tools/policy_report/</code>. 커밋 8900c307(수정) · 58f32b83(무인 하네스 · 결과).</div>
</body></html>'''

out = REPO + '/docs/FieldEntity/Policy_Result.html'
open(out, 'w', encoding='utf-8').write(html)
print('written', out, len(html))
