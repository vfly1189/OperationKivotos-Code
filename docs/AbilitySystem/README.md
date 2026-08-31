# 분산된 전투 로직 → 통합 AbilitySystem (데이터 주도 어빌리티)

> 📌 **현재 상태는 [STATUS.md](STATUS.md) 가 기준** (2026-08-19: Phase 1 머지·플레이 검증 완료, 트랙 종료). 이 문서는 이관 기록이다.

> **한 줄 요약** — 보스·플레이어·일반몹이 **제각각 하드코딩**하던 공격/스킬 로직을, **하나의 러너(AbilityRunner) + 데이터(AbilityData/EffectData) + 실행 부품(IEffect)** 로 통합했다. (`IAbilityCaster`는 규약이 아니라 **시전자 신원 마커**다 — 계획했던 공통 진입점은 `UseAbility`의 2축 구조로, 태그 노출은 `AbilityContext.Tags`로 각각 흡수됐다.) 새 공격은 이제 코드가 아니라 **데이터(Effect 조합)** 로 만든다.
>
> **📄 Part 1(이 문서)** = 몹 이관(AR/RL/Tank) + 총구화염 VFX. **후속: [BossMigration.md](BossMigration.md)** = 보스 이관(BT+Timeline+Signal 정리, 패턴/분기/소환 Effect, Anchor 공용화). **[CharacterMigration.md](CharacterMigration.md)(Part 3)** = 플레이어 캐릭터 12종 이관(캐릭터=클래스 제거, 크리 경로, SpreadProjectiles/DelayEffect, 발동 경로 3갈래 완전 통합).

| 축 | 이전(분산·하드코딩) | 현재(통합 AbilitySystem) |
|---|---|---|
| 공격 정의 위치 | 각 컨트롤러 메서드에 하드코딩 | `AbilityData`(SO) + `EffectData[]` |
| 캐스터별 경로 | 보스/플레이어/몹 3갈래 제각각 | 전부 `AbilityRunner.TryCast` 단일 경로 |
| 새 공격 추가 | 새 클래스/메서드 작성 | asset 조합 (디자이너 편집) |
| 스킬 단위 | 스킬 1개 = 클래스 1개 (`BossSkillBase` 파생) | 스킬 = Effect 블록의 조합 |
| 연사 발수/간격 | 코드 상수(SerializeField) | 데이터 값(`_count`/`_interval`) |
| 관심사 | 계산·연출·데미지·발동이 컨트롤러에 뒤섞임 | 계산=Stat / 실행=Effect / 배달=투사체 / 발동=Runner |

---

## 1. 배경 — 기존 구조의 문제점

전투 로직이 **세 갈래로 흩어져** 있었다.

- **보스**: `BossSkillController`(문자열 이름으로 캐스트) + `BossSkillBase.Cast(BossSkillContext)` + 스킬마다 클래스 하나(`GenesisSkill`, `SummonMonsterSkill` …). 쿨다운·코스트 프레임워크 없음. `BossSkillContext`는 보스 전용으로 강결합.
- **플레이어**: `BaseCharacter`/유닛(`ArisCharacter` 등)에 `FireOneBullet`, `PerformAttackAction`이 하드코딩.
- **일반몹**: `MonsterARController.RapidFireAsync`, `MonsterTankController.ArtilleryAttackSequenceAsync` 등 각 컨트롤러에 발사 로직이 직접 박혀 있었음.

### 무엇이 문제였나
1. **중복·비일관** — "총을 쏜다"는 개념이 캐스터마다 따로 구현됨. 공통 규약이 없어 유지보수가 3배.
2. **확장성 부재** — 새 공격을 만들려면 **코드를 새로 짜야** 했고, 디자이너가 손댈 수 없었다.
3. **스킬 = 클래스 1개** — 조합이 안 됨. "데미지 + 상태이상 + 이펙트"를 붙이려면 매번 새 클래스.
4. **관심사 미분리** — 데미지 계산·연출·발동 조건·투사체 생성이 한 메서드에 뒤엉킴.
5. **값이 코드에 박힘** — 발수/간격/배율이 `SerializeField` 상수라, 변형마다 컴포넌트가 갈라짐.

→ 목표: **"누가 쓰든 같은 파이프라인으로 흐르고, 새 공격은 데이터로 만든다."**

---

## 2. 새 구조 — 통합 AbilitySystem

