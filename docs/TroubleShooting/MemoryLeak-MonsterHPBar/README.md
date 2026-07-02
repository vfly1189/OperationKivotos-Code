# 트러블슈팅: 씬 반복 전환 시 UI 오브젝트 메모리 누수 추적 및 해결

> **한 줄 요약** — 비동기 리소스 파이프라인을 점검하던 중, 던전을 반복 왕복하면 UI 오브젝트가 계속 누적되는 것을 Memory Profiler로 추적했고, **`DontDestroyOnLoad` 캔버스에 방치된 몬스터 HP바**가 원인임을 특정해 생명주기 정리로 누수를 제거했다.

| 항목 | 내용 |
|---|---|
| **문제** | 씬(GameScene ↔ Dungeon) 반복 전환 시 uGUI 오브젝트가 왕복에 비례해 누적 |
| **측정** | Memory Profiler `Compare Snapshots` + `Shortest Path To Root` (변수 통제 시나리오) |
| **원인** | 몬스터 HP바가 DDoL 월드 캔버스에 붙는데, 사망 시 `Destroy`가 아닌 `SetActive(false)`만 호출 → 자가정리 `LateUpdate`가 비활성 상태에서 실행되지 않아 영구 방치 (+ 이벤트 구독 미해제) |
| **해결** | 정리 책임을 HP바의 매 프레임 자가진단에서 **소유자(몬스터)의 생명주기로 이전** — 사망/디스폰 시 명시적 파괴 + 구독 해제 |
| **결과** | 동일 시나리오 재측정 시 **`GameObject` 증가 +291 → +3**, uGUI 누적(`RectTransform`/`CanvasRenderer`/`Sprite`) **완전 소멸** |

---

## 1. 배경

리소스/비동기 로딩 파이프라인을 점검하던 중, 게임의 코어 루프가 `GameScene ↔ Dungeon`을 반복 전환하는 구조(허브형)임에 주목했다. 씬을 오갈 때마다 씬 단위 Addressable 핸들이 로드/해제되는데, **이 해제가 실제로 완전한지**를 검증하기로 했다.

> 목표: "메모리를 최적화했다"는 주장이 아니라, **통제된 측정으로 누수 유무를 증명**하는 것.

---

## 2. 측정 환경 및 시나리오 (변수 통제)

측정 신뢰도의 8할은 환경 통제다. 아래 조건을 고정했다.

- **비교 지점 고정**: 두 스냅샷 모두 **동일한 논리적 상태**(같은 GameScene · 게임플레이 정지 · 캐시 워밍업 후)에서 캡처
- **캐시 워밍업**: 던전 1회 왕복을 먼저 수행해 **일회성 캐시**(SpriteAtlas, 셰이더 배리언트)를 채운 뒤 baseline 캡처 → 첫 로드의 일회성 증가가 "가짜 누수"로 잡히는 것을 방지
- **게임플레이 변동 제거**: 스냅샷 직전 스폰/전투를 정지시켜 "살아있는 오브젝트 수" 변동 노이즈를 제거
- **GC 정리**: 캡처 직전 `GC.Collect()`로 관리 힙이 "살아있는 객체"만 반영하도록

### 측정 절차

```text
1. 게임 시작 → GameScene 진입 완료
2. (워밍업) GameScene → Dungeon → GameScene 1회
3. GameScene에서 GC.Collect() → [스냅샷 A]
4. GameScene ↔ Dungeon N회 반복 왕복
5. GameScene 복귀 → GC.Collect() → [스냅샷 B]
6. Compare(A, B) → 오브젝트 "개수" 증가분 분석
```

> **지표 선택**: 바이트(Managed Heap)는 GC 상태에 따라 출렁여 노이즈가 크므로, **오브젝트 개수(Count)** 증가를 1차 지표로 사용했다.

---

## 3. 1차 측정 결과 (Before)

`Unity Objects` 테이블을 `Count Difference` 내림차순으로 정렬한 결과.

| 타입 | Δ Count | A → B |
|---|---:|---|
| GameObject | **+291** | 91 → 382 |
| MonoBehaviour | **+288** | 17 → 305 |
| RectTransform | **+288** | 25 → 313 |
| Sprite | +171 | 171 → 342 |
| CanvasRenderer | +144 | 17 → 161 |
| Texture2D | +1 | 4 → 5 |
| Mesh / Material / Shader | 0 | (변화 없음) |

