# 플레이어 캐릭터 전투 이관 (AbilitySystem — Part 3)

> **한 줄 요약** — 캐릭터마다 `BaseCharacter`를 상속해 `PerformAttackAction`/`FireOneBullet`을 **하드코딩**하던 12종 플레이어 유닛을, Part 1(몹)·Part 2(보스)에서 만든 **AbilitySystem(AbilityData + Effect 조합)** 위로 이관했다. "캐릭터 = 클래스 1개" 안티패턴을 걷어내 **12종 중 10종의 서브클래스를 삭제**하고, 캐릭터 정체성을 **프리팹 + AbilityData 조합**으로 옮겼다. 이로써 **보스·몹·플레이어 3갈래 발동 경로가 단일 `TryCast`로 통합**된다(포트폴리오 핵심 주장 완성). Part 1 → [README.md](README.md), Part 2 → [BossMigration.md](BossMigration.md).

| 축 | 이전(캐릭터별·하드코딩) | 현재(통합 AbilitySystem) |
|---|---|---|
| 공격 정의 위치 | 각 유닛 클래스의 `PerformAttackAction`/`FireOneBullet` | `AbilityData`(SO) + `Effect[]` |
| 캐릭터 = ? | **클래스 1개**(`ArisCharacter`, `HoshinoCharacter` …) | **프리팹 + AbilityData** (단일 `BaseCharacter` 컴포넌트) |
| 발사 패턴 차이 | 유닛 클래스마다 for문·코루틴 재구현 | **재사용 Effect**(`SpreadProjectiles` 등) + asset 값 |
| 데미지/크리 | `BaseCharacter.CalculatedDamage`가 유닛 경로마다 호출 | `Stat.BuildOutgoingDamage`(크리 롤) 단일 소유 |
| 발동 시점 | 애니 이벤트 → 유닛 하드코딩 메서드 | 애니 이벤트 → `TryUseAbility(0)` → `Effect[]` |
| 새 캐릭터 추가 | 새 서브클래스 작성 | 대부분 **코드 0줄**(프리팹 + AbilityData) |

---

## 1. 배경 — 마지막 하드코딩 캐스터

Part 1/2에서 몹·보스는 이관됐지만 **플레이어만 옛 방식**으로 남아 있었다.

- `BaseCharacter`는 `IAbilityCaster`를 **선언만** 하고 껍데기였다: `TryUseAbility(int)` 비어 있음, `_abilityRunner`는 **`new` 조차 안 됨**(쓰면 NRE), 기본 `PerformAttackAction()`도 빈 메서드.
- 유닛 12종이 각자 `PerformAttackAction`/`FireOneBullet`을 override해 하드코딩:
  - **단발**(Shiroko·Ako·Aru·Asuna·Iori·Karin·Serika) — `FireOneBullet` 1발.
  - **연사**(Hina·Nonomi·Toki) — `RapidFireAsync` for문(발수/간격 SerializeField).
  - **샷건**(Hoshino) — 부채꼴 for문.
  - **차징**(Aris) — 차지 이펙트 생성 → 코루틴 딜레이 → 발사. 전용 투사체 `ArisBulletController`.

### 무엇이 문제였나
1. **"캐릭터 = 클래스 1개"** — Part 1이 없앤 "스킬 = 클래스" 안티패턴의 캐릭터 버전. 값(탄·발수)만 다른데 클래스로 갈라짐.
2. **발동 경로 3갈래 잔존** — 보스·몹은 `TryCast`인데 플레이어만 유닛 메서드. "누가 쓰든 같은 파이프라인"이 미완.
3. **크리 계산 중복·유실 위험** — 크리 롤이 `BaseCharacter.CalculatedDamage`에 있어, Effect로 이관하면 크리가 사라질 판.
4. **투사체 타입 강결합** — `SpawnProjectiles`가 `GetComponent<BulletController>()` 하드코딩. Aris의 `ArisBulletController`(다른 `Init` 시그니처)는 못 담음.

