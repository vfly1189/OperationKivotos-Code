# 08. SkillSystem 전체 구조 — 보스 · 일반몹 공용 (2026-11-04)

> 커밋: `8c071058` 공용화 · `fb55dfaf` RL 이관(**.cs 0**) · `13b82158` Tank 이관 — 플레이 확인 (보스 · AR · RL · Tank)
> 시각화판: [08_SkillSystem_Overview.pdf](08_SkillSystem_Overview.pdf) (HTML 원본 같은 이름)

## 0. 한 줄

**같은 SkillSystem을 보스는 HP선 · 가중치로, 일반몹은 탄 수로 부른다 — SS는 누가 부르는지 모른다.**
GAS를 모티브로 "언제 / 써도 되나 / 순서 / 신호 / 무엇을 / 어디에 / 결과"를 층으로 나눴고,
보스 하나(Hieronymus)와 일반몹 세 종류(AR · RL · Tank)가 같은 코드 위에서 에셋만 다르게 돈다.

| 주장 | 증거 |
|---|---|
| 층 분리 | 아래 1절 — 층마다 아는 것이 하나 |
| 공용 | 호출자 둘: `BossMonsterPatternSelector`(HP선 · 가중치) · `AmmoPatternSelector`(탄 수) |
| 재사용 | AR 다음 **RL은 프리팹 · 클립만** 바꿔 이관 (`fb55dfaf`, .cs 0개) |
| 확장 방식 | Tank의 새 행동(경고 → 지연 타격)은 새 Event가 아니라 **StrikeEvent 옵션** 하나 — 보스도 같이 얻는다 |

## 1. 층 지도

| 층 | 질문 | 담당 | 아는 것 |
|---|---|---|---|
| 언제 | 지금 행동할까 | 컨트롤러 BT (`BossMonsterController` · `NormalMonsterController`) | 사망 · 리쉬 · 사거리 · 간격 |
| 무엇을 쓸까 | 어떤 패턴 | 선택기 (`BossMonsterPatternSelector` · `AmmoPatternSelector`) | HP선 · 가중치 / 탄 수 |
| 써도 되나 · 실행 | 허락 · 실행 | `SkillSystem` (Grant · CanUse · TryUseSkill · Cancel) | 보유 목록 · 쿨 · 태그 |
| 순서 | 무엇 다음 무엇 | `PatternRunner` → `Pattern` → `PatternAction` | Action 순서 · 현재 Action |
| 신호 | 언제 반응 | `TagMarker`/애니 이벤트 → Receiver → `SS.SendTag` → Action.OnTag · `PatternTask` | 태그 + memo 조건 |
| 무엇을 | 결과 하나 | `PatternEvent` 파생 (Strike · Field · Projectile · Summon · Activation · Choose) | 점 하나에 무엇을 |
| 어디에 · 간격 | 점 목록 | `PointEmitter` · `Placement` · `Anchor` | 기준점 · 모양 · 간격 · 회차 |
| 결과 | 얼마나 · 누구에게 · 계산 | `DamageSpec` → `HitShape`/투사체 → `Health` | 패킷 · 판정 · 방어/사망 |

규칙
1. 아래층은 **PatternContext만 읽는다** (시전자 · 타겟 출처 · 소켓 · memo · 스탯 · 토큰).
2. Event는 **상속 한 번**, 위치 · 간격 · 데미지 · 판정은 **필드 조합**(PointEmitter · DamageSpec · HitShape).
3. Action은 상태를 가져도 된다(한 번에 하나), **Event는 무상태**(겹쳐 Fire될 수 있다).
4. `new DamageInfo`는 **Stat 안에서만**. 판정 쪽은 HitPoint만 채운다.

## 2. 한 패턴의 일생

1. BT가 "행동 중 아님 · 사거리 안"을 확인 → 선택기 `TryPick`
2. 선택기가 `SS.CanUse(pattern)`(보유 · 쿨 · 태그)로 거른 뒤 확정 — 같은 프레임에 같은 조건으로 SS가 받으므로 어긋나지 않는다
3. `SS.TryUseSkill(pattern, BuildContext())` → `Runner.Start` (memo 비움)
4. `SS.Update` → `Runner.Tick` → 첫 Tick에 `Action.Begin` (애니 CrossFade / 타임라인 Play)
5. 클립 · 타임라인의 신호 → Receiver → `SS.SendTag` → **Action.OnTag**(끝 태그) → **Task.Matches**(태그 + 조건) → `Event.Fire(ctx)`
6. Event: Fire 때 `DamageSpec.Pack` 1번 → `PointEmitter.Run`(점 · 간격 · 회차) → 점마다 결과
7. 끝 태그(`Event.End`) 또는 상태 종료 → Action Success → 다음 Action → 마지막이면 `PatternEnded`

## 3. 몬스터에 붙는 방식

