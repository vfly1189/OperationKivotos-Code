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

## 측정 결과 (Before) — 2026-07-16 측정

측정 경로: Start → GameScene → 팝업 열람 → BossDungeon 왕복 → NormalDungeon 왕복

### 1. 프리로드 소요 시간 (참고 지표)

| 씬 | 에셋 수 | 소요 시간(ms) |
|---|---|---|
| GameScene (Preload_GameScene) | 9 | **177** |
| NormalDungeon (Preload_NormalDungeon) | 8 | **119** |
| BossDungeon | 8 | **99** |

> **판단**: 에셋 수가 씬당 8~9개 수준이라 순차→배치 로드(P6) 개선 폭은 크지 않을 것.
> 프리로드 시간은 **주 비교 축이 아니라 참고 지표**로 유지하고, Before/After의 주 비교 축은 **② 수명(스코프) 정합성**과 **⑥ 메모리 사용량**으로 잡는다.

### 2. 팝업 열람 후 global 버킷 (P1 확정)

| 시점 | global 핸들 수 | 비고 |
|---|---|---|
| GameScene 진입 직후 | **50** | |
| 팝업 5종 열고 닫은 후 | **58** (+8) | 닫아도 해제 안 됨: `UI_EscapeMenu`, `UI_SoundSetting`, `UI_EquipmentUpgradePanel` + 아틀라스(`EscapeMenuAtlas`, `ImageFontsAtlas`, `CharacterPortraitsAtlas`, `WeaponIconAtlas`, `StandingImagesAtlas`) |
| 던전 왕복 후 | **61** (+3) | 던전 루트 UI가 추가로 고정: `EquipmentIconAtlas`, `ItemGradeAtlas`, `MaterialIconAtlas` (← `UI_ItemSlot`) |

→ **global 단조 증가 확인 (50 → 58 → 61).** 한 번이라도 연 팝업/아틀라스는 게임 끝까지 상주. UI를 더 열수록 계속 누적된다.

### 3. 씬 전환별 핸들 추이

**1차 측정 (팝업 열람 + Boss/Normal 왕복 세션)**

| 시점 | global | scene | atlasSprites |
|---|---|---|---|
| 처음 GameScene (팝업 열람 후) | 58 | 19 | 109 |
| BossDungeon 클리어 → GameScene 복귀 | 61 | 13 | 293 |
| GameScene → NormalDungeon 입장 | 61 | 17 | 293 |
| NormalDungeon 클리어 직후 (던전 내) | 61 | 18 | 293 |

**2차 측정 (새 세션, UI 미열람 → Normal 왕복만)**

| 시점 | global | scene | atlasSprites |
|---|---|---|---|
| GameScene 진입 직후 (UI 안 엶) | 50 | 14 | 36 |
| NormalDungeon 진입 직후 | 50 | 16 | 36 |
| NormalDungeon 클리어 직후 (루트 UI 노출) | 53 | 18 | **220** |
| GameScene 복귀 | 53 | 13 | 220 |

- **씬 버킷 자체는 정상 동작**: 왕복 시 던전 항목 → GameScene 항목으로 완전 교체, 잔존 없음 (Boss/Normal 모두 확인)
- **global은 두 세션 모두 단조 증가** (50→58→61 / 50→53) — 한 번 오른 것은 절대 안 내려옴 (P1)
- **AtlasSpriteCache: 던전 클리어 루트 UI 한 번에 36 → 220 (6배)** — 아틀라스를 여는 순간 내부 전체 스프라이트를 캐싱하는 구조 + 해제 경로 없음

### 4. ~~신규 발견 의심~~ → 재확인 결과 정상 (측정 타이밍 문제)

1차 측정에서 "NormalDungeon 복귀 후 던전 에셋 잔존"으로 보였던 것은 **리포트를 전환 완료 전에 출력한 타이밍 문제**로 판명.

- 2차 측정에서 복귀 시 `[ResourceReport] (씬 전환 직전 → Game)` / `(프리로드 완료)` 자동 로그 2종 정상 출력 확인
- 복귀 후 scene 버킷 = GameScene 항목 13개로 교체 — Loading 흐름의 Clear/프리로드 정상
- 교훈: 수동 스냅샷은 타이밍 오독 여지가 있음 → **씬 전환 자동 리포트가 판별 근거로 유효함을 확인** (도구 설계 검증)

