# 입력 시스템 리팩토링 계획 — 의도 추상화 + 입력 컨텍스트 + 선입력 버퍼

> 목표: 지금의 "InputManager 단일 폴링 + magic string 이벤트" 구조를
> **의도(Intent) 추상화 → 입력 컨텍스트(Action Map + Stack) → 선입력 버퍼** 로 옮기고,
> 슬롯 입력을 **기존 AbilitySystem에 자연스럽게 물린다.**
>
> 대전제 1: **빅뱅 재작성 금지.** `Managers.Input` 파사드와 기존 구독 API를 유지한 채 안쪽만 단계적으로 교체.
> 대전제 2: **조연으로 설계한다.** 이 프로젝트의 플래그십은 Addressable 스폰 파이프라인 / AbilitySystem이다.
> 입력은 그 둘을 받쳐주는 "잘 만든 조연"이며, 아래 "범위 밖" 항목은 의도적으로 넣지 않는다(YAGNI).

---

## 0. 현황 진단

### 0.1 현재 데이터 흐름

```
Managers.Update()
  └─ InputManager.OnUpdate()               // 매 프레임 Keyboard.current / Mouse.current 직접 폴링
       ├─ escapeKey.wasPressedThisFrame     → OnEscapePressed
       ├─ WASD isPressed 조합               → OnMoveInput(Vector2)  (매 프레임 무조건 Invoke)
       ├─ Mouse leftButton                  → MouseAction(Press/Click)
       │                                       └ PlayerController._isMousePressed(bool) 저장
       │                                          → PlayerController.OnUpdate() 에서 폴링 → Attack
       └─ foreach(_actionMap) + _keyMap 조회 → wasPressedThisFrame 검사 → Invoke (버퍼 없음)
            ├─ "E_Skill" → HandleSkillE → BaseCharacter.UseSkillE()
            │                              └ _combat.TryUseSkillE() 게이트 → UseAbility(Ex)  ← AbilitySystem
            ├─ "Q_Skill" → HandleSkillQ → BaseCharacter.UseSkillQ()
            ├─ "Interact"/"Info"/"Inventory" → UI (IsPopupOpen 체크 산재)
            └─ "Swap_1~4" → PartyInputHandler → PartySwapController.TrySwap
```

관련 파일:
- `Assets/Scripts/Managers/Core/InputManager.cs` — 폴링 + 이벤트 + `Dictionary<string,Key>` 수동 키맵
- `Assets/Scripts/Controllers/Character/PlayerController.cs` — 입력 → 캐릭터 API 변환, `IsPopupOpen` 땜빵 산재
- `Assets/Scripts/Managers/Core/PartyManager/PartyInputHandler.cs` — 스왑 키(magic string)
- `Assets/Scripts/Controllers/Character/BaseCharacter.cs` — `UseSkillE/Q` → 게이트 → `UseAbility(slot, beat)` (AbilitySystem 진입점)

### 0.2 프로젝트 제약(중요) — 설계 결정의 근거

- **필드에는 항상 조종 캐릭터 1명만** 존재. 나머지 파티원은 `SetActive(false)`.
- 스왑 = "A 비활성 → B 활성"이지, "A를 AI에게 이양"이 아니다.
- 몬스터 AI는 현재 캐릭터의 **위치값만 참조**할 뿐, 캐릭터 조종 API(`Move`/`Attack`/`UseSkill*`)를 타지 않는다.

→ **입력을 소비하는 주체가 언제나 1명뿐이다.** 이 사실이 아래 "범위 밖(IInputSource)" 판단의 핵심 근거다.

### 0.3 문제점

