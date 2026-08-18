# 리소스 수명 시스템 리팩터 — 전체 여정 (상세판)

> 기간: 2026-07-16 ~ 2026-07-28 · 대상: `ResourceManager` / `ResourceScope` / `ResourceRegistry` / `PoolManager` / 씬 전환
> 성격: **처음부터 지금까지의 과정을 한 문서로.** 각 단계 결과 문서(`Baseline.md`, `Phase3_Result.md`, `Phase4_Result.md`, `ProblemSolving_Log.md`)의 상위 통합본.
> 요약만 필요하면 → [Portfolio_Summary.md](./Portfolio_Summary.md) / `Portfolio_Summary.pdf`

---

## 0. 이 리팩터가 푼 문제 (한 문장)

> 리소스의 **실제 수명은 5종 이상**(팝업 동안 / 던전 동안 / 파티 유지 동안 / 씬 동안 / 영구)인데, 표현 수단이 **2개(Global / Scene)뿐**이라 애매한 것이 전부 "안전한 쪽 = Global"로 도피 → 영구 상주 메모리 누적. 수명 계층을 실제 사용 패턴대로 다층화하고, 리소스를 제 수명에 재배치해 상주 메모리를 회수했다.

**결과(진입 직후 기준):** Global 핸들 메모리 **513 → 205MB (−60%)**, 합계 **616.7 → 363.2MB (−41%)**, 세션 종료 합계 **655 → 379MB (−42%)**, Global 단조 증가 **+56 → +16MB (−72%)**. 수동 `Release` 호출은 **한 줄도 추가하지 않았다** — 스코프가 refCount를 자동 정산한다.

**핵심 주장은 수치가 아니라 방법론과 구조다:**
- **방법론** — "측정 → 진단 → 구조 → 검증" 루프. 각 결정에 트레이드오프 근거를 남겼다.
- **구조** — HP바 누수는 개인의 주의력 실수가 아니라 **2버킷 구조가 만든 선택**이었다. 스코프는 "Release를 기억하는 책임"을 **구조로 대체**한 것.

---

## 1. 진단 — 2버킷의 구조적 결함 (Phase 0 이전)

### 1.1 기존 구조

```
호출부 46개 파일 · 130곳  (string key + isGlobal bool 직접 결정)
   │
   ▼
ResourceManager (POCO)
   ├─ _globalHandles    : 영구 버킷 (게임 종료까지 해제 없음)
   ├─ _sceneHandles     : 씬 버킷 (씬 Clear 시 일괄 Release, 중간 해제 불가)
   ├─ _atlasSpriteCache : 영구 캐시 (해제 경로 없음)
   └─ Instantiate → Poolable이면 PoolManager (DDOL 루트, 에셋 핸들 ref 안 잡음)
```

### 1.2 문제점 7건 (P1~P7)

| # | 문제 | 영향 |
|---|------|------|
| **P1** ⭐ | 수명 결정이 130곳에 산재 + 캐시 히트가 `isGlobal` 무시(승격 없음). 팝업 UI는 전부 `isGlobal:true`라 게임 끝까지 상주 | Baseline으로 정량화: 팝업 세션 +287MB 영구, 핸들 메모리 95%가 global |
| **P2** | NoCache use-after-release — 핸들 `Release` 후 result 반환. 언로드 지연 덕에 우연히 동작 | 잠재 크래시 (타이밍 의존 UB) |
| **P3** | 해제 순서 위험 + 이중 Clear — 이전 씬 렌더 중 씬 핸들 전부 Release | 화면에 뜬 오브젝트의 텍스처/메시 파괴 가능 |
| **P4** | 풀↔에셋 수명 미연결 — 풀은 인스턴스만 들고 에셋 핸들 ref를 안 잡음 | 순서 하나 어긋나면 핑크 텍스처 |
| **P5** | 타입 충돌 핸들 교체 핵 — 캐시 키가 string뿐 | 복잡성·경합. `(key,type)` 키면 근본 소멸 |
| **P6** | 프리로드 순차 로드 | **측정 결과 개선 축 아님**(씬당 8~9개, 99~177ms) → 참고 지표로 강등 |
| **P7** | 계측 부재 → **Phase 0으로 해소** | — |

