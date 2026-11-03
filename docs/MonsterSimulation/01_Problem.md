# 01. 현재 구조와 문제 진단

## 1. 현재 구현 (②단계)

실물 3파일 360줄 + 벤치 토글.

| 파일 | 역할 |
|---|---|
| [Sector.cs](../../Assets/Scripts/Managers/Core/SectorManager/Sector.cs) | 트리거 볼륨. 플레이어 진입 감지 → 매니저에 통보 |
| [SectorManager.cs](../../Assets/Scripts/Managers/Core/SectorManager/SectorManager.cs) | 섹터 등록, "현재 활성 섹터" 1개를 추적하며 하드 스왑 |
| [MonsterSpawner.cs](../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs) | 스폰 포인트별 UniTask 스폰·리스폰, 취소 토큰 관리 |
| [BenchmarkTester.cs](../../Assets/Scripts/Debug/BenchmarkTester.cs) | F1 전체 활성 / F2 전체 비활성 (현 포트폴리오 수치 생성기) |

**콘텐츠 규모**: `Assets/Resources_moved/Prefabs/Map/BackStreetMap.prefab` 에 **Sector 5개 / MonsterSpawner 23개**.

**동작**: 플레이어가 섹터 트리거에 진입 → 이전 섹터의 스포너 전부 `DespawnMonsters()` → 새 섹터 전부 `SpawnMonsters()`.

---

## 2. 문제 — 세 갈래

### G1. 경계에서 전투 중인 몬스터가 사라진다 ★가장 큰 증상★

A구역에서 몬스터와 싸우다가 B구역으로 넘어가면, **A의 스포너가 통째로 꺼지면서 때리던 몬스터가 눈앞에서 없어진다.**

근거: [SectorManager.cs](../../Assets/Scripts/Managers/Core/SectorManager/SectorManager.cs) `OnPlayerEnterSector()` — 이전 섹터를 즉시 `DeactivateSector()`.
그리고 [Sector.cs](../../Assets/Scripts/Managers/Core/SectorManager/Sector.cs) 의 `OnTriggerExit`은 **빈 몸통**이다(주석만 있음).

### G2. 판정 단위가 화면보다 훨씬 크다

카메라 실측: [CameraController.cs](../../Assets/Scripts/Controllers/CameraController.cs) 의 `_delta = (0, 4, -5)` — 플레이어 뒤 5m·위 4m에서 내려다보는 타이트한 쿼터뷰.
**화면에 들어오는 지면은 대략 20~30m.**

반면 섹터는 5개가 맵 전체를 나눠 가지므로 한 변이 100m 단위다.
→ **지금도 화면 밖 몬스터를 대량으로 켜고 있다.** (그래서 "인접 섹터도 켜기"는 낭비를 2~3배로 만드는 틀린 방향)

### G3. 진입 프레임에 스폰이 몰린다

[MonsterSpawner.cs](../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs) `SpawnMonsters()` — `data.spawnList`를 순회하며 **모든 스폰 포인트의 UniTask를 즉시 `Forget()`** 한다. 프레임 예산 개념이 없다.

### G4. 섹터를 나가도 메모리는 안 준다

- 디스폰 = `Managers.Resource.Destroy` → `Pool.Push` → **인스턴스는 풀에 남음**
- 풀은 [PoolManager.cs](../../Assets/Scripts/Managers/Core/PoolManager.cs) `Pool.Init`에서 `AcquirePoolRef`로 **프리팹 핸들 refCount 티켓을 잡고 있음**
- 로드 경로는 [MonsterFactory.cs](../../Assets/Scripts/Factory/MonsterFactory.cs) → `Managers.Resource.InstantiateAsync(..., isGlobal:false)` → **Scene 스코프 고정**

→ **"공간 분할을 했다"고 말해온 것의 실체는 CPU 절감뿐이고, 메모리는 1바이트도 줄지 않았다.**
이 사실 자체가 문제해결 슬라이드의 좋은 도입부가 된다.

