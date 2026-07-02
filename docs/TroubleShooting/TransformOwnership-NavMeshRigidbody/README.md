# 트러블슈팅: 캐릭터를 순간이동시켜도 안 움직이던 문제 — NavMeshAgent·Rigidbody의 transform 소유권

> **한 줄 요약** — 전투 승리 연출에서 캐릭터를 엔딩 위치로 `transform.position`으로 옮겼는데 **꿈쩍도 하지 않았다.** 원인은 **활성화된 NavMeshAgent가 매 프레임 위치를 소유**하고 **비-kinematic Rigidbody를 물리가 덮어써** 내가 쓴 좌표가 씹혔기 때문. 배치 전 **소유권을 뺏고(끄기/Warp/kinematic) → 연출 후 반납**하는 대칭 규칙으로 정착시켰다.

| 항목 | 내용 |
|---|---|
| **문제** | 승리 연출 시 캐릭터가 지정한 엔딩 위치로 이동하지 않음 (좌표를 써도 무시/튕김) |
| **원인** | 활성 `NavMeshAgent`가 transform 위치를 소유 → 직접 쓴 좌표가 씹힘. 비-kinematic `Rigidbody`는 물리가 위치를 덮어씀 |
| **해결** | 배치 직전 **소유권 회수**(`agent.enabled=false` + `rb.isKinematic=true`) 후 좌표 지정. 스폰처럼 즉시 이동이 필요한 경우는 `agent.Warp()` 사용. 연출 종료 시 `isKinematic=false`로 **반납** |
| **정착** | "정적 배치 vs 즉시 활동" 두 기법을 상황별로 분리하고, **뺏기↔반납 대칭**을 스탯 리셋/마을 복귀 경로에 심어 재발 방지 |

---

## 1. 증상

보스/일반 던전을 클리어하면 승리 연출이 재생된다. 파티원들이 **미리 지정된 엔딩 위치로 정렬**해 카메라를 바라보고 승리 모션을 취해야 한다. 코드는 단순했다.

```csharp
character.transform.position = endingPositions[i].position;
character.Victory();
```

그런데 **캐릭터가 전투가 끝난 그 자리에 그대로 서서** 승리 모션만 취했다. 좌표를 분명히 대입했는데 위치가 반영되지 않았다. 로그로 `transform.position`을 찍어보면 대입 직후엔 엔딩 좌표인데, **다음 프레임에 원래 자리로 돌아와** 있었다.

---

## 2. 원인 — transform을 나만 쓰는 게 아니었다

Unity에서 transform은 "먼저 쓰는 사람"이 아니라 **매 프레임 마지막에 쓰는 컴포넌트**가 이긴다. 캐릭터에는 두 개의 다른 주인이 붙어 있었다.

**① NavMeshAgent** — 활성 상태에서는 에이전트가 내부 목적지 기반으로 **매 프레임 transform.position을 자기 값으로 덮어쓴다.** 내가 `transform.position`에 순간이동 좌표를 넣어도, 같은 프레임 에이전트 업데이트가 이를 무시하고 NavMesh상의 원래 위치로 되돌린다. (씹힘)

**② Rigidbody** — 비-kinematic 상태에서는 물리 시뮬레이션이 위치를 관장한다. 순간이동으로 다른 콜라이더와 겹치면 **물리가 밀어내(push-out)** 좌표가 튕겨나간다.

> 핵심: **"위치가 반영 안 됨"은 내 코드의 버그가 아니라, transform의 소유권을 다른 컴포넌트가 쥐고 있었기 때문**이다. 해결은 코드를 고치는 게 아니라 **소유권을 회수**하는 것이다.

---

## 3. 해결 — 소유권을 뺏고, 끝나면 반납한다

배치 직전에 두 주인을 모두 오프라인으로 돌린 뒤 좌표를 지정했다.

