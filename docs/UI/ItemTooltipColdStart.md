# 아이템 툴팁 콜드 스타트 제거 — 첫 호버 스파이크 + 초기화 레이스

> **한 줄 요약** — 인벤토리에서 아이템에 마우스를 **처음 올릴 때만** ① 툴팁 스탯이 비어 보이고 ② 프레임이 튀는 두 현상을 잡았다. 원인은 **첫 호버 순간에 오브젝트 13개를 한꺼번에 생성**하면서, 그 생성이 끝나기도 전에 `SetInfo`가 먼저 실행되는 **초기화 레이스**였다. 생성을 **패널 오픈 시점으로 미리 옮기고**(워밍), 초기화를 **await 가능하게** 바꾸고, `async void` Show/Hide 경합을 **요청 토큰**으로 막아 해결했다.

| 축 | 이전 (콜드 스타트) | 현재 (워밍 + 견고화) |
|---|---|---|
| 툴팁·스탯 슬롯 생성 시점 | **첫 호버 순간** (오브젝트 13개 동시 생성) | 패널 오픈 시 **미리 생성**(Preload) |
| 첫 호버에 스탯 표시 | **빈 채로 뜸**(레이스) | 정상 표시 |
| 첫 호버 프레임 | 생성 스파이크(히칭) | 스파이크 없음(동기 SetActive+SetInfo만) |
| Show/Hide 경합 | 마우스 이탈해도 툴팁이 켜진 채 남음 | 요청 토큰(`_pendingSlot`)으로 **폐기** |
| 재료 아이콘 | 항상 안 뜸(아틀라스 하드코딩) | 타입별 아틀라스 분기 |

---

## 1. 증상

- 인벤토리를 열고 아이템에 **처음** 마우스를 올리면 툴팁 **스탯이 비어** 뜨고, 프레임이 한 번 **튄다**.
- 두 번째 호버부터는 정상 → "처음에만" 발생한다는 게 결정적 단서.

## 2. 원인 — 하나의 뿌리(콜드 스타트) + 곁다리 버그 2개

호버 → `UI_Inventory.OnSlotPointerEnter` → `UI_ItemInfo.ShowTooltip(slot, pos)`. 문제는 `ShowTooltip` 내부였다.

```csharp
var tooltip = await GetInstanceAsync();   // ① 첫 호출 때 여기서 툴팁 최초 생성
tooltip.gameObject.SetActive(true);       // ② 이제서야 Start() 예약됨
tooltip.SetInfo(slot);                    // ③ Start/Init 실행 전에 즉시 실행
```

**① 첫 호버 = 오브젝트 13개 동시 생성 (프레임 스파이크)**
`GetInstanceAsync`가 툴팁 본체 1개를 만들고, 그 `Init()`이 스탯 슬롯 `UI_MainStatInfo`×6 + `UI_SubStatInfo`×6 = **12개를 또 비동기 생성**한다. 전부 첫 호버 순간에 몰려 Addressable 로드 + Instantiate가 겹치며 히칭이 된다. 두 번째부터는 이미 있으니 무비용 → **"처음만" 튐**.

**② `SetInfo`가 `Init`보다 먼저 실행 (스탯이 빔)**
`UI_Base.Start()`가 `Init()`을 호출하는데, **비활성 오브젝트는 `Start`가 돌지 않는다.** 툴팁은 생성 직후 `SetActive(false)`라, `Init`은 ②에서 `SetActive(true)` 된 **다음 프레임 디스패치**에야 돈다. 그런데 ③ `SetInfo`는 **같은 프레임에 동기로 즉시** 실행 → `SetMainStat`이 아직 비어 있는 `_mainStatSlots`(Count 0)를 순회 → **스탯이 하나도 안 뜬다.** 게다가 `Init`은 `async void`라 12개 슬롯이 이후 여러 프레임에 걸쳐 채워진다.

**③ `async void` Show/Hide 경합 (툴팁이 안 꺼짐)**
`ShowTooltip`이 `async void`라, 첫 로드를 기다리는 동안 마우스가 슬롯을 벗어나면 `HideTooltip`이 먼저 돌지만 그땐 `_instance`가 아직 null → 아무것도 못 끄고, 뒤늦게 Show가 완료돼 `SetActive(true)` → **포인터가 떠났는데 툴팁이 켜진 채 남는다.**

**곁다리 A — 재료 아이콘이 항상 안 뜸**: `SetIcon`이 카테고리와 무관하게 `"EquipmentIconAtlas"`를 **하드코딩**. 재료/소비 아이템은 다른 아틀라스라 sprite=null.
**곁다리 B — 런타임 파일에 `using NUnit.Framework;`**: IDE 자동추가 쓰레기. 빌드 리스크라 제거.

## 3. 해결

핵심 통찰: **"이 UI는 무겁다"가 아니라 "무거운 생성을 호버 시점에 치르고 있다"가 문제.** 생성 비용 자체는 없앨 수 없다. 대신 **치르는 시점을, 유저가 반응을 기다리는 호버 프레임에서 이미 로딩 텀인 패널 오픈 프레임으로 옮긴다.**

