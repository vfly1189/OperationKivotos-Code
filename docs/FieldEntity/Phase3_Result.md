# Phase 3 결과 — 스포너 재설계 · Sector 연결

> 2026-10-02 · 계획: [02_Plan.md](02_Plan.md) Phase 3 · 구조 원본: [Spawner_Structure.pdf](Spawner_Structure.pdf) · 개념 그림: [Spawner_ReadyAt_Gen.pdf](Spawner_ReadyAt_Gen.pdf)
> 커밋: `03fd714e`(1단계 Record·Slot) → `98740026`(2단계 SpawnerManager · Sector 연결 · 로딩 커버 버그) → `2cad9d49`(Phase 3 마무리)
> 상태: **플레이 확인 완료** (Sector 3→2→1 반복 왕복 · 처치 → 리스폰 · 예외 0 · `[Spawner]` 경고 0)

---

## 1. 한 줄 요약

**정책은 그대로, 실행 층은 새로.** Sector의 동작(다른 구역에 들어가면 이전 구역을 끄고 산 몹을 즉시 회수)은 그대로 두고,
그 밑의 스포너를 "포인트마다 돌던 비동기 작업"에서 **슬롯 상태 기계 + 숫자 두 개(ReadyAt · Gen)** 로 바꿨다.
이제 Sector · 거리 · 청크는 모두 `Activate(id)` / `DeActivate(id)`만 부르면 되고, 결과 차이는 정책 때문이라고 말할 수 있다.

```
정책   Sector (→ 거리 → 청크)        "언제 켤지" — 스포너 id 목록만 안다
         │ Activate(id) / DeActivate(id)
시스템 SpawnerManager (Managers.Spawner)  "켜진 걸 어떻게 굴릴지" — 틱 · 상태 전이 · 사망 연결 · 씬 정리
         │ 슬롯이 스폰 요청
데이터 SpawnerRecord × 23 / SpawnSlot × 363   상태만 보관 (MonoBehaviour 아님)
저작   MonsterSpawner                    씬 로드 시 1회 Build → Record, 이후 역할 없음
아래층 MonsterFactory → Pool → 몬스터 생애   Phase 1 결과 그대로
```

## 2. 층별 역할

| 층 | 누가 | 하는 일 | 하지 않는 일 |
|---|---|---|---|
| 정책 | `Sector` | 켤 스포너 id 결정 · `Activate` / `DeActivate` 호출 | 몬스터 · 풀 · 팩토리를 모름 |
| 시스템 | `SpawnerManager` | 틱 · 슬롯 상태 전이 · 팩토리 요청 · 사망 연결 · 씬 정리 | 누가 켜는지 모름 |
| 데이터 | `SpawnerRecord` · `SpawnSlot` | 스포너 하나 / 포인트 하나의 상태 보관 | 스스로 움직이지 않음 |
| 저작 | `MonsterSpawner` | 자식 포인트 좌표 + JSON 표 조인 → Record, 기즈모 | 스폰 · 상태 · 타이머 |
| 생성 · 회수 | `MonsterFactory` · `Resource.Destroy` → `Pool` | 실제 오브젝트 생성과 회수 | 언제 만들지 판단 |

## 3. 슬롯 상태 기계

```
Waiting ──(켜짐 ∧ now ≥ ReadyAt ∧ 예산)──▶ Spawning ──(도착 ∧ gen 일치)──▶ Alive ──(HP 0)──▶ Dying
   ▲          Gen++                         │                                       │
   │                                        └─ 도착했는데 gen 불일치 → 결과만 회수    │
   └───────────── OnDespawned(연출 끝 · 풀 반납) → ReadyAt = now + respawnTime × (1 ± 0.2) ┘

DeActivate:  Waiting = 그대로 (타이머가 계속 흐름)
             Spawning = Gen++ (날아가는 주문 무효화) · Waiting · ReadyAt = now
             Alive    = 구독 해제 → 즉시 회수 · Waiting · ReadyAt = now   ← Before(경계 소실) 재현용으로 유지
             Dying    = 그대로 (연출이 끝나면 OnDespawned가 알아서 Waiting으로)
```

슬롯의 런타임 상태는 필드 4개가 전부다: `_state` · `_readyAt` · `_gen` · `_instance`.

