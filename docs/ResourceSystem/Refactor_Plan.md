# 리소스 시스템 리팩토링 — 수명 다층화(ResourceScope) + 재배치

## 주 목적 (한 문장)

> **리소스의 실제 수명(팝업 동안 / 던전 동안 / 파티 유지 동안 / 씬 동안 / 영구)은 5종류 이상인데, 표현 수단이 2개(Global/Scene)뿐이라 애매한 것들이 전부 "안전한 쪽 = Global"로 도피 → 영구 상주 누적. 수명 계층을 실제 사용 패턴대로 다층화하고, 리소스를 제 수명에 재배치해서 상주 메모리를 회수한다.**

- **인과가 핵심**: 팝업을 씬 버킷에 넣으면 씬 전환 때 죽어서 불안하고, 수동 Release는 누수 위험(HP바 사례) → 개발자가 global로 도피한 건 개인의 실수가 아니라 **구조가 만든 선택**이다.
- **메커니즘은 세트**: 나누기(스코프)만 하면 조기 해제 사고(핑크 텍스처)가 생긴다. **나누기(스코프) + 겹침 허용(refCount: 여러 스코프가 같은 리소스 공유, 마지막 참조가 해제)** 이 한 세트.

## 결과 요약 (TL;DR)

| 단계 | 내용 | 측정치 영향 | 상태 |
|---|---|---|---|
| **Phase 0** | 계측 도구(디버그 창·메모리 측정·자동 리포트) + Before 측정 → [Baseline.md](./Baseline.md) | — (기준점 확보) | ✅ 완료 (2026-07-16) |
| **Phase 1** | 정확성 버그 3건 — NoCache use-after-release · 캐시 히트 승격 · 해제 순서 일원화 | **수치 무관** (잠재 크래시 리스크 제거로만 정당화) | ⬜ 예정 |
| **Phase 2** | `(key, type)` ref-count 레지스트리 + `ResourceScope`(IDisposable). 기존 API는 어댑터 유지 | **수치 무관** — 1:1 매핑이므로. 가치는 Phase 3을 누수 없이 가능하게 하는 전제조건 | ⬜ 예정 |
| **Phase 3 ★** | **수명 재배치 (메인)** — 팝업/아틀라스/프리로드/던전을 제 수명으로 | **모든 수치가 여기서 움직임** | ⬜ 예정 |
| **Phase 4** | 풀·인스턴스 수명 통합 | 안전성 위주, 수치 소폭 | ⬜ 예정 |
| **Phase 5** | 프리로드 매니페스트 + 배치 로드 (선택) | 참고 지표(프리로드 시간)만 | ⬜ 선택 |

**정직한 매핑**: 수치를 움직이는 건 전부 Phase 3(재배치)이고, 스코프 구조(Phase 2)는 재배치를 안전하게 하기 위한 수단이다. 지금 2버킷에서 재배치하려면 팝업마다 수동 Release가 필요하고, 그게 바로 HP바 누수를 만든 방식이다. → **"재배치가 목적, 스코프는 재배치를 누수 없이 하기 위한 수단"**

## 성공 기준 (Baseline 대비, After는 같은 도구·같은 시나리오로 재측정)

| 지표 | Before ([Baseline.md](./Baseline.md)) | After 목표 |
|---|---|---|
| 팝업 세션 후 영구 증가 | **+287 MB** (global 982→1270) | **0** (씬/팝업 수명으로 회수) |
| 진입 직후 Global 핸들 메모리 | **982 MB** | 대폭 감소 (프리로드 다이어트) |
| 던전 산물 영구 고정 | 아틀라스 3종 등 | 퇴장 시 회수 |
| AtlasSpriteCache | ~300 영구 상주 | 소비자 수명과 동행 |
| Scene 버킷 동작 | 정상 (65~94MB, 전환 시 교체) | **불변** (회귀 없음 증명) |
| 프리로드 시간 | 99~177ms | (참고 지표 — 개선 축 아님) |

