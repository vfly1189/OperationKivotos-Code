# 프로젝트에 적용된 CS 지식 정리 (OperationKivotos)

> 면접 대비용 — "개념 → 적용 위치 → 왜/효과" 매핑. 파일 경로는 실제 코드 기준.

## 1. 자료구조 (Data Structures)

| 개념 | 적용 위치 | 왜 / 효과 |
|---|---|---|
| **Dictionary (해시맵)** | `BaseCharacter._slotMap` = `Dictionary<CharacterAbilitySlot, List<AbilityData>>` / `AbilityRunner._cd` = `Dictionary<AbilityData,(end,dur)>` | O(1) 슬롯·쿨다운 조회. 선형 탐색 제거 |
| **Dictionary 캐싱** | `ResourceManager` / `DataManager` 로드 핸들 캐싱 | 중복 로드 방지, 재조회 O(1) |
| **List 순서 = 의미** | 슬롯당 어빌리티 리스트의 인덱스 = "비트 인덱스" | 등장 순서를 데이터 의미로 활용 |
| **Object Pool (자유 리스트)** | `PoolManager`, `AutoReturnToPool`, 투사체/VFX | 스폰마다 alloc 대신 재사용 → GC·프레임 스파이크 제거 |

## 2. 알고리즘 · 공간분할

| 개념 | 적용 위치 | 왜 / 효과 |
|---|---|---|
| **공간 분할 (Spatial Partitioning)** | `SectorManager`, `Sector` — 맵을 섹터로 쪼개 플레이어 섹터만 활성 | 전체 맵 AI/렌더 연산 병목 제거 (Script ▼57%, Triangles ▼67%) |
| **경계 히스테리시스 (계획)** | 로드 반경 > 언로드 반경 | 경계 왕복 시 로드/언로드 스래싱 방지 |
| **랜덤 선택 / 가중 분기** | 보스 BT `RandomNode`, `SelectRandomChoice`/`BranchByChoice` Effect | 패턴 다양성을 데이터로 |

## 3. 디자인 패턴 (Design Patterns)

| 패턴 | 적용 위치 | 왜 / 효과 |
|---|---|---|
| **State Machine (FSM)** | `CharacterStateMachine` (Idle/Move/Attack/Q_Skill/E_Skill/Death…) | 상태별 전이·가드 명시화 |
| **Behavior Tree** | `BTNode`(Selector/Sequence/RandomNode/ActionNode), 보스·몹 AI | FSM 상태폭발 문제를 트리 조합으로 해결 |
| **Strategy / Composition** | `IEffect` 조합 = 스킬 (`DealDamage`+`SpawnProjectiles`+…) | "스킬=클래스 1개" 상속 대신 부품 조합. 새 스킬 = 데이터 |
| **Factory** | `MonsterFactory`, `EquipmentFactory` | 생성 로직 캡슐화 + 풀링·Addressable 일원화 |
| **Object Pool** | `PoolManager` | 생성/파괴 비용 상수화 |
| **Observer (이벤트 구독)** | `event Action` — `OnDead`, `OnStateChanged`, `Stat.OnChanged` → UI/보상/토스트 | 데이터-UI 결합도 분리 (Push 기반 갱신) |
| **Service Locator / Singleton** | `Managers` (Resource/UI/Sound/Party…) | 전역 서비스 단일 접근점 |
| **Prototype (SO ↔ 런타임)** | `EffectData(SO)` → `CreateRuntime()` → `IEffect` | 프리팹↔인스턴스 개념. 상태 없으면 `=>this`(무할당) |

## 4. OOP · SOLID

| 원칙 | 적용 위치 | 왜 / 효과 |
|---|---|---|
| **인터페이스 · 다형성** | `IAbilityCaster`, `IEffect`, `IChoiceHandler`, `IDamageable`, `IProjectile` | 구현 교체·확장, 캐스터 3진영 단일 계약 |
| **SRP (단일 책임)** | God `CharacterStat` 분해 → `MonsterReward`(경제)·`DamageNumberPresenter`(UI)·`Health`(체력) | 책임 분리, 변경 파급 축소 |
| **OCP (개방-폐쇄)** | 새 Effect/어빌리티 = 코드 수정 없이 asset·클래스 **추가** | 기존 파이프라인 불변 |
| **DIP (의존 역전)** | Effect가 씬 구현이 아니라 `AbilityContext`·인터페이스에 의존 | SO가 씬 오브젝트 직접 참조 안 함 |

## 5. 동시성 · 비동기 (Concurrency & Async)

| 개념 | 적용 위치 | 왜 / 효과 |
|---|---|---|
| **비동기 파이프라인** | `UniTask` 기반 Addressable 로드/씬 전환 | 동기 일괄 로딩의 프레임 블로킹 제거 |
| **레이스 컨디션 해소** | `UniTask`의 PlayerLoop 통합 → Load/Unload 순서 보장 | C# `Task`의 `AssetBundle.Unload` 순서역전 버그 해결 |
| **취소 토큰 (수명 관리)** | `CancellationToken` / `_actionCts` — 상태 전환·파괴 시 진행 중 작업 중단 | await 후 파괴 객체 접근 방지 |
| **스냅샷 (동시성 안전)** | 투사체가 `DamageInfo` 값 패킷 배달 (계산=공격자, 배달=투사체) | 공격자 사망(풀 반환) 후에도 안전 |

## 6. 메모리 관리 · 성능

| 개념 | 적용 위치 | 왜 / 효과 |
|---|---|---|
| **GC 할당 최소화** | `CreateRuntime() => this` (상태 없는 Effect 무할당), 구조체 기반 UniTask | 캐스트마다 힙 alloc 0 |
| **에셋 수명 관리** | global/scene 버킷으로 씬 전환 시 누수 0 검증 | Memory Profiler `GameObject +291→+3` |
| **풀링 누수 수정** | `MonsterFactory` async 경로가 `Object.Instantiate`로 풀 우회 → `Managers.Resource.Instantiate` 통일 | 콜드 356ms → 웜 194ms |
| **데이터 주도 (파싱 부하 제거)** | Excel → SO/JSON 베이킹, 런타임 파싱 제거 | 런타임 GC 스파이크 제거 |

---

## 면접 각도 요약

- **자료구조/알고리즘**: 해시맵 O(1) 조회, 공간 분할로 연산량 감축(정량 수치 보유).
- **패턴**: 상속(스킬=클래스) → **조합(Strategy)**로 전환한 의사결정 서사가 핵심.
- **동시성**: `Task` vs `UniTask` 트레이드오프(PlayerLoop·레이스·GC)를 근거로 선택.
- **메모리**: 풀링·수명 버킷·무할당 Effect를 **프로파일러 Before/After**로 실증.

> 참고: 포트폴리오의 이터널 리턴(DirectX11) 프로젝트는 **QuadTree·Spatial Grid·Deferred Rendering** 등 저수준 CS를 추가로 커버 — 자료구조/렌더링 파이프라인 깊이는 그쪽에서 보강.