| 필드 | 뜻 | 바뀌는 순간 |
|---|---|---|
| `_state` | Waiting · Spawning · Alive · Dying | 틱 · 팩토리 반환 · 사망 · 회수 · DeActivate |
| `_readyAt` | 이 시각부터 스폰 가능 (**알람 시각**) | 회수 시 `now + respawnTime × 지터` · DeActivate로 되돌릴 때 `now` · 스폰 실패 시 백오프 |
| `_gen` | 지금 유효한 **주문 번호** | 스폰 요청 시 `+1` · Spawning 중 DeActivate 시 `+1` |
| `_instance` | 슬롯이 관리하는 몬스터 | gen이 맞는 결과가 도착하면 설정 · Detach 시 비움 |

### ReadyAt — 왜 카운트다운이 아니라 시각인가
옛 방식은 포인트마다 `UniTask.Delay(10초)`를 돌렸다. 타이머가 "돌고 있는 작업" 안에 숨어 있어서
끄면 취소해야 하고(놓치면 S1 꺼진 구역 부활 · S2 CTS 폐기 문제), 켜면 처음부터 다시 셌다.
ReadyAt은 시각 하나만 적어 두고 틱이 `now ≥ ReadyAt`만 비교한다. 취소할 것이 없고, 꺼진 동안에도 시간이 흐른다.

### Gen — 왜 CTS 취소가 아니라 번호 비교인가
팩토리는 비동기라 요청과 도착 사이에 스포너가 꺼졌다 켜질 수 있다. Gen이 없으면 늦게 도착한 결과가
슬롯을 덮어써 **한 포인트 2마리(S3)** 가 되거나 **꺼진 스포너에 몬스터가 생긴다(S1)**.
로드 · Instantiate가 진행된 뒤에는 취소가 깔끔히 먹지 않으므로, 결과는 어차피 올 수 있다고 인정하고
**도착한 순간 한 곳에서** `gen != slot._gen`이면 회수한다.

## 4. 바뀐 것

### 4.1 신규 · 대폭 변경

| 파일 | 내용 |
|---|---|
| [SpawnerManager.cs](../../Assets/Scripts/Managers/Core/Field/SpawnerManager.cs) | 신규. `Init`(등록) · `Run`(가동) · `Activate` · `DeActivate` · `OnUpdate`(틱) · `SpawnAsync` · `HandleDead` · `HandleDespawned` · `Detach` · `RecoverLost` · `Clear` |
| [SpawnSlot.cs](../../Assets/Scripts/Managers/Core/Field/SpawnSlot.cs) | `_readyAt` · `_gen` 추가 (가변 필드는 readonly 아님) |
| [SpawnerRecord.cs](../../Assets/Scripts/Managers/Core/Field/SpawnerRecord.cs) | `_id` → `_spawnerId`, `using NUnit.Framework` 제거 |
| [MonsterSpawner.cs](../../Assets/Scripts/Managers/Core/Field/MonsterSpawner.cs) | `Build`에 데이터 방어(범위 밖 · 중복 · 미사용 경고), `using NPOI` 제거 |
| [Sector.cs](../../Assets/Scripts/Managers/Core/SectorManager/Sector.cs) | `List<MonsterSpawner>` → `int[] _spawnerIds`, `Managers.Spawner`로 위임 |

### 4.2 연결

| 파일 | 내용 |
|---|---|
| [Managers.cs](../../Assets/Scripts/Managers/Managers.cs) | `Managers.Spawner` 등록, `Update`에서 `_spawner.OnUpdate()` |
| [GameScene.cs](../../Assets/Scripts/Scenes/GameScene/GameScene.cs) | `CreateMainVillage`에서 `Spawner.Init` · `CreateCharacters` 뒤 `Spawner.Run()` · `Clear`에서 `Spawner.Clear()` · 맵 null 가드 |
| [BaseMonsterController.cs](../../Assets/Scripts/Controllers/Monster/BaseMonsterController.cs) | `OnDespawned`를 `Action` → `Action<BaseMonsterController>` (핸들러 하나로 "누가 끝났는지" 앎) |

### 4.3 팩토리 시그니처

[MonsterFactory.cs](../../Assets/Scripts/Factory/MonsterFactory.cs) — `Transform spawnPoint` → `Vector3 position, Quaternion rotation`.
팩토리가 쓰던 건 위치 · 회전뿐이었고, 슬롯은 순수 C#이라 Transform을 들 수 없다.
덤으로 `await LoadAsync` **뒤에서** 포인트 Transform을 읽던 위험(로드 중 파괴 시 `MissingReferenceException`)이 사라짐.
호출부 3곳 갱신: `SummonMonsters` · `NormalDungeonScene` · `BossDungeonScene`.