> **잘 되어 있던 것(유지):** `Addressables.*` 직접 호출이 ResourceManager로 수렴, 프리로드→동기 Instantiate 패턴, UniTask 취소 토큰 전파, 풀 위임. **씬 버킷의 씬 단위 해제 자체도 정상**(고장난 게 아니라 계층이 부족했던 것).

### 1.3 왜 UniTask인가 (면접 방어)

`ResourceManager`/`DataManager`/`SaveManager`/`SectorManager`가 전부 **순수 C# 클래스(비-MonoBehaviour)라 코루틴 자체가 불가** → UniTask 필연. + 제로 얼로케이션, `PlayerLoopTiming` 프레임 제어, `GetCancellationTokenOnDestroy` 수명 통합.

---

## 2. Phase 0 — 계측 + Baseline (2026-07-16)

**원칙: 계측 먼저.** 숫자 없이 최적화하면 "느낌"으로 일하게 된다.

- **산출물 ①** `Tools > Resource Debug 창` — 살아있는 핸들 실시간 목록(버킷/타입/로드 출처) + 씬 전환 자동 리포트/assert + [메모리 측정]. **동작 무변경 원칙**(읽기 전용 관찰 + 로그).
- **산출물 ②** [Baseline.md](./Baseline.md) — Before 수치 + 발견 7건.

**측정 결과(Before):**
- 팝업 5종 열고 닫기 = global 핸들 50 → 58 (닫아도 해제 안 됨), **메모리 +287MB 영구 고정**
- 진입 직후 Global 핸들 메모리 **982MB**, 그중 **95%가 global 버킷**
- `AtlasSpriteCache`: 루트 UI 1회 노출에 36 → 220 (아틀라스 여는 순간 내부 전체 스프라이트 캐싱 + 해제 경로 없음)

**측정 축 전환 결정:** 프리로드 시간(99~177ms, 씬당 8~9개)은 드라마틱한 개선 불가 → **주 비교 축을 메모리로 전환**.

**도구가 스스로 검증됨:** 1차 측정에서 "던전 에셋 잔존"으로 보인 것이 **측정 타이밍 오독**이었음을 씬 전환 자동 리포트가 판별. (도구 유효성 검증 = 이후 반복될 "도구의 숫자도 교차 검증" 원칙의 첫 사례.)

---

## 3. Phase 0.5 — 아틀라스 콘텐츠 트랙 + Baseline v2 (2026-07-17)

**왜 스코프 작업보다 먼저:** 아틀라스 설정 변경은 메모리 절대값을 크게 바꿔 Baseline을 무효화한다. 나중에 하면 "스코프로 −X MB" 주장에 아틀라스 몫이 섞여 귀속 불가("그거 그냥 압축 켠 거 아니에요?"). 먼저 하고 **v2를 재측정**하면 콘텐츠 트랙과 구조 트랙의 성과가 각각 독립된 Before/After를 가진다.

### 3.1 진단 2회 반전 (측정 방법론의 백미)

1. **1차 진단**: grep으로 "12개 아틀라스 전부 무압축(`textureCompression: 0`)" → 436MB의 원인으로 지목.
2. **1차 반전**: 에디트 모드 검사(`GetSprites`)는 개별 원본 텍스처 바인딩처럼 보임 → **플레이 모드 런타임 검사로 확정: 아틀라스는 DXT5 8192² 단일 페이지 64MB로 정상 패킹 중.**
3. **2차 반전 = 도구의 계통 오차 발견**: v1의 436MB/982MB는 [메모리 측정]의 `CollectDependencies`가 **빌드에 안 들어가는 원본 텍스처까지** 세던 과대 계상. 검산: 원본 185×2(에디터 CPU/GPU 사본) + 페이지 64 ≈ 435.8, 측정치와 일치. **아틀라스 항목은 ×2가 아니라 ×6.8이었다.**

> **교훈**: 에디트 모드와 플레이 모드가 상반된 결과를 냈고, **측정 도구 자체가 6.8배 오차를 만들고 있었다.** 도구의 숫자도 교차 검증 대상이다.

### 3.2 진짜 문제로 재정의 + 해체

