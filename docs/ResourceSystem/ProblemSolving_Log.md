# 리소스 수명 시스템 재구축 — 문제해결 로그 (포트폴리오 재료)

> 작업일: 2026-07-18 · 대상: `ResourceManager` / `ResourceScope` / `ResourceRegistry` / 씬 전환
> 성격: **"측정→진단→구조→검증" 루프**의 기록. 결과물(스코프 시스템)보다 **의사결정 근거와 트레이드오프**가 포트폴리오의 핵심.
> 상위 계획: [Refactor_Plan.md](./Refactor_Plan.md) · 도식: [NewSystem_Diagram.pdf](./NewSystem_Diagram.pdf) · [SceneTransition_Design.pdf](./SceneTransition_Design.pdf)

## 한 줄 요약

기존 **2버킷(Global/Scene) + 호출부 bool** 리소스 관리를 **`(key,type)` refCount 레지스트리 + 수명 스코프** 구조로 재구축하고, 씬 전환을 그 위에 재설계했다. 빅뱅 없이 어댑터로 130개 호출부를 무수정 유지한 채 내부만 교체.

---

## Part 1. 왜 — 2버킷의 구조적 결함 (문제의 근본)

### 상황
리소스의 실제 수명은 **팝업 동안 / 던전 동안 / 파티 유지 동안 / 씬 동안 / 영구** — 5종 이상인데, 표현 수단은 **Global / Scene 2개뿐**이었다.

### 진단 (핵심 인과)
수명이 애매한 리소스(예: 팝업 UI)는 개발자가 매번 `isGlobal`을 손으로 판단해야 했고, "씬 버킷에 넣으면 씬 전환 때 죽어서 불안 + 수동 Release는 누수 위험" 사이에서 **전부 안전한 쪽 = Global로 도피**했다. 그 결과 Global만 누적되어 영구 상주 메모리가 쌓였다.

> **결정적 프레이밍**: 몬스터 HP바 누수는 개인의 주의력 실수가 아니라 **구조가 만든 선택**이었다. 2버킷 구조 자체가 개발자를 global 도피로 몰았다.

### 그래서 목표
> 수명 계층을 실제 사용 패턴대로 다층화하고, 리소스를 제 수명에 재배치해 상주 메모리를 회수한다.
> **"나누기(스코프)"만 하면 조기 해제 사고(핑크 텍스처)가 난다. "겹침 허용(refCount)"과 한 세트여야 안전.**

---

## Part 2. 설계 — 레지스트리 + 스코프 3원칙

```
ResourceManager (파사드)  →  ResourceScope (수명 티켓)  →  ResourceRegistry (핸들 소유)  →  Addressables
```

1. **핸들 소유는 레지스트리 한 곳.** 스코프는 참조 티켓(`HashSet<ResourceKey>`)만 든다 → "누가 Release하나" 문제 자체가 소멸.
2. **스코프 내 중복 로드 = refCount 1.** `HashSet.Add` 반환값으로 처리 → 호출부가 로드 횟수를 안 세도 됨.
3. **취소 ≠ 해제.** 취소 토큰은 '기다림 중단'만, 핸들 수명은 스코프가 소유 → `await` 후 use-after-release가 구조적으로 불가능.

**핵심 방어 질문**: *"Addressables가 이미 refCount인데 왜 그 위에 또?"*
→ Addressables refCount는 **핸들 단위 수동 짝맞추기** — 호출부가 Release를 '기억'해야 하고, 그 기억의 실패가 HP바 누수였다. 스코프는 **기억을 구조(수명 단위 자동 정산)로 대체**한 것.

---

## Part 3. 설계 단계의 고민들 (트레이드오프 기록)

### 3-1. refCount를 누가 올리는가 — 이중 카운트 함정
**고민**: 레지스트리가 요청받을 때마다 무조건 `refCount++` 하면, 같은 스코프가 같은 키를 두 번 로드했을 때 refCount=2가 되는데 Dispose 땐 HashSet 기준 1번만 Release → **refCount가 영영 0에 도달 못 함 = 누수.**