### 5. 그 외 관찰 (개선 대상 메모)

- `GameScene.LoadCharacterSequential`이 파티 캐릭터 4종(GUID 키)을 **global**로 로드 — 파티 교체가 생겨도 영구 상주하는 구조
- 같은 UI 기능의 에셋이 버킷에 분산: 팝업 본체는 global(`UI_EquipmentUpgradePanel`), 그 서브아이템은 scene(`UI_ItemSlot` 등, `MakeSubItemAsync` 기본값) — **같은 기능인데 수명이 둘로 갈라짐** (P1의 또 다른 얼굴)
- 던전에서 루트 UI가 처음 노출되면 아틀라스 3종(`EquipmentIconAtlas`, `ItemGradeAtlas`, `MaterialIconAtlas`)이 그 자리에서 global로 고정 — "던전 수명"이어야 할 것이 "영구 수명"이 되는 대표 사례 → **Phase 3 던전 스코프의 직접적 개선 대상**
- AtlasSpriteCache는 장시간 플레이 시 **~300 근처에서 포화** (모든 아틀라스가 한 번씩 열리면 상한 도달). 무한 증가는 아니지만 **전량 영구 상주 자체가 문제** — 개별 크기가 큰 텍스처(스탠딩 이미지 등)까지 붙잡음

### 6. 메모리 사용량 (주 비교 축)

디버그 창 [메모리 측정]: 핸들별 의존성(텍스처/메시/오디오) 포함 런타임 메모리 추정 + 버킷 합계(중복 제거) + Unity 전체 지표.

| 시점 | Global 핸들 메모리 | Scene 핸들 메모리 | 전체 텍스처 | 전체 할당 |
|---|---|---|---|---|
| GameScene 진입 직후 (UI 안 엶) | **982.2 MB** | 93.6 MB | 647.2 MB | 1424.3 MB |
| 팝업 열람 후 | **1269.7 MB (+287.5)** | 68.3 MB | 826.5 MB | 1424.1 MB |
| Boss+Normal 던전 왕복 후 | **1287.0 MB (+17.3)** | 64.9 MB | 819.1 MB | 1435.2 MB |

**P1의 비용을 MB로 환산한 결과:**

- **팝업 한 세션 열람 = global에 +287 MB 영구 고정.** 닫아도, 씬을 옮겨도 게임 종료까지 안 내려옴
- 핸들 메모리의 **95%가 global 버킷** (1287 vs 65 MB) — "씬 버킷은 얼추 돌아가고, 문제는 스코프"라는 진단이 메모리에서도 그대로 확인됨
- 던전 왕복의 순증(+17.3 MB)이 작아 보이는 이유: 신규 고정된 `EquipmentIconAtlas`(150 MB)의 텍스처 대부분이 이미 `UI_EquipmentUpgradePanel`(329 MB)의 의존성으로 잡혀 있었기 때문 (공유 의존성 중복 제거)

**개별 대형 에셋 (상위, 의존성 포함 개별 크기):**

| 에셋 | 크기 | 고정 시점 | 비고 |
|---|---|---|---|
| `UI_Info` | 587 MB | **프리로드(Global)** — 시작부터 | 프리팹이 대형 텍스처들을 직접 참조 |
| `UI_EscapeMenu` | 563 MB | ESC 한 번 누른 순간 | + `EscapeMenuAtlas` 147 MB |
| `StandingImagesAtlas` | 436 MB | 캐릭터 정보창 한 번 연 순간 | 스탠딩 일러스트 전체 |
| `UI_EquipmentUpgradePanel` | 329 MB | 강화창 한 번 연 순간 | |
| `UI_ItemInfo` | 172 MB | 프리로드(Global) | |

> **측정 주의**: 에디터 측정치는 텍스처 CPU 사본 등으로 실기기보다 과대 추정됨. 절대값보다 **상대 비교(시점 간 증감, Before/After)** 로 사용한다. 개별 크기는 공유 의존성이 중복 포함되므로 합산하지 말 것 (버킷 합계는 중복 제거된 값).