**두 가지 사실이 드러났다.**

1. **무거운 Addressable 에셋(Texture2D · Mesh · Material · Shader)은 평평** → 씬 핸들 해제 로직은 정상 동작.
2. 증가분이 **전부 uGUI 계층**(GameObject / RectTransform / CanvasRenderer / Sprite)에 몰려 있음 → 런타임 생성 UI가 정리되지 않고 누적.

> 📷 `images/before-compare.png` — Compare 결과 스크린샷

---

## 4. 근본 원인 분석

증가한 UI 오브젝트 하나를 선택해 **Shortest Path To Root**로 참조 경로를 추적했다.

```text
Fill → RectTransform "MonsterHPBar" → "@Canvas_World" → "@UI_Root" → ROOT: Scene "DontDestroyOnLoad"
```

> 📷 `images/shortest-path-to-root.png` — 참조 경로 스크린샷

누적 대상은 **몬스터 HP바**였고, 그 뿌리가 **`DontDestroyOnLoad`** 씬이었다. 코드를 추적해 **3중 원인**을 특정했다.

**① 생성** — HP바를 DDoL 월드 캔버스에 부착 (`MonsterController` / `NormalMonsterController`)

```csharp
Transform uiParent = Managers.UI.CanvasWorld.transform; // @Canvas_World = DontDestroyOnLoad
_hpBar = await Managers.UI.MakeSubItemAsync<UI_MonsterHPBar>("MonsterHPBar", uiParent);
Stat.OnHpChanged += _hpBar.UpdateHpBar;                  // 구독만, 해제 없음
```

**② 사망** — 파괴가 아니라 비활성화만 수행

```csharp
if (_hpBar != null) _hpBar.gameObject.SetActive(false); // ← Destroy 아님
```

**③ 유일한 파괴 경로** — HP바 자신의 `LateUpdate` 내 null 체크 (`UI_MonsterHPBar`)

```csharp
private void LateUpdate()
{
    if (_targetTr == null) { Managers.Resource.Destroy(gameObject); return; }
    // ...
}
```

### 왜 새는가

- **비활성 GameObject는 `LateUpdate`가 실행되지 않는다.** 몬스터가 죽으면 `SetActive(false)` → 그 순간부터 자가정리 null 체크가 영원히 실행되지 않아 HP바가 비활성 상태로 방치된다.
- 부모 캔버스가 **`DontDestroyOnLoad`**라 **씬 언로드로도 정리되지 않는다.**
- 사망 시 `OnHpChanged` **구독 해제(`-=`)가 없어** 매니지드 참조까지 남아 GC 대상에서도 제외된다.

→ **몬스터를 죽일 때마다 비활성 HP바가 캔버스에 하나씩 쌓이고, 씬을 왕복해도 사라지지 않는다.** 1차 측정의 UI 누적이 정확히 이 현상이었다.

---

## 5. 해결

정리 책임을 **HP바의 매 프레임 자가진단**에서 **소유자(몬스터)의 생명주기**로 이전했다. (결정론적 정리)

```csharp
// 사망/디스폰 시
if (_hpBar != null)
{
    Stat.OnHpChanged -= _hpBar.UpdateHpBar;       // ① 이벤트 구독 해제
    Managers.Resource.Destroy(_hpBar.gameObject); // ② 실제 파괴 (풀 대상이면 풀 반환)
    _hpBar = null;                                 // ③ 참조 해제
}
```

### ⚠️ 2차 이슈 — 풀링 대상의 생명주기 대칭

"사망 시 파괴"만 추가하면 **풀에서 재사용되는 몬스터**에서 새로운 버그가 생긴다. HP바는 `Start()`에서만 생성되는데, `Start()`는 **풀 재사용(OnEnable) 시 다시 실행되지 않는다.** 따라서 첫 사망으로 HP바가 파괴된 뒤 몬스터가 부활하면 **HP바가 사라진 채로 되살아난다.**

→ **파괴(사망)와 생성(스폰)을 반드시 짝지어야 한다.** 생성 로직을 공용 메서드로 추출하고, 최초 스폰과 풀 재사용을 구분(`_initialized` 플래그)해 이중 생성 없이 재생성한다.