**결정**: 증가 여부 판단 주체를 **스코프**로. `_acquired.Add(rk)`가 `true`(이 스코프 첫 참조)일 때만 레지스트리가 `refCount++`.
```csharp
// ResourceScope
bool isFirst = _acquired.Add(rk);           // 처음이면 true
return _registry.LoadAsync<T>(rk, incrementRef: isFirst, tok);
```
→ **refCount의 의미 = "이 리소스를 소유한 서로 다른 스코프 수".** 마지막 스코프가 놓을 때만 실제 해제.

### 3-2. `(key, type)` 복합 키 — 타입 충돌 근절
**상황**: 기존 캐시 키가 string뿐이라 같은 키를 다른 T로 로드하면 핸들을 빼서 재로드하는 증상치료 로직(P5)이 있었다.

**결정**: 키를 `(string Key, System.Type Type)` 복합 struct로. `IEquatable` + `GetHashCode((Key,Type))`. 같은 문자열이라도 `GameObject`/`Sprite`는 서로 다른 엔트리 → 충돌 근본 소멸.

### 3-3. 프리로드 타입 딜레마 — 제네릭 T vs Location ★
가장 깊었던 설계 문제.

**상황**: 프리로드는 **라벨**에서 출발한다. 라벨 하나 → 프리팹·SO·오디오 등 **타입이 제각각인 N개**로 펼쳐진다. 이걸 `LoadAsync<T>`로 하려면 각 타입을 컴파일 타임에 알아야 하는데, 라벨 내용은 **런타임에야** 안다.

**막다른 길**: `LoadAsync<location.ResourceType>` 는 불가 — 제네릭 `<T>`는 컴파일 타임 '타입 이름'만 받고, `ResourceType`은 런타임 '값'이다. 리플렉션(`MakeGenericMethod`)은 느리고·할당·async 깨짐 → 기각.

**결정**: **Location 기반 비제네릭 로드 경로**를 추가.
```csharp
// 라벨 → location 목록 (카탈로그 조회, 실제 로드 아님)
var handle = Addressables.LoadResourceLocationsAsync(labels, MergeMode.Union);
// location마다: 실제 타입으로 키잉해서 로드
var rk = new ResourceKey(loc.PrimaryKey, loc.ResourceType);   // ★ 런타임 타입
Addressables.LoadAssetAsync<Object>(location);                // Object로 담아도 실제 객체 타입은 유지
```
**왜 `<Object>` 로드가 안전한가**: 실제 Result는 여전히 GameObject. 타입 구분은 '키'가 하고 로드는 부모 타입으로 담아도 무방. 나중에 `LoadAsync<GameObject>(key)` = `(key, typeof(GameObject))`가 프리로드한 `(key, GameObject)`와 정확히 히트 → 재로드 없음.

> 통찰: `LoadAsync<T>(key)`도 **내부적으로 key→location 변환 후** 로드한다. Location 방식은 "조회 단계를 이미 끝낸 지름길" + "런타임에 발견한 섞인 타입 묶음을 다루는 유일한 길".

### 3-4. 스프라이트 함정 — 측정으로 확인
**상황**: Sprite 모드 텍스처는 한 주소에 Texture2D(메인) + Sprite(서브) 두 얼굴. `LoadAssetAsync<Object>`는 **메인인 Texture2D를 반환** → UI가 Sprite를 기대하면 어긋남 (과거에 겪은 문제).

**판단**: Location 방식이 이걸 자동 해결한다고 **보장 못 함**(location의 ResourceType이 Sprite냐 Texture2D냐가 세팅·버전 의존). 크래시는 아니고 "프리로드 무의미 + 이중 로드" 형태의 낭비로 나타남.

**결정**: **스프라이트/아틀라스는 프리로드 대상에서 제외**, 쓰는 쪽에서 타입 명시(`LoadAsync<Sprite>`)로 on-demand. 아틀라스는 `GetSpriteFromAtlasAsync` 전용 경로 유지. → 검증은 프리로드 루프에 `loc.ResourceType.Name` 찍어 실제 카탈로그가 뭘 주는지 눈으로 확인("도구 숫자도 교차검증" 원칙).

---

## Part 4. 씬 전환 재설계

### 4-1. `SceneDataSO`가 사실상 "라벨 배달부"임을 진단
전체 코드에서 `SceneDataSO`의 4필드 사용을 추적:

