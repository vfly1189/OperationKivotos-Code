# GameplayTag 레이어 — AbilitySystem 확장 (Lite GAS)

> **한 줄 요약** — 선형 캐스트 파이프라인(`TryCast → 이펙트 실행`)은 그대로 두고, **태그(GameplayTag) 레이어**를 얹어 발동을 게이팅하고 지속 상태를 관리한다. 태그 자체는 힘이 없고, **"쓰는 쪽(write)"과 "읽는 쪽(read)"이 공유 어휘로 이어질 때만** 의미를 갖는다.
>
> 관련: [README](README.md) · [RoleSystemDesign](RoleSystemDesign.md)

---

## 1. 왜 태그인가

언리얼 GAS의 핵심은 "이펙트를 순서대로 실행"이 아니라 **태그로 어빌리티가 서로를 게이팅/취소하는 상태 문법**이다. 기존 시스템은 이 절반(선형 시퀀서)만 있었다. 태그가 채우는 것:

- **자기 상태 게이팅**: "기절 중 발동 불가", "가드 중 특정기만" → `if`문이 아니라 **데이터(Blocked/Required)** 로.
- **상태 = 병렬 축**: FSM(배타적 모드: Idle/Move/Attack…)과 **직교**한다. 스턴·가드·표식은 어느 모드에도 겹칠 수 있어 FSM 상태로 넣으면 조합 폭발 → 태그가 이 병렬 축을 담당. **FSM은 그대로 두고** 태그를 추가한다.

---

## 2. 구조 — write 2 / read 2

```
WRITE ①  발동 중(캐스트 스코프) : AbilityRunner가 GrantsTags를 시전자에 부여 → finally에서 해제
WRITE ②  지속시간(효과)        : ApplyStatusTag → StatusRunner가 부여 후 duration 만료 시 해제
──────────────────────────────────────────────────────────────
READ  ①  발동 게이트           : AbilityRunner가 시전자 OwnedTags 검사 (Required 전부 + Blocked 없음)   ← 현재 구현됨
READ  ②  명중/피격 시점        : (예정) Health.TakeDamage가 방어자 태그를 읽어 반응 — 저스트가드 등
```

- **READ ②는 오늘 한 번 만들었다가 롤백**했다. 마크(방깎)에는 과설계였기 때문(§5). 단 **저스트가드에는 정당한 도구**라 내일 되살린다(§6).

### 핵심 타입

| 파일 | 역할 |
|---|---|
| `Ability/Tags/GameplayTagSO.cs` | 태그 = SO 에셋 한 장. 참조 동등성으로 비교(오타·해시충돌 0). `parent`는 계층 매칭용 예약. |
| `Ability/Tags/GameplayTagContainer.cs` | **카운트형 멀티셋**(`Dictionary<Tag,int>`). 같은 태그 다중 소스 안전(스턴 2중첩 → 하나 풀려도 유지). `HasAll`/`HasNone`은 빈 목록=제약 없음. |
| `Ability/Tags/GameplayTagOwner.cs` | 엔티티에 붙어 컨테이너 보관(`EnsureOn`). `OnDisable`에서 초기화(풀 이월 방지). |
| `AbilityData`의 태그 3필드 | `RequiredTags` / `BlockedTags` / `GrantsTags` |
| `AbilityRunner.TryCast` | 게이트: 쿨다운 → **태그 검사** → 쿨다운 시작 → GrantsTags 부여 → 이펙트 → `finally` 해제 |
| `StatusRunner.ApplyTimedTag` | 지속시간 태그 부여+자동 만료. 기존 `ApplyTimed`(modifier)와 대칭. |
| `ApplyStatusTag`(Effect) | 대상에 태그 부착 (Self/Target/Party). `ApplyStatModifier`의 쌍둥이. |