### 도식 (읽는 순서 추천)

1. 📄 [ResourceFlow_Example.pdf](./ResourceFlow_Example.pdf) — **예시로 이해하기**: 고블린 스폰 스토리 4장면 (용어: 창고/영수증/장부/티켓북)
2. 📄 [ResourceScope_Structure.pdf](./ResourceScope_Structure.pdf) — 새 구조 한눈에 + refCount 타임라인
3. 📄 [ResourceSystem_ASIS_TOBE.pdf](./ResourceSystem_ASIS_TOBE.pdf) — AS-IS/TO-BE 비교 도식 + 문제 지점 P1~P7 매핑

---

> 대전제 1: **빅뱅 재작성 금지.** `Managers.Resource` 파사드와 기존 호출 API(130곳)를 유지한 채 안쪽만 단계적으로 교체. 기존 `isGlobal` 시그니처는 어댑터로 남긴다.
> 대전제 2: **각 Phase = 독립 커밋 = 롤백 지점.** (입력 리팩토링과 동일 방식)
> 대전제 3: 이 작업은 **플래그십 #1(Addressable 리소스 라이프사이클 + 스폰 파이프라인)의 본편**이다. 기존 [AsyncResourcePipeline](../AsyncResourcePipeline/README.md) 문서의 "global/scene 버킷" 서사가 "스코프 기반 수명 관리"로 업그레이드되는 구조.

---

## 0. 현황 진단

### 0.1 현재 데이터 흐름

```
호출부 46개 파일 · 130곳 (string key + isGlobal bool 직접 결정)
   │  Load / Instantiate
   ▼
ResourceManager (POCO)
   ├─ _globalHandles   : 영구 버킷 (게임 종료까지 해제 없음)
   ├─ _sceneHandles    : 씬 버킷 (씬 Clear 시 일괄 Release, 중간 해제 불가)
   ├─ _atlasSpriteCache: 영구 캐시 (해제 경로 없음)
   └─ Instantiate → Poolable이면 PoolManager 위임
                        └─ @Pool_Root(DDOL) · 이름 키 · 에셋 핸들 ref 안 잡음
씬 전환:
SceneManagerEx.LoadSceneAsync
   → CurrentScene.Clear() ← 이전 씬이 아직 렌더 중일 때 씬 핸들 전부 Release
   → Loading 씬에서 Resource/Pool/UI Clear (이중 호출) → 라벨 프리로드(순차) → 다음 씬
```

관련 파일:
- `Assets/Scripts/Managers/Core/ResourceManager.cs` — 2버킷 + 캐시 + 프리로드 + Instantiate (+ Phase 0 계측 region)
- `Assets/Scripts/Managers/Core/PoolManager.cs` — 이름 키 풀, DDOL 루트
- `Assets/Scripts/Scenes/BaseScene.cs`, `Assets/Scripts/Scenes/LoadingScene/LoadingScene.cs`, `Assets/Scripts/Managers/Core/SceneManagerEx.cs` — 씬 전환·해제 흐름
- `Assets/Scripts/Factory/MonsterFactory.cs`, `Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs` — 스폰 소비자
- `Assets/Editor/ResourceDebugWindow.cs` — Phase 0 계측 창 (Tools > Resource Debug 창)

### 0.2 문제점