| 필드 | 실사용 | 판정 |
|---|---|---|
| `preloadLabels` | ✅ LoadingScene 프리로드 | 유일하게 살아있음 |
| `sceneAddress` | ❌ 미사용("일단 나중에") | 죽은 필드 |
| `clearPreviousMemory` | ❌ 미사용(항상 clear) | 죽은 필드 |
| `sceneType` | ❌ 테이블 키와 중복 | 죽은 필드 |

→ **2단 SO 구조가 하는 일은 "씬 enum → preload 라벨" 매핑 하나뿐.** `SceneTableSO.SceneEntry`에 라벨을 인라인하고 SceneDataSO 제거.

### 4-2. 전용 로딩 씬 유지 — 실무 패턴 비교 후 근거 있는 선택
| | 전용 로딩 씬 (채택) | 퍼시스턴트+애디티브 (모던 기본) |
|---|---|---|
| 옛 씬 정리 | 자동 (Single이 전부 언로드) | 수동 언로드 |
| **전환 중 메모리 피크** | **가장 낮음** (두 씬 비공존) | 더 높음 (두 씬 잠시 공존) |
| 전환 느낌 | 하드컷 | 심리스 가능 |

**결정 근거**: 이 프로젝트의 #1 주제가 "리소스 수명·메모리". Single 모드 완전 언로드 → 전환 피크 억제가 주제와 정합. 심리스 전환은 이 게임(메뉴→선택→게임→던전)에 불필요 → 전용 로딩 씬의 단점이 발현 안 됨. **취향이 아니라 측정 근거 있는 트레이드오프.**
(던전 왕복은 GameScene 상태 리셋 방식 채택 = YAGNI. 상태 보존이 필요하면 "허브 애디티브"라는 다른 설계.)

### 4-3. 오케스트레이션 이전 + 해제 일원화
- **전환 로직을 `SceneManagerEx`(영속 POCO)로 이전.** 씬 경계를 넘는 흐름을 매 전환마다 파괴되는 씬 MonoBehaviour에 두지 않음. `LoadingScene`은 트리거+UI 전달만.
- **해제를 한 곳으로**: 이전엔 (1)`SceneManagerEx`가 이전 씬 렌더 중 `Resource.Clear()`(언로드 창) + (2)`LoadingScene`에서 또 Clear = 이중·이른 해제. → **`ChangeSceneScope()` 단 한 번**(이전 씬 언로드 완료 후, 새 스코프 생성→이전 Dispose 순서 강제).
- 전환 상태를 산재 프로퍼티(`NextSceneData` 등) → **`Pending` 값 struct** 하나로.

---

## Part 5. 마이그레이션 중 디버깅 서사 (실전 3버그)

### 5-1. `KeyNotFoundException: 'Global'` — 초기화 순서
**증상**: 부팅 즉시 `_scopes['Global']` 없음.
**원인**: `Managers.Init()`이 다른 매니저는 `Init()`을 부르는데 **`_resource.Init()`만 누락**. 예전 `ResourceManager.Init()`은 `_sceneHandles.Clear()`뿐이라 안 불러도 무해했지만, 지금은 **스코프를 등록하는 유일한 지점**이라 빈 딕셔너리 → 예외.
**해결**: `Managers.Init()`에 `_resource.Init()` 한 줄 추가.
**교훈**: 무해하던 초기화가 리팩터로 **필수 지점**이 됐다 — "안 불러도 되던 것"의 계약 변화를 놓치기 쉽다.

### 5-2. `NullReferenceException` in SelectScene — split-brain ★
**증상**: StartScene→SelectScene 진입 시 `_uiCanvas` null → NRE.
**원인**: 마이그레이션 과도기의 **split-brain**. `CreateMainUI`가
- `LoadAsync<GameObject>("SelectSceneCanvas_New")` → **새 레지스트리**에 저장
- `ShowSceneUI → Instantiate(string key)` → **옛 딕셔너리**를 뒤짐 (비어 있음)

→ 못 찾음 → null → NRE. **"로드는 새 시스템, 스폰은 옛 시스템"으로 갈라진** 것.
**해결**: (1)`Instantiate(string key)`를 `_registry.TryGetAsset<GameObject>(key)` 조회로 교체 + (2)StartScene의 Global 프리로드도 옛 `LoadDependenciesAsync`→새 `LoadAsyncPreload`로. 두 개가 한 쌍이라 같이 고쳐야 통합됨.
**교훈**: 파사드 안쪽을 교체할 때 **읽기 경로(Instantiate)와 쓰기 경로(LoadAsync)가 같은 저장소를 보는지**가 생명. 한쪽만 옮기면 조용히 null이 샌다.