### 게이트 실행 순서 (`AbilityRunner.TryCast`)
```
쿨다운 검사 → 태그 게이트(HasAll(Required) && HasNone(Blocked))
  → 쿨다운 시작 → GrantsTags 부착 → CastTime → 이펙트 순차 → finally: GrantsTags 해제
```
- **게이트 실패 시 쿨다운 미소모** (StartCooldown이 게이트 뒤).
- **취소 안전**: GrantsTags는 `try/finally`라 이펙트 중단돼도 반드시 해제 → 누수 0.

---

## 3. 오늘 만든/바꾼 파일

**신규**
- `Assets/Scripts/Ability/Tags/{GameplayTagSO, GameplayTagContainer, GameplayTagOwner}.cs`
- `Assets/Scripts/Data/Ability/Effects/Status/ApplyStatusTag.cs`
- 태그 에셋: `State.Guarding / State.Marked / State.Stunned / State.Casting`
- 이펙트 인스턴스: `GuardApplyStatusTag`, `BuffApplyStatusTag` (ApplyStatusTag)

**수정**
- `AbilityData.cs` — 태그 3필드 + `AbilityContext.Tags`(+`CloneAt`)
- `AbilityRunner.cs` — 게이트 삽입 + **전체 UTF-8 재작성**(기존 CP949 깨짐 TODO 해결)
- `StatusRunner.cs` — `ApplyTimedTag` 추가(만료·`OnDisable` 정리 포함)
- `BaseCharacter.cs` — `Init`에서 `GameplayTagOwner.EnsureOn` + `TryCast` ctx에 `Tags`
- `Hoshino_Attack_Ability.asset` — `BlockedTags=[State.Guarding]`

**롤백(생성 후 삭제)** — `MarkedVulnerability.cs`, `IIncomingDamageModifier.cs`, `Health.cs` seam (§5)

---

## 4. 현재 상태 — 인프라는 완성, 실사용은 0 (의도된 중간 지점)

프로젝트 전체 감사 결과:

| | 현황 |
|---|---|
| 태그 **리더** | 딱 하나 — `Hoshino_Attack_Ability.BlockedTags=[State.Guarding]` (매달린 참조) |
| 태그 **라이터** | `GuardApplyStatusTag`·`BuffApplyStatusTag` 둘 다 **고아**(어떤 어빌리티도 참조 안 함) |

→ **살아있는 producer→consumer 쌍이 0.** 유일한 리더는 그 태그를 찍는 라이터가 없어 영영 발동 안 함. 태그 인프라는 다 깔렸으나 **아직 아무것도 안 함.** 내일 CC/저스트가드로 첫 실사용 쌍을 만든다(§6).

### Breaker는 태그 없이 이미 동작 (검증됨)
브레이커 룰("적중 시 방어력 감소, 끝")은 **태그 없이 on-hit modifier로 완결**:
```
BreakerAbility → BreakerProjectile(SpawnProjectiles, 10발)
  → BulletTrail_Breaker.prefab
      → BulletController._onHitEffects: [ BreakerStatModifier ]
          = ApplyStatModifier(Defense, −30%, 6s, scope=Target)
```
`BulletController.ApplyOnHitEffects`가 **명중 순간** `Target=hitTarget`으로 이펙트를 실행 → 대상 `StatusRunner`가 방어력 −30% 유지 → 그 적을 때리는 모든 딜이 자동 증가. `ctx.Target=null`(캐릭터는 조준만) 문제를 **투사체 on-hit이 해결**한다.

---

## 5. 설계 결정 로그

- **태그 = SO**(문자열/enum 아님): 참조 동등성(빠름·오타 0), 인스펙터 드래그, SO 테마 일관.
- **카운트형 멀티셋**: 다중 소스 스턴 안전. `HashSet` 아님.
- **`ApplyStatModifier`와 `ApplyStatusTag`는 분리 유지**: 서로 다른 서브시스템(Stat 숫자 vs 태그)을 건드리는 다른 동사. 자주 **따로** 쓰인다(마크=modifier만, 가드게이트=태그만). 조합은 Ability의 Effect 리스트가 담당. 반복되는 짝만 나중에 목적형 래퍼로.
- **MarkedVulnerability 롤백**: "표식→피해증폭"을 별도 %증폭기로 만들었으나, 마크의 페이오프는 **방깎 modifier가 데미지 공식(`Amount−Defense`)으로 이미 자동 제공** → 중복. 태그도 마크엔 불필요(순수 방깎). 과설계라 제거.
- **FSM은 불변**: 배타적 모드(Idle/Move/Attack/E/Q/Death)는 FSM, 병렬 조건(Stunned/Guarding)은 태그. 합치지 않는다.

