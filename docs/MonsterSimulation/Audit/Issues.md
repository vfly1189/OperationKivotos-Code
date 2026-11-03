# 몬스터 · 스포너 코드 점검 — 결함 목록

> 작성: 2026-08-20 · 대상: Sector 재설계 착수 **전** 기반 코드 점검
> 구조·흐름 도식은 [MonsterStructure.pdf](MonsterStructure.pdf)에 있다. 이 문서는 **결함만** 다룬다.
> **코드는 수정하지 않았다.** 전부 읽기·검증만 한 결과다.
> 생명주기 훅 배치(Awake/OnEnable/OnSpawn)에 국한된 후속 점검은 [Lifecycle.md](Lifecycle.md)에 있다 —
> M1 · M7과 겹치는 지점이 있고, 그쪽에서 L0 · L5로 다시 다룬다.

## 검증 방법

각 항목은 추정이 아니라 실제 코드로 확인했다. 아래 세 가지는 별도로 파일을 열어 사실 확인했다.

| 확인한 것 | 결과 |
|---|---|
| 몬스터 프리팹의 `Poolable` 유무 | AR/RL/Tank = 있음(풀 경유) · Hieronymus_Boss = 없음(`Object.Destroy`) |
| 맵 프리팹의 비활성 오브젝트 | 4개 전부 `sb_city_05_prop_16` — **비활성 스포너는 현재 없음** |
| `PartyManager`가 이벤트를 비우는지 | `ClearParty()`는 `OnActiveCharacterChanged`를 건드리지 않음 |

## 등급 기준

| 등급 | 뜻 |
|---|---|
| **치명** | 현재 플레이 경로에서 재현 가능하고, 성능·게임플레이에 직접 영향 |
| **중간** | 재현되지만 조건이 좁거나 증상이 작음 / 데이터 오타 한 번에 씬이 죽는 류 |
| **잠재** | 지금은 다른 사실에 가려 발현하지 않음. 그 사실이 바뀌면 즉시 깨짐 |
| **정리** | 동작에는 영향 없음. 설명력·일관성 문제 |

---

## 요약표

| # | 등급 | 위치 | 한 줄 |
|---|---|---|---|
| S1 | 치명 | MonsterSpawner | 비활성 섹터에 몬스터가 되살아나 영구히 남는다 |
| S2 | 치명 | MonsterSpawner | CTS를 인덱스로 정리해서 다른 태스크의 CTS를 파괴한다 |
| S3 | 치명 | MonsterSpawner | `StartRespawn`이 점유 검사 없이 덮어써 한 포인트에 2마리 |
| S4 | 중간 | MonsterSpawner | 취소 직후 생성 완료된 몬스터를 거르지 않는다 |
| S5 | 중간 | MonsterSpawner | `SpawnerData` null·음수 인덱스 미방어 |
| S6 | 중간 | SpawnerData.json | 전 항목 `delay: 5.0` — 같은 프레임에 스폰이 몰린다 |
| M1 | 치명 | NormalMonsterController | 파티 이벤트 구독을 해제하지 않는다 (세션 누수) |
| M2 | 중간 | NormalDungeon / BossDungeon | 던전 스폰 경로에 취소 토큰이 없다 |
| M3 | 중간 | RangedMonsterController | 스폰 1프레임 내 사망 시 `Dead`를 `Idle`로 덮어쓴다 |
| M4 | 중간 | Health ↔ BT | 사망 통보는 즉시, 사망 처리는 다음 프레임 — 그 사이 공격이 나간다 |
| M5 | 잠재 | BTNode | 상태를 가진 노드는 풀 재사용에서 리셋되지 않는다 |
| M6 | 잠재 | BossMonsterController | 사망·연출 플래그가 `OnEnable`에서 리셋되지 않는다 |
| M7 | 잠재 | NormalMonsterController | HP바 최초 로드 중 디스폰되면 HP바 없는 몬스터가 된다 |
| M8 | 정리 | MonsterStat | 이벤트 소유자가 남의 구독을 지운다 (`OnMonsterDead = null`) |
| M9 | 정리 | BTNode | `RandomNode`가 추첨할 때마다 로그를 찍는다 |
| M10 | 정리 | MonsterFactory | 오버로드 2개가 40줄을 그대로 복제한다 |

---

# 치명

## S1. 비활성 섹터에 몬스터가 되살아난다

