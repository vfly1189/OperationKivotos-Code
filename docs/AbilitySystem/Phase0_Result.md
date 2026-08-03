# 플레이어 재설계 Phase 0 — 안전 이음새 (완료)

> **한 줄 요약** — 본격 리팩터(Phase 1: 게이팅 단일화) 이전에, **리스크 0**짜리 사전 작업 두 가지를 처리했다.
> ① `AbilityRunner`가 시각을 `IClock`으로 주입받도록 이음새를 냈고(동작 100% 불변), ② `PlayerController`의 입력 구독 누수(재진입 시 중복 발동)를 수정했다.
>
> 배경/전체 계획: [Player_Refactor_Blueprint.pdf](Player_Refactor_Blueprint.pdf) · 선행 개념: [Concept_Overview.pdf](Concept_Overview.pdf) · [Flow_Walkthrough.pdf](Flow_Walkthrough.pdf)

---

## 왜 Phase 0인가

청사진의 핵심은 **게이팅(쿨·코스트·태그)을 `AbilityRunner` 하나로 모으는 것**(Phase 1)이다. 그 전에, 그 리팩터를 안전하게 만들어 줄 사전 작업 두 개가 있었다. 둘 다 **동작을 바꾸지 않거나(이음새), 명백한 버그 수정**이라 독립적으로 먼저 커밋할 수 있다.

- **왜 지금 IClock인가** — Phase 1에서 러너가 쿨다운의 유일한 권위가 된다. 그때 시각 소스가 `Time.time` 하드코딩이면 테스트도, 훗날 선택적 시간 제어(몹만 슬로우 등)도 막힌다. 미리 **읽기전용 시각 제공자**로 한 겹 빼두되, 기본 구현이 `Time.time`을 그대로 돌려주게 해 **동작은 완전히 동일**하게 유지한다.
- **왜 지금 Dispose 수정인가** — Phase와 무관한 독립 버그다. 발견된 김에 같은 안전 커밋에 묶었다.

> 참고 — "전역 `TimeManager`를 두지 않는 이유": 쿨다운 **상태**는 per-caster(러너)가 소유하고, 하나로 모을 수 있는 건 **시각 소스**뿐이다. 그래서 매니저가 아니라 주입 가능한 `IClock` 이음새로 처리한다.

---

## 변경 내용

### ① `IClock` 이음새 (신규 + 러너 주입)

**신규** `Assets/Scripts/Ability/IClock.cs`
```csharp
public interface IClock { float Now { get; } }

public sealed class UnityClock : IClock
{
    public static readonly UnityClock Instance = new UnityClock();
    public float Now => Time.time;      // 기본: Time.time 그대로
}
```

**수정** `Assets/Scripts/Ability/AbilityRunner.cs` — 생성자 주입 + `Time.time` → `_clock.Now`
```csharp
private readonly IClock _clock;
public AbilityRunner(IClock clock = null) => _clock = clock ?? UnityClock.Instance;

public bool IsOnCooldown(AbilityData a)
    => _cooldownEnd.TryGetValue(a, out float end) && _clock.Now < end;   // 이전: Time.time < end
private void StartCooldown(AbilityData a)
    => _cooldownEnd[a] = _clock.Now + a.Cooldown;                        // 이전: Time.time + a.Cooldown
```

- 기존 호출부 `new AbilityRunner()`(`BaseCharacter`, `BaseMonsterController`)는 **선택 인자라 그대로 컴파일**되고, 기본 `UnityClock.Instance`를 쓴다 → **동작 불변**.
- `UnityClock`은 무상태라 공유 단일 인스턴스로 충분(할당 0).

### ② `PlayerController` 입력 구독 누수 수정

`Assets/Scripts/Controllers/Character/PlayerController.cs`

- **문제**: `Dispose`가 `OnMoveInput`/`MouseAction`만 해제하고, `RegisterAction`으로 건 `Info`/`Inventory`/`SkillQ`/`SkillE`/`Interact` 콜백은 **미해제**. `PlayerController` 재생성(예: GameScene 재진입) 시 `_actionMap[intent] += callback`이 누적 → **Q/E가 한 입력에 여러 번 발동**.
- **수정**:
  - `RegisterInputEvents`에서 액션 인텐트도 `Unregister → Register`로 **멱등화**(기존 이동/마우스와 동일 패턴).
  - `Dispose`에서 5개 인텐트를 **대칭 해제**.
- 근거: `InputManager.UnregisterAction`은 `_actionMap[intent] -= callback`이라 **메서드 그룹으로 정확히 짝**이 맞는다.

---

## 검증

- **IClock**: 기본 경로가 `Time.time`을 그대로 반환 → 쿨다운 계산식·값 변화 없음. 호출부 시그니처 무변경(선택 인자). **동작 동등**(behavior-preserving by construction).
- **Dispose 누수**: GameScene 재진입 후 Q/E 1회 입력 → 1회만 발동(중복 0). 등록 멱등화로 이중 진입에도 안전.
- ⚠️ Unity 에디터 컴파일/플레이 실측은 별도로 돌려 확인 필요(이 커밋은 소스 레벨 안전성까지).

---

## 다음 — Phase 1 (게이팅 단일화)

이 이음새 위에서 본 작업을 진행한다: `CharacterStat`의 Q/E 쿨 → 러너로, `CharacterCombat` 평타레이트 → `AbilityData.Cooldown`, `Stat`은 에너지(자원)만, 러너에 코스트 위임 seam + UI 읽기 API, `PartyManager.TickCooldowns` 제거. → [Player_Refactor_Blueprint.pdf](Player_Refactor_Blueprint.pdf) §04 참조.