| # | 문제 | 근거(코드) | 영향 |
|---|------|-----------|------|
| **P1** | **수명 결정 산재 + 캐시 히트가 수명 의도 무시** ⭐ — `isGlobal` bool이 9개 파일에 흩어짐. 캐시 히트 경로는 `isGlobal` 인자를 무시 → scene으로 먼저 로드된 키를 global로 요청해도 승격 없음 → 씬 전환 시 파괴된 에셋 참조 위험. 팝업 UI는 전부 `isGlobal:true`라 게임 끝까지 상주 | `ResourceManager.cs` LoadAsync 캐시 히트 경로, `UIManager.cs` ShowPopupUIAsync | **Baseline으로 정량화됨: 팝업 세션 +287MB 영구, 핸들 메모리 95%가 global** |
| **P2** | **NoCache use-after-release** — 핸들을 `Release`한 뒤 result 반환. refcount 0 → 번들 언로드로 에셋이 파괴될 수 있는데 호출부(JSON)는 그 후 사용. 현재는 언로드 지연 덕에 우연히 동작 | `ResourceManager.cs` LoadAsyncNoCache | 잠재 크래시/데이터 소실. 타이밍 의존 UB |
| **P3** | **해제 순서 위험 + 이중 Clear** — 이전 씬이 렌더 중일 때 씬 핸들 전부 Release → 번들 언로드 창(window) 존재. LoadingScene에서 `Resource.Clear()` 재호출 | `SceneManagerEx.cs` LoadSceneAsync, `LoadingScene.cs` | 화면에 떠 있는 오브젝트의 텍스처/메시 파괴 가능성 |
| **P4** | **풀↔에셋 수명 미연결** — 풀은 인스턴스만 들고 에셋 핸들 ref를 안 잡음. DDOL 풀 루트 vs 씬 버킷 핸들 수명 불일치. Clear 순서에만 의존해 안 터지는 상태 | `PoolManager.cs` 전반 | 순서 하나 어긋나면 즉시 핑크 텍스처 |
| **P5** | **타입 충돌 핸들 교체 핵** — 캐시 키가 string뿐이라 같은 키를 다른 T로 로드하면 기존 핸들을 빼서 재로드하는 증상 치료 로직 | `ResourceManager.cs` LoadAsyncInternal | 복잡성·경합 위험. `(key, type)` 키면 근본 소멸 |
| **P6** | **프리로드 순차 로드** — location마다 `LoadAssetAsync<Object>`를 하나씩 await | `ResourceManager.cs` LoadDependenciesAsync | **측정 결과 개선 축 아님** (씬당 8~9개, 99~177ms) — 참고 지표로 강등 |
| **P7** | **계측 부재 + magic string** — ~~살아있는 핸들/로드 출처를 볼 수단 없음~~ → **Phase 0으로 해소**. 130개 호출부 raw string 키는 잔존 | 전 호출부 | 오타 런타임 발견 |

> 잘 되어 있는 것(유지): 직접 `Addressables.*` 호출이 ResourceManager로 거의 수렴, 프리로드→동기 Instantiate 패턴, UniTask 취소 토큰 전파, 풀 위임, 측정 기반 최적화 문화([AsyncResourcePipeline](../AsyncResourcePipeline/README.md)). **씬 버킷의 씬 단위 해제 자체도 정상 동작(Baseline 확인)** — 고장난 게 아니라 계층이 부족한 것.

---

## 1. 목표 구조

핵심 전환: **"2버킷 + 호출부 bool" → "ref-count 레지스트리 + 스코프(수명 객체)"**

```
ResourceManager (파사드)
   ├─ 새 API: LoadAsync<T>(key, scope)          ← 수명은 스코프가 소유
   ├─ 어댑터: LoadAsync<T>(key, isGlobal)        ← 기존 130곳 호환 (점진 전환)
   └─ RotateSceneScope() / CreateScope()

ResourceRegistry — 핸들 소유 유일한 곳
   └─ Dictionary<(key, type), Entry { handle, refCount, owners(디버그) }>
      · 키+타입당 핸들 1개 (P5 소멸) · 로딩 중이면 같은 핸들 공유 대기
      · refCount 0이 되는 순간에만 실제 Addressables.Release

ResourceScope : IDisposable — '참조 티켓(HashSet<ResourceKey>)'만 보유
   Global (앱 수명)
    └ Scene (BaseScene 소유, 씬 전환 시 Rotate)
       └ Dungeon/Encounter (입장~퇴장)          ← 신설 계층
          └ Popup/임시 (using 블록)
   · 같은 스코프가 같은 키를 N번 로드해도 refCount 1 (Dispose 한 번으로 정산)
   · Dispose → 보유 키 전부 release
```