압축이 아니라 **전량 로드**(1명 열람 = 64MB 페이지 통째, 히치 원인) + 패킹 낭비(세로형 일러 vs 정사각 페이지 ~25% 허공) + 수명. → `StandingImagesAtlas`·`EscapeMenuAtlas` **해체**(개별 스프라이트를 Addressable 키로, 소비처 `LoadAsync<Sprite>`로 전환). 아이콘류 8개(Weapon 4MB·Skill 1MB·Emblem 0.5MB·Portraits 4MB)는 이미 최적 → **작업 제외**(범위 축소).

### 3.3 성과 (콘텐츠 몫)

| 지표 | Before | v2 |
|---|---|---|
| 정보창 1명 열람 시 로드 | 13장 전량(64MB 페이지) | **열람한 1장만** |
| ESC 메뉴 데코 로드 | 9장 전량 | **선택 학교 3장만** |
| UI_Info 첫 오픈 최악 프레임 | 219.7ms | **123.4ms (−44%)** |
| 씬 전환 피크 | 1907~2100MB | 1710~1893MB (전 구간 ≈ −200MB) |

**Baseline v2가 공식 기준선이 됨:** 진입 Global **512.8MB** / 팝업 세션 **+46.8MB** / 왕복 **+18.7MB** / `AtlasSpriteCache` 268 상주. **v1 수치(982/+287)는 이후 인용 금지.**

---

## 4. 새 코어 설계 — 레지스트리 + 스코프 (Phase 1·2 통합)

Phase 1(버그 4건)은 따로 커밋하지 않고 **새 코어 구축이 흡수**하기로 결정(유저가 내부를 전량 이관할 계획이라). 매핑: NoCache use-after-release·캐시 히트 승격·해제 순서/이중 Clear는 레지스트리+스코프 구조로 자동 소멸, `_isLoadingPopup` 미복구만 Phase 3a에서 try/finally로 정리.

### 4.1 목표 구조

```
ResourceManager (파사드)
   ├─ 새 API   : LoadAsync<T>(key, scopeType)      ← 수명은 스코프가 소유
   ├─ 어댑터   : LoadAsync<T>(key, isGlobal)        ← 기존 130곳 호환 (점진 전환)
   └─ ChangeSceneScope() / CreateScope() / DisposeScope()

ResourceRegistry — 핸들 소유 유일한 곳
   └─ Dictionary<(key, type), Entry { handle, refCount }>
      · 키+타입당 핸들 1개 (P5 소멸) · 로딩 중이면 같은 핸들 공유 대기
      · refCount 0이 되는 순간에만 실제 Addressables.Release

ResourceScope : IDisposable — 참조 티켓(HashSet<ResourceKey>)만 보유
   · 같은 스코프가 같은 키를 N번 로드해도 refCount 1 (Dispose 한 번으로 정산)
   · Dispose → 보유 키 전부 release
```

### 4.2 설계 3원칙

1. **핸들 소유권은 레지스트리 한 곳** — 스코프는 티켓만. "누가 Release하나" 문제 자체가 소멸.
2. **스코프 내 중복 로드 = refCount 1** — `HashSet.Add` 반환값으로 판단. 호출부가 로드 횟수를 안 세도 됨.
3. **취소 ≠ 해제** — 취소 토큰은 '기다림 중단'만. 핸들 수명은 스코프 책임 → `await` 후 use-after-release가 구조적으로 불가능.

> **핵심 방어 질문 — "Addressables가 이미 refCount인데 왜 그 위에 또?"**
> Addressables refCount는 **핸들 단위 수동 짝맞추기** — 호출부가 Release를 '기억'해야 하고, 그 기억의 실패가 HP바 누수였다. 스코프는 **기억을 구조(수명 단위 자동 정산)로 대체**한 것.

### 4.3 설계 단계의 3가지 고민 (트레이드오프)

**(1) refCount를 누가 올리는가 — 이중 카운트 함정.** 레지스트리가 요청마다 무조건 `refCount++`하면 같은 스코프가 같은 키를 두 번 로드 시 refCount=2인데 Dispose는 HashSet 기준 1번만 Release → **refCount가 영영 0에 도달 못 함 = 누수.** → 증가 판단 주체를 **스코프**로: `_acquired.Add(rk)`가 `true`일 때만 `refCount++`. **refCount의 의미 = "이 리소스를 소유한 서로 다른 스코프 수".**

**(2) `(key, type)` 복합 키.** 같은 문자열이라도 `GameObject`/`Sprite`는 서로 다른 엔트리 → P5 타입 충돌 근본 소멸.