| 자리 | 하는 일 |
|---|---|
| `BaseMonsterController` | 사망 = `Health.OnDead` 이벤트 → `OnDeath`(SS.Cancel · 콜라이더 · Death · 디스폰) · SS 생애(OnSpawn `ResetState` · OnDespawn `Cancel`) · `BuildContext()` · `ITargetSource` |
| `BossMonsterController` | BT: 행동 중 → 간격 대기 → `BossMonsterPatternSelector.TryPick` → TryUseSkill · 등장 패턴 `PlaySkill` |
| `NormalMonsterController` | BT: 리쉬 → **행동 중(SS.IsBusy)** → 전투 → 추적 → 대기 · 리쉬가 행동을 끊으면 `SS.Cancel` · 행동 중 회전은 `Pattern._faceTarget` |
| `SkillMonsterController` | 전투 = 사거리 안이면 `AmmoPatternSelector.TryPick` → TryUseSkill (무기 · 탄 · 발사 방식은 전부 프리팹의 Pattern 에셋) |

MonsterState(FSM 잔재)는 없앴다 — 정보마다 원래 주인으로: 살았나 = Health, 행동 중 = SS, 귀환 중 = 리쉬, 교전 = 계산(살아 있고 귀환 · 행동 · 감지 중).

## 4. 데미지

| 역할 | 담당 | 시점 |
|---|---|---|
| 공격력 읽기 | `DamageSpec.Pack(ctx)` → `BaseStat.BuildOutgoingDamage` | **Event Fire 때 1번** (스냅샷) |
| 누가 맞았나 | 제자리 = `HitShape.Apply`(OverlapSphere, 트리거 포함 · 중복 제거) / 날아감 = 투사체 콜라이더 | 점마다 · 틱마다 · 충돌 때 |
| 계산 | `Health.TakeDamage` (무적 · 방어 · 사망) | 피격 시 |
| 보이기 | VFX (스크립트 0) | 점마다 |

- Strike: 점마다 판정, 중복 제거는 Fire 단위. `_delay` > 0 이면 경고 → 지연 → 같은 자리 (점마다 따로 기다림)
- Field: 장판마다 틱 루프, 중복 제거는 틱 단위
- Projectile: 패킷을 투사체에 넘김 (움직이는 것은 콜라이더가 판정)

## 5. 신호 입구 둘

| 길 | Unity 전달 방식 | 입구 |
|---|---|---|
| 타임라인 마커 (`TagMarker`) | `INotificationReceiver.OnNotify` | `TimelineTagReceiver` |
| 애니메이션 이벤트 | **함수 이름**으로 같은 오브젝트의 모든 컴포넌트에 | `AnimationTagReceiver.OnAnimTag` · `PlaySfx` |

교훈 (11-04 버그): 이벤트 함수를 `SendTag`로 두자 `SkillSystem.SendTag`도 같이 불려 **태그가 두 번** 들어갔다(Tank 포탄 2배, AR 10발).
→ 애니 이벤트 함수 이름은 오브젝트 안에서 유일해야 한다(`OnAnim…` 접두사). SS는 Unity 전달 방식을 모르게 입구를 SS 밖에 둔다.
두 입구는 컴포넌트 하나로 합칠 수 있다(전달 방식이 둘일 뿐) — 지금은 둘로 둔다.

## 6. 끊기 · 생애

| 경우 | 경로 | 이미 나간 Event |
|---|---|---|
| 사망 | `Health.OnDead` → `OnDeath` → `SS.Cancel` (+ 보스는 생애 토큰 취소) | 생애 토큰으로 멈춤 (보스) |
| 회수 | `Pool.Push` → `OnDespawn` → `SS.Cancel` → 토큰 취소 | 멈춤 |
| 리쉬 귀환 | `StopAction` → `SS.Cancel` | **계속 돈다** — 패턴 토큰 없음 (남은 일 1) |
| 새 생애 | `OnSpawn` → `SS.ResetState`(쿨) · 선택기 `ResetState` | — |

## 7. 이관 기록 = 확장 증거

| 커밋 | 대상 | .cs | 새로 생긴 것 |
|---|---|---|---|
| `8c071058` | 공용화 + AR | 다수 | SS 생애를 Base로 · 애니 신호 · 끝 태그 · `_repeat` · ProjectileEvent · AmmoPatternSelector · SkillMonsterController |
| `fb55dfaf` | RL | **0** | 없음 — 프리팹 · 클립만 |
| `13b82158` | Tank | 1 (StrikeEvent) | 경고 · 지연 · VFX 수명 옵션 (보스도 사용 가능) |

"공용으로 만들려면 SS로 무엇을 올려야 했나" = 사망 이벤트 · 생애 처리 · ctx 조립 · 애니 이벤트 신호 · 태그로 끝나는 Action.
보스만 쓸 때는 필요 없던 것들이다.

