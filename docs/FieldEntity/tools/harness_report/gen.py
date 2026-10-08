# Harness_Result.html 생성 — python gen.py (이 폴더에서) → docs/FieldEntity/Harness_Result.html
#  PDF: Chrome headless로 인쇄 (docs-pdf 방식, 입출력 모두 Windows 절대경로)
import sys, os, re, importlib.util
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.dont_write_bytecode = True
import charts
from charts import L
from data import *

spec = importlib.util.spec_from_file_location('af', REPO + '/docs/FieldEntity/tools/analyze_field.py')
af = importlib.util.module_from_spec(spec); spec.loader.exec_module(af)

def af_load(k):
    d, stem = RUNS[k]
    return af.load(f'{d}/field_{stem}_frames.csv', None)

SUM = {k: af.summarize(af_load(k)) for k in RUNS}

# ---- 구간별 (①1회 · ⑦ v2)
def seg_table(k):
    d = af_load(k)
    rows = OrderedDict()
    for r in d['frames']:
        g = af.group(af.segment_of(int(r['frame']), d['segments']))
        x = rows.setdefault(g, dict(dt=[], churn=0, flick=0))
        x['dt'].append(float(r['dtMs'])); x['churn'] += int(r['spawns']) + int(r['recalls'])
    for c in d['flicker'][1]:
        g = af.group(af.segment_of(c[0], d['segments']))
        if g in rows: rows[g]['flick'] += 1
    return rows

SEG1, SEG7 = seg_table('s1'), seg_table('c2')

map_svg = charts.fig_map()
cdf_svg = charts.fig_cdf()
live_svg = charts.fig_live()
dt_svg = charts.fig_dt()
pol_svg, POL = charts.fig_policy()
sc_svg, (A, B, AR, BR) = charts.fig_scatter()
bd_svg, DEV, ED, N1, N2 = charts.fig_breakdown()
df_svg = charts.fig_deferred()

style = open(REPO + '/docs/FieldEntity/Shadow_Result.html', encoding='utf-8').read()
style = style[style.index('<style>'):style.index('</style>') + 8]
style = style.replace('</style>', '  .fig svg{ margin:2px 0; }\n  .stat b{ font-size:12.5pt !important; } .stat b.o{ color:var(--amber-ink); } .stat b.g{ color:var(--green-ink); }\n</style>')

s1, s2, s3, c1, c2 = (SUM[k] for k in ('s1', 's2', 's3', 'c1', 'c2'))

def rng(key, fmt='{:.0f}'):
    v = [SUM[k][key] for k in ('s1', 's2', 's3')]
    return ' / '.join(fmt.format(x) for x in v)

def spread(key):
    v = [SUM[k][key] for k in ('s1', 's2', 's3')]
    m = sum(v) / 3
    return f'{(max(v) - min(v)) / m * 100:.1f}%' if m else '0%'

rep_rows = ''.join(
    f'<tr><td class="k">{lbl}</td><td class="c">{rng(k, f)}</td><td class="c">{spread(k)}</td><td>{note}</td></tr>'
    for k, lbl, f, note in [
        ('spawns', '스폰', '{:.0f}', '1개 차이 — 유예된 몹이 어느 구간에서 회수되느냐(몹 AI 타이밍)'),
        ('recalls', '정책 회수', '{:.0f}', ''),
        ('defers', '교전 유예', '{:.0f}', ''),
        ('on_spawn', '화면 안 등장', '{:.0f}', '탈락 조건'),
        ('flicker_wiggle', '왕복 구간 떨림', '{:.0f}', '탈락 조건 (전체 140 중 136)'),
        ('max_spawn_frame', '한 프레임 최대 스폰', '{:.0f}', ''),
        ('live_mean', '평균 몹 수', '{:.1f}', ''),
        ('dt_p50', '프레임 시간 p50 (ms)', '{:.2f}', '약 383fps — 프레임 상한 해제가 실제로 적용됨'),
        ('dt_p99', '프레임 시간 p99 (ms)', '{:.2f}', ''),
    ])