---

## 6. 내일 계획 — 태그를 실제로 살리기 (CC + 저스트가드)

**목표**: 첫 live producer→consumer 쌍을 만들어 태그 인프라를 정당화한다. 셋 다 **기존 아키텍처로 가능**하며, 새로 필요한 훅은 2개뿐.

### A. 보스 기절 부여 → 플레이어 기절 상태
- **A1. 보스가 `State.Stunned`를 플레이어에 부여** — *새 메커니즘 불필요.*
  - 투사체 패턴: 보스 탄 프리팹의 `BulletController._onHitEffects`에 `ApplyStatusTag(State.Stunned, scope=Target, duration)` 추가. (브레이커 방깎과 **동일 경로** — 이미 존재.)
  - AoE 패턴: `AreaStrike`가 on-hit 이펙트를 지원하는지 확인 → 미지원이면 `OverlapSphere` 대상에 `ApplyStatusTag` 실행하도록 소폭 확장.
- **A2. 플레이어 기절 처리**
  - 어빌리티 차단: 평타/Q/E의 `BlockedTags=[State.Stunned]`. → **게이트로 즉시 됨(신규 코드 0).**
  - 이동 차단 + 기절 애니: 게이트는 어빌리티만 막고 **이동은 어빌리티가 아님.** → **[새 훅 ①] Movement/FSM이 `State.Stunned`를 읽어야 함.**
    - 방법 후보: 태그 부여 시 FSM을 `Stunned` 상태로 강제 / `BaseCharacter.Move`에서 태그 검사. (FSM×Tag 이음새 — RoleSystemDesign에서 논의한 "모드 태그 발행"의 첫 실사례)
  - 기절 중엔 무적 아님(피격 정상).

### B. 저스트가드 → 몇 초 무적
- **B1. 가드가 `State.Guarding` 부여** — 지난번 끊긴 링크 연결: `GuardAbility.Effects`에 `GuardApplyStatusTag` 넣기(고아 해제).
- **B2. 가드 타이밍 중 피격 → 무적 부여** — **[새 훅 ②] 방어자측 데미지 시점 태그 읽기.**
  - 경로: `Health.TakeDamage`에서 방어자의 `State.Guarding`(또는 좁은 just 윈도우) 확인 → 피해 무효화 + 무적 부여. **← 오늘 롤백한 `IIncomingDamageModifier` seam이 여기선 정확한 도구.** (마크엔 과설계였지만 저스트가드엔 정당 — 재도입.)
  - just 윈도우: 가드 시작 시 `State.JustGuard`(예: 0.2s 짧은 창)를 함께 부여하고, **그게 있을 때만** 무적 성립. → 태그 2단(지속 `Guarding` + 짧은 `JustGuard`).
  - 무적: 기존 `Health.IsInvincible`를 N초 set(StatusRunner 타이머) 또는 `State.Invincible` 태그.

### 필요한 신규 훅 요약
| # | 훅 | 이유 | 근거 |
|---|---|---|---|
| ① | Movement/FSM이 `State.Stunned` 읽기 | 게이트는 이동을 안 막음 | FSM×Tag 이음새 |
| ② | `Health.TakeDamage`가 방어자 태그 읽기 | 저스트가드는 피격 시점 반응 | READ ② 재도입(정당화됨) |

### 완료 기준
- 보스 특정 패턴 피격 → 플레이어 기절(이동·발동 다 멈춤) → 만료 후 복귀.
- 가드 타이밍 피격 → 무효화 + 무적 창 → 만료.
- 감사 재실행 시 **live 태그 쌍 ≥ 2**(Stunned 게이트, JustGuard 무적).