### 설계 3원칙

1. **핸들 소유권은 레지스트리 한 곳** — 스코프는 티켓만. "누가 Release하나" 문제 자체가 소멸
2. **스코프 내 중복 로드 = refCount 1** — `HashSet.Add` 반환값으로 처리. 호출부가 로드 횟수를 안 세도 됨
3. **취소 ≠ 해제** — 취소 토큰은 '기다림 중단'만. 핸들 수명은 스코프 책임 → use-after-release 구조적 불가

### 씬 전환 계약 (P3 해결)

이전 씬 스코프의 Dispose는 반드시 **새 씬 진입 완료 후** — `RotateSceneScope(next)`가 새 스코프 생성 → 이전 스코프 Dispose 순서를 강제. 렌더 중 해제 창 제거, 이중 Clear 제거.

### 임시 코드 스케치

Phase 2 착수 시 참고할 초안이 대화에서 작성됨 — 핵심 시그니처:
- `ResourceScope`: `HashSet<ResourceKey> _acquired`, `TryAcquire(key)` (처음이면 true → 그때만 refCount++), `Dispose()` → 전부 release
- `ResourceKey`: `(string Key, Type Type)` 복합 키 struct
- `ResourceRegistry.LoadAsync<T>(key, scope, token)`: 스코프 acquire → 캐시/로딩 공유 → 신규 로드. 취소돼도 핸들은 레지스트리 소유로 유지
- `ResourceManager`: `GlobalScope`/`SceneScope` 상시 + `CreateScope(name)`, 어댑터 `LoadAsync<T>(key, isGlobal)` = `isGlobal ? GlobalScope : SceneScope`

---

## 2. 단계별 계획

### Phase 0 — 계측 + Baseline ✅ 완료 (2026-07-16)

- 산출물 ①: `Tools > Resource Debug 창` (버킷별 핸들·로드 출처·[메모리 측정]), 씬 전환 자동 리포트/assert, 프리로드 Stopwatch — **영구 자산, Phase 2 후에도 레지스트리를 가리키도록 유지**
- 산출물 ②: [Baseline.md](./Baseline.md) — 수치·발견 7건
- 커밋: `6d3e8680`(도구), `87672de5`(메모리 계측 + Baseline)

### Phase 1 — 정확성 버그 3건 (반나절, 구조 변경 없이) — **수치 무관, 리스크 제거**

- P2: `LoadAsyncNoCache` — 사용 완료 후 Release로 계약 변경 (JSON은 `.text` 복사 후 해제)
- P1 일부: 캐시 히트 시 global 요청이면 scene → global 승격
- P3: 씬 핸들 Release 시점을 Loading 씬 진입 이후로 일원화, `BaseScene.Clear()`의 `Resource.Clear()` 이중 호출 제거

### Phase 2 — 레지스트리 + ResourceScope (1~2일) — **수치 무관, Phase 3의 전제조건**

- 내부를 `(key, type)` 키 + refCount 레지스트리로 교체 (P5 핵 제거)
- `ResourceScope`(IDisposable): Global/Scene 기본 스코프 + `CreateScope()`
- 어댑터 유지로 130개 호출부 일괄 수정 불필요
- `RotateSceneScope`로 씬 전환 배선
- 완료 기준: **디버그 창에서 씬 왕복 후 잔존 핸들 = Baseline과 동일** (동작 보존 증명 — 이 Phase는 수치가 "안 변해야" 성공)

### Phase 3 ★ — 수명 재배치 (메인, 1~2일) — **모든 성공 기준이 여기서 달성됨**