```text
[캐스터]  BaseMonsterController · BaseCharacter  (IAbilityCaster 구현)
   │  보유: AbilityRunner _runner,  List<AbilityData> _abilities
   │  발동: PerformAttackAction / 입력 → _runner.TryCast(ability, ctx, token)
   ▼
[러너]    AbilityRunner            ← 캐스터마다 1개 (쿨다운·캐스트타임 상태)
   │  쿨다운 검사 → CastTime 대기 → BuildRuntimeEffects()
   ▼
[데이터]  AbilityData (SO)         = 코스트·쿨다운·캐스트타임 + EffectData[]
   │        └ EffectData (추상 SO) ──CreateRuntime()──▶ IEffect (런타임)
   ▼
[실행]    IEffect.ExecuteAsync(ctx, token)   순서대로 실행
   │        DealDamage · SpawnProjectiles · AreaStrike …
   ▼
[적용]    DamagePipeline(TakeDamage) · (StatusSystem 예정) · 투사체 배달
```

### 2-1. 핵심 타입

| 타입 | 파일 | 역할 |
|---|---|---|
| `IAbilityCaster` | `Assets/Scripts/Ability/IAbilityCaster.cs` | 캐스터 공통 계약 (`TryUseAbility`) |
| `AbilityRunner` | `Assets/Scripts/Ability/AbilityRunner.cs` | 캐스터별 1개. 쿨다운·캐스트타임 관리, `TryCast` |
| `AbilityData` | `Assets/Scripts/Data/Ability/AbilityData.cs` | 스킬 정의(SO): 코스트·쿨다운·캐스트타임 + `EffectData[]` |
| `AbilityContext` | 〃 (같은 파일) | 발동 문맥: caster·casterGO·stat·target·targetPoint |
| `EffectData` | `Assets/Scripts/Data/Ability/EffectData.cs` | 인스펙터 편집용 추상 SO. `CreateRuntime()` |
| `IEffect` | `Assets/Scripts/Interface/IEffect.cs` | 런타임 실행 부품. `ExecuteAsync(ctx, token)` |

구체 Effect: `DealDamage`, `SpawnProjectiles`, `AreaStrike`, `SpawnVFX`, `RepeatEffect`.
보조: `VfxAnchorSet`(`Assets/Scripts/Ability/VfxAnchorSet.cs`) — 캐스터 프리팹이 VFX 부착점을 id로 보관. `SpawnVFX`의 `Named` 앵커가 런타임에 조회.

### 2-2. 데이터 흐름 (예: AR 몹 연사)
```
BT 사거리 감지 → state=Attacking → 공격 애니메이션
  → (애니 이벤트) PerformAttackAction
      → ctx 조립(Origin=총구, Stat, CasterGO) → _runner.TryCast(_abilities[0], ctx, token)
          → 쿨다운/캐스트타임 → SpawnProjectiles.ExecuteAsync
              → for _count: 총알 스폰(풀) + BulletController.Init(DamageInfo)
      → 각 총알이 날아가 충돌 시 TakeDamage  (데미지는 '캐스트'가 아니라 '명중' 시점)
```

---

## 3. 핵심 설계 원칙 (오늘 확립)

### 3-1. 값 / 로직 / 상태 — 3축 분류
> **"값이 다르다 → asset. 로직이 다르다 → 클래스. 기억할 상태가 있다 → new 인스턴스."**

| 상황 | 해결 |
|---|---|
| 배수·발수·간격·지속시간이 다름 | **`.asset`을 여러 개** (클래스 하나) |
| 발사 패턴·메커니즘이 근본적으로 다름 | **Effect 클래스 분리** (예: 투사체 vs 지연 AoE) |
| 발동 도중 변하며 기억할 상태 있음 | **런타임 `new` 인스턴스** (DoT `_elapsed`, 콤보 `_hitCount`) |

- 예: 라이플 5발 vs 바주카 1발 → **값 차이** → `SpawnProjectiles.asset` 두 개(Count 5/1).
- 예: 총 발사(투사체) vs 탱크 폭격(지연 AoE) → **로직 차이** → `SpawnProjectiles` / `AreaStrike` 별도.

### 3-2. EffectData(SO) ↔ IEffect(런타임) = 프리팹 ↔ 인스턴스
- `EffectData`(SO) = **프리팹**: 프로젝트에 1개, 값 보관, 공유·불변.
- `IEffect` = **Instantiate한 인스턴스**: 발동마다 만들 수 있고, 자기 상태를 가짐.
- `CreateRuntime()` = **Instantiate()의 Effect 버전**.
- **상태 없으면** `EffectData`가 `IEffect`를 겸하고 `CreateRuntime() => this`(안 찍어냄, GC 0). `DealDamage`/`SpawnProjectiles`/`AreaStrike`가 이 형태.
- **상태 있으면** 반드시 `new`로 분리 (DoT 등).