### 5-4. `UI_LootNotification` use-after-release — 계측이 찾아낸 기존 버그 ★

**증상**: 던전 왕복 후 `UI_LootNotification`(178MB)이 **다시 로드되지 않는다.** Phase 3c 측정 로그에서 첫 던전 전환 이후 이 키가 영영 안 나타나는 것으로 발견.

**원인 체인** (셋이 맞물려야 재현되는 종류):
1. [UIManager.cs:37](../../Assets/Scripts/Managers/Core/UIManager.cs) — UI 루트가 `DontDestroyOnLoad` → **인스턴스는 씬 전환을 넘어 생존**
2. [UI_LootNotification.cs](../../Assets/Scripts/UI/Common/Toast/UI_LootNotification.cs) `PreloadAsync` — `if (_instance != null) return;` → static이 살아있으니 재로드를 건너뜀
3. 그런데 `MakeSubItemAsync`는 **Scene 스코프**로 로드 → 씬 회전에서 핸들이 반납됨

**결과**: 에셋 핸들은 반납됐는데 그 에셋으로 만든 인스턴스는 계속 살아있는 **use-after-release**. 에디터에서는 Addressables가 실제 언로드를 미뤄 티가 안 나지만, 빌드에서 번들이 내려가면 참조가 깨질 수 있다. R4(NoCache/JSON)와 같은 계열.

**중요 — 이건 Phase 3c가 만든 버그가 아니다.** `MakeSubItemAsync`는 이전부터 Scene 스코프였고, 버그도 그때부터 있었다. **다버킷 계측을 붙이자 비로소 "핸들은 사라졌는데 인스턴스는 남은" 불일치가 로그에 드러난 것.** 계측이 리팩터의 부산물이 아니라 도구라는 증거.

**해결 — (A) 채택**: 인스턴스가 `DontDestroyOnLoad` 싱글톤이므로 에셋도 Global이 맞다. (B)는 설계와 싸우는 쪽이고, 178MB도 공유 의존성 중복 계상이 섞인 값이라 실이득이 불확실했다.
측정 결과 Global +140.8 / Scene −141.0 / **합계 −0.3 = 완벽한 보존** — 메모리가 는 것이 아니라 **어느 버킷에도 안 잡히던 유령이 회계에 드러난 것**이다.

**교훈**: 에셋의 수명 스코프와 그 에셋으로 만든 **인스턴스의 수명이 어긋나면**, refCount가 아무리 정확해도 안전하지 않다. 스코프를 고를 때 "누가 이 에셋을 쓰는가"가 아니라 **"그것으로 만든 것이 언제까지 사는가"**를 물어야 한다.

### 5-3. `sceneName` 빈 문자열 폴백
**증상 가능성**: 테이블에 `sceneName` 미기입 시 `LoadSceneAsync("")` 실패.
**원인**: 폴백을 `string.Empty`로(무의미한 삼항). 씬은 enum 이름으로 주소화됨.
**해결**: `string.IsNullOrEmpty(entry.sceneName) ? type.ToString() : entry.sceneName`.

---

## Part 6. 빅뱅 금지 전략

- 파사드 `LoadAsync<T>(key, isGlobal)` 시그니처를 **어댑터로 유지** → 130개 호출부 무수정. 내부만 `isGlobal ? GlobalScope : SceneScope`로 라우팅.
- 각 단계 = 독립 커밋 = 롤백 지점.
- 과도기엔 옛 2버킷과 새 레지스트리가 공존(split-brain) → 5-2처럼 읽기/쓰기 경로를 단계적으로 새 시스템으로 수렴.

---

## Part 7. 남은 작업 (다음 세션 재개용)