def segrow(g, label):
    a, b = SEG1.get(g), SEG7.get(g)
    p99 = f'{af.pct(a["dt"], 99):.1f}' if a else '–'
    return (f'<tr><td class="k">{g.split("_")[0]}</td><td>{label}</td>'
            f'<td class="c"><span class="{"ng" if a and a["churn"] else "ok"}">{a["churn"] if a else "–"}</span></td>'
            f'<td class="c"><span class="{"ng" if a and a["flick"] else "ok"}">{a["flick"] if a else "–"}</span></td><td class="c">{p99}</td>'
            f'<td class="c"><span class="{"ng" if b and b["churn"] else "ok"}">{b["churn"] if b else "–"}</span></td>'
            f'<td class="c"><span class="{"ng" if b and b["flick"] else "ok"}">{b["flick"] if b else "–"}</span></td></tr>')

seg_rows = ''.join(segrow(g, lbl) for g, lbl in [
    ('c1_chunk_x50_S5', '칸 경계 x = 50 (S5)'), ('c2_sector_S4S5', '섹터 경계 S4/S5'), ('c3_sector_S3S4', '섹터 경계 S3/S4'),
    ('c4_sector_S2S3', '섹터 경계 S2/S3'), ('c5_radius20_1010', '⑤ 켜기 반경 20m (1010)'), ('c6_sector_S1S2', '섹터 경계 S1/S2'),
    ('c7_chunk_x10_S1', '칸 경계 x = 10 (S1)'), ('c8_chunk_x50_S1', '칸 경계 x = 50 (S1)')])

pol_rows = ''.join(
    f'<tr><td class="k">{NAMES[p]}</td><td class="c">{POL[p]["mean"]:.1f}</td><td class="c m">{PRED[p][0]}</td>'
    f'<td class="c">{POL[p]["peak"]}</td><td class="c m">{PRED[p][1]}</td><td class="c">{POL[p]["maxch"]}</td><td class="c m">{PRED[p][2]}</td></tr>'
    for p in range(1, 8))

