# Phase 4 결과 — 풀·인스턴스 수명 통합 (R5)

> 대상: `PoolManager` / `ResourceRegistry` / `ResourceManager` / 각 씬 `CreatePool` · 성격: **구현 완료 + 실측 검증**
> 상위: [Phase4_Spec.md](./Phase4_Spec.md) · 측정 세션: **2026-07-28 01:10:15 ~ 01:11:54** (에디터 플레이, GameScene → 팝업 5종 → BossDungeon 왕복)

---

## 0. 한 줄 결론

풀이 원본 프리팹의 **refCount 티켓을 쥐고**, `@Pool_Root`의 **DDOL을 제거**했다.
계측 로그가 티켓 acquire/release를 refCount로 증명했고, 전체 왕복 후 메모리가 **flat**(358.6 → 359.1 MB)이라 누수·회귀가 없음을 확인했다. 순서 의존이 구조 보장으로 바뀌었다.

---

## 1. 티켓 동작 — refCount로 증명

### Scene 스코프 프리팹 (진짜 위험 대상)

풀이 만든 프리팹 핸들이 `ref 2` = **소유 스코프(1) + 풀 티켓(1)**:

```
[Scene] c59d1aed709a4294fa55993a90d680e0  (ref 2, GameObject)  ←  GameScene.CreatePool       # _preloadData.bullet
[Scene] e915d7faeb229354cbb477b7e07865a6  (ref 2, GameObject)  ←  BossDungeonScene.CreatePool # monsterRL
```

이 총알은 Global 프리로드가 아니라 **Scene 스코프**로 로드된다 → 티켓이 없었다면 씬 전환 시 refCount 0 → 언로드 → DDOL 인스턴스가 언로드된 에셋 참조(핑크). 티켓이 이 창을 닫는다.

### Global 프리로드 프리팹 (몬스터)

`Droid_Helmet_*`는 Global 프리로드라 refCount 흐름이:

```
Game 중:             Droid_Helmet_AR (ref 3)   ←  Global(1) + Scene 스코프(1) + 풀 티켓(1)
BossDungeon 진입 후:  Droid_Helmet_AR (ref 1)   ←  Global 만 남음
```

`1 → 3 → 1` — 티켓이 정확히 획득·반납된다. (Global이라 언로드 위험은 원래 없으나, 티켓 정산이 정상임을 확인)

---

## 2. 누수·회귀 없음 — 메모리 왕복 flat (핵심 성공 지표)

| 시점 | Global | Scene | Party | 합계 |
|---|---|---|---|---|
| 진입 직후 (체크1) | 204.0 | 100.3 | 54.3 | **358.6 MB** |
| 전체 왕복 후 (체크5) | 204.4 | 100.4 | 54.3 | **359.1 MB** |

Game → 팝업 5종(첫 오픈·재오픈) → BossDungeon → Game 왕복 후 **+0.5 MB(노이즈)**.
DDOL 제거에도 풀이 씬 넘어 누적되지 않고, 모든 버킷이 예측대로 "불변". **딴 것을 안 망가뜨렸다는 증명.**

| 지표 | P4 예측 | 실측 | 판정 |
|---|---|---|---|
| Global 핸들 메모리 | 불변 | 204.0 → 204.4 | ✅ |
| Scene 버킷 | 불변 | 100.3 → 100.4 | ✅ |
| Party | 불변 | 54.3 → 54.3 | ✅ |
| 왕복 후 합계 | 불변 | 358.6 → 359.1 | ✅ |
| 핑크 텍스처 (씬 왕복) | 없음 | 없음(육안) | ✅ |
| PoolRoot DDOL | 해제 | 해제 확인 | ✅ |

---

## 3. 잔존 인스턴스 경고 — 대부분 정상, 하나만 실질

측정이 통제 시나리오가 아니라 **실제 플레이**(몬스터 스폰·전투·인벤토리)라 인플라이트 인스턴스에 경고가 뜬다:

```
[PoolLeak] 'Droid_Helmet_AR' 미반환 3개    ← 필드몬스터 3마리 활성 중 전환 (정상)
[PoolLeak] 'Droid_Helmet_RL' 미반환 3개    ← (정상)
[PoolLeak] 'BulletTrail'     미반환 43개   ← 날아가던 총알 (정상)
[PoolLeak] 'UI_ItemSlot'     미반환 30/10/9개  ← ★ 실질 후보 (아래 §5)
```

- Pop된 인스턴스는 **씬의 자식**이라 씬 언로드로 파괴돼도 `Push`를 안 거쳐 카운트가 남는다 → 인플라이트는 정상.
- **정정(운영 지표)**: "잔존 경고 = 0"은 필드몬스터가 자동 스폰되는 씬에선 문자 그대로 달성 불가(진입만 해도 몬스터 활성). **실질 성공 신호는 §2의 "메모리 왕복 flat"**이며, 경고는 pass/fail 게이트가 아니라 **진단 도구**로 쓴다. (에디터/개발 빌드 전용)

---

## 4. 부수 관찰 — 재로드 비용 (버그 아님)

```
[LoadStats] c59d1aed... 3회 로드 / UI_ItemSlot 2회 / GameSceneCanvas_New 3회 ...
```