### 리소스 시스템
| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| R1 | 옛 딕셔너리 은퇴 | ✅ | `_globalHandles`/`_sceneHandles` 필드 + `LoadAsyncInternal`·`LoadDependenciesAsync`·`Clear()`·`RemoveSceneLoadSources` + 주석 처리된 옛 `LoadAsync`/`Instantiate` 전량 삭제. `Init()`도 슬림화. 라이브 경로는 스코프/레지스트리만 참조 |
| R2 | **디버그 창/계측을 레지스트리로** | ✅ | `ResourceRegistry.GetSnapshot`/`Count` 신설 + `ResourceScope.Count`/`Contains` 노출. 파사드 `GetHandleSnapshot`/`LogAliveReport`/`AssertSceneHandlesCleared`/카운트를 레지스트리+스코프로 배선. 버킷 라벨=스코프 소유(`ResolveBucket`), Global 우선 방출로 MeasureMemory 귀속 불변식 유지. 로드 출처 추적 복구(`LoadAsync` 파사드+`LoadAsyncPreload`). **측정 차단 해제** — 이제 디버그 창이 실제 값 표시 |
| R3 | `_atlasSpriteCache` 수명화 | ✅ | 3d에서 해소. `GetSprites()`가 돌려주는 것이 **클론 Sprite**라 영구 보관 시 (1)클론 누수 (2)refCount와 무관한 텍스처 고정 = 스코프 회수 무효화. 레지스트리 해제 통지로 수명 동행 |
| R4 | NoCache/JSON use-after-release | ⏳ | `LoadAsyncNoCache`(P2) → `LoadTextAsync`(임시 스코프+`.text` 복사) |
| R5 | 풀↔에셋 수명 통합 | ⏳ | PoolManager를 스코프 소속으로 (Phase 4) |
| R6 | `UI_LootNotification` use-after-release | ✅ **구현·검증 대기** | **정책 (A) 채택** — 인스턴스가 `DontDestroyOnLoad` 싱글톤이므로 에셋도 Global. `MakeSubItemAsync`에 스코프 명시 오버로드 추가(기본은 Scene 유지). 곁들여 `_isLoading` try/finally + 실패 시 `WaitUntil` 영구 정지 수정 + 정적 진입점 null 가드 |
| R7 | `DisposeScope()` 호출부 없음 | ✅ | 3b의 `ClosePopupUI`가 첫 호출부가 되어 해소 |

### 씬 전환
| # | 항목 | 상태 |
|---|---|---|
| S1 | 전환 계측 재배선 | ✅ `BeginSceneTransition`을 `LoadScene`(트리거 시점)에, `EndSceneTransition`을 `RunLoadSequenceAsync` finally에 재삽입. `LogAliveReport`(회전 직전/프리로드 완료)·`AssertSceneHandlesCleared`(회전 직후)도 새 흐름에 복귀 |
| S2 | `RunLoadSequenceAsync` try/catch(OCE)/finally | ✅ 씬 활성화 시 Loading 씬 파괴로 토큰 취소되는 OCE를 삼킴(정상 종료 경로) + finally에서 `EndSceneTransition` 보장 |
| S3 | 죽은 코드 제거 | ✅ `LoadSceneAsync`·`GetSceneName`·`NextSceneData/Name`(SceneManagerEx) + `LoadProcessAsync`(LoadingScene) + 잡동사니 using(`Org.BouncyCastle.Ocsp`/`static NPOI...HSSFColor`) 삭제. **`SceneDataSO.cs`는 삭제 취소** — PreloadSO 5종(Start/Select/Game/NormalDungeon/BossDungeon)의 베이스 클래스로 실사용 중(문서 판단이 stale했음, 코드로 재검증). SceneManagerEx의 `SceneDataSO` 의존만 제거됨 |
| S4 | `ShowCover`/`_transitionUI` | ⏳ 현재 `_transitionUI` 미할당 → no-op. 페이드 쓸지/제거할지 결정 |