## 5. 문제와 해결 — 작업 중 만난 것

설계 리뷰 단계에서 잡은 것과 플레이에서 드러난 것을 구분한다.

### 5.1 설계 · 코드 리뷰에서 잡은 것 (실행 전)

| # | 문제 | 왜 문제인가 | 해결 |
|---|---|---|---|
| R1 | `Activate`에서 슬롯마다 바로 `SpawnAsync` | Alive 슬롯 중복 스폰 · ReadyAt 무시 · 예산 무시 · State 미전이 | `Activate`는 `IsActive`만 켜고, 스폰 요청은 틱이 한다 |
| R2 | `SpawnAsync(slot, slot._gen++)` | 후위 증가라 **옛 번호**를 들고 감 → 도착 시 항상 불일치 → 모든 스폰이 회수됨 | `slot._gen++` 후 `slot._gen`을 넘김 |
| R3 | 사망 연결 딕셔너리에 `Add`만 있고 `Remove` 없음 | 풀이 같은 인스턴스를 다른 슬롯에 재사용하는 순간 **중복 키 예외** → 그 슬롯은 사망 연결 없이 굳음 | `Detach(slot)` — 구독 해제 · 딕셔너리 제거 · `_instance` 비움. HandleDespawned와 DeActivate(Alive) 두 곳에서 호출, 회수보다 먼저 |
| R4 | `Clear`가 CTS만 정리, 아무 데서도 안 불림 | 씬을 나가도 레코드가 `IsActive`로 남아 다른 씬에서 스폰 시도 → `_sceneCTS` null로 NRE | `_records` · 딕셔너리까지 비우고 null 가드, `GameScene.Clear`에서 호출 |
| R5 | 팩토리 null 반환 시 슬롯을 Waiting으로만 되돌림 | ReadyAt이 이미 지난 시각이라 **매 프레임 재요청** · 에러 로그 폭주 | 실패 시 ReadyAt을 리스폰 간격만큼 미룸(백오프) |
| R6 | SpawnerManager.cs가 CP949로 저장됨 | git · GitHub에서 한글 주석이 깨짐 | UTF-8로 재저장 |

### 5.2 플레이에서 드러난 것

#### P1. 파티가 준비되기 전에 스폰 → `ArgumentOutOfRangeException`

```
PartyRegistry.GetCurrent ← PartyManager.GetCurrentCharacter ← NormalMonsterController.OnSpawn ← MonsterFactory ← SpawnerManager.SpawnAsync
```

- **원인**: `GameScene.CreateCharacters`가 캐릭터를 하나씩 `await`로 로드하며 스폰 포인트에 바로 생성한다.
  첫 캐릭터가 Sector 트리거에 닿아 `Activate` → 다음 프레임 틱이 즉시 스폰 → 몬스터 `OnSpawn`이 파티를 읽는데,
  `Party.Init`은 **전부 로드된 뒤**에야 불려 목록이 비어 있었다.
  옛 스포너는 첫 스폰 전에 `delay` 5초를 기다려서 이 순서 문제가 가려져 있었다.
- **해결**: 등록과 가동을 나눔. `Init`은 레코드만 받고, `Run()`(= `_isRunning`)은 `CreateCharacters` 뒤에 GameScene이 부른다.
  가동 전에 온 `Activate`는 `IsActive`만 켜 두므로 신호를 잃지 않고, 가동 순간 시작 섹터가 채워진다.
- **기각한 대안**: `GetCurrent()`가 빈 목록에서 null 반환 — 예외는 사라지지만 `Party.Init`이 `OnActiveCharacterChanged`를 발생시키지 않아
  그 사이 나온 몬스터는 타깃 없이 서 있게 된다. 증상만 숨김.

#### P2. 몬스터가 실행마다 나왔다 안 나왔다 함 — 로딩 커버가 `BaseScene`이었다

