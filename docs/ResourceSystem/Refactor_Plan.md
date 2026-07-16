# 리소스 시스템 리팩토링 — 2버킷 수동 관리 → ref-count 레지스트리 + ResourceScope

## 결과 요약 (TL;DR)

| 단계 | 내용 | 예상 | 상태 |
|---|---|---|---|
| **Phase 0** | 계측 먼저 — 핸들 디버그 창 + 씬 전환 후 누수 assert + **Before 상태 측정** | 반나절~1일 | ⬜ 예정 |
| **Phase 1** | 정확성 버그 수정 — NoCache use-after-release · 캐시 히트 승격 · 해제 순서 일원화 | 반나절 | ⬜ 예정 |
| **Phase 2** | **본편** — `(key, type)` ref-count 레지스트리 + `ResourceScope`(IDisposable). `isGlobal` bool → 스코프 인자. 기존 API는 어댑터 유지 | 1~2일 | ⬜ 예정 |
| **Phase 3** | 풀·인스턴스 수명 통합 — 스코프 소속 풀(에셋 ref 보유) + 인스턴스 카운트 추적 + 던전 스코프 신설 | 1일 | ⬜ 예정 |
| **Phase 4** | 프리로드 매니페스트 + 키 정리 — 라벨 배치 로드, magic string 축소 (선택) | 1일 | ⬜ 예정 |

**핵심 전환 한 줄**: 리소스 수명의 주체를 "호출부의 `isGlobal` bool"에서 "**스코프 객체**"로 옮기고, 핸들 소유는 레지스트리 한 곳으로 모은다. → 반납을 '기억'하는 코드가 사라져 누수가 구조적으로 차단된다.

### 도식 (읽는 순서 추천)

1. 📄 [ResourceFlow_Example.pdf](./ResourceFlow_Example.pdf) — **예시로 이해하기**: 고블린 스폰 스토리 4장면 (용어: 창고/영수증/장부/티켓북)
2. 📄 [ResourceScope_Structure.pdf](./ResourceScope_Structure.pdf) — 새 구조 한눈에 + refCount 타임라인
3. 📄 [ResourceSystem_ASIS_TOBE.pdf](./ResourceSystem_ASIS_TOBE.pdf) — AS-IS/TO-BE 비교 도식 + 문제 지점 P1~P7 매핑 + 로드맵

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
- `Assets/Scripts/Managers/Core/ResourceManager.cs` — 2버킷 + 캐시 + 프리로드 + Instantiate
- `Assets/Scripts/Managers/Core/PoolManager.cs` — 이름 키 풀, DDOL 루트
- `Assets/Scripts/Scenes/BaseScene.cs`, `Assets/Scripts/Scenes/LoadingScene/LoadingScene.cs`, `Assets/Scripts/Managers/Core/SceneManagerEx.cs` — 씬 전환·해제 흐름
- `Assets/Scripts/Factory/MonsterFactory.cs`, `Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs` — 스폰 소비자

### 0.2 문제점

