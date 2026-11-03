# 전투 행동 구조 — 최종 정리 (④ GAS 구조판)

> 2026-10-31 · 커밋 `b7660195`(코드) · `ff5067f0`(문서 04·05) · 보스 패턴 6개(입장 + 일반 5) 플레이 확인
> 시각 자료판: [06_Final_Structure.pdf](06_Final_Structure.pdf)

한 줄 요약: **BMC가 "언제", MPS가 "무엇을 고를지", SkillSystem이 "할 수 있나 + 실행", Pattern·Action이 "흐름", 타임라인 신호가 "순간", Task가 "조건", Event가 "무엇을", Placement가 "어디에", PointEmitter가 "간격"을 맡는다.** 아래층은 위층을 모르고 `PatternContext`만 읽는다.

---

## 1. 층 지도

| 층 | 질문 | 클래스 | 한 줄 |
|---|---|---|---|
| 결정 | 언제 쓰나 | `BossMonsterController` (BMC) | BT: 사망 → 실행 중 → 간격 대기 → 고르기·쓰기 |
| 고르기 | 무엇을 쓰나 | `MonsterPatternSelector` (MPS) | 기믹(HP 줄) 우선, 없으면 일반 패턴 가중치 추첨 |
| 실행 창구 | 쓸 수 있나 · 실행 | `SkillSystem` (SS) | 쿨 확인 → Runner 시작 · 신호(`SendTag`) 창구 · 끝나면 `PatternEnded` |
| 흐름 | 어떤 순서로 | `Pattern` → `PatternAction`(`TimelineAction` · `AnimationAction`) | 동작을 순서대로, 각 동작은 끝날 때까지 기다림 |
| 신호 | 언제 반응하나 | `TagMarker` → `TimelineTagReceiver` | 타임라인 마커가 `GameplayTagSO`를 보냄 |
| 조건 | 반응해도 되나 | `PatternTask` | 태그 + (선택) memo 조건 → Event 목록 |
| 무엇을 | 무슨 일을 | `PatternEvent` 파생 5개 | Strike · Field · Summon · Activation · Choose |
| 간격 | 어떤 리듬으로 | `PointEmitter` | interval · batch · saveAs, 대기 뒤 토큰 확인 |
| 어디에 | 어느 자리에 | `Placement` + `Anchor` | 기준점 하나 + 모양 → `List<Pose>` |
| 칠판 | 패턴 동안 기억 | `PatternMemo` | 저장한 점(`zone`) · 고른 값(`relic`) |
| 바깥 정보 | 타겟 · 소켓 | `ITargetSource`(BMC) · `IAnchorSet`(`AnchorSet`) | 인터페이스로만 노출 |

## 2. 한 패턴의 일생

1. **BMC** BT가 `TryUseSkill` 노드에 도달 → `MPS.TryPick(HP 줄)`
2. **MPS** 지나간 기믹 줄이 있으면 그 패턴, 아니면 쿨이 끝난 일반 패턴 중 가중치 추첨
3. **SS** `TryUseSkill` — 쿨 확인 → 통과하면 `Runner.Start(pattern, ctx)` + 사용 시각 기록
4. **Runner** memo를 비우고 `ctx.memo`에 넣음 → 다음 `Update`에서 첫 Action `Begin`
5. **TimelineAction** 타임라인 바인딩 · 재생, `director.state`가 Playing인 동안 Running
6. **TagMarker** 정해진 프레임에 `TimelineTagReceiver.OnNotify` → `SS.SendTag(tag)` → `Runner.OnEvent(tag)`
7. **Task** 현재 Action의 Task 목록에서 `Matches(tag, memo)`가 참인 것만 `Fire(ctx)` — 목록 순서대로
8. **Event** `Fire` → `FireAsync(...).Forget()` — 기다릴 게 없는 Event는 이 호출 안에서 끝남
9. **PointEmitter** `Placement.Collect`(Fire 순간 한 번 = 위치 고정) → saveAs → 점마다 `at(pose)` · 묶음 사이 interval 대기 · 대기 뒤 토큰 확인
10. **Action 끝** 타임라인이 멈추면 Success → 다음 Action, 마지막이면 Runner가 Success
11. **SS** `PatternEnded` → **BMC** 다음 패턴 시각 = 지금 + `_patternInterval`, Idle 크로스페이드

## 3. 의존 방향