- **3a. 팝업 재배치**: `ShowPopupUIAsync`의 `isGlobal:true` → 씬 스코프 (기본 정책 A). 목표: 팝업 세션 영구 증가 +287MB → 0
- **3b. 대형 아틀라스 재배치**: `StandingImagesAtlas`(436MB) 등을 소비 UI 스코프에 귀속 → 닫으면 회수
- **3c. Global 프리로드 다이어트**: `*_Select` 13종 → SelectScene 스코프, 비파티 캐릭터 9종 로드 제외(파티 스코프) → 진입 직후 982MB 감소
- **3d. 던전 스코프 + AtlasSpriteCache 스코프화**: 던전 산물(루트 UI 아틀라스 등) 퇴장 시 회수, 스프라이트 캐시를 아틀라스 핸들 수명에 동행
- **트레이드오프 (정책 결정 기록)**: lazy화하면 첫 오픈 히치 발생
  - **A. 팝업 = 씬 스코프 (기본 채택)**: 씬당 첫 1회만 히치, 씬 전환 시 회수
  - B. 열림 동안만: 매번 로드 (대형 아틀라스에만 선별 적용하는 하이브리드 고려)
  - "히치 vs 상주를 측정으로 결정"하는 과정 자체를 문서화할 것
- 완료 기준: **성공 기준 표의 After 목표 달성 + 개발 빌드 실측 1회**

### Phase 4 — 풀·인스턴스 수명 통합 (1일) — 안전성 위주

- PoolManager를 스코프 소속으로: 풀 생성 시 에셋 ref acquire, 풀 파괴 시 release. DDOL 루트 제거
- `InstantiateAsync`/`Destroy`가 키별 인스턴스 카운트 추적 → 스코프 Dispose 시 잔존 인스턴스 경고

### Phase 5 — 프리로드 매니페스트 + 키 정리 (선택)

- 라벨 배치 `LoadAssetsAsync`, 씬별 매니페스트 + 에디터 검증, magic string 축소
- 측정상 개선 폭이 작아 우선순위 최하 — 여력 있을 때만

---

## 3. 범위 밖 (YAGNI) / 별도 트랙

- **프레임 예산 스트리밍** — 단일 섹터 활성 설계라 불필요 판단 완료 ([AsyncResourcePipeline §3-2](../AsyncResourcePipeline/README.md))
- **원격 CDN/카탈로그 업데이트** — 포트폴리오 범위 밖
- **UI 프리팹의 대형 텍스처 직접 참조 구조** (`UI_Info` 587MB의 근본 원인) — 스코프로 "언제 해제되냐"는 고쳐지지만 "왜 587MB냐"는 콘텐츠 구조 문제. **별도 트랙 후보로 보류, Phase 3 이후 재판단** (하나라도 고치면 콘텐츠 이해도 증명에 좋음)

## 4. 포트폴리오 서사 — 가치 판단과 예상 질문 방어 (2026-07-16 논의)

**판단: 진행 가치 충분 (신입~주니어 게임 클라 상위권 재료). 가치의 원천은 스코프 시스템(결과물)이 아니라 "측정→진단→구조→검증 루프"(방법론)와 말할 수 있는 숫자.**

완성 조건 3가지:
1. **After까지 완주** — Before만 있는 측정은 "문제 발견"이지 "해결"이 아님
2. **개발 빌드 실측 1회** — 에디터 측정치는 과대 추정이라 실측 없이는 숫자가 공격받음
3. 아래 예상 질문 방어를 문서에 명시