### Phase 3 (수명 재배치) — 명세: [Phase3_Spec.md](./Phase3_Spec.md)
| # | 항목 | 상태 | 비고 |
|---|---|---|---|
| 3a | 팝업 프리팹 → 씬 스코프 | ✅ **검증 완료** | `ShowPopupUIAsync`의 `isGlobal` 제거 + `_isLoadingPopup` try/finally. 팝업 세션 Global 영구증가 **+56.0 → +10.4MB(−81%)**, Scene 버킷 101.4 → 160.7 → **101.4 왕복 복귀**(회수 증명), 히치 무변화. 측정 상세 [Baseline.md §9](./Baseline.md) |
| 3b | 스탠딩 → 팝업 스코프 신설 | ✅ **검증 완료** | Popup 스코프(스택 0→1 생성, 1→0 Dispose, 중첩 공유). Global 단조 증가 **+40.1 → +30.9MB**, 스탠딩·데코가 Global에서 소멸, `[?]` 고아 0건. `UI_Info` 첫 오픈 히치 563.6 → **119.8ms**(v2 수준 복귀, **메커니즘 미설명 — 11-3**). 상세 [Baseline.md §11](./Baseline.md) |
| — | Popup 회수의 실체 | ✅ | **실제 회수 확정.** `[LoadStats]`상 `Img_Hoshino_Standing` 2회 로드 / `UI_Info` 2회 오픈 = **1오픈 1로드**. 첫 로드 197ms · 재로드 ~4ms라 히치로 안 나타났을 뿐 (Use Existing Build = 실제 번들 환경 값) |
| 3c | 파티 스코프 + 라벨 다이어트 | ✅ **검증 완료** | Global 라벨 42→12개. 진입 Global **513.0 → 65.8MB(−87%)**, 합계 616.7 → 362.4MB. Party 54.3MB 전 구간 불변(씬 전환 생존). 대가는 팝업 첫 오픈 +14~20ms. 상세 [Baseline.md §10](./Baseline.md) |
| 3d | 아틀라스 캐시 수명 동행 (R3) | ✅ **검증 완료** | `ResourceRegistry.OnReleased` 신설 → 아틀라스 해제 시 클론 스프라이트 동반 파기. `AtlasSpriteCache` **278 고정 → 씬마다 순환**, 단조 증가 +30.9 → **+16.1MB**. **던전 스코프는 미착수** — 목표(+18.7→0)가 3a/3c로 이미 달성돼 근거 없이 스코프를 늘리지 않음 |

### 검증 (막힘 순서)
~~**R2(디버그 창 레지스트리 이관)가 선행되어야** Phase 2 성공 기준을 잴 수 있다~~ → **해소**. R1/R2 완료로 측정 재개, Phase 3a에서 실제 수치가 움직였다(§9).

**남은 검증 부채**: ① 개발 빌드 실측 **0회** (전부 에디터 측정 = 내부 비교 전용) ② 히치 n=1 (§8-5 기준상 15ms 이하 차이 주장 금지) ③ "씬 왕복 후 재오픈" 구간 미측정 = 정책 A의 비용이 아직 미정량.

---

## Part 8. 포트폴리오 각도 (어필 포인트)

- **방법론이 결과물보다 세다**: "측정→진단→구조→검증" 루프. 각 결정에 트레이드오프 근거(Part 3~4).
- **말할 수 있는 숫자**: 3a 확정 — 팝업 세션 Global 영구증가 **+56.0 → +10.4MB(−81%)**, Scene 버킷 왕복 복귀로 회수 증명(수동 Release 0줄). 3c 이후 예정 — 진입 Global 513MB 다이어트, 던전 왕복 +18.7MB→0.
  - ⚠ **"→0"으로 말하지 말 것**: 3a 후에도 +10.4MB가 남는다. 잔여분은 `Preload(Global)` 라벨로 강제 상주하는 항목들이고 3c의 몫 — "팝업 프리팹은 0이 됐고, 남은 것은 라벨 문제로 원인이 분리됐다"가 정확한 서술.
- **측정 방법론 자체가 재료**: 계측 도구를 R2에서 갈아엎었기 때문에 Baseline v2와 직접 차감이 불가능했다 → **`isGlobal` 한 줄만 토글해 같은 도구로 Before를 재측정**해 단일 변수 A/B를 만들었다(§9-1). 그 과정에서 옛/새 도구가 같은 값(512.8MB)을 내는 것이 확인돼 기준선 연속성까지 덤으로 얻음. "숫자를 믿기 전에 숫자를 만든 도구를 먼저 검증했다".
- **정직함**: split-brain 버그(5-2)를 숨기지 않고 "과도기 구조가 만든 버그를 계측/디버깅으로 잡았다"로 서술. 프레임 예산 스트리밍·프리로드 배치화는 측정 근거로 **제외**(필요한 것만 했다는 증거).
- **방어 질문 준비**: "Addressables refCount 위에 왜?"(Part 2), "본인 문제를 본인이?"(2버킷은 초기 설계, 계측으로 한계 발견→구조로 재발 차단=성장 서사), "1인 프로젝트 오버엔지니어링?"(측정치가 필요성 증명).