### 부수 미결
- 스포너 자동등록 TODO ([Sector.cs](../../Assets/Scripts/Managers/Core/SectorManager/Sector.cs) `InitSpawners` 주석)

---

## 3. 포트폴리오상 이 파트가 약해 보이는 이유

세 가지이고, **구현 깊이는 그중 하나일 뿐**이다.

### 3.1 같은 덱 안에서 주제가 중복된다 ★가장 큼★

이터널리턴(DX11) 파트가 이미 **QuadTree(충돌 135배·피킹 4.6배)** 와 **Spatial Grid(21.5배)** 로 *공간 분할*을 더 깊게, 더 큰 수치로 커버한다.
그 상태에서 Unity 파트 첫 문제가 또 "공간 분할(▼57%)"이면 **엔진 기능으로 다시 한 하위호환**으로 읽힌다.

→ **같은 축으로 더 파도 개선폭이 반감된다. 축을 바꿔야 한다.**
공간 분할("무엇을 덜 계산하나") → **시뮬레이션 관리("얼마나 자주·몇 개를 돌리나")**.

### 3.2 Before 비교군이 인위적이다

현 수치의 Before는 `BenchmarkTester` **F1(전체 섹터 켜기)** 이다. 이는 **아무도 출시하지 않을 구성**이라, "그 Before가 실제 빌드 구성이었나요?"라는 질문 한 번에 무게가 빠진다.
ResourceSystem 파트에서 변수 통제·워밍업까지 잡아둔 측정 규율과 대비되어 더 도드라진다.

→ 새 Before/After는 **"내 v1 커밋 vs 내 v2 커밋"** 으로 잡는다.

### 3.3 구현이 실제로 얕다

G1~G4가 그 내용. 5개짜리 맵이라 안 터진 것이지 해결한 것이 아니다.

> **이 프레이밍이 오히려 더 좋다.**
> "공간 분할을 했습니다"보다 **"이 구조를 대규모 맵 규모로 밀면 어디서 먼저 깨지는지 확인하고 그걸 막았습니다"** 가 훨씬 클라이언트 프로그래머다운 서사다.

---

## 4. 현재 코드에서 이미 잘 되어 있는 것 (서사에 쓸 재료)

새로 배우는 게 아니라 **이미 쓰던 패턴을 시스템 전체로 일반화**하는 그림이 된다.

| 패턴 | 위치 |
|---|---|
| **증분 갱신** — 목표가 0.5m 이상 움직였을 때만 `SetDestination` | [NormalMonsterController.cs:194](../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:194) |
| **푸시(이벤트) 기반 타겟 갱신** — 매 프레임 탐색 대신 `OnPlayerCharacterChanged` | [NormalMonsterController.cs](../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs) `UpdateTarget` |
| **대표값 시뮬레이션** — 몬스터 없이 스포너가 리스폰 타이머만 소유 | [MonsterSpawner.cs](../../Assets/Scripts/Managers/Core/SectorManager/MonsterSpawner.cs) |
| **취소 토큰 수명 관리** — 스폰 포인트별 CTS | 동일 |

## 5. 반대로 손봐야 할 지점

| 지점 | 문제 |
|---|---|
| [BaseMonsterController.cs:99](../../Assets/Scripts/Controllers/Monster/BaseMonsterController.cs:99) | `Update()`에서 몬스터마다 `_topNode.Evaluate()` — **제어점 없음** |
| [NormalMonsterController.cs:179](../../Assets/Scripts/Controllers/Monster/NormalMonsterController.cs:179) | `Vector3.Distance` (제곱근). 비교 목적이면 `sqrMagnitude`면 충분 |
| [AreaStrike.cs:54](../../Assets/Scripts/Data/Ability/Effects/Damage/AreaStrike.cs:54) | `Physics.OverlapSphere` — 호출마다 배열 할당. `NonAlloc` + 재사용 버퍼로 |