| 예상 질문 | 방어 |
|---|---|
| "Addressables가 이미 refCount인데 왜 그 위에 또?" | Addressables의 refCount는 **핸들 단위 수동 짝맞추기** — 호출부가 Release를 '기억'해야 하고, 그 기억이 실패한 게 HP바 누수였다. 스코프는 **기억을 구조로 대체**한 것 (수명 단위 자동 정산) |
| "본인이 만든 문제를 본인이 고친 것 아닌가?" | 맞다 — 숨기지 않는다. "초기 2버킷 설계의 한계를 계측으로 발견했고, 개인의 주의력이 아니라 구조로 재발을 차단했다." 레거시 개선은 실무의 일상이며, 이 프레이밍이 성장 서사가 된다 |
| "1인 프로젝트에 오버엔지니어링 아닌가?" | 982MB·+287MB라는 측정치가 필요성을 증명. 반대로 프레임 예산 스트리밍·프리로드 배치화는 측정 근거로 **제외/강등**했다 (필요한 것만 했다는 증거) |

## 5. 진행 로그 (의사결정 기록)

### 2026-07-16 — 진단 → 계획 → Phase 0 완료 → 계획 재구성

1. **구조 점검**: 리소스 관련 전 스크립트 훑고 P1~P7 진단. 도식 PDF 3종 제작
2. **계획 수립** (`a3665521`): Phase 0(계측)~4 구성. "계측 먼저"가 원칙
3. **Phase 0 도구** (`6d3e8680`): 디버그 창 + 로드 출처 추적(스택 기반, 에디터/개발 빌드 전용) + 씬 전환 자동 리포트/assert + 프리로드 Stopwatch. **동작 무변경 원칙**
4. **Baseline 1차 측정**: "NormalDungeon 복귀 후 던전 에셋 잔존"으로 보이는 이상 발견 → 재측정 결과 **측정 타이밍 오독**으로 판명 (씬 전환 자동 리포트가 판별 근거). 씬 버킷 해제는 정상
5. **측정 축 전환 결정**: 프리로드 시간(99~177ms, 씬당 8~9개)은 드라마틱한 개선이 불가 → **주 비교 축을 메모리로 전환**, 디버그 창에 [메모리 측정] 보강 (`87672de5`)
6. **메모리 Baseline 확정**: 팝업 세션 +287MB 영구 / 진입 직후 Global 982MB / 핸들 메모리 95%가 global / AtlasSpriteCache ~300 포화
7. **계획 재구성 (이 문서)**: "Phase 2(구조)만으로는 수치가 안 움직인다"는 인식 → **Phase 3 수명 재배치를 메인으로 승격**. 주 목적을 "수명 다층화 + 재배치"로 재정의, 성공 기준을 Baseline 수치로 고정
8. **포트폴리오 가치 판단**: 진행 가치 충분, 완성 조건 3가지 확정 (§4)

## 6. 다음 세션 가이드

**재개 지점: Phase 1 (정확성 버그 3건).** 읽을 순서: 이 문서 → [Baseline.md](./Baseline.md) → (개념이 흐리면) 도식 PDF 3종.

- Phase 1 작업 대상: `ResourceManager.cs`의 `LoadAsyncNoCache`(use-after-release) / `LoadAsync` 캐시 히트 경로(global 승격) / `SceneManagerEx.LoadSceneAsync`·`BaseScene.Clear`(이중 Clear·해제 시점)
- 각 Phase 완료 시: 배치 컴파일 확인(에디터 닫혀 있을 때) 또는 에디터 컴파일 → 디버그 창으로 씬 왕복 검증 → 독립 커밋
- 검증 습관: Phase 2는 "수치가 안 변해야 성공", Phase 3는 "성공 기준 표 달성이 성공"
- 커밋 컨벤션: `리소스 Phase N — <내용>` (기존: `a3665521` → `9c92a30f` → `6d3e8680` → `87672de5`)

## 관련 문서

- [Baseline.md](./Baseline.md) — Before 측정 결과·발견 7건 (Phase 0 산출물)
- [AsyncResourcePipeline](../AsyncResourcePipeline/README.md) — 현 파이프라인의 설계 동기·측정 성과 (이 계획의 전편)
- [MemoryLeak-MonsterHPBar](../TroubleShooting/MemoryLeak-MonsterHPBar/README.md) — "수동 Release 기억의 실패" 실사례 (스코프 도입의 동기)