| # | 문제 | 근거(코드) | 영향 |
|---|------|-----------|------|
| **P1** | **선입력 버퍼 부재** ⭐ — 스킬/공격 락 중 눌린 E·Q는 `wasPressedThisFrame`이라 그 프레임에 소비 못 하면 소실 | `InputManager.cs:57`, `BaseCharacter.UseSkillE()`(게이트 false면 그냥 return) | "미리 눌렀는데 안 나감" 또는 "끝을 보고 눌러야 함 → 콤보 끊김". 조작감 손해. **면접 정답 지점** |
| **P2** | **입력 컨텍스트 부재** — "UI 열림 시 게임플레이 입력 차단"을 소비자마다 `if (IsPopupOpen)`로 땜빵 | `PlayerController.cs:74, 108, 116` | 상태(인벤/대화/컷신) 늘 때마다 `if`가 번식. "입력 받아도 되나?" 판단 책임이 소비자에 흩어짐 |
| **P3** | **의도가 magic string** — `RegisterAction("E_Skill", ...)` | `PlayerController.cs:31~35`, `PartyInputHandler.cs` | 오타 런타임 버그, 타입 안전성 없음, 자동완성 불가 |
| **P4** | **입력 모델 혼재** — Move·스킬은 이벤트, Attack은 `_isMousePressed` bool 폴링 | `PlayerController.cs:129` vs `71~90` | 한 프레임 입력 상태를 한 곳에서 못 봄, 처리 순서 추론 어려움 |
| **P5** | **New Input System 절반만 사용** — `InputActionAsset`/Action Map 대신 `Dictionary<string,Key>`로 리맵핑·상태전환 수동 재발명 | `InputManager.cs:16~27, 115~124` | 묶어서 켜고 끄기(P2 해결 수단)·리바인딩·패드를 직접 재발명 |

> 잘 되어 있는 것(유지): 입력을 `Update`에서 읽음(물리 프레임 함정 회피), 이동이 (0,0)도 전송해 정지 처리, 슬롯 입력이 이미 `UseAbility`로 AbilitySystem에 연결됨.

---

## 1. 설계 원칙과 범위 결정

### 1.1 넣는 것 (In scope)

| 요소 | 해결 | 근거 |
|---|---|---|
| **Intent enum** | P3 | 싸고, 이후 모든 작업의 기반 |
| **Action Map + 입력 컨텍스트 스택** | P2, P5 | `IsPopupOpen` 땜빵이 **이미 번지고 있다** = 실재하는 코드 냄새 |
| **선입력 버퍼(InputBuffer)** | P1 | 조작감의 실체. 면접 질문 정답 |
| **입력 ↔ AbilitySystem 연결 정리** | — | 플래그십(AbilitySystem)과 물려 "시스템이 유기적"임을 보이는 최고 가치 지점 |

### 1.2 **넣지 않는 것 (Out of scope) — 의도적 제외**

| 제외 대상 | 이유 |
|---|---|
| **IInputSource(입력 소스 추상화)** | 필드에 입력 소비자가 **1명뿐**(0.2). 소스가 2개 이상 공존할 일이 없어 추상화가 정당화되지 않음 → over-engineering. |
| **리플레이 / 넷코드 결정론(deterministic input)** | 그 자체가 별도 플래그십 급. 입력 리팩토링에 끼우면 범위가 산으로 감. |
| **커스텀 리바인딩 UI, 게임패드/터치 스킴** | 한계효용 급락 구간. 조작감(P1)·구조(P2)와 무관. 필요해지면 Action Map 도입(아래) 덕에 나중에 저비용으로 착수 가능. |

> **이 "제외와 그 근거"가 포트폴리오 자산이다.** "IInputSource도 검토했으나 소비처가 하나여서 뺐다"처럼
> **안 한 것과 이유를 대는 것**이 YAGNI 판단력의 증거다. 패턴을 많이 쓰는 것보다 "언제 안 쓸지"를 아는 게 세다.

---

## 2. 목표 아키텍처

```
        [ InputManager (Reader) ]        ← 매 프레임 raw 입력을 읽어 Intent로 변환
              │
              ▼
        [ InputContextStack ]            ← 지금 어떤 Action Map이 활성인가 (Gameplay/UI/Dialogue)
              │  top 컨텍스트의 입력만 통과 (P2: IsPopupOpen 땜빵 제거)
              ▼
        [ InputBuffer ]                  ← 이산 입력(E/Q/Attack/Interact)을 N프레임 보관
              │  TryConsume(intent) → 성공 시 소비, 실패 시 유지 (P1: 선입력)
              ▼
   [ PlayerController → BaseCharacter.UseAbility(slot) ]   ← 버퍼를 폴링·소비해 AbilitySystem 발동
```