→ 목표: **캐릭터 C#를 `BaseCharacter` 단일 컴포넌트로 수렴시키고, 캐릭터 차이는 데이터(프리팹 + AbilityData)로.**

---

## 2. 새 구조 — 캐스터가 된 BaseCharacter

```
[입력]  PlayerController        키/마우스 → 의미 동사(Move/Attack/UseSkill…) — 캐릭터 종류 무지
   ▼
[상태]  CharacterStateMachine  Attack 상태 진입 → 공격 애니메이션 재생
   ▼
[발동 비트]  애니메이션 이벤트  OnAttackEvent → PerformAttackAction
   │            = BaseCharacter 기본 => TryUseAbility(0)      ← 발동 시점은 '애니 비트'
   ▼
[캐스터]  BaseCharacter.TryUseAbility(id)
   │   ctx 조립(Caster/CasterGO/CasterStat/Object=_firePoint/TargetPoint) → _abilityRunner.TryCast
   ▼
[기계]  AbilityRunner → AbilityData → Effect[]   (몹·보스와 동일)
```

핵심 원칙: **입력이 어빌리티를 직접 쏘지 않는다.** 입력은 의미 동사(`Attack()`)만 부르고, 실제 발동은 **애니메이션 이벤트**가 `TryUseAbility`를 호출한다. 이래야 총구화염·판정·타이밍이 연출과 항상 일치하고, 몹(애니 이벤트)·보스(Timeline Signal)와 원리가 같아진다.

### 2-1. "스위치" — BaseCharacter 기본 공격 채우기
```csharp
// BaseCharacter.cs — 이 한 줄이 '서브클래스 없이 공격'을 가능케 함
protected virtual void PerformAttackAction() => TryUseAbility(0);
```
- 이전엔 빈 메서드 → 유닛이 override해야만 공격됨(= 서브클래스 강제).
- 이제 기본이 `_abilities[0]` 발동 → **프리팹에 AbilityData만 꽂으면 서브클래스 불필요.**
- 차징 등 특수 발동이 필요한 유닛만 override(현재는 아무도 안 함 — 차징도 데이터로 표현).

### 2-2. TryUseAbility 본체
```csharp
public void TryUseAbility(int id)
{
    if (Stat == null || Stat.IsDead) return;
    if (id < 0 || id >= _abilities.Count || _abilities[id] == null) return;

    var token = _actionCts?.Token ?? CancellationToken.None;
    var ctx = new AbilityContext {
        Caster = this, CasterGO = gameObject, CasterStat = Stat,
        Object = _firePoint,                           // 총구 = 스폰 기준
        TargetPoint = transform.position + transform.forward,
    };
    _abilityRunner.TryCast(_abilities[id], ctx, token).Forget();
}
```
`_abilityRunner`는 이제 `= new AbilityRunner()`로 초기화(미초기화 NRE 버그 수정).

---

## 3. 오늘 확립한 설계 원칙 (핵심)

### 3-1. 변주의 축은 '캐릭터'가 아니라 'Effect/투사체'
"발사 패턴이 다른 캐릭터가 여럿" ≠ 캐릭터 클래스 여럿. 확장점은 **3개인데 전부 재사용 가능하고 캐릭터당이 아니다**:

| 무엇이 다른가 | 어디로 | 캐릭터 클래스? |
|---|---|---|
| 탄 모양·발수·간격·배율 | **asset 값**(`SpawnProjectiles`/`RepeatEffect`) | ❌ |
| 발사 기하(부채꼴/방사) | **Effect 클래스**(`SpreadProjectiles`) — 전 캐릭터 공유 | ❌ |
| 투사체 비행(유도/관통) | **`IProjectile` 컨트롤러 클래스** — 전 캐릭터 공유 | ❌ |

예) Hoshino 샷건 = 새 `SpreadProjectiles` **한 개** 만들면, 이후 산탄 캐릭터는 `{angle/count/bullet}` **값만** 바꿔 공유. 캐릭터 클래스 0개.