```
BMC ──▶ MPS ──▶ SS ──▶ Runner ──▶ Pattern ▶ Action ▶ Task ▶ Event ▶ PointEmitter ▶ Placement ▶ Anchor
 │                                                                                              │
 └─ implements ITargetSource ◀──────────────── PatternContext.targets ◀─────────────── 읽기 ────┤
AnchorSet ─ implements IAnchorSet ◀─────────── PatternContext.sockets ◀─────────────── 읽기 ────┘
```

- 화살표는 한 방향. Event · Placement 코드에 `BossMonsterController`라는 이름이 한 번도 안 나온다.
- 아래층이 위층 정보가 필요하면 **인터페이스**(`ITargetSource` · `IAnchorSet`)로 뒤집는다.
- SS는 BT 용어(`NodeState`)를 밖으로 내보내지 않고 `event PatternEnded`만 쏜다.

## 4. PatternContext

| 필드 | 채우는 곳 | 쓰는 곳 |
|---|---|---|
| `owner` (SkillSystem) | BMC `BuildContext` | Anchor.Caster · Action(Animator/Director 찾기) · SummonEvent(ISummonRegistry) |
| `targets` (ITargetSource) | BMC `BuildContext` (= this) | Anchor.Target · FacingSource.TowardTarget — **Fire 순간에 물어봄** |
| `sockets` (IAnchorSet) | BMC `BuildContext` | Anchor.Socket · SocketsPlacement |
| `memo` (PatternMemo) | **Runner.Start** | PointEmitter.saveAs · Anchor.Saved · ChooseEvent · ActivationEvent · Task 조건 |
| `cToken` | BMC (전투 토큰) | 모든 대기 — 사망 · 회수 시 취소 |

## 5. Event — 상속은 한 번, 위치는 조합

| Event | 하는 일 | PointEmitter | 자기 값 |
|---|---|---|---|
| `StrikeEvent` | 점마다 VFX (피해는 나중에 HitEffect) | ○ | `_vfx` · `_vfxEuler` |
| `FieldEvent` | 점마다 장판 + 수명 뒤 회수 | ○ | `_field` · `_duration` · `_fieldEuler` |
| `SummonEvent` | 점마다 소환 + 등록 | ○ | `_monsterKey` |
| `ActivationEvent` | 프리팹 안 오브젝트 켜기 · 끄기 | × | `_targets` · `_active` · `_choiceKey` |
| `ChooseEvent` | 0~N-1 중 하나를 memo에 | × | `_key` · `_count` |

공용: `PatternEvent.SpawnAt` (풀에서 꺼내 그 자리에 놓고 Clear → Play).

**PointEmitter 값**

| 값 | 의미 |
|---|---|
| `_where` | Placement — 몇 개 · 어떤 순서 |
| `_interval` | 묶음 사이 간격(초). 0 = 전부 동시 |
| `_batch` | 한 번에 몇 개씩. 0 = 전부 (예: 원 12개씩 = 12) |
| `_saveAs` | memo에 점 저장 → 다른 Event가 `Anchor.Saved`로 기준점 |

## 6. Placement — 기준점 + 모양

**Anchor**

| source | 기준점 | key |
|---|---|---|
| Caster | 시전자 위치 · 방향 | — |
| Target | 지금 타겟 (Fire 순간) | — |
| Socket | `AnchorSet`에서 키로 | 소켓 키 |
| Saved | memo에 저장된 점 첫 번째 | memo 키 |

`facing`: AnchorForward(기준점 방향) · TowardTarget(시전자 → 타겟) · World(항상 북쪽). `offset`은 facing 기준 로컬. 기울기는 버리고 좌우 방향만 남긴다.

**모양**

| 클래스 | 값 | 결과 |
|---|---|---|
| `PointPlacement` | — | 기준점 1개 |
| `CirclePlacement` | `radii[]` · `countPerRing` · `startAngle` · `clockwise` · `pointFacing` | 동심원, radii 순서 = 터지는 순서 |
| `RandomInRingPlacement` | `minRadius` · `maxRadius` · `count` · `pointFacing` | 고리 안 무작위 (넓이 균등) |
| `SocketsPlacement` | `keys[]` | 소켓 여러 개, 키 순서 = 실행 순서 (`_anchor` 안 씀) |