- **증상**: 같은 코드로 어떤 실행은 정상, 어떤 실행은 몬스터가 보이지 않음. 캐릭터를 바꿔도 안 나옴. 예외 0.
- **추적**: 임시 진단 로그(`[SpawnDiag]`)로 단계별 확인.
  - 실패한 실행도 요청 → 도착(`gen 1/1`, null 아님)까지는 성공 실행과 **로그가 완전히 같았다**
  - 3초 뒤 확인 → 6마리 전부 **"인스턴스 파괴됨, 슬롯 Alive"** — 그런데 `Dead` · `Despawned` · `DeActivate` 로그는 없음
  - 풀 회수(`Push`)는 끄기만 하고 파괴하지 않으므로, 누군가 `Object.Destroy`를 직접 불렀다는 뜻
- **원인**:
  1. `Pool.Pop`은 부모를 안 주면 `Managers.SceneEx.CurrentScene.transform` 밑에 붙인다
  2. `CurrentScene`은 `FindAnyObjectByType<BaseScene>()` — 씬에 있는 BaseScene **아무거나 하나**
  3. GameScene은 초기화 중 로딩 커버를 띄우는데, 커버의 `LoadingSceneController`가 **`BaseScene`을 상속**하고 있었다
  4. 커버가 집히면 몬스터가 커버 밑에 붙고, `FadeInSequence`가 커버를 `Destroy`할 때 같이 파괴됨
  - 어느 쪽이 집힐지 정해져 있지 않아 **비결정적**. 옛 스포너는 5초 지연 덕에 커버가 사라진 뒤 스폰해서 안 드러났다
- **해결**: `LoadingSceneController`를 `MonoBehaviour`로. 로딩 UI일 뿐 씬이 아니고, BaseScene 기능을 하나도 쓰지 않았다.
  스크립트 GUID 유지 · BaseScene 직렬화 필드 없음 → 프리팹(`LoadingCanvas` · `LoadingCover` · `LoadingCover_Zero`) · `Loading.unity` 영향 없음.
- **같이 해결된 것**: `SceneManagerEx.LoadScene`의 `CurrentScene?.Clear()`가 커버의 `Clear`를 대신 불러
  씬 정리(`Spawner.Clear` · 입력 구독 해제)가 빠질 수 있던 문제. SelectScene · NormalDungeon · BossDungeon도 같은 커버를 쓰므로 같은 위험이 사라짐.
- **교훈**: 기존 버그가 "타이밍이 바뀌면서" 드러났다. 진단 로그를 **단계별(요청 → 도착 → N초 뒤 생존)** 로 찍어야 "만들어지지 않았다"와 "만들어진 뒤 사라졌다"를 가를 수 있었다.

### 5.3 Phase 3 마무리에서 넣은 방어 (`2cad9d49`)

| 항목 | 내용 |
|---|---|
| S5 데이터 방어 | `Build`에서 `pointIndex` 범위 밖 · 중복 지정은 경고 후 건너뜀 (예전엔 `GetChild` 예외로 **로딩 전체 중단**), 미사용 자식 포인트 수 경고 |
| S6 리스폰 지터 | `respawnTime × (1 ± _respawnJitter)`, 기본 0.2. 난수는 **맵 ID 시드** → 같은 경로 재주행 시 같은 값(측정 재현성) |
| 굳은 슬롯 복구 | 회수 경로 밖에서 파괴된 인스턴스(Alive/Dying)를 틱에서 탐지 → 딕셔너리 연결만 끊고 Waiting으로. 파괴된 몹은 `Stat` 접근 불가라 구독 해제는 생략(이벤트는 오브젝트와 함께 사라짐). DeActivate(Alive)도 같은 가드 |
| 맵 null 가드 | `CreateMainVillage`의 null 검사가 `if` 밖이던 것 — 에러 로그 후 반환 |

## 6. 확정한 결정

| 결정 | 내용 | 이유 |
|---|---|---|
| 이름 | `SpawnerSystem`(문서) 대신 **`SpawnerManager`** (`Managers.Spawner`) | 기존 매니저 관례 |
| 첫 스폰 대기 | **첫 활성화는 즉시** (`_readyAt` 초기값 0) | 섹터 진입 시 바로 채워짐. 파티 준비 문제는 `Run` 게이트로 분리 |
| JSON `delay` | **리스폰 간격**으로 사용 | 옛 스포너도 리스폰 대기에 같은 값을 썼음 → 리스폰 간격은 예전과 동일 |
| 리스폰 기준 시점 | 사망 순간이 아니라 **`OnDespawned`(연출 끝 · 풀 반납 직전)** | 시체가 남은 동안 같은 자리에 새 몹이 겹치지 않게. 실제 간격 = 연출 2초 + respawnTime |
| DeActivate의 Alive | **즉시 회수 유지** | Before 영상(경계 소실)을 찍으려면 지금 Sector 동작이 남아 있어야 함. 전투 유예는 Phase 5 공통 옵션 |
| 위치 전달 | 값(`Vector3` · `Quaternion`) | Record는 씬 오브젝트를 참조하지 않는다 |
| Phase 2(풀 용량) | **진행하지 않음** (유저 결정, 2026-10-02) | 아래 7절 주의 참고 |

