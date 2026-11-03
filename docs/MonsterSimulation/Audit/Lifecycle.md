# 몬스터 생명주기 계약 — 훅 배치 결정과 현재 코드 판정

> 작성: 2026-08-25 · 대상: 풀링 전환 **전** 생명주기 훅 배치 확정
> [07_Prerequisites 2.1](../Comparison/07_Prerequisites.md)의 **상세판**이다.
> 07은 "무엇을 해야 하는가"(계약 표 + 목표 순서), 이 문서는 **"왜 그렇게 되는가"(Unity 훅 의미론)** 와
> **"지금 코드가 그 계약을 얼마나 지키는가"(판정)** 를 다룬다.
> **코드는 수정하지 않았다.** 전부 읽기·검증만 한 결과다.

---

## 0. 이 문서가 답하는 질문

1. Awake / Start의 정확한 실행 조건은 무엇인가 — "활성화 여부와 무관"은 어디까지 참인가
2. "외부 객체 참조는 Start에서" 라는 통념은 풀링 객체에도 적용되는가
3. 채택한 3단 분리(Awake / OnEnable / OnSpawn)에서 **따라나오는 불변식**은 무엇인가
4. 현재 코드는 그 계약을 지키는가 — 잘 된 것 / 안 되는 것 / 상관없는 것

---

## 1. Unity 훅 의미론 (확정)

### 1.1 Awake

"인스턴스 생성 시 활성화 여부와 무관하게 무조건 실행"은 **절반만 참**이다.
"활성화"가 두 가지인데 갈린다.

| 조건 | Awake |
|---|---|
| 컴포넌트 `enabled == false` (인스펙터 체크박스 해제) | **실행됨** |
| GameObject `SetActive(false)` | **실행되지 않음. 보류** |

`Instantiate()`로 만들어도 루트가 비활성이면 Awake는 돌지 않는다.
나중에 `SetActive(true)` 하는 **그 순간** `Awake → OnEnable` 순으로 동기 실행된다.

> **Awake의 정확한 보장: "생성 시점"이 아니라 "최초로 활성화되는 시점에, OnEnable보다 먼저, 평생 1회".**

### 1.2 Start

"활성화 상태일 때 Update 전 한 프레임"이 아니라 **"그 컴포넌트의 첫 Update 직전"** 이다.
그리고 Awake와 달리 **컴포넌트 `enabled`도 본다.**

| | GameObject 비활성 | 컴포넌트 disabled | 재활성화 시 |
|---|---|---|---|
| `Awake` | 보류 | **실행됨** | 1회만 |
| `Start` | 보류 | 보류 | 1회만 |
| `OnEnable` | — | — | **매번** |

### 1.3 프리웜에 직접 영향 ★

Awake가 "생성 시점"이 아니라 "최초 활성화 시점"이라는 사실이 프리웜에서 그대로 드러난다.

| 프리팹 루트 상태 | `Instantiate` 시 | 결과 |
|---|---|---|
| **active** | Awake 실행 → `SetActive(false)` | 프리웜 목적 달성 |
| **inactive** | Awake 미실행 | **첫 스폰에서 Awake 비용 발생 — 프리웜 무의미** |

`Awake`에는 `ConstructBehaviorTree()`가 있다([BaseMonsterController.cs:84](../../../Assets/Scripts/Controllers/Monster/BaseMonsterController.cs:84)).
프리팹 루트 상태에 따라 **첫 스폰 히칭 유무가 갈린다.**
히칭 프레임 수는 [02_Measurement](../Comparison/02_Measurement.md) 4절의 핵심 지표이므로 통제 변수다.

현재 `PoolManager.Pool.Create()`는 `Object.Instantiate(Original)` 후 `Push()`가 `SetActive(false)`를 한다
([PoolManager.cs:41](../../../Assets/Scripts/Managers/Core/PoolManager.cs:41)).
즉 **프리팹 루트가 active여야 프리웜이 성립한다 — 프리팹 실물 확인 필요.**