```csharp
// 승리 연출 배치 (DungeonSequenceDirector)
var agent = character.GetComponent<NavMeshAgent>();
if (agent != null) agent.enabled = false;   // ① NavMesh 위치 소유권 회수

var rb = character.GetComponent<Rigidbody>();
if (rb != null) rb.isKinematic = true;       // ② 물리 밀림 차단

character.transform.position = endingPositions[i].position; // ③ 이제 좌표가 먹는다

// 카메라를 바라보도록 회전
Vector3 targetPos = camObj.transform.position;
targetPos.y = character.transform.position.y;
Vector3 dir = targetPos - character.transform.position;
if (dir != Vector3.zero)
    character.transform.rotation = Quaternion.LookRotation(dir);

character.Victory();
```

### ⚠️ 대칭이 없으면 다음 전투가 깨진다

여기서 멈추면 **캐릭터가 영원히 kinematic**으로 남는다. 다음 전투/마을 복귀 시 물리·이동이 죽어버린다. 그래서 **회수한 소유권을 반드시 반납**하도록 스탯 리셋 경로에 대칭 코드를 심었다.

```csharp
// CharacterStat.Reset() / ReturnToTownState() — 소유권 반납
Rigidbody rigid = GetComponent<Rigidbody>();
if (rigid != null) rigid.isKinematic = false;
```

> **설계 규칙으로 승격**: "연출을 위해 뺏은 것은 연출이 끝나면 반드시 되돌린다." 뺏기(`enabled=false` / `isKinematic=true`)와 반납(`isKinematic=false`)을 **짝**으로 관리한다. HP바 누수 케이스에서 얻은 *"생성/파괴를 대칭으로 짝지어라"* 는 교훈과 같은 결의 원칙이다.

---

## 4. 같은 원인, 다른 상황 — 스폰에는 왜 Warp를 썼나

승리 연출은 "**정지한 채 그 자리에 고정**"이라 에이전트를 완전히 꺼도 된다. 하지만 **풀에서 재사용되는 몬스터 스폰**은 다르다. 스폰 직후 **곧바로 NavMesh 위를 걸어야** 하므로 에이전트를 꺼버리면 안 된다. 이때는 `agent.Warp()`가 정답이다.

```csharp
// ResourceManager.Instantiate — 스폰 위치 세팅
var agent = go.GetComponent<NavMeshAgent>();
if (agent != null)
{
    agent.Warp(position);          // 에이전트를 살린 채 NavMesh 위로 재샘플링해 이동
    go.transform.rotation = rotation; // ⚠️ Warp는 회전을 처리하지 않으므로 회전은 별도 지정
}
else
{
    go.transform.position = position; // 에이전트 없는 오브젝트는 그냥 transform
    go.transform.rotation = rotation;
}
```

**같은 뿌리(에이전트가 위치를 소유)에서 두 갈래의 기법**이 나온다.

| 기법 | 언제 | 이유 |
|---|---|---|
| `agent.enabled = false` (+ `isKinematic = true`) | **정적 배치** — 승리 포즈, 엔딩 고정 | 이동/네비가 더 필요 없음. 에이전트 오프라인 + 물리 밀림 차단 |
| `agent.Warp(pos)` | **즉시 활동** — 풀 재사용 몬스터 스폰 | 에이전트를 살린 채 NavMesh로 정확히 이동. 단 **회전은 별도** |

풀 매니저·몬스터 초기화 쪽에도 같은 규칙이 배어 있다. `PoolManager`는 오브젝트를 꺼낼 때(Pop) **위치를 잡기 전** `agent.enabled=false`로 먼저 끄고, `MonsterController`도 `Awake`에서 에이전트를 끈 상태로 NavMesh 배치를 준비한다 — **"위치 확정 전엔 에이전트를 켜지 않는다"** 는 순서 규칙.

---

## 5. 진화 과정 (커밋 서사)

이 규칙은 한 번에 나온 게 아니라, 같은 함정을 여러 지점에서 밟으며 **점진적으로 정착**했다.