총알(Scene 스코프)이 씬 전환마다 언로드→재로드된다. "Scene 스코프 + lazy"의 예상된 히치 비용이지 정합성 버그가 아니다. Global 프리로드로 상주시킬지 Scene lazy로 둘지는 **히치 vs 상주 트레이드오프**(Phase 3 §4 정책)이고 별개 결정.

---

## 5. UI_ItemSlot 미반환 경고 — 조사 결과: 실제 버그 아님

`UI_ItemSlot`이 씬 전환마다 30/10/9개 "미반환"으로 잡혀 조사했다. **반납 경로는 존재하고 정상 작동한다**:

- 생성: `MakeSubItemAsync` → `Instantiate(prefab)` → Poolable → `Pool.Pop` ✓
- 반납: `UI_Inventory.OnDestroy()` → `Managers.Resource.Destroy(slot.gameObject)` → Poolable → `Pool.Push` ✓

전환 시 경고가 뜬 원인은 **씬 teardown의 파괴 순서 아티팩트**다:

```csharp
// UI_Inventory.cs:153-156
foreach (var slot in _activeSlots)
    if (slot != null) Managers.Resource.Destroy(slot.gameObject);  // 슬롯이 먼저 파괴되면 skip
```

씬 언로드 시 Unity가 오브젝트를 임의 순서로 파괴한다. 슬롯(`_contentParent` 자식)이 `UI_Inventory.OnDestroy`보다 먼저 파괴되면 `slot != null`이 false → `Push` 스킵 → `_activeCount` 미감소 → 경고. 하지만 **메모리는 씬 언로드로 이미 회수**되므로 실제 누수가 아니다.

→ **결론: 총알·몬스터와 동일 부류(인플라이트/teardown 아티팩트).** 유저가 인벤토리를 정상적으로 닫으면(씬 생존 중) 슬롯은 제대로 반납된다. 별도 수정 불요. 이 조사 자체가 "새 계측이 오탐을 걸러내는 데도 유효"함을 보여준다.

---

## 6. 변경 파일

| 파일 | 변경 |
|---|---|
| `ResourceRegistry.cs` | `TryAddRef(key)` — 로드 없이 refCount +1, 엔트리 없으면 false |
| `ResourceManager.cs` | `AcquirePoolRef/ReleasePoolRef` — 레지스트리 직접(스코프 우회) |
| `PoolManager.cs` | 티켓 acquire/release, DDOL 제거 + `EnsureRoot`, `CreatePool(…, sourceKey, …)`, 잔존 경고(dev) |
| `GameScene.cs` | CreatePool 4곳 key 전달 (bullet=`RuntimeKey.ToString()`, 몬스터3종=`addressableKey`) |
| `NormalDungeonScene.cs` / `BossDungeonScene.cs` | bullet·monsterRL 풀 key 전달 |

**구현 중 확정 사실**: ① `_preloadData.*`는 `AssetReferenceGameObject` → 티켓 키를 `.RuntimeKey.ToString()`로 맞춤(`LoadAsync(AssetReference)`와 동일 키잉). ② 던전 씬 2곳도 동일 병리 → 함께 티켓 배선.

---

## 7. 후속 — R4 + UI DDOL(진단 기반 재결론)

### R4 — NoCache use-after-release 차단
`LoadAsyncNoCache<T>`(핸들 Release 후 참조 반환)를 **`LoadTextAsync(key)`**로 교체 — 핸들 Release 전에 `.text`를 값으로 복사해 반환. JSON 파서(`DataManager.LoadAndCacheJsonAsync`)가 언로드 가능한 에셋을 만지던 잠재 크래시 제거. 구 메서드(footgun)는 삭제.

### UI 루트 DDOL — 진단 계측 → 스코프 재배치로 귀결
"전환 중 인플라이트 낭비"를 진단하려 프로브를 넣었으나, **기존 `[ResourceReport]`+`[LoadStats]`가 이미 범인을 담고 있었다**: 파티 HUD 아틀라스가 Scene 스코프라 전환마다 재로드(churn).

| 범인 | 조치 |
|---|---|
| `ActiveCharacterHUD.ChangeStaticDataAsync` → SkillIconAtlas | Scene → **Party** 스코프 |
| `PartyHUD.LoadEmblemFromAtlasAsync` → CharacterEmblemsAtlas | Scene → **Party** 스코프 |

**측정 근거로 DDOL 캔버스 대공사는 기각** — 메모리 왕복 flat(누수 0)이라 use-after-release가 없다. Phase 3에서 Dungeon 스코프를 근거 없이 안 만든 것과 같은 "근거 기반 축소". 진단 프로브는 목적 달성 후 제거(기존 계측과 중복).

**실측 결과(2026-07-28 02:24 세션)**:
- SkillIconAtlas·CharacterEmblemsAtlas: 재로드 **3회 → 0**(목록에서 소멸), `[AtlasMetric]` 재전개 없음. 두 아틀라스 [Party] 버킷으로 이동(party 4→6).
- 왕복 후 합계 358.6 → 359.1MB, Party +1.5MB(상주) = 의도된 "재로드 churn ↔ 소량 상주" 트레이드. 누수 0.

## 8. 남은 것

- 개발 빌드 실측 1회 (리팩터 전체 완성 조건 — 이월).
- (선택) 잔존 경고를 "풀 created 수가 재사용 없이 증가"(무한증식 신호)로 정밀화 — 현재는 진단용.
- (선택) Phase 5 매니페스트.