## 8. 대조 — AbilitySystem(플레이어) · SkillSystem(몬스터) · GAS

| 항목 | AbilitySystem (플레이어, 7~8월) | SkillSystem (보스 · 몹, 10~11월) | GAS (UE) |
|---|---|---|---|
| 단위 | `AbilityData`(SO) = Effect 목록 | `Pattern`(프리팹 컴포넌트) = Action 목록 | `GameplayAbility` |
| 무엇을 쓸까 | 입력(Q · E · 평타) | 선택기 (보스 HP선 · 가중치 / 몹 탄 수) | AI · 입력 바인딩 (ASC 밖) |
| 써도 되나 | `AbilityRunner.Commit` — 쿨 · Required/Blocked 태그 | `SS.CanUse` — 보유 · 쿨 · (태그 자리만) | `CanActivateAbility` — 쿨 GE · 비용 · 태그 |
| 쿨 소유 | Runner(캐스터별 절대 시각) | SS(캐스터별 마지막 사용 시각) | 쿨다운 GameplayEffect |
| 흐름 | Effect를 **순서대로 await** (Delay · Repeat도 Effect) | Action **순서** + Task **신호 반응** | Ability 그래프 + AbilityTask |
| 타이밍 | 애니 비트 번호 → `UseAbility(slot, beat)` | 태그 (TagMarker · 애니 이벤트) | AnimNotify → GameplayEvent · WaitGameplayEvent |
| 분기 · 선택 | 없음 (선형) | ChooseEvent + Task 조건(memo) | 그래프 분기 |
| 위치 | `Anchor`(Effect 필드) | `Placement` · `Anchor` · `PointEmitter` | TargetActor · TargetData |
| 결과 | Effect(SpawnProjectiles · ApplyStatusTag …) | Event + DamageSpec · HitShape | GameplayEffect (Spec · Modifier · Execution) |
| 연출 | Effect 안 (SpawnVFX) | Event 안 부품 | GameplayCue (분리) |
| 상태 태그 | Required · Blocked · **Grants**(발동 중 부여) | 자리만 (`CheckCurrentTag`) | Activation/Blocked/Cancel Tags · 계층 태그 |
| 취소 | 캐스터 액션 토큰 | SS.Cancel + 생애 토큰 (패턴 토큰 없음) | EndAbility · CancelAbilitiesWithTag |
| 데미지 | `BuildOutgoingDamage` → 투사체 → Health | 같음 (DamageSpec이 Pack) | GE Execution → AttributeSet |
| 네트워크 | 없음 | 없음 (완전 싱글) | 예측 · 복제 |

읽는 법
- **AbilitySystem은 GAS의 "허락" 층이 가장 가깝다** — Required/Blocked/Grants 태그 · Commit/Fire 분리. 대신 흐름이 선형 Effect 목록이라 보스(선택 · 분기 · 여러 단계)를 담지 못했다(② 실패).
- **SkillSystem은 GAS의 "흐름 · 신호 · 타게팅" 층이 가깝다** — Action 순서 + 태그 신호 + Placement. 대신 상태 태그 게이트는 아직 자리만 있다.
- 둘이 이미 **같은 이음새에서 만난다**: `BaseStat.BuildOutgoingDamage` → `IDamageable/Health`. 플레이어 이관은 이 이음새 위에서 하면 된다.

가져오지 않은 것 (범위 결정)
| GAS | 이유 | 트리거 |
|---|---|---|
| AttributeSet · GE Modifier | 스탯은 `BaseStat`이 이미 담당, 버프는 플레이어 쪽 `ApplyStatModifier` | 몬스터 버프가 생길 때 |
| GameplayCue | 연출과 결과가 같은 점 · 같은 순간 → Event 안 부품으로 충분 | 연출만 따로 재사용할 때 |
| 상태 태그 게이트 · 패턴 취소 | 생애 중 끊는 경우가 리쉬뿐 | 스턴 · 무력화 |
| 예측 · 복제 | 완전 싱글 | — |

## 9. 남은 일

1. **패턴 토큰** — Runner가 패턴마다 토큰(생애 토큰에 연결) → 리쉬 · 스턴이 연사 중간을 끊게. 장판처럼 남아야 하는 것은 생애 토큰 선택.
2. **상태 태그 게이트를 SS로** — AbilitySystem의 Required/Blocked/Grants를 `CheckCurrentTag` 자리에(`State.Casting` 부여 = 실행 중 거절).
3. **두 번째 보스** — 보스 키 데이터화(`BossDungeonScene` 하드코딩) → Prefab Variant + 패턴 재구성, diff .cs 0.
4. **플레이어 SS 이관** — 데미지 이음새는 이미 같다. 입력 = 선택기 자리.
5. (선택) 신호 입구 통합 · Tank 착탄 지면 스냅 · 끄기 실험 Animator 항목 정리.