## 7. 검증

### 7.1 플레이로 확인한 것 (Editor.log 기준)

| 항목 | 결과 |
|---|---|
| 섹터 진입 시 스폰 | Sector3 진입 → 스포너 1000 · 1001(각 3슬롯) 활성 → `Run` 시점 레코드 23 중 2 활성 → 6마리 도착 |
| 섹터 이동 시 회수 | 3→2→1→2→1→2(→1) 이동마다 이전 섹터 Alive 즉시 회수 → 풀 반납 → 다른 슬롯에서 재사용 |
| 처치 → 리스폰 | `Dead` 5 = `Despawned` 5 짝 일치, 해당 슬롯 `gen 2` · `gen 3`으로 재스폰 (2단계 실행) · 처치 6회 (마무리 실행) |
| 수정 후 생존 | 스폰 3초 뒤 전부 `active True` · 부모 = 게임 씬 쪽 · 파괴 0 |
| 오류 | `Exception` · `PoolLeak` · NRE · `[Spawner]` 경고 0 |

### 7.2 아직 검증하지 않은 것

- **Gen 불일치 경로** — 로그상 gen은 전부 일치했다. "스폰 진행 중 섹터 끄기"가 재현되지 않아 S3 · S4를 막는 핵심 경로가 실제로 돈 증거가 없다
- **리스폰 지터 실측** — 진단 로그 제거 후라 리스폰 시각 분산이 로그에 없음
- **방어 경로** — 굳은 슬롯 복구 · 스폰 실패 백오프는 정상 상황에선 실행되지 않음
- **불변식 I1~I5** — 개발 빌드 검사 미구현
- **던전 회귀** — 팩토리 시그니처 변경으로 일반 던전 · 보스 던전 · 소환 어빌리티 호출부를 고쳤으나 플레이 미확인
- Phase 1 완료 조건 중 미측정분(파티 구독자 수 · 통제 시나리오 `[PoolLeak]`)은 여전히 남음

## 8. 남은 위험 · 알려진 한계

| 항목 | 내용 | 언제 |
|---|---|---|
| 프레임 예산 무제한 | `_spawnBudgetPerFrame = int.MaxValue` → 섹터 첫 진입 시 슬롯 전부가 한 프레임에 요청됨 (Sector2 ≈ 100+) | Phase 4 측정 지표와 함께 |
| 풀 30 고정 | RL이 많은 섹터 첫 진입 시 풀 즉석 증설. **정책 비교 시 스폰 비용에 섞일 수 있음** → 측정 시 워밍업 구간을 두거나 감안 | Phase 2 생략 — 측정 때 감안 |
| V1 `_lastDestPosition` | `NormalMonsterController`가 `OnSpawn`에서 리셋하지 않음 → 재사용 몹이 이전 생애의 목적지를 기억 | 필요 시 |
| `Radius` 없음 | 거리 정책이 읽을 `SpawnerRecord.Radius`가 아직 없음 (`Center`만) | Phase 5 직전 |
| 맵 실패 시 진행 불가 | 맵 null 가드는 메시지만 명확해졌을 뿐, 이어지는 `CreateCharacters`도 맵을 씀 | 범위 밖 |
| 공개 저장소 | OperationKivotos-Code 미동기화 | 별도 |

## 9. 다음

1. **던전 회귀 확인** (10분) — 일반 · 보스 던전 · 소환 어빌리티에서 몬스터가 나오는지
2. **Phase 4** — `IActivationPolicy`(Sector · 거리 · 청크를 같은 틀로, 런타임 전환) → 고정 경로 하네스 → Sector 기준선 3회 → Before 영상
3. Phase 5 거리(B) · Phase 6 Sector vs B 측정 · Phase 7 청크(C) 스케일 테스트