레이어 책임:
- **InputManager(Reader)**: raw 입력 → Intent. "누가 소비하는지" 무지.
- **InputContextStack**: "지금 입력을 받아도 되는 상태인가"를 **입력 레이어에서** 결정. 소비자는 몰라도 됨.
- **InputBuffer**: 이산 입력을 짧은 시간창 동안 살려둠. 소비자가 "지금 발동 가능"해지면 그때 꺼내 씀 → 선입력.
- **소비자**: 버퍼를 폴링해 `UseAbility`로 AbilitySystem에 넘김.

---

## 3. 단계별 계획

각 단계는 **독립 커밋 가능**하고, 이전 단계까지만으로도 게임이 정상 동작하도록 설계한다.

### Phase 1 — 의도(Intent) 추상화 (magic string 제거) · 리스크 낮음

**무엇**: string 키맵을 타입 안전한 enum으로. 파사드 API는 유지하되 오버로드 추가.

```csharp
// 신규: Assets/Scripts/Managers/Core/Input/InputIntent.cs
public enum InputIntent
{
    None,
    Attack, SkillE, SkillQ,             // 전투 (버퍼 대상)
    Interact, Info, Inventory, Escape,  // 시스템
    Swap1, Swap2, Swap3, Swap4,         // 파티
}
```

- `Dictionary<string,Key> _keyMap` → `Dictionary<InputIntent,Key>`.
- `RegisterAction(string, Action)` 은 **한동안 유지**하되 내부에서 `InputIntent`로 매핑하는 어댑터를 둔다(호출부 점진 이관).
- `PlayerController`/`PartyInputHandler`의 `"E_Skill"` 등을 `InputIntent.SkillE`로 교체.

**완료 기준**: magic string 소멸, 기능 동일. 컴파일·조작 동작 그대로.

---

### Phase 2 — 입력 컨텍스트 (Action Map + Stack) · 리스크 중간, **P2 해결**

**무엇**: "UI 열림 시 입력 차단"을 소비자 `if`에서 걷어내 **입력 레이어**로 올린다.

```csharp
// 신규: Input/InputContext.cs  — 하나의 상황(맵)
public enum InputContext { Gameplay, UI }   // 대화·컷신 등은 필요해질 때 추가(YAGNI)

// 신규: Input/InputContextStack.cs — 비어있으면 Gameplay(암묵적 바닥)
public class InputContextStack
{
    private readonly Stack<InputContext> _stack = new();
    public InputContext Current => _stack.Count > 0 ? _stack.Peek() : InputContext.Gameplay;
    public bool IsGameplay => Current == InputContext.Gameplay;
    public void Push(InputContext ctx) => _stack.Push(ctx);
    public void Pop() { if (_stack.Count > 0) _stack.Pop(); }   // 직전 컨텍스트로 자동 복귀
    public void Clear() => _stack.Clear();
}
```

- **핵심: 전부 차단(all-or-nothing)이 아니라 "컨텍스트별 허용 Intent 집합"** — Unity Action Map의 정확한 모델(맵마다 액션의 부분집합만 활성). InputManager가 `IsAllowed(intent)`로 필터.
  - **Gameplay**: 전부 허용.
  - **UI**: `{ Move, Swap1~4 }` 만 허용 → **인벤/정보창 열려도 이동·스왑은 되고, 평타·스킬·상호작용·UI토글은 차단.** (실제 RPG 조작감: 메뉴 열어도 이동은 자연스러움)
