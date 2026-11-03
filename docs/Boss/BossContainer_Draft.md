# 보스 구조 초안 — 컨테이너 + 패턴 조각

> 상태: **초안 2 (2026-10-19)** · 결정 D1~D8 반영 (D6 = 패턴 단위 컴포넌트 + 조건 조각 SO) · 10-18 초안(`BossPattern_Draft.md`)을 대체
> 레퍼런스: 로스트아크 레이드 보스

---

## 1. 요구사항

1. **고정 패턴(기믹):** 체력이 정해진 줄 아래로 내려가면 정해진 패턴을 **반드시** 수행한다.
2. **짤패턴:** 그 밖의 구간에서는 여러 짤패턴 중 하나를 무작위로 수행한다.
3. **흐름:** 타겟팅 → 패턴 → 반복 … → 임계 도달 → 고정 패턴 → 타겟팅 → 짤패턴 → 반복 …
4. **보스마다 스크립트를 만들지 않는다.** 보스 컨트롤러는 공통 컨테이너 하나, 보스별 차이는 **조각을 끼워 맞춰서** 만든다.

---

## 2. 결정 사항

| # | 질문 | 결정 |
|---|---|---|
| D1 | 한 번에 여러 줄을 넘거나, 기믹 중 다음 줄에 닿으면 | **통과한 기믹을 전부 순서대로 실행.** 체력 0(사망)만 예외 |
| D2 | 짤패턴 도중 임계에 닿으면 | **모든 패턴은 원자적.** 하던 패턴이 끝난 뒤 기믹 시작 |
| D3 | 기믹 중 체력 | **잠그지 않음.** 계속 피격 가능 |
| D4 | 기믹 이후 짤패턴 변화 | **NextFillers** — 비어 있으면 유지, 있으면 교체 |
| D5 | 사망 시 정리 범위 | 타임라인·장판·투사체는 정리, 소환물은 사망 때 일괄 정리 |
| D6 | 조각의 크기와 재료 | **패턴 단위 조각(컴포넌트) + 기믹 조건만 교체 가능한 조각(SO)** (§4) |
| D7 | 결정 구조 | **BT를 기본으로** |
| D8 | 사거리 밖 | 사거리까지 이동 후 진행. **지금은 고정형이라 추적 생략** (자리만 남김) |

D1 + D3의 결과: HP 잠금이 없으므로 큰 피해 한 방에 줄 두 개를 넘을 수 있다 → **기믹 대기열(큐)**이 필요하다. 사망하면 대기열은 비운다.

D2의 결과: 패턴 중간에 끊는 경로는 **사망 하나뿐**이다. 10-18 초안의 `Interruptible`은 필요 없다.

---

## 3. 전체 구조

```
보스 프리팹
 ├─ BossController (공통 컨테이너)          ← "언제" 하는가
 │    ├─ 짤패턴 표   : [조각, 가중치, 쿨]
 │    ├─ 기믹 표     : [조건 조각, 조각, NextFillers]
 │    ├─ BT          : 사망 > 기믹 대기열 > 짤패턴
 │    ├─ 선택기      : 가중치 · 직전 제외 · 쿨
 │    ├─ 실행기      : 조각 하나를 끝까지 (취소는 사망 때만)
 │    └─ 조건 확인   : 아직 안 쓴 기믹의 조건이 맞으면 대기열에 추가
 │
 └─ 패턴 조각들 (컴포넌트)                  ← "무엇을" 하는가
      ├─ TimelinePattern  "내려찍기"       (타임라인 하나 재생)
      ├─ TimelinePattern  "운석 낙하"
      ├─ TimelinePattern  …
      └─ (전용 조각)       코드가 꼭 필요한 기믹만 하위 클래스로

조건 조각 (SO 에셋, 기믹 표의 한 칸)       ← "언제"를 바꿔 끼우는 조각
 ├─ HpLineCondition   "60줄 이하"
 └─ (나중에) TimeCondition "전투 시작 후 N초" (로아 광폭화 같은 것)
```