**근본 원인**: 스포너에는 "지금 켜져 있는가"를 나타내는 상태가 없다. 대신 `gameObject.activeInHierarchy`를 그 대용으로 쓰는데([MonsterSpawner.cs:173](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs), [:182](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs)), `DeactivateSector()`는 GameObject를 끄지 않는다. **이 가드는 항상 참이다.**

**재현 경로**

1. 경계 근처에서 몬스터를 죽인다 → `HandleMonsterDead`가 슬롯을 비우고 `OnDespawned`에 리스폰 람다를 건다
2. 섹터를 빠져나간다 → `DespawnMonsters()`는 `_activeMonsters`만 순회한다. **죽는 중인 몹은 이 목록에 이미 없다** → 회수되지 않음
3. 2초 뒤 시체 연출이 끝나며 `OnDespawned` 발화 → `StartRespawn` → 가드 통과 → 5초 뒤 **꺼진 섹터에 몬스터가 스폰**

**영향**: 이 몬스터를 회수하는 주체가 없다. 그 섹터에 다시 들어갔다 나와야만 사라진다. 화면 밖에서 BT를 매 프레임 도는 개체가 스폰 포인트당 1마리씩 쌓이는데, **이건 Sector 시스템이 없애려던 바로 그 비용이다.**

**포트폴리오상 더 중요한 점**: 플레이 경로에 따라 결과가 달라지는 **측정 오염원**이다. `BenchmarkTester`의 F1/F2는 이 경로를 타지 않아 잡히지 않는다.

## S2. CTS를 인덱스로 정리해서 남의 것을 파괴한다

`SpawnPointRoutineAsync`의 `finally`가 `DisposeCts(pointIdx)`로 **인덱스 기준** 정리를 한다([:144](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs)). 자기가 만든 CTS인지 확인하지 않는다.

```
프레임 N   : DespawnMonsters → CancelAllSpawnTasks (idx 3 취소)
프레임 N   : 플레이어가 되돌아 진입 → SpawnMonsters → _respawnCts[3] = 새 CTS
프레임 N+1 : 취소된 옛 태스크가 깨어나 finally → DisposeCts(3)   ← 새 CTS를 파괴
```

`UniTask.Delay`의 취소는 `Cancel()` 시점이 아니라 다음 PlayerLoop 틱에 처리되므로, 그 사이에 새 CTS가 들어앉을 창이 실제로 존재한다.

**결과 두 가지**
- `_respawnCts[3] = null` → 다음 `SpawnMonsters`가 같은 포인트에 **두 번째 루틴**을 띄운다
- 진행 중인 루틴의 CTS 참조를 잃어 **더 이상 취소할 수 없다** → S1과 같은 유령이 또 생긴다

## S3. `StartRespawn`이 점유 검사 없이 덮어쓴다

[:184](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs)는 기존 `_respawnCts[pointIdx]`를 확인도 `Dispose`도 없이 덮어쓴다.

**재현**: 시체가 사라지기 전(2초 창)에 플레이어가 재진입 → `SpawnMonsters`가 그 슬롯을 "비었다"고 판단해 루틴 A 시작 → 2초 뒤 `StartRespawn`이 루틴 B로 덮어씀 → **한 포인트에 2마리**, 루틴 A의 CTS는 회수 불가.

이 세 건(S1~S3)은 전부 **"취소·사망·재진입이 겹치는 창"** 에서 터진다. 지금 맵이 5섹터라 눈에 안 띌 뿐이고, [01_Problem.md](../01_Problem.md)의 *"5개짜리 맵이라 안 터진 것"* 진단과 정확히 같은 계열이다.

## M1. 몬스터가 파티 이벤트 구독을 해제하지 않는다

[NormalMonsterController.cs:54-55](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs)에서 `Managers.Party.OnActiveCharacterChanged`를 구독하는데, **`OnDisable`·`OnDestroy` 어디에도 `-=`가 없다.**

같은 이벤트를 쓰는 다른 곳은 전부 해제한다 — `CameraController`(OnDestroy), `GameSceneCanvas`, `UI_Info`. **몬스터만 예외다.**

**영향**
- `PartyManager`는 세션 수명이고 `ClearParty()`도 이 이벤트를 비우지 않는다 → 씬을 왕복할 때마다 **파괴된 몬스터 인스턴스가 델리게이트에 영구 잔류**한다
- 캐릭터를 교체할 때마다 죽은 구독자까지 전부 순회한다
- 구독 핸들러가 유니티 API를 만지지 않아(`_target` 필드 대입뿐) 예외는 안 난다 → **조용히 샌다**