### 1.4 "외부 참조는 Start에서" 통념의 적용 범위

이 통념이 존재하는 이유는 하나다: **씬 로드 시 GameObject 간 Awake 실행 순서가 비결정적**이라는 것.
A의 Awake가 B의 Awake 산출물을 읽으면 경쟁 조건이 된다. Start는 그 방벽이다 —
같은 활성화 배치의 **모든 Awake가 끝난 뒤에** Start들이 돈다.

**그런데 런타임에 풀에서 스폰되는 객체에는 이 전제가 통째로 사라진다.**
Managers는 씬 로드 시 이미 존재하고, 몬스터는 한참 뒤에 등장한다. 경쟁할 상대가 없다.
반면 **Start는 평생 1회**라는 대가는 그대로 치른다.

> **결론: "Start에서 하라"의 풀링판 번역은 "OnSpawn에서 하라"이다.**
> Start는 씬에 미리 배치된 객체끼리 참조할 때만 유효한 답이다.

---

## 2. 채택한 계약

### 2.1 훅 배치

| 훅 | 성격 | 담을 것 |
|---|---|---|
| `Awake` | 인스턴스당 1회 | 컴포넌트 캐시, `EnsureOn`, **BT 구축**, 프리팹 고정 파라미터 |
| `OnSpawn(ctx)` | 매 스폰 · **비활성 상태에서 호출** | 스탯 주입 후 상태·타겟·쿨·BT 리셋, **이벤트 구독**, 데이터 주입 파라미터 |
| `OnEnable` | 활성화 반응만 | Collider 켜기, Animator 리바인드 + 스폰 애니메이션 |
| `OnDespawn()` | 매 디스폰 · **켜진 채로 호출** | 구독 해제, HP바 반납, CTS 취소 |
| ~~`Start`~~ | — | **비워둔다** (§3.3 참조) |

파라미터 배치 기준:

- **프리팹 고정값** (`updateRotation` 등) → `Awake`
- **데이터 주입값** (`speed`, `stoppingDistance` = `_attackRange` 의존) → `OnSpawn`
- **생애 상태** (`_agent.enabled = false`) → `OnSpawn` / `OnEnable`

### 2.2 풀 흐름

```
Deactivate(obj)   obj.SetActive(false) ; stack.Push(obj)

Create()          obj = Instantiate(active)      // Awake · OnEnable
                  Deactivate(obj)                // OnDisable

Get(init)         obj = stack.Empty ? Create() : stack.Pop()
                  init(obj)                      // 위치 · 스탯 주입 · OnSpawn  ← 비활성 상태
                  obj.SetActive(true)            // OnEnable

Release(obj)      OnDespawn(obj)                 // 켜진 채로 정리
                  Deactivate(obj)                // OnDisable
```

핵심은 **`init` → `SetActive(true)` 순서**다. 상태가 먼저, 활성화 반응이 나중.
이것이 07의 "활성화 순서를 뒤집는 것이 핵심" 과 같은 말이다.

---

## 3. 흐름에서 따라나오는 불변식

계약을 정하면 자동으로 참이 되는 것들이다. **위반 여부가 곧 버그 판정 기준**이 된다.

### 3.1 OnEnable은 OnSpawn에 의존할 수 없다 ★

`Create()`에서 **OnEnable이 OnSpawn 한 번도 없이 실행된다.**

> **OnEnable은 자기 완결적이어야 한다. OnSpawn이 만든 상태를 읽으면 `Create()` 경로에서 깨진다.**

L1이 정확히 이 위반이다.

### 3.2 OnEnable은 첫 인스턴스에서 2회 돈다

`Create()`에서 1회(무의미), 첫 `Get`에서 1회(실제). 따라서 **OnEnable은 멱등해야 한다.**

### 3.3 Start는 죽지 않는다 ★

`Create()`는 active로 Instantiate 후 같은 프레임에 Deactivate하므로 Start는 **보류**된다.
첫 `Get`의 `SetActive(true)` 이후, 그 프레임 첫 Update 직전에 **1회 실행**된다.