### 3-2. 크리티컬은 Stat이 소유 — `BuildOutgoingDamage`
발사 데미지 조립을 컨트롤러에서 **Stat으로** 옮겼다. 크리 롤이 한 곳에 있고, 모든 발사 경로(플레이어·몹·보스)가 자동으로 크리를 얻는다.
```csharp
// BaseStat: 기본 = 크리 없음 (몹이 그대로 사용)
public virtual DamageInfo BuildOutgoingDamage(GameObject attacker)
    => new DamageInfo(Attack.Value, attacker, false);

// CharacterStat: override = 크리 롤 (옛 BaseCharacter.CalculatedDamage 이관)
public override DamageInfo BuildOutgoingDamage(GameObject attacker) {
    bool isCrit = Random.value < CritRate.Value;
    float dmg   = isCrit ? Attack.Value * CritDamage.Value : Attack.Value;
    return new DamageInfo(dmg, attacker, isCrit);
}
```
`SpawnProjectiles`/`SpreadProjectiles`가 `ctx.CasterStat.BuildOutgoingDamage(ctx.CasterGO)`를 호출 → **크리 자동 반영, 몹은 회귀 없음**.

### 3-3. 발사 방향 = '몸통(조준)', 위치 = '총구'
옛 코드는 위치만 총구에서 따오고 **방향은 캐릭터 몸통**(`transform.rotation`)을 강제했다. 이관 초기 `SpawnProjectiles`가 **총구 Transform의 회전**(`ctx.Object.rotation`)을 써서 버그가 났다.
```csharp
// 위치=총구, 방향=캐스터 몸통. 총구 본(_firePoint)은 무기 본에 붙어
// 몸통과 다른 각으로 돌아가 있거나 발사 애니로 흔들리므로 회전 기준으로 쓰면 안 된다.
Instantiate(_bulletPrefab, ctx.Object.position, ctx.CasterGO.transform.rotation);
```
→ 5-1(Ako 직각), 5-2(연사 흔들림) 해결.

### 3-4. 배치 회전도 Anchor에서 — `_useCasterRotation`
총구화염 VFX(`SpawnVFX`)는 배치를 전부 공용 `Anchor`에 위임하므로, 개별 Effect가 아니라 **Anchor에 토글**을 추가했다. 켜면 위치는 소스(총구) 유지, **회전만 캐스터 몸통** 기준. 총알과 같은 규칙을 VFX에도 한 곳에서 적용.
```csharp
// Anchor.Resolve 내부
if (_useCasterRotation && ctx.CasterGO != null)
    baseRot = ctx.CasterGO.transform.rotation;
```
기본 false → 기존 보스·몹 앵커 무영향.

### 3-5. 입력 흐름 — 입력은 '의도'만
- **좌클릭(평타)** = hold 폴링 → `Attack()` → 상태 → 애니 이벤트 → `TryUseAbility(0)`.
- **방향키** = `Move()`. 어빌리티 아님.
- **Q(궁극기)** = Timeline 재생 → (설계) Signal → `CastAbility(AbilityData)` (보스 배선 재사용). *현재는 기존 컷신 방식 유지, 어빌리티 편입은 후속.*
- **E(가드)** = 상태 메커니즘(무적/패링), 어빌리티 아님. 반격 시에만 `TryUseAbility(counterId)`. *현재 미구현, 후속.*

### 3-6. 서브클래스는 '최후 탈출구'
castable 어빌리티·데이터·투사체 컨트롤러로도 표현 안 되는 **진짜 캐릭터 고유 C#**만 서브클래스로 남긴다. 현재 2종:
- **Hoshino** — `OnESkillEvent`(E스킬 발사). 평타(샷건)는 이관됨.
- **Serika** — `OnCutsceneEnded`(컷신 종료 커스텀). 평타는 이관됨.

`BaseCharacter`는 `sealed`로 막지 않음 — 이 탈출구를 위해.

---

## 4. 새/변경 프리미티브