**원칙: 조각은 "무엇을", 컨테이너의 표는 "언제".**
조각은 자기가 짤인지 기믹인지 모른다. 같은 조각을 짤 표에도, 기믹 표에도 넣을 수 있다.

---

## 4. D6 — 조각의 크기와 재료

### 4-1. 크기: 패턴 단위 + 조건 조각

"조각"을 어디서 자를지 세 가지를 놓고 비교했다.

| | 조각 하나 = | 판단 |
|---|---|---|
| **1. 패턴 단위** | 패턴 하나 (내려찍기, 운석) | **채택** |
| 2. 단계 단위 | 패턴 안의 한 단계 (바라보기, 재생, 장판, 대기) | 안 함 — 타임라인(애니·이펙트·사운드·시그널 순서)과 `AbilityData` 이펙트(장판·피해)가 이미 단계를 맡고 있어서, 순서를 정하는 곳이 세 군데가 됨 |
| 3. 기능 단위 | 컨트롤러 기능 (선택 규칙, 기믹 조건, 이동) | **기믹 조건만** — 지금은 HP 줄뿐이지만 시간 조건(광폭화) 등으로 바뀔 여지가 있는 곳은 여기뿐. 선택 규칙·이동은 보스 하나로는 바뀔 일이 없음 |

### 4-2. 재료: 프리팹 안의 것을 쓰면 컴포넌트, 값만 보면 SO

| 조각 | 재료 | 이유 |
|---|---|---|
| **패턴 조각** | 보스 프리팹 위의 **컴포넌트** | `PlayableDirector`, `SpawnPointSet`·`VfxAnchorSet`, 유물 같은 프리팹 안의 것을 직접 가리켜야 함. SO는 프리팹 안을 가리킬 수 없음 |
| **조건 조각** | **ScriptableObject** 에셋 | HP·전투 시간 같은 값만 읽음. 프로젝트의 `EffectData`(추상 SO)와 같은 방식이라 새 관례가 생기지 않음 |

검토했다가 뺀 것

- **`[SerializeReference]` 인라인 목록:** 파일이 안 생기는 건 장점이지만, 타입을 고르는 드롭다운을 에디터 코드로 직접 만들어야 하고 프로젝트에서 쓴 적이 없는 방식이다. 조건 에셋 파일이 너무 많아지면 그때 다시 본다.
- **조각이 자기 역할을 스스로 들고 있기 ("나는 짤, 가중치 3"):** 조각을 붙이기만 하면 되지만, 보스의 흐름을 보려면 조각을 하나씩 열어 봐야 하고 NextFillers를 표현하기 어렵다. → 표는 컨테이너에 둔다.

### 4-3. 상태는 어디에

- 조건 조각은 **판정만** 한다 (`IsMet(ctx)`). 상태가 없으니 `EffectData`처럼 런타임 객체를 따로 만들 필요가 없다.
- "이 기믹을 이미 썼는가", 쿨, 직전 패턴은 **컨테이너**가 기억한다.

## 5. 코드 모양

```csharp
// 패턴 조각 — "무엇을"
public abstract class BossPattern : MonoBehaviour
{
    [SerializeField] protected string _displayName;
    public float Range = 0f;      // 0 = 거리 무관. 지금은 고정형이라 전부 0 (D8)

    public abstract UniTask Run(BossContext ctx, CancellationToken ct);   // ct는 사망 때만 취소됨
}

public class TimelinePattern : BossPattern
{
    [SerializeField] private TimelineAsset _timeline;
    // Run: ctx.Director에 꽂고 재생 → stopped까지 대기. ct 취소 시 Stop()
}
```