```
OnSpawn → OnEnable → Start → Update      (첫 생애에 한해)
```

즉 **Start는 "안 쓰는 훅"이 아니라 "첫 생애에만 OnSpawn 뒤에 끼어드는 훅"** 이다.
비워두지 않으면 첫 생애와 이후 생애의 동작이 갈린다. L6 참조.

### 3.4 OnSpawn은 비활성 상태의 제약을 받는다

| 제약 | 현재 코드 |
|---|---|
| `StartCoroutine` 불가 (비활성 GO에서 throw) | 코루틴 미사용 · UniTask — **안전** |
| UniTask는 GO 활성과 무관하게 진행 | `isActiveAndEnabled` 가드가 위험 — **L5** |
| NavMeshAgent는 비활성 GO에서 켤 수 없음 | `ResetAgent`는 끄기만 함 — **안전** |
| `Animator.Rebind()`는 비활성에서 무의미 | OnEnable에 있음 — **맞음** |
| `transform.position`은 비활성에서 안전하고 **선호됨** | 1프레임 구위치 노출 없음 |

마지막 항목이 부수 효과를 낳는다 → L1 해결.

---

## 4. 현재 코드 판정

등급 기준은 [Issues.md](Issues.md)와 동일하다.

| # | 등급 | 위치 | 한 줄 |
|---|---|---|---|
| L0 | 치명 | NormalMonsterController | 구독은 `Start`(1회)·해제는 `OnDespawn`(매번) — 비대칭 |
| L1 | 치명 | RangedMonsterController | `OnEnable`이 `_monsterCts`에 의존 — `Create()` 경로에서 NRE |
| L2 | 치명 | 전역 | `OnSpawn`/`OnDespawn`을 **아무도 호출하지 않는다** |
| L3 | 치명 | PoolManager ↔ ResourceManager | 회수 경로가 둘인데 사망 경로가 `OnDespawn`을 건너뛴다 |
| L4 | 정리 | RangedMonsterController | 탄약 초기화가 `OnEnable`·`OnSpawn` 양쪽에 |
| L5 | 잠재 | NormalMonsterController | HP바 생성 가드가 활성 상태 기준 — OnSpawn 이관 시 깨진다 |
| L6 | 중간 | NormalMonsterController | `Start`가 비어 있지 않다 — 첫 생애만 동작이 다르다 |
| L7 | 잠재 | NormalMonsterController | agent를 **켜는** 코드가 RangedMC에만 있다 |

---

### L0. 구독/해제 훅이 짝이 맞지 않는다 · 치명

[NormalMonsterController.cs:38](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:38) 구독 ·
[:73](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:73) 해제.

```
Start      → 구독 + 초기 타겟 설정   ... 평생 1회
OnDespawn  → 구독 해제              ... 매 생애
```

풀에서 두 번째로 꺼내면:

1. `base.OnSpawn`이 `_target = null`
2. `Start`는 다시 안 돎 → 초기 타겟 설정 없음
3. 구독은 1생애 `OnDespawn`에서 끊김 → 재구독 없음
4. `CheckDetectRange()`가 `_target == null`이면 즉시 `Failure`
   ([:191](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:191))

→ **재사용된 몬스터는 영원히 Idle.** `_target`을 되찾을 경로가 코드에 없다.

부수 문제: `async void Start()`에서 `await CreateHpBarAsync()` **뒤에** 구독이 있다.
로드 대기 중 디스폰되면 `CreateHpBarAsync`는 `return`으로 정상 복귀하고 Start는 이어져서
**이미 죽은 몬스터가 Party 이벤트를 구독**한다. `OnDespawn`은 이미 지나갔다.

> [Issues.md](Issues.md) **M1**(구독 미해제 세션 누수)과 원인은 같고 증상이 다르다.
> M1은 누수, L0은 **재사용 시 무력화**. 계약 배선으로 둘 다 사라진다.