- 게이트가 연속 입력을 **자연 중립화**: Move는 차단 시 매 프레임 `(0,0)`(정지), Attack은 차단 시 `Click`으로 눌림 해제 → 별도 전환 플래그 불필요.
- 스택 구동은 **UIManager가 `_popupStack`을 push/pop하는 바로 그 두 지점**에서 `PushContext(UI)`/`PopContext()` → 두 스택이 구조적으로 동기. (별도 병렬 스택 신설 안 함 = 어긋남 리스크 0)
- **`PlayerController`의 `if (Managers.UI.IsPopupOpen)` 3곳 제거** (HandleMouse/HandleInventory/HandleInfo).
- ESC는 게이트 **앞**에서 처리 → UI 열려도 닫기 정상.
- (Phase 4에서 실제 `InputActionAsset`의 Action Map으로 매핑. 지금은 컨텍스트 enum + 허용집합 필터로 충분.)

**완료 기준**: 인벤/정보창 열면 **평타·스킬·상호작용은 입력 단에서 막히고, 이동·스왑은 유지**되며, 닫으면 전투 입력 복귀. `IsPopupOpen` 게임플레이 게이팅 분기 소멸.

---

### Phase 3 — 선입력 버퍼 · 리스크 중간, **P1 해결 · 핵심 가치**

**무엇**: 이산 입력을 N프레임(또는 t초) 보관하고, 소비자가 "지금 가능"할 때 꺼내 쓴다.

```csharp
// 신규: Input/InputBuffer.cs
public class InputBuffer
{
    private readonly float _window = 0.15f;                 // 선입력 유효 시간(튜닝값)
    private readonly Dictionary<InputIntent, float> _stamp = new();

    public void Push(InputIntent i) => _stamp[i] = Time.time;

    public bool Peek(InputIntent i)                          // 소비하지 않고 유효성만
        => _stamp.TryGetValue(i, out var t) && Time.time - t <= _window;

    public bool TryConsume(InputIntent i)                    // 유효창이면 소비(제거)하고 true
    {
        if (Peek(i)) { _stamp.Remove(i); return true; }
        return false;
    }

    public void Flush(InputIntent i) => _stamp.Remove(i);
    public void Clear() => _stamp.Clear();
}
```

**소비 방식 전환** (`PlayerController.OnUpdate`):
```csharp
// 기존: 이벤트에서 즉시 UseSkillE() (실패 시 소실)
// 변경: 매 프레임 버퍼 폴링, 발동 성공했을 때만 소비
if (_buffer.Peek(InputIntent.SkillE) && _currentTarget.UseSkillE()) // UseSkillE가 bool 반환
    _buffer.TryConsume(InputIntent.SkillE);
if (_buffer.Peek(InputIntent.SkillQ) && _currentTarget.UseSkillQ())
    _buffer.TryConsume(InputIntent.SkillQ);

_currentTarget.Attack(_attackHeld);   // Attack은 held 기반 유지
```

> 이를 위해 `BaseCharacter.UseSkillE()/UseSkillQ()`를 **성공/실패 bool 반환**으로 살짝 바꾼다.
> 내부 게이트는 이미 `_combat.TryUseSkillE()`가 bool을 반환하므로, 그 결과를 그대로 리턴하면 됨(작은 변경).
> 게이트/쿨타임 실패면 false → 버퍼 유지 → 락 풀리는 프레임에 재시도·발동.

- Move는 연속값이라 **버퍼 대상 아님**(현재 이동 값 그대로).
- 컨텍스트가 Gameplay가 아닐 때는 **Push 자체를 막는다**(팝업 닫는 순간 발동 방지 — Phase 2와 연동).

**완료 기준**: 평타/스킬 모션 중 E를 미리 눌러두면 락 풀리는 순간 발동(현재는 씹힘). `_window=0`이면 기존 동작과 동일 → A/B 회귀 검증.

---

### Phase 4 (선택·후속) — Unity InputActionAsset 이관 · 리스크 높음, 별도 과제

**무엇**: 손으로 만든 `Dictionary<InputIntent,Key>` + 컨텍스트 enum을 실제 `InputActionAsset`의 Action Map으로 대체.
Phase 2에서 만든 컨텍스트 경계 덕분에, 상위 레이어(Intent/Buffer/소비자)는 무변경으로 갈아끼울 수 있다.