**(3) 프리로드 타입 딜레마 — 제네릭 T vs Location ★ (가장 깊었던 문제).** 프리로드는 **라벨**에서 출발 → 타입이 제각각인 N개로 펼쳐진다. `LoadAsync<T>`는 T를 컴파일 타임에 알아야 하는데 라벨 내용은 **런타임에야** 안다. 리플렉션(`MakeGenericMethod`)은 느리고·할당·async 깨짐 → 기각. → **Location 기반 비제네릭 로드 경로** 추가: `loc.ResourceType`으로 키잉하고 `LoadAssetAsync<Object>(location)`으로 담아도 실제 객체 타입은 유지 → 나중에 `LoadAsync<GameObject>(key)`가 정확히 히트. (스프라이트/아틀라스는 Texture2D 함정 때문에 프리로드 제외, on-demand 타입 명시.)

### 4.4 씬 전환 재설계

- **오케스트레이션을 `SceneManagerEx`(영속 POCO)로 이전** — 씬 경계를 넘는 흐름을 매 전환마다 파괴되는 씬 MonoBehaviour에 두지 않음.
- **해제 일원화** — 이전엔 (1)렌더 중 `Resource.Clear()` + (2)LoadingScene에서 또 Clear = 이중·이른 해제. → **`ChangeSceneScope()` 단 한 번**(새 스코프 생성 → 이전 Dispose 순서 강제, 렌더 중 해제 창 제거).
- **전용 로딩 씬 유지** — 실무 패턴(퍼시스턴트+애디티브) 비교 후 근거 있는 선택: Single 모드 완전 언로드 → 전환 피크 억제가 이 프로젝트 #1 주제(리소스 수명·메모리)와 정합. 심리스 전환은 이 게임에 불필요. **취향이 아니라 측정 근거.**

### 4.5 빅뱅 금지 전략

파사드 `LoadAsync<T>(key, isGlobal)` 시그니처를 **어댑터로 유지** → 130개 호출부 무수정, 내부만 `isGlobal ? GlobalScope : SceneScope`로 라우팅. 각 단계 = 독립 커밋 = 롤백 지점.

### 4.6 마이그레이션 중 실전 버그 3건

| # | 버그 | 교훈 |
|---|------|------|
| **초기화 순서** | `Managers.Init()`에서 `_resource.Init()`만 누락 → `KeyNotFoundException('Global')`. 예전엔 `Clear()`뿐이라 무해했으나 지금은 **스코프 등록 유일 지점** | 무해하던 초기화가 리팩터로 **필수 지점**이 됐다 — 계약 변화를 놓치기 쉽다 |
| **split-brain** ★ | 로드는 새 레지스트리에, `Instantiate`는 옛 딕셔너리를 뒤짐 → `_uiCanvas` null NRE | **읽기 경로와 쓰기 경로가 같은 저장소를 보는지**가 생명. 한쪽만 옮기면 조용히 null이 샌다 |
| **sceneName 폴백** | 빈 문자열 폴백 → `LoadSceneAsync("")` 실패 | `IsNullOrEmpty ? type.ToString() : name` |

**검증 원칙:** Phase 2는 "수치가 **안 변해야** 성공"(1:1 매핑 보존). 씬 왕복 후 잔존 핸들 = Baseline v2와 동일.

---

## 5. Phase 3 — 수명 재배치 (메인, 2026-07-21) ★

**모든 수치가 여기서 움직인다.** 스코프 구조(Phase 2)는 재배치를 누수 없이 하기 위한 수단이었다.

### 5.1 수명 계층 (최종)

```
enum ResourceScopeType { Global, Scene, Party, Popup }
```

| 스코프 | 수명 | Dispose 훅 | 담는 것 |
|---|---|---|---|
| **Global** | 부팅 → 종료 | 없음(불변) | 공용 VFX 5, exitPopup, 상시 UI 3, DDOL 토스트 |
| **Scene** | 씬 진입 → 전환 | `ChangeSceneScope`(자동) | 맵·씬 UI·팝업 프리팹·아이콘 아틀라스 |
| **Party** | 파티 구성 → 교체 | `CreateCharacters`(수동) | 인게임 캐릭 프리팹 |
| **Popup** | 스택 0→1 → 1→0 | `ClosePopupUI`(수동) | 스탠딩 이미지, ESC 데코 |

