# 분산 하드코딩 전투 로직 → 데이터 주도 단일 AbilitySystem

> **포트폴리오용 요약 문서** — 기존 UniTask 섹션을 대체하는 "전투 시스템 아키텍처" 파트.
> 상세 이관 기록은 [README.md](README.md) · [BossMigration.md](BossMigration.md) · [CharacterMigration.md](CharacterMigration.md) 참고.

---

## 한 줄 요약

보스·플레이어·일반몹이 **제각각 하드코딩**하던 공격/스킬 로직을, **하나의 계약(IAbilityCaster) + 러너(AbilityRunner) + 데이터(AbilityData/EffectData) + 실행 부품(IEffect)** 으로 통합했다. **새 공격은 이제 코드가 아니라 데이터(Effect 조합)로 만든다.**

| 축 | 이전 (분산·하드코딩) | 현재 (통합 AbilitySystem) |
|---|---|---|
| 공격 정의 위치 | 각 컨트롤러 메서드에 하드코딩 | `AbilityData`(SO) + `EffectData[]` |
| 캐스터별 발동 경로 | 보스/플레이어/몹 **3갈래 제각각** | 전부 `AbilityRunner.TryCast` **단일 경로** |
| 새 공격 추가 | 새 클래스/메서드 작성 (프로그래머) | **asset 조합** (디자이너 편집 가능) |
| 스킬 단위 | 스킬 1개 = 클래스 1개 (상속) | 스킬 = **Effect 블록 조합** |
| 연사 발수·간격 | 코드 상수(SerializeField) | **데이터 값**(`_count`/`_interval`) |
| 관심사 | 계산·연출·데미지·발동이 한 메서드에 뒤엉킴 | 계산=Stat / 실행=Effect / 배달=투사체 / 발동=Runner |

---

## 1. 문제상황 — 전투 로직이 세 갈래로 흩어져 있었다

"공격을 발동한다"는 **같은 개념**이 캐스터마다 따로 구현돼 있었다.

- **보스**: `BossSkillController`가 문자열 이름으로 캐스트 → `BossSkillBase.Cast(BossSkillContext)`, 스킬마다 클래스 하나(`GenesisSkill`, `SummonMonsterSkill`, `RelicAttack` …). 게다가 신규 AbilitySystem 경로와 **이중으로 공존**하고 있었다.
- **플레이어**: `BaseCharacter`/유닛에 `FireOneBullet`, `PerformAttackAction`이 하드코딩. 평타·Q·E의 발동 경로가 서로 달랐다.
- **일반몹**: `MonsterARController.RapidFireAsync`, `MonsterTankController`의 포격 시퀀스 등 컨트롤러마다 발사 로직이 직접 박혀 있었다.

### 무엇이 문제였나

1. **중복·비일관** — 공통 규약이 없어 "총을 쏜다"를 캐스터 수만큼 유지보수해야 했다.
2. **확장성 부재** — 새 공격을 만들려면 **코드를 새로 짜야** 했고, 디자이너가 손댈 수 없었다.
3. **스킬 = 클래스 1개** — 조합이 안 됐다. "데미지 + 상태이상 + 이펙트"를 붙이려면 매번 새 클래스.
4. **관심사 미분리** — 데미지 계산·연출·발동 조건·투사체 생성이 한 메서드에 뒤엉켰다.
5. **값이 코드에 박힘** — 발수·간격·배율이 상수라, 변형마다 컴포넌트가 갈라졌다.

> **목표: "누가 쓰든 같은 파이프라인으로 흐르고, 새 공격은 데이터로 만든다."**

---

## 2. 문제해결 — 통합 AbilitySystem

```text
[캐스터]  BaseCharacter · BaseMonsterController · BossMonsterController   (IAbilityCaster)
   │  보유: AbilityRunner,  AbilityData 목록
   │  발동: 애니메이션 이벤트 / 입력 / Timeline Signal → runner.TryCast(ability, ctx, token)
   ▼
[러너]    AbilityRunner            ← 캐스터마다 1개 (쿨다운·캐스트타임 상태 소유)
   │  쿨다운 검사 → CastTime 대기 → BuildRuntimeEffects()
   ▼
[데이터]  AbilityData (SO)         = 코스트·쿨다운·캐스트타임 + EffectData[]
   │        └ EffectData (추상 SO) ──CreateRuntime()──▶ IEffect (런타임)
   ▼
[실행]    IEffect.ExecuteAsync(ctx, token)   순서대로 실행
   │        DealDamage · SpawnProjectiles · AreaStrike · SummonMonsters …
   ▼
[적용]    DamagePipeline(TakeDamage) · StatusSystem · 투사체 스냅샷 배달
```