| 시점 | 커밋 | 무슨 일이 있었나 |
|---|---|---|
| 2026-02-04 | `3c5008fd` 전투 성공/실패 구현 | **최초 발견·대응.** 승리 포즈 코루틴에서 캐릭터가 안 움직이던 문제를 만나 `agent.enabled=false` + `isKinematic=true` 패턴을 처음 도입. 당시 주석: *"NavMeshAgent가 살아있으면 위치 이동을 씹거나 튕겨날 수 있으므로 방지"* |
| (초기) | `a0cea2f5` 리소스매니저 책임 분리 | 스폰 경로에서 같은 원인을 만나 **다른 기법**으로 대응 — Agent 있으면 `Warp`, 없으면 transform. *"위치는 무조건 Warp로 이동해야 씹히지 않음"* |
| 2026-03-04 | `274250a4` 코루틴→UniTask | 승리 연출을 코루틴에서 `UniTaskVoid`로 전환 (연출 파괴 시 취소 토큰으로 안전 종료 결합) |
| 2026-04-07 | `e0bcf59a` 클리어 로직 정리 | 승리 연출을 `DungeonSequenceDirector`로 **분리·이관**하며 소유권 회수 코드를 재배치. 이때 **반납(`isKinematic=false`)을 스탯 리셋에 추가**해 대칭 완성. 보스 소환 스킬에 남아있던 죽은 Warp 코드도 정리 |
| 2026-05-06 | `f4345de0` 보스 클리어 버그 수정 | 소유권은 잡혔지만, 그 연출을 **감싸는** 시퀀싱 버그를 마저 수정 — 컷신 중 월드 캔버스 토글(`SetActiveWorldCanvas`)과 던전 씬에서 BGM 프리로드가 `UniTask.WhenAll` 목록에서 누락돼 소리가 빠지던 레이스 |

> 요약하면: **정적 배치(끄기)와 즉시 활동(Warp)이라는 두 기법을 각 상황에 맞게 갈라 쓰고**, 뺏은 것을 스탯 리셋에서 반납하는 대칭을 심어, 흩어져 있던 대응을 하나의 규칙으로 수렴시켰다.

---

## 6. 회고

- **"내 코드가 무시당한다"의 진짜 원인은 소유권.** Unity에서 transform은 공유 자원이고, NavMeshAgent·Rigidbody 같은 컴포넌트가 매 프레임 이를 덮어쓴다. 순간이동·강제 배치가 먹지 않으면 **좌표 대입 코드가 아니라 "지금 이 transform의 주인이 누구인가"를 먼저 의심**해야 한다.
- **뺏었으면 반납한다.** 연출을 위해 물리/네비를 끈 오브젝트는 반드시 되돌리는 짝을 갖춰야 한다. 대칭이 빠지면 "이번 연출"은 되지만 "다음 전투"가 조용히 깨진다.
- **같은 원인이라도 상황이 다르면 기법이 다르다.** 고정 배치는 `enabled=false`, 즉시 활동은 `Warp`. 하나의 해법을 모든 곳에 복붙하지 않고, "이 오브젝트가 배치 직후 움직여야 하는가"로 갈라 쓴 것이 핵심 판단이었다.
- 이 문제는 **HP바 생명주기 케이스와 같은 결**이다 — *생성/뺏기와 파괴/반납을 대칭으로 관리하라.* 흩어진 특수 케이스를 **하나의 원칙**으로 묶는 것이 재발 방지의 본질.

---

### 관련 코드

| 위치 | 역할 |
|---|---|
| `Gameplay/Director/DungeonSequenceDirector.cs` | 승리 연출 — 소유권 회수 후 엔딩 배치 |
| `Managers/Core/ResourceManager.cs` (`Instantiate`) | 스폰 — `Warp`로 즉시 배치 |
| `Managers/Core/PoolManager.cs` (`Create`/`Pop`) | 풀 재사용 — 위치 확정 전 에이전트 오프 |
| `Controllers/Monster/MonsterController.cs` (`Awake`) | 몬스터 초기화 — 에이전트 오프 후 NavMesh 준비 |
| `Data/Stat/CharacterStat.cs` (`Reset`/`ReturnToTownState`) | 소유권 반납 — `isKinematic=false` |

### 관련 문서
- [씬 반복 전환 시 UI 메모리 누수 추적](../MemoryLeak-MonsterHPBar) — "생성/파괴 대칭" 원칙의 자매 사례
- [동기 일괄 로딩 → Addressable 비동기 리소스 파이프라인](../../AsyncResourcePipeline)