**순서 규칙(Party·Popup 공통):** ① 인스턴스 파괴 → ② 스코프 Dispose. 역순이면 살아있는 인스턴스가 프리팹의 메시·텍스처를 참조하는 동안 핸들만 반납되어 **refCount는 풀려도 실제 언로드가 안 일어난다.**

### 5.2 단계별 기여

| 단계 | 한 일 | 성과 |
|---|---|---|
| **3a** | 팝업 프리팹 `isGlobal` 제거 → Scene | 팝업 세션 Global 증가 **+56.0 → +10.4MB (−81%)**. Scene 버킷 101 → 160 → **101 왕복 복귀**로 회수 증명 |
| **3c** | Party 스코프 + **Global 라벨 42→12개** | 진입 Global **513 → 65.8MB**. **합계까지 −254MB** = 비파티 캐릭 8종 미로드 |
| **3b** | Popup 스코프 신설(첫 수동 훅) | 단조 증가 +40.1 → +30.9MB. 스탠딩·데코가 Global에서 소멸 |
| **3d** | 아틀라스 캐시 수명 동행(R3) | 단조 증가 +30.9 → **+16.1MB**. `AtlasSpriteCache` 278 고정 → 씬마다 순환 |
| **R6** | LootNotification 수명 불일치 수정 | 메모리 변화 0 — **회계 정직화** |

> **3c의 진짜 성과는 라벨 다이어트다.** Party 스코프 자체의 메모리 이득은 ≈0(인스턴스가 프리팹을 계속 참조). 그 값어치는 "파티 교체 시 회수 경로가 존재한다"는 구조적 정합성. 이 구분을 흐리지 않는다.
>
> **3d 상세:** `ResourceRegistry.OnReleased` 이벤트 신설 → 아틀라스 핸들 실제 해제 시 `GetSprites()`가 만든 **클론 스프라이트**를 동반 파기. 이게 없으면 캐시가 아틀라스를 붙잡아 refCount와 무관하게 텍스처가 고정 = 스코프 회수가 장부상으로만 끝난다.

### 5.3 던전 스코프는 만들지 않았다 (근거 기반 축소)

명세의 목표(던전 왕복 +18.7MB → 0)가 3a/3c로 **이미 달성**(측정 +0.9/−0.8MB). 목표가 충족된 상태에서 스코프를 추가하는 것은 순수한 복잡도 증가 → **명세를 따르지 않고 측정을 따랐다.** (프레임 예산 스트리밍을 단일 섹터 설계 근거로 제외한 것과 같은 결.)

### 5.4 비용 — 정직하게

lazy화는 공짜가 아니다. **상시 부담(진입 메모리)을 일회성 지연(첫 오픈)으로 바꾼 거래.**

| 팝업 | 3a 이전 요청→표시 | 3c 이후 |
|---|---|---|
| UI_Info | 2.7ms | **23.2ms** |
| UI_Inventory | 1.4ms | **15.2ms** |
| UI_EquipmentUpgradePanel | 36.1ms | 36.8ms (원래 lazy, 대조군) |

- 재오픈은 전 팝업 8.9~10.6ms로 **회귀 없음**
- 재로드 키 18 → 21개(아틀라스가 씬마다 재로드). 스탠딩 재로드 실측: 첫 로드 197ms, **재로드 ~4ms**(실제 번들 값)
- **진입 −308MB의 대가 = 팝업 첫 오픈 +14~20ms.** 이 트레이드오프를 측정으로 판단한 것이 이 단계의 핵심 활동.

### 5.5 계측이 도구였다 — 드러난 기존 버그 3건

셋 다 Phase 3가 만든 게 아니라 **스코프화와 다버킷 계측이 드러낸 기존 버그.** 공통 뿌리 = **"에셋의 수명 ≠ 그 에셋으로 만든 인스턴스의 수명".**

| # | 버그 | 정체 |
|---|------|------|
| R6 | `UI_LootNotification` use-after-release | DDOL 인스턴스 + static 캐시 + Scene 스코프 에셋. 던전 왕복 후 재로드도 안 됨 |
| 3a 동반 | `_isLoadingPopup` 미복구 | 조기 반환 시 플래그가 true로 남아 **이후 모든 팝업 영구 차단** |
| R6 동반 | `PreloadAsync` 영구 정지 | 로드 실패 시 `_instance` null인데 플래그만 내려가 `WaitUntil` 무한 대기 |