`PointFacing` (Inherit · Outward · Inward)는 점 하나하나가 볼 쪽 — 기하라서 Placement가 정한다. 프리팹 자체 자세 보정만 Event(`_vfxEuler`).

## 7. 보스 프리팹 구조

```
Hieronymus_Boss   [BMC · MonsterPatternSelector · SkillSystem · TimelineTagReceiver · AnchorSet]
├ Patterns            ← 흐름 (Pattern → Action → Task)
│  ├ Pattern_Entrancce   → OP [AnimationAction]
│  ├ Pattern_Skill0      → NormalAttack [TimelineAction] → Task
│  ├ Pattern_Skill1      → NormalSkill_1 [TimelineAction] → Task.Cast · Task.StrikeIn · Task.StrikeOut · Task.End
│  ├ Pattern_Skill2      → NormalSkill_2 → Task
│  ├ Pattern_Skill3      → NormalSkill_3 → Task
│  └ Pattern_Skill4      → NormalSkill_4 → Task
├ Events              ← 반응 조각 보관함 (Task가 참조)
│  ├ Skill0/NormalAttackStrike [Strike] + PointPlacement
│  ├ Skill1/Choose · RelicActivationOn · Skill1_Warning_Event [Field] · StrikeIn · StrikeOut · RelicActivationOff
│  ├ Skill2/StrikeLightning [Strike] + CirclePlacement
│  ├ Skill3/StrikeMagicCircle [Field] + PointPlacement
│  └ Skill4/SummonDriodRL [Summon] + SocketsPlacement
├ Boss_RedRelic · Boss_GreenRelic (Magic fire pro red/green = Activation 대상)
└ … 본체 · 본
```

흐름(Patterns)과 반응 조각(Events)을 다른 가지에 둔다 — Task는 Events 쪽 컴포넌트를 참조만 한다.

소환 위치(Spawn_1~4)는 방 기준이라 **맵**이 들고 있고, `BossDungeonScene`이 보스 생성 직후 `AnchorSet.Register("Spawn_i", 점)`으로 등록한다.

## 8. 실제 조립 (프리팹 값 그대로)

| 패턴 | 태그 → Task | Event · Placement · 값 |
|---|---|---|
| 입장 | — | `AnimationAction` OP (Task 없음) |
| Skill0 평타 | Event.Strike | Strike · Point(Target) |
| Skill1 유물 | Event.Cast | Choose(relic, 2) → Activation(red/green, choiceKey relic) → Field(Point · Caster (0,0,5), saveAs **zone**, 5초) |
| | Event.Strike, `relic == 0` | Strike · RandomInRing(**Saved zone**, 0~3, 10개) · interval 0.1 |
| | Event.Strike, `relic == 1` | Strike · RandomInRing(**Saved zone**, 3~10, 30개) · interval 0.1 |
| | Event.End | Activation(red/green, off) |
| Skill2 조여오는 번개 | Event.Strike | Strike · Circle(Target, radii 8·6·4·2·0, 12개, Outward) · interval 0.15 · batch 12 |
| Skill3 장판 | Event.Strike | Field · Point(Target) · 5초 |
| Skill4 소환 | Event.Summon | Summon(Droid_Helmet_RL) · Sockets(Spawn_1~4) · interval 0.3 |

MPS: 일반 5개 (가중치 Skill1 = 100, 나머지 1), 기믹 0개. 쿨은 전부 0.

## 9. 규칙 (계약)

1. **Event는 PatternEvent를 한 번만 상속한다.** 위치 · 간격은 PointEmitter 필드로 조합.
2. **분기는 Event가 아니라 Task 조건.** 고르기(Choose)와 분기(Task `_ifKey`)를 뗀다 — Event 안에 if가 없다.
3. **Collect는 순수.** 대기 · 부수효과 없음, 점 순서 = 실행 순서. Fire 순간 한 번 → 위치 고정.
4. **아래층은 ctx만 읽는다.** 시전자 · 타겟 · 소켓 · memo 모두 ctx로.
5. **대기 뒤에는 토큰부터.** 시전자는 풀 오브젝트 — 회수 후 재사용된 몸을 건드리지 않게. PointEmitter가 대신 해 줌.
6. **같은 Task 안 순서 보장.** 기다리지 않는 Event는 `Fire` 안에서 끝나므로 앞 Event의 memo를 뒤 Event가 바로 읽는다.
7. **memo는 패턴 하나 동안만.** Runner가 하나를 재사용, 패턴 시작마다 비움.
8. **패턴은 원자 실행.** 끊는 건 사망 · 회수뿐(`SS.Cancel` + 전투 토큰 취소). 취소 시 `PatternEnded`를 보내지 않는다(= 정상 종료만).