```csharp
// 컨테이너 — "언제"
public class BossController : BaseMonsterController, ISummonRegistry
{
    [SerializeField] private int   _totalLines = 100;   // 체력 줄 수
    [SerializeField] private float _interval   = 1.5f;  // 짤패턴 사이 대기
    [SerializeField] private List<FillerEntry> _fillers;
    [SerializeField] private List<GimmickEntry> _gimmicks; // 위에서부터 확인 = 실행 순서
}

[Serializable] public class FillerEntry
{
    public BossPattern Pattern;   // 같은 프리팹 위의 조각
    public int   Weight   = 1;
    public float Cooldown = 0f;
}

[Serializable] public class GimmickEntry
{
    public BossCondition Condition;       // 조건 조각 (SO)
    public BossPattern   Pattern;
    public List<FillerEntry> NextFillers; // 비어 있으면 유지 (D4)
}
```

```csharp
// 조건 조각 — "언제"를 바꿔 끼우는 부분. 판정만 하고 상태는 없음
public abstract class BossCondition : ScriptableObject
{
    public abstract bool IsMet(BossContext ctx);
}

[CreateAssetMenu(menuName = "Boss/Condition/HpLine")]
public class HpLineCondition : BossCondition
{
    [SerializeField] private int _atLine;   // 이 줄 이하가 되면
    public override bool IsMet(BossContext ctx) => ctx.CurrentLine <= _atLine;
}

// (나중에) TimeCondition: ctx.CombatTime >= _seconds
```

- `BossContext.CurrentLine` = `ceil(현재 HP / maxHp × _totalLines)`. 기획 표기는 로아처럼 "30줄".
- `OnValidate`에서 표가 가리키는 패턴 조각이 같은 프리팹 위에 있는지 확인한다.

---

## 6. 흐름 (BT)

```
Selector
├─ Sequence: 사망?        → 실행 중 조각 취소 · 대기열 비움 · 정리 · 사망 처리
├─ 실행 중인 조각?        → Running (원자적: 무엇도 끼어들지 않음)
├─ Sequence: 기믹 대기열? → 맨 앞 기믹 실행 → 끝나면 NextFillers 적용
└─ Sequence: 평상
     ├─ 간격 대기
     ├─ 타겟팅 (대상 갱신)
     ├─ 선택기: 짤패턴 하나
     ├─ (추적: 사거리 밖이면 이동 — 지금은 생략)
     └─ 실행기에 시작 요청

매 틱, BT 평가 전 (조건 확인)
  - 기믹 표를 위에서부터 보며, 아직 안 쓴 기믹 중 IsMet이 참인 것을 대기열에 추가
  - 한 번 넣은 기믹은 "씀"으로 표시 → 다시 들어가지 않음
  - 체력은 잠그지 않음 (D3)
```

- HP 이벤트(`OnHpChanged`)를 구독하지 않고 매 틱 확인하는 이유: 시간 조건처럼 HP와 무관한 조건도 같은 자리에서 확인할 수 있다. 조건 몇 개를 매 틱 확인하는 비용은 무시할 수 있다.
- 한 방에 줄 두 개를 넘으면 같은 틱에 둘 다 들어가고, 표 순서대로 실행된다 (D1).

- 기믹 대기열이 있으면 짤패턴 사이 간격을 기다리지 않고 바로 들어간다(제안).
- 실행 중 판단은 실행기 하나에 물어본다. 지금의 `_isActionRunning`·`_isSkillFiring` 두 플래그와 `OnTimelineStopped`는 사라진다.

### 선택기 규칙

1. 현재 짤패턴 표
2. 쿨 중인 조각 제외
3. 직전 조각 제외 (같은 패턴 연속 방지)
4. 남은 후보에서 가중치 추첨
5. 후보가 0개 → 직전 제외만 풀고 다시

---

## 7. 실제 게임들은 어떻게 하나