**얻는 것**: 무료 리바인딩 UI 연동, 게임패드/터치/컨트롤 스킴, `.performed/.canceled` 콜백, Processors(데드존 등).

**주의**: 조작감(P1)·컨텍스트(P2)와 독립. **급하지 않음** → "설계상 열어두기"만 하고 보류 권장.

---

## 4. 신규/변경 파일 요약

```
Assets/Scripts/Managers/Core/Input/
  ├─ InputIntent.cs           (신규, P3)
  ├─ InputContext.cs          (신규, P2)
  ├─ InputContextStack.cs     (신규, P2)
  └─ InputBuffer.cs           (신규, P1)

변경:
  InputManager.cs      → 키맵 enum화, top 컨텍스트 기준으로 Intent 발화(게이트)
  PlayerController.cs  → 이벤트 즉시 발동 → 버퍼 폴링·소비, IsPopupOpen 분기 제거
  PartyInputHandler.cs → string → InputIntent
  BaseCharacter.cs     → UseSkillE/UseSkillQ 를 bool 반환으로 (게이트 결과 리턴)
  UI 오픈/클로즈 지점   → InputContextStack.Push/Pop 연동
```

---

## 5. 리스크 & 회귀 검증

- **스왑 시 버퍼 정책**: 스왑(타겟 교체) 시 전투 버퍼(SkillE/Q)는 **flush 권장**(직전 캐릭터의 입력이 새 캐릭터로 새는 것 방지). `SetControlTarget`에서 `_buffer.Clear()`.
- **팝업 중 Push 차단**: 컨텍스트가 Gameplay가 아니면 전투 Intent를 버퍼에 **Push하지 않는다**(안 그러면 팝업 닫는 순간 발동).
- **컨텍스트 복귀 누락**: UI 닫힘 콜백에서 `Pop`을 빠뜨리면 입력이 영구 잠김 → 닫힘 경로마다 Pop 보장(또는 팝업 파괴 시 자동 Pop 훅).
- **불변식 검증**: `_window=0` 이면 현행과 동일해야 한다.
- **스모크(각 Phase 후 `/verify`)**: 이동 · 평타 · E · Q · 스왑 · 상호작용 · 인벤/정보창 열고닫기 · ESC — 8개 경로.

---

## 6. 권장 착수 순서

1. **Phase 1** (반나절) — 안전. magic string 제거로 기반 마련.
2. **Phase 2** — `IsPopupOpen` 땜빵 제거. 구조 개선 체감.
3. **Phase 3** — 조작감(선입력) 개선 체감. 여기까지가 "실무 권장 조합" 본체.
4. **Phase 4** — 여유 될 때 별도 과제로. (안 해도 됨)

> 순서 근거: Phase 3(버퍼)의 소비 정책은 Phase 2(컨텍스트)에서 "언제 Push하나"가 정해져야 안전하다.

---

## 7. 포트폴리오 / 면접 서술 포인트

- **P1(선입력)** = "플레이어 조작에서 중요한 것 = 조작감"의 실체. **입력이 들어온 시점과 소비되는 시점을 분리**해, 프레임 단위 정확도를 인간에게 요구하지 않는다. 형제 개념: 코요테 타임 · 점프 버퍼.
- **P2(컨텍스트 스택)** = "패턴을 안다"가 아니라 **"`IsPopupOpen` 체크가 소비자 코드에 번지는 냄새를 감지 → 입력 활성 판단을 입력 레이어로 승격 → 중첩 UI 복귀를 스택으로 자동화"** 라는 진단→해결 서술로 말한다.
- **AbilitySystem 연결** = 슬롯 입력이 (컨텍스트 게이트 → 버퍼 → `UseAbility`)를 거쳐 데이터 주도 어빌리티로 발동. 입력이 독립 자랑거리가 아니라 **플래그십과 유기적으로 물린다**는 증거.
- **범위 결정(YAGNI)** = "IInputSource·리플레이·결정론도 검토했으나, 필드 입력 소비자가 1명뿐이라 over-engineering으로 판단하고 제외했다." **멈춘 이유를 대는 것**이 성숙함의 신호.