## 10. 취소 경로

| 무엇이 | 어떻게 멈추나 |
|---|---|
| 실행 중 Action (타임라인) | `HandleDeadState` / `OnDespawn` → `SS.Cancel` → `Runner.Cancel` → `Action.Cancel` (director Stop) |
| 간격 대기 중인 Strike · Summon | 전투 토큰 취소 → PointEmitter 대기가 끝나며 return |
| 로드 중인 소환 | 로드 후 토큰 확인 → 소환수 회수 |
| 장판 수명 | 토큰 취소 → 즉시 회수 |
| 켜 둔 유물 | **정리 안 됨** — Event.End가 안 오면 켜진 채 남음 (남은 일) |

## 11. 확장하려면 어디를 고치나

| 추가할 것 | 고칠 곳 | 시스템 코드 |
|---|---|---|
| 같은 부품으로 새 패턴 | 프리팹에 Pattern · Action · Task + Events 조립, 타임라인에 마커 | 0줄 |
| 새 모양 (부채꼴 · 일직선) | `Placement` 파생 1개 (`Generate`만) | 0줄 |
| 새 행동 (투사체 · 경고) | `PatternEvent` 파생 1개 (`at(pose)`만) | 0줄 |
| 새 기준점 출처 | `AnchorSource` + `Resolve` 한 갈래 | Anchor 1곳 |
| 패턴 분기 | Task 조건 + ChooseEvent | 0줄 |
| 두 번째 보스 | 프리팹 · MPS 표 · 타임라인 | 0줄 (목표, 증명 예정) |

## 12. GAS 대응

| GAS | 여기 | 비고 |
|---|---|---|
| AbilitySystemComponent | SkillSystem | 쿨 · 실행 · 이벤트 창구 |
| GameplayAbility | Pattern | 쿨 · 단계 |
| AbilityTask | PatternAction | 끝날 때까지 기다림 |
| AnimNotify → GameplayEvent | TagMarker → SendTag(GameplayTagSO) | 신호 = 태그 |
| (Ability 안 이벤트 대기 분기) | PatternTask(태그 + 조건) | |
| TargetActor · LocationInfo | Placement · Anchor | 우리는 "어디"만, "누구"는 Event가 |
| TargetData | `List<Pose>` + PatternMemo | |
| GameplayEffect | (예정) HitEffect | 지금 Strike · Field는 연출만 |
| GameplayCue | — | 안 가져옴 |

## 13. 남은 일 · 주의

1. **피해** — Strike · Field에 공용 HitEffect (점 하나 = 판정 하나). 장판 프리팹의 `GenesisAttack`은 제거 대상.
2. **경고 → 타격 같은 자리** — `WarningEvent` · `SavedPlacement` (04 문서 7절).
3. **유물 사망 시 정리** — OnSpawn 리셋 또는 Activation "취소 시 되돌림" 결정.
4. **패턴 끝 vs 진행 중 Event** — 연속 타격 · 장판 수명은 `PatternEnded` 뒤에도 돈다. 지금은 타임라인 길이로 맞추는 에셋 규칙.
5. **프리팹 값 확인** — Skill1 가중치 100(테스트 값으로 보임) · Skill2 `radii` 마지막 `0` = 중심에 12발 겹침(중심 1발이면 Point Event 하나가 맞음) · 쿨 전부 0.
6. **② 잔재 정리** — `SpawnPointSet`(+씬 주입) · `BossRelicController`(프리팹에서 비활성) · `SummonMonsters` 등 옛 Effect. 빈 `Tag_.asset`(미커밋).
7. **증명** — 텍스처 반전 두 번째 보스로 "시스템 코드 0줄" 확인.

## 관련 문서

- [04_Placement_Explained.pdf](04_Placement_Explained.pdf) — Placement 설계 · GAS Targeting 대응
- [05_Pattern_Event_Structure.pdf](05_Pattern_Event_Structure.pdf) — Event 구조 구현 시점 설명
- [03_SkillSystem_Structure.pdf](03_SkillSystem_Structure.pdf) — BMC · MPS · SS 뼈대 점검