**해소**: 구독을 `OnSpawn`으로 이동 (해제는 이미 `OnDespawn`에 있음).

---

### L1. `OnEnable`이 `OnSpawn`이 만든 CTS에 의존한다 · 치명

[RangedMonsterController.cs:19](../../../Assets/Scripts/Controllers/Monster/RangedMonsterController.cs:19)

```csharp
protected override void OnEnable() {
    ...
    EnableAgentDelayAsync(_monsterCts.Token).Forget();   // _monsterCts는 OnSpawn에서 생성
}
```

`Pool.Init()` → `Create()` → `Instantiate(Original)` → 프리팹 루트가 active면 그 자리에서 Awake·OnEnable
→ **`_monsterCts == null` → NullReferenceException.**

§3.1 불변식 위반의 교과서 사례다. **계약을 채택해도 `Create()` 경로는 그대로 터진다.**

**해소 — 흐름 변경이 원인을 제거한다.**
이 1프레임 대기는 *위치 확정 전에 agent를 켜면 안 되니까* 존재한다.
그런데 새 흐름은 `init(obj)`에서 위치를 잡고 → `SetActive(true)` 하므로
**OnEnable 시점에 위치가 이미 확정**된다. → 1프레임 대기 자체가 불필요해지고 NRE도 함께 사라진다.

- 07 2.1의 "`agent.Warp` 분기가 불필요해진다"와 같은 뿌리
- **NavMesh 위 스폰 보장은 별도 검증 필요** (스폰 포인트가 NavMesh 밖이면 `Warp` 필요)

---

### L2. `OnSpawn` / `OnDespawn`을 아무도 호출하지 않는다 · 치명

`.OnSpawn(` / `.OnDespawn(` 전체 검색 결과가 `base.OnSpawn(ctx)` 계열 4건뿐. **외부 호출부 0개.**

- 스폰: `MonsterFactory.CreateMonsterByMonsterIDAsync` 경유
  ([MonsterSpawner.cs:112](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs:112))
- 사망 회수: `DespawnAsync` → `Managers.Resource.Destroy` → `Pool.Push`
- 스포너 정리: `Managers.Resource.Destroy`
  ([MonsterSpawner.cs:61](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs:61))

> **"현재 상태 초기화가 잘 되어 있나"의 답은 "아직 존재하지 않는다".**
> 설계는 있고 배선이 없다. 그 공백을 `OnEnable` + `Start`가 떠맡고 있고, L0·L1이 그 증상이다.

`SpawnContext`도 아직 빈 껍데기다 — 필드 3개가 전부 `private`이고 접근자·생성자가 없다
([BaseMonsterController.cs:9](../../../Assets/Scripts/Controllers/Monster/BaseMonsterController.cs:9)).

---

### L3. 회수 경로가 둘인데 하나만 `OnDespawn`을 부를 수 있다 · 치명

`Release(obj)`를 새로 만들면 **정상 회수만** 커버한다. 그런데 실제 소멸은 대부분 사망이고,
사망 경로는 `Resource.Destroy` → `Pool.Push`로 직행해 `OnDespawn`을 건너뛴다.

**뒤집어 보면 — 모든 회수가 `Resource.Destroy`를 지나간다.**

> **결정: `Release()`를 따로 만들지 않는다. `Pool.Push` 안에서 `IPooledMonster.OnDespawn()`을 호출한다.**

근거:
- `Push`는 진입 시점에 아직 켜져 있고 다음 줄에서 `SetActive(false)`를 한다
  ([PoolManager.cs:66](../../../Assets/Scripts/Managers/Core/PoolManager.cs:66))
  → **"켜진 채로 정리 → 끄기"라는 요구를 이미 만족**한다
- 사망·정상·씬정리 세 경로가 자동으로 하나가 된다
- `IPooledMonster`가 존재해야 할 이유가 이것이다 (현재 파일은 `using UnityEngine;` 한 줄뿐)

