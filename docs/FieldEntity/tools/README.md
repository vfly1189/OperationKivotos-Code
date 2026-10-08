# tools — 존재 판정 비교용 스크립트

| 파일 | 하는 일 |
|---|---|
| `parse_map.py` | 맵 프리팹(YAML) → 섹터 · 스포너 JSON. `python parse_map.py <BackStreetMap*.prefab> <출력.json>` |
| `maps/original.json` · `maps/balanced.json` | 위 스크립트 출력 — 원본 맵 · 변형 맵(0-8). 예측 시뮬레이션과 같은 입력 |
| `analyze_shadow.py` | 그림자 측정 로그 분석. `python analyze_shadow.py <MetricsLogs/activation_*_shadow_*.csv> [original\|balanced]` |
| `analyze_field.py` | 계측(0-4) 분석. `python analyze_field.py <MetricsLogs/field_*_frames.csv> [--skip 초]` — 같은 시각의 `_events.csv` · `activation_*.csv`를 자동으로 찾는다. 하네스 기록이면 a_sweep ~ end만 보고 구간별 표를 더한다. 파일을 여러 개 주면 실행 비교(3회 편차) |
| `harness_report/` | 측정 기록 문서(Harness_Result.html) 생성 — `cd harness_report && python -B gen.py`. 쓰는 로그는 `data.py`의 `DEV` · `RUNS` |
| `run_harness.ps1` | 개발 빌드를 정책 × 회차로 반복 실행(0-5). `powershell -ExecutionPolicy Bypass -File run_harness.ps1 -Exe <빌드\OperationKivotos.exe> [-Policies Sector,Distance] [-Runs 3]` |

그림자 측정: GameScene 인스펙터 `Activation Shadows` 켜고 플레이 → 종료 시 `MetricsLogs/activation_{실제 정책}_shadow_{시각}.csv`.
한 파일에 7개 정책의 켜고 끈 기록이 같은 프레임 번호로 쌓이고, 머리 `#` 줄에 동등성 검사(⑤=⑥ · ⑦=⑤ 1프레임 전) 요약이 들어간다.

분석 출력: 정책별 켬/끔 수 · 평균 활성(몬스터, 프레임 가중) · 최대 · 한 프레임 최대 변경(첫 스폰 제외) · 떨림(끈 뒤 2초 안 다시 켬) + 예측 대조,
⑤⑥⑦ 히스테리시스 위반(켬 > 20m · 끔 < 25m), 떨림 사례 위치, 경로가 안 닿은 스포너.

맵 프리팹의 스포너 배치를 바꾸면 `maps/*.json`을 다시 만든다.

계측(0-4): 플레이할 때마다 `MetricsLogs/field_{정책}[_shadow]_{시각}_frames.csv` · `_events.csv`가 같이 생긴다(씬 Clear · 플레이 종료 때).
frames = 프레임마다 시간(dt · 메인 스레드 · 마커 9개 μs) · 수(스폰 · 회수 · 유예) · 상태(몹 · 화면 안 · 유예 · 켜진 스포너 · 메모리).
events = 스폰(activate/respawn) · 정책 회수(policy/deferred) · 교전 유예 한 건씩 + 그 순간 화면 판정 · 교전 여부.
비용을 비교할 땐 그림자를 끈다 — 그림자 선택 비용이 프레임 시간에 섞인다(`shadowUs` 열로 따로 보이긴 한다).