```csharp
protected override void OnEnable()
{
    base.OnEnable();
    // 풀 재사용 시: 사망 때 파괴된 HP바를 다시 생성 (최초 Start 이후에만)
    if (_initialized && _hpBar == null)
        CreateHpBarAsync().Forget();
}
```

**설계 관점의 교훈**: HP바 수명을 "매 프레임 null 체크"라는 **간접적·비결정적 신호**에 의존시킨 것이 취약점이었다. 생성한 주체가 파괴까지 책임지는 **결정론적 생명주기**로 바꾸되, 풀링 대상은 **생성/파괴 시점을 스폰/디스폰에 대칭으로** 맞춰야 한다. (방어선으로, 씬 전환 시 월드 캔버스를 일괄 정리하는 훅도 고려 가능하다.)

---

## 6. 재측정 결과 (After)

수정 후 **동일 시나리오**로 다시 측정했다.

| 타입 | 수정 전 Δ | 수정 후 Δ |
|---|---:|---:|
| GameObject | +291 | **+3** |
| MonoBehaviour | +288 | **~0** (목록에서 소멸) |
| RectTransform | +288 | **~0** |
| Sprite | +171 | **~0** |
| CanvasRenderer | +144 | **~0** |

uGUI 누적이 **완전히 사라졌다.** HP바 생명주기 정리가 의도대로 동작함을 확인.

> 📷 `images/after-compare.png` — 수정 후 Compare 결과 스크린샷

---

## 7. 누수(Leak) vs 풀링(Pooling) 구분

재측정에 남은 잔여 `+3`(GameObject / Transform / ParticleSystem / ParticleSystemRenderer)을 **같은 방법으로** 추적했다.

```text
Sparks → "Droid_Helmet_RL" → "Droid_Helmet_RL_Pool_Root" → "@Pool_Root" → ROOT: Scene "DontDestroyOnLoad"
```

- `Sparks`(타격 VFX)는 **몬스터의 자식**이고, 그 몬스터는 **오브젝트 풀(`@Pool_Root`)에 파킹**된 상태였다.
- 즉 이것은 **누수가 아니라 정상적인 풀 성장**이다. 풀은 **피크 동시 수요만큼** 커진 뒤 평평해지며, HP바처럼 왕복마다 무한 증가하지 않는다.

> **핵심**: 측정으로 **진짜 누수(HP바 — 수정)와 정상 풀 성장(VFX — 유지)을 구분**했다. 풀 성장을 누수로 오판해 "수정"했다면 오히려 개악이었을 것이다.

---

## 8. 회고

- **측정 없는 최적화는 주장에 불과하다.** 변수를 통제한 재현 시나리오 → 도구 기반 근본원인 특정 → 수정 → **재측정 검증**까지의 루프를 세웠다.
- Unity의 **`DontDestroyOnLoad` + 비활성 오브젝트의 생명주기 함정**은 실무에서도 흔한 누수 원인이다. 정리 책임을 **결정론적 소유자 생명주기**에 두는 것이 재발 방지의 핵심.
- 이 절차는 **재사용 가능**하다 — 동일한 방법으로 잔여 후보(VFX)까지 분석해 "누수 vs 풀링"을 판정했다.

### 후속 조치

- [x] 베이스 `MonsterController` / `NormalMonsterController` 양쪽에 사망-파괴 + 스폰-재생성 대칭 적용 (파생 클래스 일관성 확보)
- [ ] 풋프린트가 문제가 될 경우, 몬스터에 박힌 타격 VFX를 **공용 VFX 풀에서 온디맨드 생성**하는 방식으로 분리 검토

> **검증 필요(에디터)**: ①몬스터 처치 후 풀 재사용으로 부활 시 HP바가 정상 표시되는지, ②동일 시나리오 재측정 시 누수가 여전히 0인지.

---

### 첨부 이미지 (별도 캡처 후 `images/`에 배치)

| 파일 | 내용 |
|---|---|
| `images/before-compare.png` | 수정 전 Compare (uGUI 누적) |
| `images/shortest-path-to-root.png` | HP바 참조 경로 (→ DontDestroyOnLoad) |
| `images/after-compare.png` | 수정 후 Compare (누적 소멸) |
| `images/pooling-sparks.png` | 잔여 VFX의 풀 경로 (→ @Pool_Root) |