**계측 자체의 버그도 2건:** ① `RecordLoad` 호출부가 R1 죽은 코드 제거 때 함께 삭제 → 로드 통계 死(레지스트리 캐시 미스 경로에 재배치). ② `AssertSceneHandlesCleared`가 "이전 씬 미반납"이 아니라 "회전 후 새 적재"를 잡던 위치 오류(→ await 이전으로 이동 + 취소된 획득 되돌리기).

> **교훈**: 죽은 코드를 지울 때 **그 안에 살아있는 계측이 얹혀 있는지** 확인해야 한다. 계측은 눈에 안 띄므로 함께 사라져도 즉시 티가 안 나고, 몇 단계 뒤 "왜 이 지표가 비지?"로 돌아온다.

### 5.6 방법론 — 단일 변수 A/B

Baseline v2는 옛 2버킷 딕셔너리로 잰 값인데 그 사이 R2가 계측을 레지스트리 기반으로 갈아엎었다. 도구가 바뀐 구간을 두고 직접 차감하면 성과와 도구가 섞인다. → **`isGlobal` 한 줄만 토글해 같은 도구로 Before를 재측정**했고, 그 쌍만 비교. 부수 소득: 재측정 Before의 512.8/515.4MB가 v2의 512.8/515.5와 **소수점까지 일치** → 도구 등가성 실측 확인. **숫자를 믿기 전에 그 숫자를 만든 도구를 먼저 검증했다.**

---

## 6. Phase 4 — 풀·인스턴스 수명 통합 (R5, 2026-07-28)

풀이 원본 프리팹의 **refCount 티켓을 풀당 한 장** 쥐고, `@Pool_Root`의 **DDOL을 제거**했다. 인스턴스 파괴와 핸들 반납이 씬 전환 한 지점에서 정산 → **순서 의존이 구조 보장으로 바뀌었다.**

### 6.1 티켓 설계

- 티켓은 **스코프 우회 레지스트리 직접**(`ResourceRegistry.TryAddRef` + `ResourceManager.AcquirePoolRef/ReleasePoolRef`). 스코프로 잡으면 Scene HashSet 중복 제거로 no-op이고, `ChangeSceneScope` Dispose에 티켓이 동반 소멸해 위험 창이 부활한다.
- **부류 A**(총알·필드몬스터 = 사전 LoadAsync → 레지스트리 엔트리 존재 → 티켓 필요) / **부류 B**(VFX = `SpawnVFX._vfxPrefab` SO 직접 참조 → 엔트리 없음 → `AcquirePoolRef` false → 티켓 불요, 하드 참조가 수명 보장). 핑크 위험이 부류 B엔 원천 부재.

### 6.2 티켓 동작 — refCount로 증명

```
[Scene] c59d1aed…  (ref 2)  ← 소유 스코프(1) + 풀 티켓(1)        # Scene 스코프 총알
Droid_Helmet_AR: 1 → 3 → 1  ← Global(1) → +Scene(1)+티켓(1) → 복귀  # Global 프리로드 몬스터
```

Scene 스코프 총알은 티켓이 없었다면 씬 전환 시 refCount 0 → 언로드 → DDOL 인스턴스가 언로드된 에셋 참조(핑크). **티켓이 이 창을 닫는다.**

### 6.3 성공 지표 — 메모리 왕복 flat

| 시점 | Global | Scene | Party | 합계 |
|---|---|---|---|---|
| 진입 직후 | 204.0 | 100.3 | 54.3 | **358.6 MB** |
| 전체 왕복 후 | 204.4 | 100.4 | 54.3 | **359.1 MB** |

+0.5MB(노이즈). DDOL 제거에도 풀이 씬 넘어 누적되지 않고 모든 버킷 불변. **핑크 없음(육안).**

> **정정(운영 지표):** "잔존 인스턴스 경고 = 0"은 필드몬스터 자동 스폰 씬에선 문자 그대로 불가(진입만 해도 활성). 실질 성공 신호는 **"메모리 왕복 flat"**이며, 경고는 pass/fail 게이트가 아니라 **진단 도구**. `UI_ItemSlot 30/10/9 미반환`도 조사 결과 버그 아님 — 씬 teardown의 파괴 순서 아티팩트(메모리는 회수됨).