### 6-1. "PNG는 27MB인데 왜 436MB?" — StandingImagesAtlas 원인 분석 (2026-07-17)

디스크 용량(PNG 합계 ~27MB)과 런타임 메모리(436MB)의 괴리를 추적한 결과, **3중 배율**로 설명됨:

| 배율 | 원인 | 근거 |
|---|---|---|
| **×4 (무압축)** | PNG는 파일 압축일 뿐, 런타임엔 RGBA32(픽셀당 4B)로 풀림. 아틀라스 `textureCompression: 0`(무압축) + 소스 .meta 기본 플랫폼도 무압축. 게다가 원본이 743×2485 같은 **4의 배수 아닌 크기**라 압축을 켜도 DXT/BC 폴백 위험 | `StandingImagesAtlas.spriteatlas` textureSettings, 소스 `.meta` |
| **×13 (전량 로드)** | 스탠딩 일러스트 13장(각 ~2000×2400, maxSize 2048 클램프)이 한 아틀라스 → **1장만 필요해도 아틀라스 페이지 전체 로드**. 리사이즈 후 합계 ~3,400만 px × 4B ≈ 135MB + 패킹 낭비(세로로 긴 이미지 vs 8192 페이지) ≈ 200MB+ | 아틀라스 packables = StandingImages 폴더 13장 |
| **×2 (에디터 사본)** | 에디터는 텍스처 CPU+GPU 사본을 합산 측정 | `GetRuntimeMemorySizeLong` 특성 |

→ 200MB+ × 2 ≈ **측정치 435.8MB와 일치**. `UI_Info` 587MB의 정체도 동일 구조 (무압축 × 아틀라스 전량 × 에디터 2배).

**개선 방향 (스코프 작업과 별개의 콘텐츠 설정 트랙, Phase 3에서 우선순위 판단):**

1. **스탠딩 아틀라스 해체** — 아틀라스는 드로우콜 배칭용(소형 아이콘 다수)이지 대형 일러스트용이 아님. 개별 스프라이트로 두고 필요한 캐릭터만 로드 → "1명 보는데 13장 로드" 제거. 스코프 재배치(3b)와 시너지
2. **압축 설정** — BC7/DXT5 적용 시 픽셀당 4B → 1B (**그것만으로 1/4**). 4의 배수 크기 정리 필요
3. 실기기/빌드 실측 필요성 재확인 (에디터 ×2 배율 제거한 진짜 수치 확보)

## Phase 0 결론

1. **P1 정량 확인**: global 단조 증가 (핸들 50→58→61, **메모리 982→1287 MB**), 팝업/아틀라스 영구 고정 목록 확보
2. **P1의 비용 = 팝업 한 세션에 +287 MB 영구 고정.** 핸들 메모리의 95%가 global 버킷
3. **AtlasSpriteCache 폭증 확인**: 루트 UI 1회 노출에 36→220 (장시간 ~300 포화, 전량 영구 상주)
4. **씬 버킷의 씬 단위 해제는 정상** — 문제는 "씬보다 짧은 수명(팝업/던전)"과 "긴 수명"을 표현할 계층이 없다는 것 → 스코프 도입(Phase 2~3)의 근거가 수치로 확보됨
5. **프리로드 시간(99~177ms)은 참고 지표로 강등** — After 비교의 주 축은 수명 정합성 + 메모리
6. 계측 도구가 1차 측정의 오독(타이밍)을 자동 리포트로 판별 — 도구 유효성 검증
7. (부수 발견) `UI_Info` 등 UI 프리팹이 대형 텍스처를 직접 참조한 채 시작부터 global 프리로드 → **진입 직후 이미 982 MB**. 스코프 도입과 별개로 UI 프리팹의 대형 텍스처 참조 구조도 개선 후보 (범위 판단은 Phase 3 이후)

---

## After 비교 (Phase 2~4 완료 후 기입)

〔같은 표 구조로 재측정하여 Before/After 비교〕