| 타입 | 파일 | 역할 | 상태 |
|---|---|---|---|
| `IProjectile` (신규) | `Interface/IProjectile.cs` | 발사체 공용 계약 `Init(DamageInfo, GameObject)`. `SpawnProjectiles`가 구체 타입(Bullet/ArisBullet…)을 몰라도 됨 | — |
| `SpreadProjectiles` (신규) | `Effects/Spawn/SpreadProjectiles.cs` | 한 총구에서 N발 부채꼴 동시 발사(샷건). `SpawnProjectiles`(직선)와 로직만 다른 payload | 무상태 |
| `DelayEffect` (신규) | `Effects/Flow/DelayEffect.cs` | Effect 체인 **중간** 대기. CastTime(맨 앞 대기)과 달리 "먼저 뭔가 → 대기 → 발사" 표현 가능 | 무상태 |
| `BuildOutgoingDamage` (신규) | `Stat/BaseStat.cs`·`CharacterStat.cs` | 발사 데미지 조립 + 크리 롤(캐릭터만) | — |
| `Anchor._useCasterRotation` (신규 토글) | `Data/Ability/Anchor.cs` | 위치는 소스, 회전만 캐스터 몸통 | — |
| `SpawnProjectiles` (변경) | `Effects/Spawn/SpawnProjectiles.cs` | `IProjectile` + `BuildOutgoingDamage`(크리) + 방향=몸통 | 무상태 |
| `BaseCharacter` (변경) | `Controllers/Character/BaseCharacter.cs` | 러너 초기화 + `TryUseAbility` 본체 + 기본 `PerformAttackAction => TryUseAbility(0)` | — |
| `ArisBulletController` (변경) | `Controllers/Projectile/ArisBulletController.cs` | `IProjectile` 구현 + **OnTriggerEnter 데미지 실제 적용**(스텁 버그 수정) | — |

---

## 5. 캐릭터별 이관 결과 (12종)

| 유형 | 캐릭터 | 어빌리티 구성 | 서브클래스 |
|---|---|---|---|
| 단발 | Shiroko·Ako·Asuna | `Common_SingleShot_Ability` = `[ Common_SingleBullet, Common_SingleShot_VFX ]` | **삭제** |
| 단발(로켓) | Aru·Iori·Karin | `Common_SingleRocket_Ability` = `[ Common_SingleRocket, Common_SingleRocket_VFX ]` | **삭제** |
| 단발(컷신 유지) | Serika | `Common_SingleShot_Ability` | 얇게 유지(컷신) |
| 연사 | Hina(8)·Nonomi(10)·Toki(10) | `{name}_Rapid` = `RepeatEffect{ Common_SingleBullet }` | **삭제** |
| 샷건 | Hoshino | `Hoshino_Attack` = `[ Hoshino_Spread, Hoshino_FireVFX ]` | 얇게 유지(E스킬) |
| 차징 | Aris | `Aris_Attack` = `[ Aris_Charge_VFX, Aris_Delay(1s), Aris_Projectile_Spawn ]` | **삭제** |

- **재사용 증명**: 단발 8종이 공용 payload(`Common_SingleBullet`)를 공유하고, 연사 3종은 그걸 `RepeatEffect`로 감싸 **count만** 다름. 신규 캐릭터 이관 = **에셋 조합, 코드 0줄.**
- **데미지는 캐릭터별 Stat**이 결정하므로(같은 ability를 공유해도) 각자 다른 데미지·크리가 나감.