---

## 7. 후속 마무리 (2026-07-28)

### 7.1 R4 — NoCache use-after-release 차단

`LoadAsyncNoCache<T>`(핸들 Release 후 참조 반환)를 **`LoadTextAsync(key)`**로 교체 — Release 전에 `.text`를 값으로 복사해 반환. JSON 파서(`DataManager`)가 언로드 가능한 에셋을 만지던 잠재 크래시 제거.

### 7.2 HUD 아틀라스 churn 제거

"전환 중 인플라이트 낭비"를 진단하려다 **기존 `[ResourceReport]`+`[LoadStats]`가 이미 범인을 담고 있었음**을 확인: 파티 HUD 아틀라스가 Scene 스코프라 전환마다 재로드(각 3회). → `ActiveCharacterHUD`→SkillIconAtlas, `PartyHUD`→CharacterEmblemsAtlas를 **Scene → Party 스코프 재배치**. 재로드 3회 → 0, Party +1.5MB 상주(의도된 트레이드). **UI 루트 DDOL 캔버스 대공사는 측정 근거(누수 0)로 기각.**

### 7.3 후속2 — 셀프 코드리뷰로 발견한 잔여 결함 2건 (미커밋→커밋 `c38816d6`)

리팩터 마무리 단계에서 코어를 다시 리뷰하다 발견·수정.

**(1) 취소 정산 버그 (유령 핸들 누수).** 취소된 최초 로드에서 `ResourceScope` 제네릭 경로는 `_acquired`를 롤백하는데 `ResourceRegistry`는 엔트리/RefCount를 **되돌리지 않아**, 어느 스코프도 소유하지 않는 유령 핸들(refCount≥1, `[?]` 버킷 누수)이 남았다. 게다가 `ResourceScope`의 **location 오버로드는 롤백 자체가 없어** 제네릭과 비대칭. → **홀리스틱 수정:** Registry에 `RollbackLoad(key, entry, addedRef)` 신설(이 호출이 더한 참조만 되돌리고, 소유자 0이면 엔트리+핸들 정리. 다른 소유자가 같은 in-flight 핸들 대기 중일 수 있어 무조건 Release 안 함 = RefCount 판단 + `cur==entry` 재진입 가드). 두 `LoadAsync` 오버로드의 await를 try/catch로 감싸 취소·예외·비throw 실패 3경로 모두 RollbackLoad(기존 `status != Succeeded` 경로의 핸들 미해제 누수도 정리). Scope location 오버로드도 async화해 동일 롤백 규약 적용.

**(2) 아틀라스 중복 전개 가드.** 아틀라스 로드 `await`에서 동시 요청 N개가 함께 깨어나 **각자 `GetSprites`로 전량 전개**(실측 ItemGradeAtlas 29회). → `HashSet<string> _expandedAtlases` 센티넬로 최초 1회만 전개, 나머지는 채워진 캐시에서 읽기. 전개 블록에 `await`가 없어 **race-free**(단일 스레드 협조 스케줄러). `OnRegistryEntryReleased`에서 `_expandedAtlases.Remove`로 재로드 정합성 유지.

**실측 검증:** `[?]` 버킷 0 / `[ResourceLeak]`·`[PoolLeak]`·`[Resource]` 에러 0 / Scene·Party 왕복 완전 flat(102.3→102.3, 55.8→55.8) / 진입 363.2·종료 379.2MB(Phase 3 기준선 일치) / Global 단조 증가 +15.9MB(=Loot 아틀라스, 의도된 상주). 전환 중 아틀라스 로드가 취소되던 경로(SkillIcon/CharacterEmblems)가 `[Party] ref 1`로 왕복 내내 안정.

---

## 8. 최종 성적표

| 지표 | Baseline(전) | 완료 후 | 변화 |
|---|---|---|---|
| 진입 직후 Global | 513.0 MB | **205.1 MB** | **−60%** |
| 진입 직후 합계 | 616.7 MB | **363.2 MB** | **−41%** |
| 세션 종료 합계 | 655.4 MB | **379.4 MB** | **−42%** |
| Global 단조 증가 | +56.0 MB | **+16.1 MB** | **−72%** |
| 던전 왕복 영구 증가 | +18.7 MB | **≈0** | 목표 달성 |
| `AtlasSpriteCache` | 278 영구 상주 | **소비자 수명 동행** | 해소 |
| 풀↔에셋 수명 | 순서 의존(핑크 위험) | **티켓으로 구조 보장** | 왕복 flat |
| 수동 `Release` 코드 | — | **0줄 추가** | 자동 정산 |