html = f'''<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>Harness Result</title>{style}</head><body>

<div class="kicker">FieldEntity · 비교 계획 Phase 0 · 0-4 계측 · 0-5 하네스</div>
<h1>측정 기록 — 계측 · 하네스 · ① 기준선</h1>
<p class="lead">2026-10-09. 측정 도구(계측 CSV · 고정 경로 하네스)를 만들고 처음 돌린 결과다. 하네스가 재현되는지, 시뮬레이션 예측이 실제 게임에서도 맞는지, ① Sector가 실제로 어떻게 실패하는지를 본다.</p>

<div class="stat">
  <div><b class="g">2820 · 2821 · 2820</b><span>① 3회 스폰 수 — 하네스 재현성</span></div>
  <div><b>71.4 vs 71</b><span>① 평균 활성: 하네스 실측 vs 예측 (7개 모두 ±1)</span></div>
  <div><b class="o">119마리 → 66.8ms</b><span>① 한 프레임 스폰 최대 (개발 빌드)</span></div>
  <div><b class="o">9 · 136</b><span>① 화면 안 등장 · 왕복 구간 떨림 → 탈락</span></div>
</div>

<h2>측정한 것</h2>
<div class="h2sub">5회 실행. 비용(프레임 시간)은 개발 빌드만 믿는다 — 에디터는 상한을 풀어도 144Hz에 묶이고 잡음이 크다. 현상(몹 수 · 이벤트)은 둘 다 쓸 수 있다.</div>
<table><thead><tr><th>실행</th><th>정책</th><th>환경</th><th>경로</th><th>쓰임</th></tr></thead><tbody>
<tr><td class="k">① ×3</td><td>① Sector · 그림자 끔</td><td>개발 빌드 · 상한 해제 · 1920×1080 창</td><td>하네스 v2 (296초)</td><td>재현성 검증 · ① 기준선 비용 · 현상</td></tr>
<tr><td class="k">⑦ v2</td><td>⑦ CullingGroup · 그림자 켬</td><td>에디터</td><td>하네스 v2 (296초)</td><td>현상 · 7개 정책 그림자(경로 ⓐ) → 예측 대조</td></tr>
<tr><td class="k">⑦ v1</td><td>⑦ CullingGroup · 그림자 켬</td><td>에디터</td><td>하네스 v1 (283초)</td><td>하네스 첫 완주 — ⓑ2 끝점 문제 발견 → v2</td></tr>
</tbody></table>

<div class="fig"><div class="figt">하네스 경로 v2 — 원본 맵 (BackStreetMap)</div>
{map_svg}
<div class="figcap">회색 점 = 스폰 포인트(23 스포너 · 363마리). 경로 ⓐ는 예측 시뮬레이션과 같은 웨이포인트 13개. 돌아오는 길에 통로(x = −5)를 따라 ⓒ 왕복 8곳과 ⓑ 끌기 2곳을 차례로 지난다. 위치는 "구간 경과 시간 × 속도"로 정해서 같은 시각엔 늘 같은 자리다.</div></div>

<h2 class="pb">1. 하네스는 재현된다</h2>
<div class="h2sub">① Sector를 개발 빌드로 3회. 이벤트 수가 같으면 하네스가 정책 비교에 쓸 만하다는 뜻이다.</div>
<table><thead><tr><th>지표</th><th>1회 / 2회 / 3회</th><th>범위/평균</th><th>비고</th></tr></thead><tbody>{rep_rows}</tbody></table>
<div class="fig"><div class="figt">프레임 시간 분포 — 3회 겹쳐 그림</div>
{cdf_svg}
<div class="figcap">누적 분포. 세 선이 거의 겹친다. p99 편차 6.5%는 3회째가 조금 느렸던 것 — 시간 지표 비교는 이 폭보다 큰 차이만 의미 있다.</div></div>
<div class="callout c-green"><span class="lbl">판정</span>3회 검증 통과. 이벤트 수는 사실상 같고(스폰 1개 차이), 시간 지표는 p50 2.6% · p99 6.5% 안에서 흔들린다. 차이가 나는 곳은 유예된 몹이 어느 구간에서 회수되느냐뿐이다 — 몹 AI가 프레임 시간에 따라 조금씩 다르게 움직인 결과지 하네스 탓이 아니다.</div>

<h2 class="pb">2. 예측은 실제 게임에서도 맞다</h2>
<div class="h2sub">⑦ v2 실행의 그림자 기록 — 7개 정책이 같은 프레임에 같은 위치로 판정한 결과 — 에서 경로 ⓐ 구간(131초)만 잘랐다. 하네스가 일정 속도로 걸으므로 시간 가중 평균 = 경로 거리 평균 = 예측과 같은 정의.</div>
<div class="fig"><div class="figt">경로 ⓐ · 정책별 활성 몬스터 — 하네스 실측(막대) vs 예측(주황 눈금)</div>
{pol_svg}</div>
<table><thead><tr><th>정책</th><th>평균 실측</th><th>예측</th><th>최대 실측</th><th>예측</th><th>한 프레임 최대 변경</th><th>예측(한 걸음)</th></tr></thead><tbody>{pol_rows}</tbody></table>
<div class="callout c-blue"><span class="lbl">읽는 법</span>평균 · 최대는 7개 모두 예측과 ±1 안이다. 다른 건 ⑤⑥⑦의 "최대 변경"(17 vs 34) 하나 — 예측은 0.5m 걸음마다 판정했고 실제는 프레임마다(5m/s에 약 0.04m) 판정해서 원을 한 번에 하나씩 넘는다. 칸 · 섹터 방식은 칸 내용으로 양자화돼 걸음 크기와 무관하므로 그대로 맞는다. 즉 <strong>"한 번에 몇 마리가 바뀌나"는 거리 방식에선 스포너 하나(17마리)가 하한</strong>이다.</div>

<h2 class="pb">3. ① Sector 기준선 — 시간에 따라</h2>
<div class="h2sub">같은 하네스 시각 축에 ①(개발 빌드)과 ⑦(에디터)의 살아 있는 몹 수. 몹 수는 프레임 시간과 무관해 둘을 같이 놓을 수 있다.</div>
<div class="fig"><div class="figt">살아 있는 몹 수 (스포너 몹 · 죽는 중 포함)</div>
{live_svg}
<div class="figcap">① 평균 {s1["live_mean"]:.1f} · 최대 {s1["live_max"]} / ⑦ 평균 {c2["live_mean"]:.1f} · 최대 {c2["live_max"]}. ①은 섹터를 넘을 때마다 계단처럼 뛰고, ⓒ 섹터 경계 왕복(초록 띠)에서 섹터 하나가 통째로 켜졌다 꺼지기를 20번 반복한다.</div></div>
<div class="fig"><div class="figt">① 프레임 시간 (개발 빌드 · 1회)</div>
{dt_svg}
<div class="figcap">평소 2.6ms(약 383fps). 솟은 곳은 전부 섹터 전환 — 섹터 하나(최대 119마리)가 한 프레임에 스폰된다. 초록 띠의 빽빽한 바늘이 섹터 경계 왕복.</div></div>

<h2>4. 전환 비용 — 몇 마리를 한 번에 바꾸면 얼마나 드나</h2>
<div class="fig"><div class="figt">그 프레임의 스폰 · 회수 수 vs 프레임 시간 — ① 3회 합침</div>
{sc_svg}
<div class="figcap">스폰만 있는 프레임과 회수만 있는 프레임을 따로. 직선 = 최소제곱(절편 {B:.1f}ms — 스폰이 있는 프레임은 평소 {s1["dt_p50"]:.1f}ms보다 다른 일도 많다).</div></div>
<div class="fig"><div class="figt">스폰 1회 분해 (마커 합 ÷ 스폰 수)</div>
{bd_svg}
<div class="figcap">풀 꺼내기가 스폰 비용의 {DEV["popUs"] / sum(DEV.values()) * 100:.0f}% (개발 빌드). 풀이 이미 만들어 둔 인스턴스를 꺼낼 뿐인데 이렇게 비싼 건 예상 밖 — 풀 루트에서 씬 루트로 옮기는 <code>SetParent</code>(계층 이동)를 의심. Phase 1-3에서 확인.</div></div>
<div class="callout c-amber"><span class="lbl">숫자로</span>스폰 1마리 ≈ <strong>{A:.2f}ms</strong>, 회수 1마리 ≈ <strong>{AR:.2f}ms</strong> (프레임 시간 기울기). 평소 프레임({s1["dt_p50"]:.1f}ms)에서 144fps 예산(6.9ms)까지 남은 여유는 스폰 약 {(6.94 - s1["dt_p50"]) / A:.0f}마리분이다. ①은 119마리를 한 번에 스폰해 66.8ms — 예산의 약 10배. 거리 방식(⑤⑥⑦)의 하한인 스포너 하나(17마리)도 약 {s1["dt_p50"] + A * 17:.1f}ms로 예산을 넘는다 → 프레임 스폰 예산(나눠 스폰)이 어느 방식이든 필요할 수 있다.</div>

<h2 class="pb">5. 경계에서 — 왕복 구간 (ⓒ)</h2>
<div class="h2sub">경계를 1m씩 10번 왕복. 판정이 경계에서 흔들리면 그때마다 스폰 · 회수가 난다. 떨림 = 끈 뒤 2초 안에 다시 켠 횟수(스포너 단위).</div>
<table><thead><tr><th>구간</th><th>경계</th><th>① 스폰+회수</th><th>① 떨림</th><th>① dt p99 (ms)</th><th>⑦ 스폰+회수</th><th>⑦ 떨림</th></tr></thead><tbody>{seg_rows}</tbody></table>
<div class="callout c-amber"><span class="lbl">①</span>섹터 경계 4곳 모두에서 섹터 전체가 왕복마다 켜졌다 꺼진다. S2/S3 · S1/S2는 4초 동안 680마리를 스폰 · 회수하고 그 구간 p99가 수십 ms다. 칸 경계 · 반경 경계에선 아무 일도 없다(①은 섹터만 본다).</div>
<div class="callout c-green"><span class="lbl">⑦</span>떨림은 8곳 모두 0. 스폰 · 회수가 있는 두 곳도 떨림이 아니다 — c4 3 = 앞 ⓑ에서 유예됐던 몹이 귀환해 회수된 것, c5 17 = 1010이 한 번 켜진 것. c5는 1010 중심에서 정확히 20.0m(켜기 반경) 위를 왕복했는데, 도착할 때 한 번 켜진 뒤 끄기 반경(25m)까지 멀어지지 않으므로 다시 바뀌지 않는다 — 히스테리시스가 실측으로 보인다.</div>

<h2>6. 화면 안 등장 · 끌고 온 몬스터</h2>
<div class="two">
<div class="card a"><div class="hd"><b>① 화면 안 등장 9</b><span>3회 모두</span></div>
<p>돌아오는 길, 통로를 따라 북쪽으로 S1에 들어서는 순간(z = 70.3) S1이 켜지며 1010의 17마리 중 9마리가 플레이어 2~7m 앞 — 화면 안 — 에 한꺼번에 나타난다(뷰포트 y 0.74~0.94).</p></div>
<div class="card g"><div class="hd"><b>예측 P7 "① 화면 안 소멸 9"은 0</b><span>교전 유지가 막음</span></div>
<p>ⓐ에서 남쪽으로 S1을 빠질 때 화면 안 몹이 <strong>정확히 9마리</strong> — 근처라 감지 거리 안 → 교전 중 → 회수 대신 유예. 화면 밖으로 멀어진 뒤 회수됐다. 등장 쪽은 교전 규칙으로 못 막는다.</p></div>
</div>
<div class="fig"><div class="figt">ⓑ 끌기 — 교전 유예 중인 몹 수</div>
{df_svg}
<div class="figcap">스포너가 꺼지는 순간 따라오던 몹이 유예로 잡히고(계단 상승), 대상을 놓쳐 집으로 돌아간 뒤 하나씩 회수된다(계단 하강). ①의 ⓑ1 끝 직후 솟은 곳은 다음 이동에서 섹터가 바뀌며 생긴 유예로, 역시 몇 초 안에 0. 실행이 끝날 때 유예는 5회 모두 0 — 누수 없음. 교전 중 회수(끌고 온 몹 소실)도 5회 모두 0.</div></div>

<h2 class="pb">7. 남은 것</h2>
<table><thead><tr><th>항목</th><th>내용</th><th>할 일</th></tr></thead><tbody>
<tr><td class="k">계측 자체 순간 비용</td><td>계측 최대 2.9ms = 프레임 기록 목록(List)이 용량을 두 배로 늘릴 때 기존 6만여 줄을 복사</td><td>묶음 단위로 쌓기</td></tr>
<tr><td class="k">할당 메모리 증가</td><td>① 1회에 392 → 476MB. 일부는 위 기록 목록, 나머지는 미확인</td><td>⑤ 측정과 비교 — 몹 수에 비례하는지</td></tr>
<tr><td class="k">ⓑ2 대기 여유</td><td>⑦ v2에서 마지막 유예 회수가 대기 끝 1.2초 전. 밀리면 다음 구간으로 넘어감(합계 · 판정엔 영향 없음)</td><td>3회 비교에서 흔들리면 대기 20초</td></tr>
<tr><td class="k">빌드 설정</td><td>이번 빌드는 Autoconnect Profiler가 켜져 있었다(에디터는 닫혀 있어 영향은 작음)</td><td>다음 빌드부터 끄기</td></tr>
<tr><td class="k">풀 꺼내기 61%</td><td>스폰 1회 중 가장 큰 몫</td><td>Phase 1-3 스폰 분해</td></tr>
</tbody></table>
<div class="callout c-violet"><span class="lbl">다음</span>계측 버퍼 수정 → 0-7 Before 영상(① · 교전 유지 끔 · ⓑ 구간) → Phase 1 몬스터 1마리 비용 → Phase 3 정책 7개 × 3회.</div>
<div class="footer">데이터: 개발 빌드 MetricsLogs <code>field_Sector_20261009_020552 · 021108 · 021623</code>, 에디터 <code>field_CullingGroup_shadow_20261009_014246 · 015436</code> (+ 같은 시각의 activation_*.csv). 분석 <code>docs/FieldEntity/tools/analyze_field.py</code> · <code>analyze_shadow.py</code>. 계획 <code>03_Comparison_Plan.md</code> 0-4 · 0-5.</div>
</body></html>'''

out = REPO + '/docs/FieldEntity/Harness_Result.html'
open(out, 'w', encoding='utf-8').write(html)
print('written', len(html))
print('fit', A, B, AR, BR)
print('dev', DEV, 'ed', ED)