### 핵심 타입

| 타입 | 역할 |
|---|---|
| `IAbilityCaster` | 캐스터 신원 표식(마커). 발동 진입점은 구현체마다 다름 |
| `AbilityRunner` | 캐스터별 1개. **쿨다운·캐스트타임** 관리, `TryCast` |
| `AbilityData`(SO) | 스킬 정의: 코스트·쿨다운·캐스트타임 + `EffectData[]` |
| `AbilityContext` | 발동 문맥: caster·casterGO·stat·target·targetPoint·총구 |
| `EffectData`(추상 SO) | 인스펙터 편집용. `CreateRuntime()`로 런타임 부품 생성 |
| `IEffect` | 런타임 실행 부품. `ExecuteAsync(ctx, token)` |

**구체 Effect(조립 부품)**: `DealDamage`, `SpawnProjectiles`, `SpreadProjectiles`, `AreaStrike`, `SpawnVFX`, `SpawnDamageField`, `SummonMonsters` / 패턴·흐름 제어 `RepeatEffect`, `ScatterPattern`, `RadialBurstPattern`, `DelayEffect`, `SelectRandomChoice`, `BranchByChoice` / 상태 `ApplyStatModifier`.

---

## 3. 핵심 설계 ① — 스킬 = "상속"이 아니라 "Effect 조합"

이전에는 스킬 하나가 클래스 하나(`BossSkillBase` 파생)였다. 지금은 스킬이 **Effect 블록의 조합**이다.

```text
"라이플 5연사"  =  RepeatEffect(count=5, interval=0.05) {
                      SpawnProjectiles(count=1),   ← 반복은 Repeat가 전담 (count=1 필수)
                      SpawnVFX(Muzzle)
                  }

"보스 광역기"    =  DealDamage → SpawnVFX → AreaStrike(warning→delay→explode)

"랜덤 분기 패턴" =  SelectRandomChoice → BranchByChoice { A: 소환, B: 포격 }
```

- **데미지 + 이펙트 + 상태이상**을 붙이려면 클래스를 새로 짜는 게 아니라 **asset에 Effect를 얹는다.**
- 발수/간격/배율은 코드 상수가 아니라 **asset 값**. 라이플 5발 vs 바주카 1발 = 같은 `SpawnProjectiles`의 값 차이일 뿐.

---

## 4. 핵심 설계 ② — 슬롯 키 + 애니메이션 이벤트 통합 (2단 조회)

플레이어의 발동 타이밍은 **애니메이션 이벤트**가 정한다(프레임 정확도). 문제는 "좌클릭·Q·E마다 콜백을 따로 만들면 확장이 안 된다"는 것.

**이전**: 애니 이벤트가 상태 가드 붙은 개별 콜백(`OnAttackEvent`, `OnESkillEvent`…)을 호출. 스킬이 늘 때마다 콜백이 늘어남.

**현재**: 콜백 하나 `AbilityBeat(AnimationEvent)`로 통일하고, **두 축을 분리**했다.

| 축 | 정하는 것 | 출처 |
|---|---|---|
| **슬롯** (어느 스킬) | Attack / Skill / Ex | **현재 상태** (`Attack 상태→Attack`, `Q_Skill 상태→Skill`) |
| **비트 인덱스** (그 스킬 안에서 몇 번째 발사) | 0, 1, 2 … | 애니 이벤트 **`intParameter`** |

```csharp
public void AbilityBeat(AnimationEvent e)
{
    var slot = SlotForState(_stateMachine.CurrentState); // 축1: 상태 → 슬롯
    if ((int)slot < 0) return;                           // 매칭 상태 아니면 무시(잔여 비트 방어)
    UseAbility(slot, e.intParameter);                    // 축2: intParameter → 비트 인덱스
}
```

- 슬롯은 어빌리티 **리스트**(`Dictionary<slot, List<AbilityData>>`)를 갖는다. 인스펙터에 같은 슬롯을 순서대로 넣으면 그게 `[0],[1],[2]`.
- **다단 발사**(모션 중 프레임마다 다른 투사체)를 **코드 없이 데이터로** 표현: Q 클립의 발사 프레임마다 `AbilityBeat`(Int=비트 인덱스)만 찍으면 된다.
- **평타는 재배선 불필요** — 항상 `Int=0`(리스트 원소 1개). E는 역할(Role)이 부여한 어빌리티가 `Ex` 슬롯 `[0]`으로 흡수돼 별도 경로가 사라졌다.