---

## 9. 측정 환경·신뢰도 (주장 시 반드시 동반)

| 등급 | 대상 | 사용 규칙 |
|---|---|---|
| **강** | 버킷 라벨 전환, Scene 왕복 복귀, 로드/재로드 키 목록, 라벨 42→12, refCount 흐름 | 그대로 인용 |
| **강**(상향) | 증감·비율(−60%, −72% 등) | Addressables **Use Existing Build = 실제 번들** 확인으로 상향(Release·언로드·번들 마운트가 실제와 동일) |
| **중** | 메모리 **절대값** | 에디터는 CPU 사본 포함으로 과대 계상. **개발 빌드 실측 0회** → 절대값 주장 금지 |
| **약** | 히치 ms | n=1~3, 15ms 이하 차이 주장 금지 |

- **TMP 동적 폰트 아틀라스** 3종이 플레이마다 `.asset`에 구워져 `전체 할당`/`텍스처` 절대값을 세션당 최대 16MB 흔든다(버킷 합계는 무영향). **커밋 금지**, 측정 후 `git checkout -- "Assets/Fonts" "Assets/TextMesh Pro"`.
- 아틀라스 절감의 정직한 표현: "−60MB"(×) → **"전량 로드 → 사용량 비례 로드"**(○). 13명 전부 열람 최악 패턴에선 64→~45MB(−30%)에 그침, 현실 패턴(1~3명)에서만 극적.

---

## 10. 남은 작업 (근거 기반 스코프 컷)

| 항목 | 판단 |
|---|---|
| **개발 빌드 실측 1회** | 계측 절반(`[ResourceMemory]` 버킷 MB)이 `EditorUtility.CollectDependencies` 의존이라 **런타임 오버레이 선행 필요**. 단, 비교 축이 비율·누수0이므로 필수 아님 — 절대값을 주장 축으로 삼지 않으면 스킵 가능 |
| **Phase 5 매니페스트** | 측정상 개선 폭 작아 우선순위 최하. **선택** |
| **측정 시나리오 고정** | 히치 지표 등급 상향용. 세션마다 팝업 오픈 순서가 달라 히치가 "약" 등급 |

> **결론: 여기서 마무리 = 근거 기반 축소.** Dungeon 스코프·DDOL 캔버스 대공사·프레임 예산 스트리밍을 측정 근거로 쳐낸 것과 같은 판단선. 더 하는 것은 오버엔지니어링.

---

## 부록 A. 관련 문서

| 문서 | 내용 |
|---|---|
| [Baseline.md](./Baseline.md) | §1~8 Before·v2 측정, §9~12 Phase 3 단계별 상세 측정 |
| [Phase3_Result.md](./Phase3_Result.md) | Phase 3 완료 보고 + 방어 논거 |
| [Phase4_Result.md](./Phase4_Result.md) | Phase 4(R5) + R4 + HUD churn 검증 |
| [ProblemSolving_Log.md](./ProblemSolving_Log.md) | 설계 트레이드오프 + 마이그레이션 버그 서사 + R1~R7 표 |
| [Refactor_Plan.md](./Refactor_Plan.md) | 원 계획서 + 진행 로그 |
| [Portfolio_Summary.md](./Portfolio_Summary.md) | 핵심 요약(면접용) + `.pdf` |

## 부록 B. 커밋 이력 (주요)

```
a3665521  계획 수립
6d3e8680  Phase 0 디버그 창
87672de5  Phase 0 메모리 계측 + Baseline
0c1be945  Phase 0.5a 계측 자동화
ac4e05ac  Phase 0.5 아틀라스 해체
9a3fb6be  Phase 2 파사드/씬 배선
a5f776a4  Phase 3 완료 (3a~3d + R6/R7)
4c39f53a  Phase 4 (풀 수명 통합 R5)
e7af2a24  Phase 4 후속 (R4 + HUD 아틀라스 churn)
c38816d6  Phase 4 후속2 (취소 정산 롤백 + 중복 전개 가드)
```