대칭으로 `Pop` 쪽도 이미 호환된다 — **`Pool.Pop`은 `SetActive(true)`를 하지 않는다**
([PoolManager.cs:73](../../../Assets/Scripts/Managers/Core/PoolManager.cs:73)).
`ResourceManager.Instantiate`의 Poolable 분기도 마찬가지다. 즉 `init` → `SetActive(true)` 순서와
**구조적으로 이미 맞다. 고칠 필요 없음.**

---

### L4. 탄약 초기화 중복 · 정리

`OnEnable`의 `_currentAmmo = _maxAmmo`
([RangedMonsterController.cs:16](../../../Assets/Scripts/Controllers/Monster/RangedMonsterController.cs:16))와
`OnSpawn`의 `ResetAmmo()`. 새 순서(OnSpawn → OnEnable)에서는 OnEnable이 나중에 덮어쓴다.
값이 같아 무해하지만 **권위가 불명확**하다. 계약상 OnSpawn이 권위 → OnEnable 쪽 삭제.

---

### L5. HP바 생성 가드가 활성 상태를 기준으로 한다 · 잠재

[NormalMonsterController.cs:105](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:105)

```csharp
if (this == null || !isActiveAndEnabled || token.IsCancellationRequested)
```

`CreateHpBarAsync`를 `OnSpawn`으로 옮기면 **비활성 상태에서 시작**된다.
어드레서블이 캐시돼 `await`이 **동기 완료**되면 아직 `SetActive(true)` 전이라
`isActiveAndEnabled == false` → **방금 만든 HP바를 즉시 파괴**한다.

**해소**: 가드를 활성 상태가 아니라 **토큰 기준**으로 바꾼다.
`_monsterCts`는 `base.OnSpawn`에서 생성되므로 `CreateHpBarAsync` 호출은 **반드시 `base.OnSpawn(ctx)` 뒤**여야 한다.

> [Issues.md](Issues.md) **M7**(HP바 최초 로드 중 디스폰)과 같은 코드 지점. 함께 처리한다.

---

### L6. `Start`가 비어 있지 않다 · 중간

§3.3에 따라 Start는 **첫 생애에 한해 `OnSpawn` 뒤에 실행**된다.
[NormalMonsterController.cs:37](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:37)의
`async void Start()`에 HP바 생성 + Party 구독이 살아 있으므로
**첫 생애와 이후 생애의 초기화 경로가 다르다.**

`_initialized` 플래그([:21](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:21))는
"Start는 1회뿐"을 손으로 우회하려고 만든 것이다. **계약을 배선하면 플래그와 함께 사라진다.**

**해소**: `Start` 오버라이드 · `_initialized` 필드 · `OnEnable`의 HP바 재생성 블록
([:59](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:59)) 전부 삭제.

---

### L7. agent를 켜는 코드가 RangedMC에만 있다 · 잠재

`ResetAgent`는 `_agent.enabled = false`로 끄고
([NormalMonsterController.cs:84](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:84)),
켜는 곳은 `RangedMonsterController.EnableAgentDelayAsync` 하나뿐이다.
`TrackTarget`은 `_agent.enabled`를 검사만 하고 켜지 않는다.

현재 구상 몬스터가 전부 RangedMC 하위(AR / RL / Tank)라 발현하지 않는다.
**근접몹을 NormalMC 직계로 만드는 순간 영원히 움직이지 못한다.**

**해소**: agent 활성화 책임을 NormalMC로 올린다. L1 해소와 같은 작업이다.

---

## 5. 잘 되어 있는 것 (유지)

| 항목 | 판정 근거 |
|---|---|
| `ConstructBehaviorTree()`=`Awake` / `_topNode.Reset()`=`OnSpawn` | **구조는 1회, 상태는 매 생애.** 이 계약의 모범 사례 |
| `CancelMonsterTasks()` null 가드 | 멱등 — `OnDespawn`·`OnDisable` 이중 호출에 안전 |
| `_col.enabled` — `OnEnable` 켜기 / `HandleDeadState` 끄기 | 대칭 유지 |
| `MonsterReward.EnsureOn` · `DamageNumberPresenter.EnsureOn` = `Awake` | 1회면 충분한 작업 |
| `Pool.Pop`이 `SetActive(true)`를 하지 않음 | 새 흐름과 **이미 호환** — 변경 불필요 |