| # | 문제 | 근거(코드) | 영향 |
|---|------|-----------|------|
| **P1** | **수명 결정 산재 + 캐시 히트가 수명 의도 무시** ⭐ — `isGlobal` bool이 9개 파일에 흩어짐. 캐시 히트 경로는 `isGlobal` 인자를 무시 → scene으로 먼저 로드된 키를 global로 요청해도 승격 없음 → 씬 전환 시 파괴된 에셋 참조 위험. 팝업 UI는 전부 `isGlobal:true`라 게임 끝까지 상주 | `ResourceManager.cs:48-59`, `UIManager.cs:115` | 양방향 수명 사고: 조기 해제(핑크 텍스처) + 과잉 상주(메모리 낭비) |
| **P2** | **NoCache use-after-release** — 핸들을 `Release`한 뒤 result 반환. refcount 0 → 번들 언로드로 에셋이 파괴될 수 있는데 호출부(JSON)는 그 후 사용. 현재는 언로드 지연 덕에 우연히 동작 | `ResourceManager.cs:138-141` | 잠재 크래시/데이터 소실. 타이밍 의존 UB |
| **P3** | **해제 순서 위험 + 이중 Clear** — 이전 씬이 렌더 중일 때 씬 핸들 전부 Release → 번들 언로드 창(window) 존재. LoadingScene에서 `Resource.Clear()` 재호출 — 해제 책임 소재 불명확 | `SceneManagerEx.cs:68`, `LoadingScene.cs:36` | 화면에 떠 있는 오브젝트의 텍스처/메시 파괴 가능성 |
| **P4** | **풀↔에셋 수명 미연결** — 풀은 인스턴스만 들고 에셋 핸들 ref를 안 잡음. DDOL 풀 루트 vs 씬 버킷 핸들 수명 불일치. Clear 순서에만 의존해 안 터지는 상태 | `PoolManager.cs` 전반 | 순서 하나 어긋나면 즉시 핑크 텍스처 |
| **P5** | **타입 충돌 핸들 교체 핵** — 캐시 키가 string뿐이라 같은 키를 다른 T로 로드하면 기존 핸들을 빼서 재로드하는 증상 치료 로직 | `ResourceManager.cs:69-79` | 복잡성·경합 위험. `(key, type)` 키면 근본 소멸 |
| **P6** | **프리로드 순차 로드** — location마다 `LoadAssetAsync<Object>`를 하나씩 await. 라벨 배치 로드 대비 느리고, `Object` 로드가 P5를 유발 | `ResourceManager.cs:203-243` | 로딩 시간 손해 |
| **P7** | **계측 부재 + magic string** — 살아있는 핸들/로드 출처를 볼 수단 없음(누수는 사후 Memory Profiler뿐). 130개 호출부가 raw string 키 | 전 호출부 | 누수 추적 비용 큼 (HP바 누수 사례), 오타 런타임 발견 |

> 잘 되어 있는 것(유지): 직접 `Addressables.*` 호출이 ResourceManager로 거의 수렴, 프리로드→동기 Instantiate 패턴, UniTask 취소 토큰 전파, 풀 위임, 측정 기반 최적화 문화([AsyncResourcePipeline](../AsyncResourcePipeline/README.md)).

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
          └ 임시 (using 블록)
   · 같은 스코프가 같은 키를 N번 로드해도 refCount 1 (Dispose 한 번으로 정산)
   · Dispose → 보유 키 전부 release