---

## 5. 핵심 설계 ③ — 보스 이중 시스템 → 단일화

보스는 한동안 **구형·신형 두 시스템이 공존**했다.

- **이전**: Timeline Signal → `BossSkillController.CastSkill("이름")` → 구형 스킬 프리팹 실행. 여기에 신규 `CastAbility` 경로가 겹쳐 있었다.
- **현재**: Timeline Signal → `BossMonsterController.CastAbility(AbilityData)` → `AbilityRunner.TryCast` **단일 경로**. Timeline은 연출·타이밍만 소유하고, 발동은 전부 AbilitySystem이 처리한다.
- **유물(Relic) 선택**도 AbilitySystem에 편입: `BossRelicController`가 `IChoiceHandler`를 구현해, `SelectRandomChoice`/`BranchByChoice` Effect가 이 핸들러로 빨강/초록 분기를 처리한다.
- 구형 `BossSkillBase` 프레임워크 8종(`BossSkillController`, `BossSkillBase`, `GenesisSkill`, `SummonMonsterSkill`, `RelicAttack` …)과 미사용 프리팹을 **전량 제거**해 보스도 단일 파이프라인이 됐다.

> 이 "Timeline=연출/타이밍, AbilitySystem=발동" 분리는 플레이어 Q 스킬의 컷신 구조(`QSkillCutScene`→`Q_Skill`)와 **동일한 철학**으로 정렬된다.

---

## 6. 관통하는 설계 원칙

- **값 / 로직 / 상태 3축 분류** — 값이 다르면 `.asset` 여러 개, 로직이 다르면 Effect 클래스 분리, 발동 중 기억할 상태가 있으면 런타임 `new` 인스턴스.
- **EffectData(SO) ↔ IEffect(런타임) = 프리팹 ↔ 인스턴스** — 상태 없는 Effect는 `CreateRuntime() => this`(GC 0), 상태 있는 Effect(DoT 등)만 `new`로 분리.
- **SO 불변 규칙** — SO 필드를 런타임에 바꾸지 않는다(공유 오염 + 에디터 영속화). 난이도 배율 등은 스탯/세션값을 곱해 읽는다.
- **투사체는 스냅샷 배달** — 데미지 계산은 공격자 Stat, **배달은 투사체가 값 패킷으로**. 공격자가 죽어(풀 반환)도 안전. 데미지는 '캐스트'가 아니라 '명중' 시점.
- **관심사 분리** — 계산=Stat / 실행=Effect / 배달=투사체 / 발동=Runner.

---

## 7. 왜 "상속(스킬=클래스)"이 아니라 "조합(스킬=데이터)"인가

| 상속 기반 (이전) | 조합 기반 (현재) |
|---|---|
| 스킬 1개 = 클래스 1개. 조합 불가 | 스킬 = Effect 블록 조합. 자유 결합 |
| 새 공격 = 새 코드 + 재컴파일 | 새 공격 = asset 편집 (**디자이너 가능**) |
| 캐스터마다 발동 경로가 다름 | `AbilityRunner.TryCast` 단일 경로 |
| 값이 코드 상수 → 변형마다 클래스 분화 | 값이 데이터 → asset 하나 복제로 변형 |
| 데미지·연출·발동이 한 메서드에 결합 | 관심사가 Effect 단위로 분리 |

---

## 8. 결과

- **3진영(플레이어·일반몹·보스) 전투 발동이 하나의 데이터 주도 파이프라인으로 수렴.**
- 새 공격/스킬을 **코드 없이 Effect 조합으로** 제작 — 프로그래머 병목 제거.
- 다단 발사·랜덤 분기·소환·상태이상까지 **부품 조합**으로 표현.
- 보스의 이중 시스템 제거 + 플레이어 평타/E 이관 완료, Q는 슬롯·비트 인프라까지 완성(콘텐츠 배선만 잔여).

---

## 관련 파일

- 캐스터/러너: `Assets/Scripts/Ability/{IAbilityCaster,AbilityRunner}.cs`, `Assets/Scripts/Ability/CharacterAbilitySlot.cs`
- 데이터/실행: `Assets/Scripts/Data/Ability/{AbilityData,EffectData}.cs`, `Assets/Scripts/Interface/IEffect.cs`
- Effect: `Assets/Scripts/Data/Ability/Effects/**/*.cs`
- 캐스터 구현: `BaseCharacter`, `BaseMonsterController`, `BossMonsterController`, `Monster{AR,RL,Tank}Controller`