1. **워밍(Preload)** — 패널이 열릴 때 `UI_ItemInfo.Preload()`로 툴팁+스탯 슬롯 12개를 **미리** 만든다(`UI_Inventory`, `UI_EquipmentDecomposePanel`, `UI_RelicUpgradePanel`의 `Init`). → 첫 호버가 **동기 SetActive+SetInfo**만 남아 스파이크·레이스 동시 소멸.
2. **초기화를 await 가능하게** — `Init`의 슬롯 생성을 `async UniTask EnsureStatSlotsAsync()`(1회 가드 + 중복 진입 대기)로 분리하고, `GetInstanceAsync`가 이를 **await한 뒤** 인스턴스를 반환. → `SetInfo`가 빈 리스트를 도는 일이 원천 차단.
3. **스테일 가드** — `ShowTooltip` 진입 시 `_pendingSlot = slot`로 최신 요청을 등록하고, `await` 뒤 `_pendingSlot != slot`이면(마우스 이탈/다른 슬롯 진입) `SetActive`를 **스킵**. `HideTooltip`은 `_pendingSlot = null`로 진행 중 Show를 취소.
4. **아틀라스 분기** + **NUnit using 제거** — 별개 버그 정리.

```csharp
// ShowTooltip 스테일 가드 (핵심)
_pendingSlot = slot;                 // 최신 요청 등록
var tooltip = await GetInstanceAsync();
if (_pendingSlot != slot) return;    // await 도중 이탈/변경 → 이 Show 폐기
tooltip.gameObject.SetActive(true);
tooltip.SetInfo(slot);
```

## 4. 결과

두 가지가 명확히 해결됐다. 둘 다 실행마다 흔들리는 숫자가 아니라 **재현 가능한 동작 변화**다.

**① 첫 호버에 스탯이 안 뜨던 문제 해결 (정확성)**
초기화 레이스를 없애 첫 호버부터 스탯이 정상 표시된다. 덤으로 마우스 이탈 후 툴팁이 켜진 채 남던 경합도 사라졌다. — 숫자가 아니라 **버그 수정**이라 이 작업의 알맹이.

**② 첫 호버 프레임의 생성 스파이크 제거**
오브젝트 13개 생성을 호버 프레임에서 **패널 오픈 프레임으로 이동**했다. 총 생성 비용은 동일하지만, **유저가 반응을 기다리는 순간(호버)에서 이미 로딩 컨텍스트인 순간(오픈)으로 옮긴 것**이라 상호작용의 히칭이 사라진다. — 즉 "제거"가 아니라 **"관측 시점 재배치"**이며, 이게 이 문제의 정직한 성격이다.

**보조 지표 (참고)** — 재현 스위치 `UI_ItemInfo.PreloadEnabled`(false=Before, true=After)와 세션 첫 호버 자동 로그(`[TooltipProfiler]`)로 "호버→표시 지연"을 잰다.

| 지표 | Before (호버에서 생성) | After (오픈에서 미리 생성) |
|---|---|---|
| 첫 호버→표시 지연(5회 중앙값) | ~6 ms | ~2.4 ms |
| 첫 호버에 스탯 표시 | 빈 채로 뜸 | 정상 |
| 생성 스파이크 위치 | 호버 프레임 | 오픈 프레임으로 이동 |

> ⚠️ 지연 6→2.4ms는 60fps 한 프레임(16.7ms)의 일부라 **절대값을 성과로 내세우지 않는다.** 핵심은 위 ①(버그 수정)과 ②(스파이크 재배치)이며, 시간은 보조 근거일 뿐이다. GC 할당은 `GC.GetTotalAllocatedBytes` 전역 카운터가 `await` 여러 프레임에 걸쳐 무관한 할당까지 섞어 잡아 노이즈가 커서 헤드라인 지표로 쓰지 않는다(정밀 측정이 필요하면 Profiler의 `GC Alloc` 샘플로).

## 5. 배운 것 / 설계 원칙

- **비활성 오브젝트는 `Start`가 안 돈다** — 만들자마자 `SetActive(false)` 하는 풀링/싱글톤 UI는 `Start`/`Init` 기반 초기화가 **첫 활성화까지 지연**된다. 동기 소비 코드가 그 앞을 지나가면 레이스. → 초기화는 **호출자가 await할 수 있는 명시적 경로**로.
- **`async void`는 순서를 보장하지 않는다** — Show/Hide처럼 서로를 취소해야 하는 비동기 쌍은 **요청 토큰**(여기선 `_pendingSlot`)으로 최신성만 확인하면 경합이 사라진다.
- **콜드 스타트는 "무겁다" 문제가 아니라 "언제 치르나" 문제** — 없앨 수 없는 생성 비용은 **유저가 안 기다리는 시점(로딩·오픈)으로 워밍**한다. 리소스 프리로드/풀 워밍과 같은 결.
- **성능 주장엔 재현 스위치를 붙인다** — `PreloadEnabled` 토글 하나로 Before/After를 언제든 재현할 수 있게 만들면, 주장이 검증 가능해진다.

## 관련 파일
- 툴팁 본체/측정: `Assets/Scripts/UI/PopUp/UI_ItemInfo.cs` (`Preload`, `EnsureStatSlotsAsync`, `_pendingSlot`, `PreloadEnabled`, `[TooltipProfiler]` 로그)
- 워밍 호출: `Assets/Scripts/UI/PopUp/UI_Inventory.cs`, `Assets/Scripts/UI/PopUp/UpgradePanel/{UI_EquipmentDecomposePanel, UI_RelicUpgradePanel}.cs`
- 호버 이벤트원: `Assets/Scripts/UI/PopUp/ItemSlot/UI_ItemSlot.cs` (`OnPointerEnter/Exit`)
- 서브아이템 생성: `Assets/Scripts/Managers/Core/UIManager.cs` (`MakeSubItemAsync`), `Assets/Scripts/UI/UI_Base.cs` (`Start → Init`)