[HP바 누수 사건](../../TroubleShooting/MemoryLeak-MonsterHPBar/README.md)과 완전히 같은 계열이다. 그때 잡은 것은 UI 쪽이었고, 이건 매니저 이벤트 쪽이다.

---

# 중간

## S4. 취소 직후 생성 완료된 몬스터를 거르지 않는다

`InstantiateAsync`는 `Instantiate` **직전까지만** 토큰을 본다([ResourceManager.cs:359](../../../Assets/Scripts/Managers/Core/Resource/ResourceManager.cs)). 그 검사 직후에 취소되면 몬스터는 생성되어 반환되고, 호출부인 [MonsterSpawner.cs:120](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs)은 `monsterObj == null`만 보므로 **취소된 스폰이 그대로 등록된다.**

## S5. 데이터 방어가 없다

- [:33](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs) — `Managers.Data.GetData<int, SpawnerData>`는 못 찾으면 `LogError` 후 **null 반환**([DataManager.cs:110](../../../Assets/Scripts/Managers/Core/DataManager.cs)). 바로 다음 줄에서 NRE다. `_spawnerId` 오타 하나로 씬이 죽는다
- [:38](../../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs) — `pointIndex`의 상한만 검사하고 하한(음수)이 없다. 상한 초과도 조용한 `continue`라, 스폰 포인트를 지우면 **경고 없이 몬스터가 사라진다**

## S6. 스폰이 같은 프레임에 몰린다

[SpawnerData.json](../../../Assets/Resources_moved/Data/SpawnerData.json)의 **모든 항목이 `delay: 5.0`** 이다. 같은 프레임에 띄운 UniTask가 같은 딜레이를 가지므로, 17포인트 스포너(1002·1003)는 **정확히 같은 프레임에 17개 `InstantiateAsync`가 완료**된다. 프레임 예산 개념이 없다.

`delay` 필드 하나가 "최초 스폰 지연"과 "리스폰 간격"을 겸하고 있는 것도 함께 정리 대상이다. → [01_Problem.md](../01_Problem.md)의 **G3**에 대한 실물 근거.

## M2. 던전 스폰 경로에 취소 토큰이 없다

[NormalDungeonScene.cs:177](../../../Assets/Scripts/Scenes/NormalDungeon/NormalDungeonScene.cs)과 [BossDungeonScene.cs:194](../../../Assets/Scripts/Scenes/BossDungeon/BossDungeonScene.cs)는 팩토리를 **토큰 없이** 호출한다(`default`). 스포너 경로만 토큰을 전파한다.

**영향**: 로딩 중 씬을 이탈해도 로드가 끝나면 몬스터가 생성된다. 리소스 스코프 작업에서 잡아둔 "취소 토큰을 로드까지 전파" 원칙이 여기서 끊긴다.

## M3. 스폰 1프레임 내 사망 시 상태가 되살아난다

[RangedMonsterController.cs:43](../../../Assets/Scripts/Controllers/Monster/RangedMonsterController.cs)

```csharp
if (_state != MonsterState.Dead && _agent != null) { _agent.enabled = true; }
_state = MonsterState.Idle;   // ← Dead 검사 밖
```

Agent 활성화는 `Dead`를 확인하는데 **상태 대입은 무조건**이다. 1프레임 창이라 좁지만, 광역기 위에 스폰되는 경우 재현 가능하다.

## M4. 사망 통보와 사망 처리 사이에 1프레임이 있다

`Health.Die()`는 피격 프레임에 즉시 이벤트를 쏘지만, 실제 사망 처리(`HandleDeadState` — 콜라이더 끄기·애니메이션)는 **다음 프레임 BT 평가**에서 일어난다([BaseMonsterController.cs:99](../../../Assets/Scripts/Controllers/Monster/BaseMonsterController.cs) 참조).

그 1프레임 동안 `_state`는 여전히 `Attacking`일 수 있고, 애니메이션 이벤트가 걸리면 **HP가 0인 몬스터가 공격을 발사한다.** 보상은 이미 지급된 뒤다.

---

# 잠재

## M5. 상태를 가진 BT 노드는 풀 재사용에서 리셋되지 않는다

`_topNode`는 `Awake`에서 한 번만 조립되고, `OnEnable`에 노드 상태 초기화가 없다.

지금 안전한 이유는 **우연**이다 — 일반 몬스터 트리는 `Selector`·`Sequence`·`ActionNode`뿐이고 셋 다 무상태다. 상태를 가진 `WaitNode`(`_isWaiting`)·`BossSequence`(`_childIndex`)·`RandomNode`(`_currentNode`)는 보스 전용인데, **보스는 풀링을 안 한다**(Poolable 없음 → 매번 새 인스턴스).