### 3-3. "상태"의 소유자
- **DoT·버프·실드** = 대상에 붙어 지속 → **대상의 `StatusSystem`**(예정)이 tick/갱신/제거. IEffect는 "부착"만.
- **여러 공격에 걸쳐 번갈아 도는 flag** = **캐스터** 소유. (SO에 두면 모든 인스턴스가 공유해 충돌, per-cast 인스턴스는 매 캐스트 리셋 → 둘 다 불가) → 탱크의 착탄점 `_flag`가 이 사례.

### 3-4. 금기 & 규칙
- **SO 필드를 런타임에 바꾸지 마라** — 공유 오염 + 에디터 영속화(플레이 후 asset이 영구 변경). 난이도 배율 등은 **스탯/세션값을 읽어 곱하거나** asset을 여러 개 두거나 **몬스터 스탯에 이미 반영**.
- **SO는 씬 오브젝트를 참조 못 한다** — `AreaStrike`가 `TankBombController`(씬)를 못 담아 **경고/폭발을 프리팹으로 스폰**하도록 바꿈.
- **투사체 데미지는 스냅샷 패킷을 투사체가 배달** — 공격자가 죽어도(풀 반환) 안전. 계산은 공격자 Stat, 배달은 투사체. 공격자 반응(흡혈 등)은 데미지 적용을 옮기지 말고 **on-hit 이벤트**로.

---

## 4. 마이그레이션 사례

### 4-1. AR 몹 — `RapidFireAsync` → `SpawnProjectiles`
- 연사 로직이 컨트롤러에서 사라지고 **Effect로 이관**. 발수/간격은 asset 값(`_count`/`_interval`).
- `MonsterARController.PerformAttackAction`은 ctx 조립 + `TryCast`만.

### 4-2. Tank 몹 — `ArtilleryAttackSequenceAsync` → `AreaStrike`
- **메커니즘이 투사체가 아니라 지연 AoE**(경고 → 딜레이 → 폭발 → `OverlapSphere`)라 별도 Effect가 맞음.
- 씬 결합이던 `TankBombController[] _bombPoints`를 제거하고 **경고/폭발 프리팹을 착탄점에 동적 스폰**.
- "2발 번갈아"의 `_flag`는 **캐스터(탱크)에 유지**. 탱크가 착탄점을 계산해 `ctx.TargetPoint`로 위임, `AreaStrike`는 그 지점에 폭격만.
- 컨트롤러에서 삭제된 것: `_bombPoints`, `ArtilleryAttackSequenceAsync`, `ApplyAreaDamage`, `GetGroundPosition`, 폭발 gizmo.

### 4-3. 총구화염(연출) — `PlayFireEffect` → `SpawnVFX` + `RepeatEffect`
이관 중 **누락됐던 총구화염**을 데이터 주도 Effect로 복원. 연출도 계산·투사체와 같은 파이프라인으로.

- **`SpawnVFX`** — 범용 원샷 VFX 스폰(총구화염·피격·시전·착탄 공용). "풀에서 꺼내 어딘가에 놓는다"는 로직은 하나, 차이는 **위치뿐**이라 `VFXAnchor`(`Muzzle`/`CasterRoot`/`TargetPoint`/`Named`)만 asset 값으로 바꿔 재사용. 반환은 프리팹의 `AutoReturnToPool`이 처리하므로 **대기하지 않음**.
- **`RepeatEffect`** — "반복" 로직을 독립 부품으로 분리한 컨테이너. 자식 Effect들을 `_count`×`_interval`로 실행. **연발 타이밍의 단일 소스**. 연발 = `RepeatEffect{ SpawnProjectiles(count=1), SpawnVFX }`로, 매 회차 **총알 1발 + 화염 1번**이 함께 나감.
  - ⚠️ Repeat 안의 `SpawnProjectiles`는 반드시 **`_count=1`**. 반복은 Repeat가 담당 — 안 그러면 `5×5=25`발.
- **`VfxAnchorSet`**(캐스터 프리팹의 MonoBehaviour) — SO는 씬 오브젝트를 못 담으므로(3-4), **씬 오브젝트가 부착점을 `(id, Transform)`으로 보관**하고 Effect는 런타임에 `ctx.CasterGO`에서 `id`로 조회. 부착점이 여러 개(총구·탄피 등)로 늘어도 id로 구분 → `GetComponentInChildren` 식 애매함 없음.
- 배선: **AR** `RifleRepeatEffect{count5}[ RifleProjectile(count1), RifleFireEffect ]`, **RL** `[ MissileProjectile(count1), MissileFireEffect ]`. 각 몹 프리팹에 `VfxAnchorSet(Muzzle→_firePoint)`.
- 기존 컨트롤러의 `FireOneBullet`/`PlayFireEffect`, `_rocketFireEffect`/`_bulletFire` 필드는 **아직 미삭제**(TODO). `_firePoint`는 `ctx.Object`로 여전히 사용 중이라 유지.