### 5-1. 차징(Aris) — CastTime이 아니라 DelayEffect
차징은 "차지 VFX 먼저 → 1초 대기 → 발사"라 **CastTime(맨 앞 대기)로는 표현 불가**(VFX가 대기 뒤에 떠버림). 그래서 `DelayEffect`를 체인 중간에 끼웠다:
```
Aris_Attack = [ SpawnVFX(charge, 총구 뒤 0.285, 몸통회전, attach),
                DelayEffect(1s),
                SpawnProjectiles(Aris_Projectile) ]
```
- 차지 VFX는 발사 시점에 사라져야 하므로 `Aris_Charging` 프리팹에 **`AutoReturnToPool(1s)`** 부착(옛 코드의 "발사 직전 Destroy" 대체).
- `ArisBulletController`는 **OnTriggerEnter가 데미지를 안 넣던 스텁**이었음 → `IDamageable`에 실제 적용하도록 수정(이관 김에 기존 버그 수정).
- 상태 취소 시 `DelayEffect`가 `_actionCts` 토큰으로 취소돼 발사 안 됨(옛 상태체크와 동치).

---

## 6. 서브클래스 안전 삭제 절차

`.cs`만 지우면 프리팹이 깨진다(프리팹이 서브클래스를 **guid로 참조** → Missing Script → BaseCharacter 기능 전체 소실). 순서를 지켜야 한다:

```
1. (전제) BaseCharacter 기본 PerformAttackAction => TryUseAbility(0)  ← 서브클래스 없이 공격 가능케
2. 유닛별 AbilityData 만들어 프리팹 _abilities[0]에 배선  (E스킬 등 남은 로직도 먼저 이관)
3. 프리팹 컴포넌트 m_Script guid → BaseCharacter(340725b7…)로 재지정
4. 서브클래스 .cs + .cs.meta 삭제
5. dangling 검사: 삭제 guid가 prefab/scene/asset에 남지 않았는지
```
- 재지정 시 서브클래스 전용 필드(`_shotCount`, `_chargeEffect` 등)는 Unity가 자동 드롭(무해).
- 결과: **10종 삭제**(Shiroko·Ako·Aru·Asuna·Iori·Karin·Hina·Nonomi·Toki·Aris), 2종 유지(Hoshino·Serika).

---

## 7. 함정 & 트러블슈팅 (실제로 겪은 것)

| 증상 | 원인 | 해결 |
|---|---|---|
| 첫 발동에 NRE | `BaseCharacter._abilityRunner`가 `new` 안 됨(선언만) | `= new AbilityRunner()` |
| 서브클래스 .cs 삭제하니 캐릭터 통째로 먹통 | 프리팹이 서브클래스를 guid로 참조 → Missing Script | 먼저 `m_Script`를 BaseCharacter로 재지정 후 삭제 |
| 재지정했더니 공격 안 나감 | BaseCharacter 기본 `PerformAttackAction`이 빈 메서드 | 기본을 `=> TryUseAbility(0)`로 |
| 크리가 사라짐 | `SpawnProjectiles`가 `Attack.Value`만 씀(크리 없음) | `Stat.BuildOutgoingDamage`(크리 롤) 도입, Effect가 호출 |
| Aris 탄이 `SpawnProjectiles`에 안 잡힘 | `ArisBulletController.Init(float,…)` 시그니처 다름 | `IProjectile`로 통일, `SpawnProjectiles`는 인터페이스로 Init |
| **Ako 총알이 총구와 직각으로 발사** | `SpawnProjectiles`가 총구 본 회전(`ctx.Object.rotation`) 사용. Ako 무기 본(`Bip001_Weapon`)이 180° 뒤집힘 | 방향을 `ctx.CasterGO.transform.rotation`(몸통)으로 |
| **연사 일부 총알만 각 지어 나감** | 총구 본이 발사 애니로 흔들려 매 발 회전이 다름 | 방향을 몸통 회전으로(총구 흔들림 면역) |
| Ako 총구화염 VFX도 90° 돌아감 | `SpawnVFX`가 Anchor(Muzzle)로 총구 본 회전 사용 | `Anchor._useCasterRotation` 토글, 총구화염 에셋에 켬 |
| Ako의 fire_01을 옮겨도 위치 안 맞음 | fire_01이 **축이 180° 뒤집힌 `Bip001_Weapon` 본** 밑 → 인스펙터 로컬 이동이 월드에서 반대로 감(Iori는 본 회전 identity라 정상) | Scene 뷰 **Global 핸들**로 이동 or fire_01을 정상 프레임 노드로 재부모화 |
| Toki 연사 발수가 예상과 다름 | 프리팹 `_shotCount`(10)가 C# 기본값(4) override | asset `_count`를 프리팹 실제값(10)에 맞춤 |
| Aris 차징 VFX가 안 사라짐(누수) | `SpawnVFX`는 스폰만·대기 안 함 | 차지 프리팹에 `AutoReturnToPool(1s)` |
| Aris 탄이 데미지를 안 줌 | `ArisBulletController.OnTriggerEnter`가 로그만 찍는 스텁 | `IDamageable`에 실제 `TakeDamage` |
| 총구화염이 총구보다 뒤/앞에 뜸 | 스폰 프리팹 내부 파티클의 로컬 offset(예: RifleFireEffect z:-0.544) | 프리팹 내부 offset이거나 `Anchor._localOffset`로 상쇄 |

