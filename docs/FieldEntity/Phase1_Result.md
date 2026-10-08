# Phase 1 결과 — 몬스터 생명주기 완성

> 2026-09-29 · 계획: [02_Plan.md](02_Plan.md) Phase 1 · 계약 원본: [MonsterSimulation/Audit/Lifecycle.md](../MonsterSimulation/Audit/Lifecycle.md)
> 상태: **플레이 확인 완료** (씬 로드 NRE 0 · Sector 왕복/사망·재스폰 시 HP바 정상 · 보스 패턴 간격 정상)

---

## 1. 한 줄 요약

**입구는 `MonsterFactory`, 출구는 `Resource.Destroy`.** 몬스터는 풀링 여부와 무관하게 이 두 지점만 지나고,
그 사이의 생애를 `IMonsterLifecycle.OnSpawn` / `OnDespawn`이 연다·닫는다.

```
입구  Spawner/Scene/Summon → MonsterFactory
        LoadAsync → Resource.Instantiate(activate:false) → SetStat → OnSpawn(ctx) → SetActive(true) → 반환
출구  사망 · Sector 이탈 → Resource.Destroy
        풀링   → Pool.Push  → OnDespawn() → SetActive(false)
        비풀링 → OnDespawn() → Object.Destroy          (보스)
씬 전환  Pool.Clear → DestroyPool이 풀 밖에 나가 있는 인스턴스(DDOL 캔버스의 HP바)까지 파괴
```

## 2. 콜백 계약 (최종)

| 콜백 | 언제 | 하는 일 |
|---|---|---|
| **Awake** | 인스턴스 평생 1회 — 풀은 `Create` 시점, 비풀링은 생성 시점 | 컴포넌트 캐싱 · BT 조립 · 보상/데미지 표시 바인딩 |
| **OnSpawn(ctx)** | 매 스폰, **꺼진 상태** | CTS · 상태 · 체력 회복 · 타겟 · 파티 구독 · BT 리셋 · HP바 연결 · 탄약 |
| **OnEnable** | 켜질 때 | 콜라이더 · 애니메이터 · agent 켜기(`IsSpawned`일 때만) — **활성화 반응만** |
| **OnDespawn** | 매 회수, **켜진 채** | HP바 해제 · 파티 구독 해제 · CTS 취소 (자식 → base 역순) |
| **Start** | — | 비움 (Normal에 빈 오버라이드 잔존, 보류) |

- `activate:false`로 받은 인스턴스는 **항상 Awake가 끝나 있다** — 그래서 OnSpawn이 BT·컴포넌트를 안전하게 만진다.
- `IsSpawned` = `_monsterCts != null`. CTS는 OnSpawn에서만 생기므로 "지금 생애 중인가"와 같다.
  풀 프리웜의 `Create`처럼 **생애 밖에서 도는 OnEnable**을 구분한다.

## 3. 바뀐 것

### 3.1 ResourceManager — 로드와 생성 분리

| 전 | 후 |
|---|---|
| `LoadAsync` 4개 (string/AssetReference × `bool isGlobal`/`ScopeType`) | 2개 — `ResourceScopeType scope = Scene` 단일형 |
| `Instantiate` 3개 + `InstantiateAsync` — 풀 경로면 비활성, 아니면 활성으로 반환(불일치) | 2개 (`parent` / `pos, rot, parent`) + `activate` — 어느 경로든 "꺼진 채 확보 → 배치 → 켬" |
| key로 받는 동기 `Instantiate` | `GetLoaded<T>(key)` (로드 쪽 조회) + `Instantiate` |
| 원본 프리팹을 `SetActive(false)`로 껐다 켬 (공유 에셋 변경) | 비풀링 + 곧 켤 것 → 꺼진 스테이징 밑에서 복제 / 꺼진 채 → 켜진 채 복제 후 즉시 끔(Awake 보장) |
| Warp로 배치 | 꺼진 상태에서 `transform` 배치 → agent는 OnEnable에서 그 위치로 시작 |

### 3.2 PoolManager