| 사례 | 구조 | 이 초안과 비교 |
|---|---|---|
| **WoW 서버 (TrinityCore / AzerothCore)** | 두 갈래. ① **SmartAI**: 공통 AI + DB 행 데이터. 한 행 = 이벤트(예: `HEALTH_PCT` 최소/최대%) + 페이즈 마스크 + 확률 + 행동. ② **레이드 보스**: 보스마다 C++ 스크립트(`BossAI`, `EventMap`, 체력 % 확인 + 한 번만 발동 플래그) | 이 초안의 표 ≈ SmartAI의 행. 단, **복잡한 보스일수록 보스별 코드로 간다**는 반례도 같이 보여줌 |
| **프롬 소프트웨어 (엘든 링)** | 목표(Goal) 스택. 보스마다 전투 스크립트가 있지만, 공용 함수로 **가중치 추첨**(거리 구간별 가중치 표) · **쿨이면 가중치 0** · 체력 기준으로 가중치 조정. 접근 같은 공용 목표는 공유 | 선택기(가중치 · 쿨 · 거리)가 거의 같음. 차이는 **보스별 스크립트 + 공용 라이브러리** 쪽이라는 것 |
| **언리얼 (GAS + BT/StateTree)** | 어빌리티를 조각처럼 **부여(grant)**하고, BT나 StateTree가 고름. 페이즈 + 어빌리티 세트 구조의 보스 툴킷이 마켓에 여럿 있음 | "조각을 끼워 맞추는" 구조와 가장 가까움 |
| **로스트아크** | 구현 방식은 공개 자료를 찾지 못함. 전투기획팀(몬스터 파트)이 보스 패턴·기믹·스킬을 기획한다는 정도만 확인 | 동작(짤 + HP 줄 기믹)만 레퍼런스로 씀 |

**정리**

- 유저 방식(공통 뼈대 + 보스별 데이터/조각)은 실제로 쓰이는 한 갈래다. WoW의 SmartAI, 언리얼의 GAS 부여 방식이 같은 계열이다.
- 반대쪽 갈래는 **보스별 스크립트 + 공용 라이브러리**(WoW 레이드 보스, 프롬)다. 기믹이 복잡하면 결국 코드가 필요해지기 때문이다.
- 이 초안은 둘을 섞는다: **평범한 패턴은 데이터(TimelinePattern), 코드가 꼭 필요한 기믹만 전용 조각.** 보스 전체를 스크립트로 만들지는 않지만, 코드가 필요한 곳에서 막히지도 않는다.

출처
- AzerothCore wiki, smart_scripts — https://www.azerothcore.org/wiki/smart_scripts
- TrinityCore 보스 스크립트 예 — https://trinitycore.net/d4/dac/boss__mal__ganis_8cpp_source
- "The Low-Tech AI of Elden Ring" — https://nega.tv/posts/low-tech-ai-of-elden-ring.html
- Unreal, Abilities in Lyra — https://dev.epicgames.com/documentation/en-us/unreal-engine/abilities-in-lyra-in-unreal-engine
- Unity, SerializeReference — https://docs.unity3d.com/kr/2021.3/ScriptReference/SerializeReference.html
- 로스트아크 인벤, 전투기획팀 인터뷰 — https://www.inven.co.kr/board/lostark/4820/3667

---

## 8. 범위 밖

- 기믹 성공/실패 분기 (무력화 성공 → 그로기, 실패 → 전멸기)
- 추적 (고정형이라 생략, `Range` 자리만 둠)
- 어그로 · 다중 대상 (플레이어 하나)
- HP 바 줄 표시 UI

---

## 9. 구현 순서

1. **실행기 + TimelinePattern:** 두 플래그 + `OnTimelineStopped`를 `await Run(ctx, ct)`로 대체. 타임라인 5개를 조각 5개로 옮기고, 동작은 지금과 같게(무작위 5개).
2. **짤패턴 표 + 선택기:** 가중치 · 직전 제외 · 쿨.
3. **조건 조각(HpLineCondition) + 기믹 표 + 대기열:** 조건이 맞으면 순서대로 실행.
4. **NextFillers:** 기믹 후 짤패턴 표 교체.
5. **전용 조각 하나:** "코드가 필요한 기믹"의 예. 후보는 유물 선택(`BossRelicController`)이지만, 지금은 `IChoiceHandler`로 어빌리티 이펙트가 부르고 있어서 이미 타임라인만으로 돌 수도 있다 → 확인 후 결정.

각 단계는 플레이로 확인할 수 있는 크기로 나눴다.