→ 일반 몬스터에 `WaitNode` 하나만 넣는 순간 깨진다. 지금 고칠 필요는 없지만 **모르고 밟으면 원인 찾기 어려운 종류**다.

## M6. 보스의 연출 플래그가 재활성화 때 리셋되지 않는다

`_isDeadProcessed` · `_entranceFinish` · `_isActionRunning` · `_isSkillFiring`이 `OnEnable`에서 초기화되지 않는다([BossMonsterController.cs](../../../Assets/Scripts/Controllers/Monster/Boss/BossMonsterController.cs)). 보스가 `Object.Destroy` 대상이라 현재는 무증상. **보스를 풀링하거나 필드 보스로 재사용하는 순간** 입장 연출을 건너뛰고 죽은 상태로 부활한다.

## M7. HP바 최초 로드 중 디스폰되면 HP바가 영영 없다

`_initialized`는 `Start`의 `await` **뒤에** 켜진다([NormalMonsterController.cs:42-47](../../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs)).

최초 로드가 진행 중일 때 디스폰 → 풀 재사용이 일어나면, `OnEnable`의 재생성 분기는 `_initialized == false`라 통과하지 못하고, 재개된 `CreateHpBarAsync`는 취소 토큰 때문에 조기 반환한다 → **HP바 없는 몬스터.** 어드레서블이 데워지기 전 최초 스폰에만 열리는 좁은 창이다.

---

# 정리 (동작 무관)

## M8. 이벤트 소유자가 남의 구독을 지운다

`MonsterStat.SetStat`이 `OnMonsterDead = null`로 시작한다([MonsterStat.cs:47](../../../Assets/Scripts/Data/Stat/MonsterStat.cs)). 현재 구독자가 스포너 하나뿐이라 무증상이지만, 구독자가 둘이 되는 순간 조용히 하나가 사라진다. 같은 파일에서 `HealthComp.OnDied`는 `-=`/`+=` 관용구를 쓰고 있어 **한 파일 안에서 두 방식이 섞여 있다.**

## M9. `RandomNode`가 추첨마다 로그를 찍는다

[BTNode.cs](../../../Assets/Scripts/Controllers/Monster/BTNode.cs)의 `GameLog.Log($"[RandomNode] {randIdx}번 스킬이 선택되었습니다!")` — 주석에 "디버그용"이라고 적혀 있다.

## M10. 팩토리 오버로드 2개가 40줄을 복제한다

`CreateMonsterByMonsterIDAsync`와 `CreateMonsterByAddressableKeyAsync`의 차이는 **첫 줄의 조회 키 하나뿐**이고 나머지는 완전히 같다([MonsterFactory.cs](../../../Assets/Scripts/Factory/MonsterFactory.cs)). 한쪽만 고치면 갈라진다.

---

# 착수 순서 제안

Sector를 A층(거리 기반)으로 재설계하더라도 **스포너는 실행 단위로 그대로 남는다.** S1~S3을 남겨두면 새 구조의 측정치에 유령 몬스터가 섞여 들어간다.

| 순서 | 내용 | 범위 |
|---|---|---|
| 1 | 스포너에 명시적 활성 상태 도입 — `activeInHierarchy` 대용 제거 | S1 |
| 2 | CTS를 인덱스가 아니라 **인스턴스 동일성**으로 정리 | S2 |
| 3 | `StartRespawn` 점유 검사 + 생성 후 토큰 재검사 | S3 · S4 |
| 4 | 자료구조 5개 → 1개로 축소 (S1~S3의 재발 방지) | 정리 |
| 5 | 몬스터 파티 이벤트 해제 | M1 |
| 6 | `delay` 지터 + 프레임 예산 | S6 · G3 — A층 작업과 함께 |

1~4는 `MonsterSpawner.cs` 한 파일 안에서 끝난다.

> ⚠ **Before 영상과의 관계** — [README](../README.md)가 경고한 G1(경계에서 **살아있는** 몹이 사라지는 장면)은 **S1~S3을 고쳐도 그대로 촬영 가능하다.** 이 셋은 죽은 몹의 리스폰 경로와 취소 정리만 건드리고, `DespawnMonsters`가 산 몹을 즉시 회수하는 동작은 손대지 않는다. 영상 때문에 버그 수정을 미룰 필요는 없다.