- `Create`가 항상 꺼진 인스턴스를 만든다 → 프리웜이든 부족분 증설이든 Awake 시점이 같다.
- 사용 중 인스턴스를 숫자(`_activeCount`) 대신 **`HashSet<Poolable>`** 로 추적 → `DestroyPool`이 풀 밖 인스턴스도 파괴.
- `Push`가 사용 중이던 것에 한해 `IMonsterLifecycle.OnDespawn()` 호출 (이중 Push에도 한 번).

### 3.3 몬스터

- `IMonsterLifecycle` + `readonly struct SpawnContext`(데이터 · 위치 · 회전). 소유 스포너는 Phase 3에서 추가.
- 이름을 `IPooledMonster`에서 바꿈 — **풀링 계약이 아니라 생애 계약.** 보스는 생애 1회로 같은 계약을 따른다.
- `MonsterFactory`: 거의 같던 두 공개 함수를 공통 `CreateAsync` 하나로.
- Ranged `OnEnable`의 `EnableAgentDelayAsync`(1프레임 대기) 삭제 → agent 켜기를 `NormalMonsterController.OnEnable`로.
- `WaitNode.Reset()`이 `_duration`(구조)까지 0으로 만들던 것 수정 — 상태만 리셋.

### 3.4 HP바

- 프리팹에 `Poolable`, 세 씬(Game · NormalDungeon · BossDungeon)의 `CreatePool`에서 미리 풀링.
- **수명 = 몬스터의 생애.** OnSpawn에서 `GetLoaded` + `Instantiate`로 **동기** 연결, OnDespawn·사망에서 `ReleaseHpBar()`(멱등).
- HP바의 자기 파괴(`_targetTr == null`이면 Destroy) 제거 → 타겟 없으면 그리지 않기만.
- 캐싱을 `Init`(Start) → `Awake`로 — 재사용된 바가 이전 슬라이더 값을 들고 나오던 문제 방지.

## 4. 해소된 항목

| ID | 내용 | 해소 방식 |
|---|---|---|
| L2 | OnSpawn/OnDespawn 호출부 0 | 팩토리 입구 · `Resource.Destroy` 출구 |
| W1 | Ranged OnEnable에서 `_monsterCts` NRE (프리웜 포함) | OnEnable에서 CTS 의존 제거 + `IsSpawned` 가드 |
| W2 · W3 | 사망/공격 시 CTS NRE | OnSpawn이 활성화 전에 CTS 생성 |
| W4 | 타겟 없음 → 전부 Idle | OnSpawn에서 타겟·파티 구독 |
| W5 · L5 · M7 | HP바 중복 생성 · 로드 중 디스폰 | 풀링 + 동기 연결 → 로드-생성 사이 틈이 없음 |
| W6 | `WaitNode._duration` 리셋 | 상태만 리셋. **배선 후 보스에서 실제로 드러났던 버그** |
| L0 · M1 | 파티 구독 비대칭 · 누수 | OnSpawn 구독 / OnDespawn 해제 |
| L1 · L7 · M3 | OnEnable→CTS 의존 · agent 켜기 위치 · 1프레임 내 사망 복귀 | 1프레임 대기 삭제, agent는 Normal.OnEnable |
| L4 | 탄약 중복 초기화 | OnSpawn만 |
| — | 풀 증설 경로의 Awake 시점 불일치 | `Create`가 항상 꺼진 인스턴스 |
| — | 씬 전환 시 DDOL 캔버스에 HP바 잔존 | 풀이 사용 중 인스턴스까지 소유·파괴 |

## 5. 남은 것

- **L6** — `NormalMonsterController.Start` 빈 오버라이드 (본문 없음, 보류)
- **완료 조건 중 미측정**: 파티 이벤트 구독자 수 = 활성 몬스터 수(Sector 10회 왕복 후) · 통제 시나리오 `[PoolLeak]` 0 · 일반던전 회귀
- 비풀링 몬스터의 `OnDespawn`은 같은 프레임 이중 `Destroy` 시 두 번 불릴 수 있음 — 현재 구현은 전부 멱등이라 무해
- 공개 저장소(OperationKivotos-Code) 동기화는 별도

## 6. 다음

Phase 2 — 풀 용량 · 관측. 풀 크기를 스포너 데이터 집계값으로 주입하고, `Pop`이 즉석 `Create`하면 `[PoolGrow]` 경고.
(2-3 HP바 `Poolable`은 이번에 선반영됨)