---

## 5. 함정 & 트러블슈팅 (실제로 겪은 것)

| 증상 | 원인 | 해결 |
|---|---|---|
| `List<AbilityData>` 컴파일 안 됨 | `using System.Collections.Generic;` 누락 (배열은 내장이라 됐음) | using 추가 |
| 빌드/이름 충돌 위험 | IDE 자동추가 `using` (NUnit.Framework, System.Configuration, NPOI, SixLabors.Fonts, VisualScripting.Internal) | 런타임 코드에서 제거 |
| 포격이 2발 중 1발만 | `AbilityData.Cooldown` 기본 1초 → 2번째 애니 이벤트가 쿨다운에 막힘 | 기본 공격 어빌리티는 **Cooldown=0** (발사 간격은 애니메이션이 통제) |
| 경고 데칼이 길쭉함 | `Quaternion.identity`로 스폰돼 데칼이 수직으로 섬 | 바닥에 눕히는 회전(`_warningEuler` 기본 `(90,0,0)`) |
| `AreaStrike`에서 씬 bomb point가 null | SO가 씬 오브젝트를 참조 못 함 | 경고/폭발을 프리팹으로 |
| 총구화염이 엉뚱한 위치/이전 위치에 뜸 | 풀 재사용 시 Play On Awake 미재생 + World 시뮬 잔여 파티클 | `SpawnVFX`가 위치 세팅 **후** `ps.Clear(true)+Play(true)` (옛 `PlayFireEffect`의 명시적 재생 복원) |
| 총구화염 SO가 씬 파티클을 못 잡음 | SO는 씬 오브젝트 참조 불가 | `VfxAnchorSet`으로 캐스터가 부착점 소유, Effect는 런타임에 id로 조회 |
| 연발이 `count²` 발 나감 | Repeat와 그 안의 `SpawnProjectiles` 둘 다 반복 | Repeat 자식의 `SpawnProjectiles._count=1` (반복은 Repeat 전담) |

---

## 6. 남은 작업 (TODO)

- [x] **플레이어/보스 이관** — 완료. 보스 → [BossMigration.md](BossMigration.md), 플레이어 → [CharacterMigration.md](CharacterMigration.md).
- [x] **크리티컬** — 완료. `BaseStat.BuildOutgoingDamage(attacker)`(캐릭터는 크리 롤 override) 도입, `SpawnProjectiles`/`SpreadProjectiles`가 호출. → [CharacterMigration.md](CharacterMigration.md) §3-2.
- [ ] **StatusSystem** — DoT/버프/디버프의 집. `ApplyStatus` Effect가 대상에 등록, 대상이 독립 tick.
- [ ] **인코딩** — 초기 Ability 파일 일부가 CP949(주석 깨짐). UTF-8로 재저장.
- [ ] **`TankBombController` 정리** — 이제 미사용. 경고/폭발 비주얼을 프리팹화 후 삭제.
- [ ] **AR/RL 죽은 코드 정리** — `SpawnVFX` 이관 후 미사용: `FireOneBullet`/`PlayFireEffect`, 프리팹의 `_rocketFireEffect`(RL)/`_bulletFire`(AR). (`_firePoint`는 `ctx.Object`로 사용 중이니 유지)
- [ ] **RL `Cooldown` 확인** — `MonsterRL_Ability`가 `Cooldown=1`. 애니 이벤트가 1초 내 두 번 오면 2번째 발사가 막힘(포격 2발 함정과 동일). 발사율 의도면 유지, 아니면 0으로.

---

## 관련 파일
- 캐스터/러너: `Assets/Scripts/Ability/{IAbilityCaster,AbilityRunner}.cs`
- 데이터/실행: `Assets/Scripts/Data/Ability/{AbilityData,EffectData}.cs`, `Assets/Scripts/Interface/IEffect.cs`
- Effect: `Assets/Scripts/Data/Ability/{DealDamage,SpawnProjectiles,AreaStrike,SpawnVFX,RepeatEffect}/*.cs`
- VFX 부착점: `Assets/Scripts/Ability/VfxAnchorSet.cs`, 원샷 반환: `Assets/Scripts/Pool/AutoReturnToPool.cs`
- 캐스터 구현: `BaseMonsterController`, `BaseCharacter`, `MonsterARController`, `MonsterRLController`, `MonsterTankController`