## 6. 상관없는 것 (건드리지 않음)

- `Stat` lazy getter + `Awake`의 `var tempStat = Stat;` — 중복이나 사실상 Awake 캐싱. 무해.
  단 `_stat.Init()`이 per-life 상태를 만든다면 재확인 (HP는 `OnSpawn`의 `Recover()`가 담당)
- 필드 초기자 `_state = MonsterState.Spawning` — 풀 재사용 시 실행되지 않지만 `OnSpawn`이 덮어씀

---

## 7. 다음 단계

1. **`IPooledMonster` 계약 정의** — `OnSpawn(ctx)` / `OnDespawn()` (현재 파일은 빈 껍데기)
2. **`Pool.Push`에 `OnDespawn` 배선** — L3. 회수 경로 단일화
3. **`Pop` 호출부에 `OnSpawn` 배선** — L2. `init` → `SetActive(true)` 순서 확립
   - `ResourceManager`의 `activate: false` 오버로드가 이음새 (07 2.1)
4. **`SpawnContext` 실체화** — 현재 필드 3개가 전부 접근 불가
5. 배선 과정에서 자연 해소: **L0 · L1 · L4 · L5 · L6**
6. 별도 확인 필요:
   - **몬스터 프리팹 루트의 active 여부** (§1.3 — 프리웜 성립 조건)
   - **스폰 포인트가 NavMesh 위인지** (L1 — `Warp` 필요 여부)
   - `MakeSubItemAsync`가 `Resource.Instantiate`를 경유하는지 (HP바 풀링 성립 조건)

### HP바 풀링 — 별건 메모

몬스터:HP바가 1:1이라 밀도 축에서 생성/파괴 횟수가 그대로 2배가 된다. UI는 파괴 시 Canvas 리빌드가 붙는다.

**거의 공짜다.** `ResourceManager.Destroy`는 `Poolable`이 있으면 `Pool.Push`로 보내고
([ResourceManager.cs:443](../../../Assets/Scripts/Managers/Core/Resource/ResourceManager.cs:443)),
`Instantiate`도 `Poolable`이 있으면 `Pool.Pop`으로 간다([:424](../../../Assets/Scripts/Managers/Core/Resource/ResourceManager.cs:424)).
→ **MonsterHPBar 프리팹에 `Poolable` 컴포넌트만 붙이면 양쪽이 자동으로 풀 경로를 탄다.**
(단 `MakeSubItemAsync`가 `Resource.Instantiate`를 경유하는지 확인 — 위 6번)

**한 층 위 레버 (지금 결정하지 않음)**: HP바는 *푸는 것보다 안 만드는 게 싼* 전형적 대상이다.
화면 밖 / 원거리 / 풀피 몬스터는 HP바가 필요 없다.
[01_Methods](../Comparison/01_Methods.md)의 품질 축(방법5)과 관통질문
"눈치채지 못하게 어디까지 줄이나"에 잘 맞는 소재. **풀링을 먼저 하고 나중에 재검토.**

---

## 관련 문서

- [07_Prerequisites 2.1](../Comparison/07_Prerequisites.md) — 계약 표 + 목표 활성화 순서 (이 문서의 상위)
- [Issues.md](Issues.md) — M1(구독 누수) · M3(스폰 1프레임 내 사망) · M5(상태 가진 BT 노드) · M6(보스 연출 플래그) · M7(HP바 로드 중 디스폰)
- [02_Measurement](../Comparison/02_Measurement.md) — 히칭 프레임 지표 (§1.3이 통제 변수인 이유)
- [MonsterStructure.pdf](MonsterStructure.pdf) — 구조·흐름 도식