```

### 설계 3원칙

1. **핸들 소유권은 레지스트리 한 곳** — 스코프는 티켓만. "누가 Release하나" 문제 자체가 소멸
2. **스코프 내 중복 로드 = refCount 1** — `HashSet.Add` 반환값으로 처리. 호출부가 로드 횟수를 안 세도 됨
3. **취소 ≠ 해제** — 취소 토큰은 '기다림 중단'만. 핸들 수명은 스코프 책임 → use-after-release 구조적 불가

### 씬 전환 계약 (P3 해결)

이전 씬 스코프의 Dispose는 반드시 **새 씬 진입 완료 후** — `RotateSceneScope(next)`가 새 스코프 생성 → 이전 스코프 Dispose 순서를 강제. 렌더 중 해제 창 제거, 이중 Clear 제거.

---

## 2. 단계별 계획

### Phase 0 — 계측 먼저 (반나절~1일)

**산출물 ① — 측정 도구 (영구 자산, 리팩토링 후에도 레지스트리를 가리키도록 유지)**

- 현재 `_globalHandles`/`_sceneHandles`를 들여다보는 에디터 디버그 창: 살아있는 핸들 목록 / 버킷 / 로드 출처
- 씬 전환 후 "씬 버킷 잔존 핸들 = 0" assert (에디터·개발 빌드)
- **먼저 하는 이유**: 이후 모든 Phase의 성공/실패를 눈으로 검증할 수단. "고치기 전에 보이게 만들었다"는 포트폴리오 서사

**산출물 ② — Before 상태 측정 (개선 전 증거 기록, `docs/ResourceSystem/Baseline.md`)**

| 측정 항목 | 방법 | 예상 결과 (P# 증거) |
|---|---|---|
| 씬 왕복 후 잔존 핸들 목록 | 디버그 창 | global 버킷에 열었던 팝업 전부 상주 (P1) |
| 던전 퇴장 직후 핸들 상태 | 디버그 창 | 던전 전용 에셋이 씬 끝까지 생존 (스코프 부재) |
| 아틀라스 캐시 크기 추이 | 디버그 창 | 단조 증가, 해제 경로 없음 |
| 라벨 프리로드 소요 시간 | Stopwatch 로그 | 순차 로드 기준치 (P6의 Before) |
| 메모리 스냅샷 | Memory Profiler | HP바 누수 때 방법론 재사용 — After 비교 기준점 |

→ 같은 도구·같은 시나리오로 Phase 2~4 이후 After를 재측정해 개선을 정량 증명한다.

### Phase 1 — 정확성 버그 즉시 수정 (반나절, 구조 변경 없이)

- P2: `LoadAsyncNoCache` — 사용 완료 후 Release로 계약 변경 (JSON은 `.text` 복사 후 해제)
- P1 일부: 캐시 히트 시 global 요청이면 scene → global 승격
- P3: 씬 핸들 Release 시점을 Loading 씬 진입 이후로 일원화, `BaseScene.Clear()`의 `Resource.Clear()` 이중 호출 제거

### Phase 2 — 레지스트리 + ResourceScope 도입 (본편, 1~2일)

- 내부를 `(key, type)` 키 + refCount 레지스트리로 교체 (P5 핵 제거)
- `ResourceScope`(IDisposable) 구현: Global/Scene 기본 스코프 + `CreateScope()`
- 기존 API는 어댑터 유지: `isGlobal:true` → GlobalScope, 기본 → 현재 SceneScope → **130개 호출부 일괄 수정 불필요, 점진 전환**
- `RotateSceneScope`로 씬 전환 배선 (BaseScene/SceneManagerEx/LoadingScene)
- 완료 기준: Phase 0 디버그 창에서 씬 왕복 후 잔존 핸들 0 확인

### Phase 3 — 풀·인스턴스 수명 통합 (1일)

- PoolManager를 스코프 소속으로: 풀 생성 시 에셋 ref acquire, 풀 파괴 시 release. DDOL 루트 제거
- `InstantiateAsync`/`Destroy`가 키별 인스턴스 카운트 추적 → 스코프 Dispose 시 잔존 인스턴스 경고
- 던전 스코프 신설: 던전 몬스터/보스 이펙트는 던전 퇴장 시 즉시 반납 (현재는 씬 전환까지 상주)

### Phase 4 — 프리로드 매니페스트 + 키 정리 (1일, 선택)

- 라벨 단위 `LoadAssetsAsync` 배치 프리로드로 교체 (P6)
- 씬별 프리로드 매니페스트 + 에디터 검증(데이터가 참조하는 키가 Addressables에 실존하는지)
- magic string → 상수 생성 또는 `AssetReferenceT` 확대 (입력 리팩토링 Phase 1과 같은 방향)

---

## 3. 범위 밖 (YAGNI)

- **프레임 예산 스트리밍** — 단일 섹터 활성 설계라 불필요 판단 완료 ([AsyncResourcePipeline §3-2](../AsyncResourcePipeline/README.md) 근거 유지)
- **원격 CDN/카탈로그 업데이트** — 포트폴리오 범위 밖
- **아틀라스 캐시 LRU 등 정교한 축출 정책** — 게임 규모상 스코프 귀속으로 충분

## 4. 검증 기준 (Phase별 공통)

- 씬(GameScene ↔ Dungeon) 반복 왕복 후 디버그 창 잔존 핸들 = 기대치(Global만)
- Memory Profiler 스냅샷 비교 — 무거운 에셋(텍스처/메시) 누적 0 (기존 검증 방법론 재사용)
- 스폰 스트레스 시나리오에서 풀 재사용 동작 유지 (Instantiate.Copy 미발생)

## 5. 관련 문서

- [AsyncResourcePipeline](../AsyncResourcePipeline/README.md) — 현 파이프라인의 설계 동기·측정 성과 (이 계획의 전편)
- [MemoryLeak-MonsterHPBar](../TroubleShooting/MemoryLeak-MonsterHPBar/README.md) — 계측 부재(P7) 비용의 실제 사례
- 도식 PDF 3종 — 상단 "도식" 절 참조