---

## 8. 결과 / 성과

- **플레이어 12종 전부 데이터 주도** — 캐릭터 정체성이 프리팹 + AbilityData로. 10종 서브클래스 삭제.
- **발동 경로 3갈래(보스/플레이어/몹) → 단일 `TryCast` 완전 통합.** "누가 쓰든 같은 파이프라인" 성립(포트폴리오 핵심 주장).
- **크리 경로 확립** — `BuildOutgoingDamage`로 모든 발사가 크리를 얻음(문서 Part 1·2의 TODO 해소).
- **재사용 라이브러리 확장** — `SpreadProjectiles`·`DelayEffect`·`IProjectile`·`Anchor._useCasterRotation`. 이후 캐릭터/스킬은 대부분 조합.
- **잠재 버그 2건 수정** — `_abilityRunner` 미초기화, `ArisBulletController` 데미지 스텁.

---

## 9. 남은 작업 (TODO)

- [ ] **Q(궁극기) 어빌리티 편입** — 현재 컷신(`skillTimeline`) 방식 유지. 보스처럼 Timeline Signal → `CastAbility(AbilityData)` 배선.
- [ ] **E = 저스트가드** — 상태 메커니즘(무적/패링 윈도우)으로 구현, 패링 성공 시 반격 어빌리티.
- [ ] **Hoshino E스킬 이관** — `OnESkillEvent`를 어빌리티로. 완료 시 Hoshino 서브클래스도 제거 가능.
- [ ] **죽은 코드 정리** — `BaseCharacter.FireOneBullet`/`CalculatedDamage`/`PlayFireEffect`는 아직 Hoshino(E스킬)가 `CalculatedDamage` 사용 → E스킬 이관 후 제거.
- [ ] **총구화염 위치 튜닝** — 캐릭터별 총구 본 리그 차이(뒤집힌 프레임)로 위치가 어긋나는 경우 `_localOffset` 또는 fire_01 재부모화.
- [ ] **Aris 차징 위치** — `_localOffset z:-0.285`는 근사값.

---

## 관련 파일
- 캐스터: `Controllers/Character/BaseCharacter.cs` (+ 유지 서브클래스 `Units/{Hoshino,Serika}`)
- 스탯/크리: `Data/Stat/{BaseStat,CharacterStat}.cs`
- 신규 Effect: `Data/Ability/Effects/Spawn/SpreadProjectiles.cs`, `Data/Ability/Effects/Flow/DelayEffect.cs`
- 변경 Effect/Anchor: `Data/Ability/Effects/Spawn/SpawnProjectiles.cs`, `Data/Ability/Anchor.cs`
- 투사체: `Interface/IProjectile.cs`, `Controllers/Projectile/{BulletController,ArisBulletController}.cs`
- 데이터 에셋: `Resources_moved/Data/Ability/Character/{_Common,Hina,Nonomi,Toki,Hoshino,Aris}/*.asset`
- Part 1(몹): [README.md](README.md) · Part 2(보스): [BossMigration.md](BossMigration.md)
