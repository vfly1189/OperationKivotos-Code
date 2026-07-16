# 리소스 시스템 Baseline — 개선 전(Before) 상태 측정

> Phase 0 산출물 ②. 측정 도구(디버그 창 + 씬 전환 리포트)로 현재 2버킷 구조의 상태를 기록한다.
> Phase 2~4 완료 후 **같은 도구·같은 시나리오**로 After를 재측정해 비교한다.
> 계획서: [Refactor_Plan.md](./Refactor_Plan.md)

## 측정 도구

| 도구 | 위치 | 용도 |
|---|---|---|
| Resource Debug 창 | 에디터 메뉴 `Tools > Resource Debug 창` | 살아있는 핸들 실시간 목록 (버킷/타입/로드 출처) |
| 씬 전환 리포트 | `LoadingScene` — "씬 전환 직전" / "프리로드 완료" 로그 | 전환 시점 핸들 전수 기록 |
| 씬 버킷 assert | `LoadingScene` — Clear 직후 | 씬 버킷 잔존 = 에러 로그 |
| 프리로드 시간 로그 | `ResourceManager.LoadDependenciesAsync` — `[Preload] N개 로드 완료 — Xms` | P6 순차 로드 Before 수치 |

## 측정 시나리오 (After 재측정 시 동일하게 반복)

1. **시작 → GameScene 진입** — 프리로드 시간 기록, 진입 직후 핸들 상태
2. **팝업 5종 열고 닫기** (인벤토리/강화/정보 등) — global 버킷 증가량 관찰 (P1 증거)
3. **GameScene → NormalDungeon → GameScene 왕복 ×3** — 왕복마다 "씬 전환 직전" 리포트의 global 수 추이 (단조 증가 여부)
4. **던전 퇴장 직후** — 던전 전용 에셋(몬스터/이펙트)이 어느 버킷에 남아있는지
5. **Memory Profiler 스냅샷** — 왕복 전/후 비교 (HP바 누수 때 방법론)

---

## 측정 결과 (Before) — 〔측정 후 기입〕

### 1. 프리로드 소요 시간 (P6 Before)

| 씬 | 에셋 수 | 소요 시간(ms) |
|---|---|---|
| GameScene | 〔〕 | 〔〕 |
| NormalDungeon | 〔〕 | 〔〕 |
| BossDungeon | 〔〕 | 〔〕 |

### 2. 팝업 열람 후 global 버킷 (P1 증거)

| 시점 | global 핸들 수 | 비고 |
|---|---|---|
| GameScene 진입 직후 | 〔〕 | |
| 팝업 5종 열고 닫은 후 | 〔〕 | 닫아도 해제 안 되는 팝업 프리팹 목록: 〔〕 |

### 3. 씬 왕복 시 핸들 추이 (누적 관찰)

| 왕복 | 전환 직전 global | 전환 직전 scene | 비고 |
|---|---|---|---|
| 1회차 | 〔〕 | 〔〕 | |
| 2회차 | 〔〕 | 〔〕 | |
| 3회차 | 〔〕 | 〔〕 | global이 단조 증가하면 P1 확정 |

### 4. 던전 퇴장 직후 던전 전용 에셋 잔존 (스코프 부재 증거)

| 에셋 | 버킷 | 퇴장 후 생존? |
|---|---|---|
| 〔몬스터 프리팹〕 | 〔〕 | 〔〕 |
| 〔보스 이펙트〕 | 〔〕 | 〔〕 |

### 5. Memory Profiler 스냅샷

- 왕복 전: 〔스크린샷/수치〕
- 왕복 3회 후: 〔스크린샷/수치〕
- AtlasSpriteCache 개수 추이: 〔〕

---

## After 비교 (Phase 2~4 완료 후 기입)

〔같은 표 구조로 재측정하여 Before/After 비교〕
