# Phase 4 명세 — 풀·인스턴스 수명 통합 (R5)

> 대상: `PoolManager` / `ResourceRegistry` / `ResourceManager` / `GameScene.CreatePool` · 성격: **구현 명세 + 계획**
> 전제: Phase 3(에셋 핸들의 4스코프 재배치) 완료·검증됨. 에셋 핸들 수명은 스코프가 정산하나 **풀 인스턴스는 아직 그 바깥**.
> 상위: [Refactor_Plan.md](./Refactor_Plan.md)(§0.2 P4 · §Phase 4) · [Phase4_Plan.pdf](./Phase4_Plan.pdf) · [ProblemSolving_Log.md](./ProblemSolving_Log.md)(R4/R5)

---

## 0. 한 줄 요약

Phase 3가 **에셋 핸들**의 수명을 스코프로 정리했지만, `PoolManager`는 여전히 바깥에 있다.
풀은 인스턴스만 들고 **에셋 핸들의 refCount를 잡지 않으며**(`PoolManager.cs:20` `//_handle` 주석), 루트가 **DDOL**(`PoolManager.cs:95`)이라 씬을 넘어 영생한다.
Phase 4는 **풀이 자기 원본 프리팹의 refCount 티켓을 쥐게** 하고 **DDOL을 제거**해, "풀 인스턴스의 수명"과 "그 인스턴스가 참조하는 에셋 핸들의 수명"을 하나로 묶는다.

**성격 — 수치가 아니라 안전성.** 메모리 그래프는 거의 안 움직인다. 유일한 신규 성과 지표는 **스코프/풀 Dispose 시 잔존 인스턴스 경고 = 0**. 값어치는 Phase 3에서 잡은 use-after-release의 마지막 서식지(풀)를 닫는 것.

핵심 규칙: **티켓은 풀당 한 장**(인스턴스 개수 무관) · **티켓은 스코프를 거치지 않고 레지스트리에 직접**(이유는 §2).

---

## 1. 현 상태 진단 (grep 근거)

두 개의 관리자가 같은 프리팹에 대해 **서로 다른 수명**을 가진다.

| | 소유자 | 수명 | 근거 |
|---|---|---|---|
| **에셋 핸들**(프리팹 원본) | Scene 스코프 | 씬 전환 시 Dispose → 언로드 | `GameScene.cs:112` `LoadAsync<GameObject>(key)` (isGlobal 기본 false) |
| **풀 인스턴스**(복사본 N개) | `@Pool_Root` (DDOL) | 씬 넘어 영생 | `PoolManager.cs:95` `DontDestroyOnLoad(_root)` |

- 풀은 핸들 ref를 **안 잡는다** (`PoolManager.cs:20` `//_handle = handle;` 주석 처리).
- `DestroyPool`은 인스턴스만 파괴, **Addressables 반납 없음** (`PoolManager.cs:79~85`).
- 풀 key가 **`original.name`(문자열)** (`PoolManager.cs:104, 111, 139`) → 씬 넘어 Dictionary 누적 시 **낡은 풀이 신규 로드를 가로막는** 정합성 위험.

### 실패 시나리오

현재 씬 전환 순서 (`SceneManagerEx.cs:82→88→90`):
```
82  ChangeSceneScope()          → Scene 스코프 Dispose → 핸들 Release → refCount 0 → 프리팹 언로드
88  AssertSceneHandlesCleared() → (새 스코프는 빈 값이라 통과)
90  Pool.Clear()                → 인스턴스 파괴
```
82와 90 사이에 **위험 창**(에셋은 언로드됐는데 인스턴스는 살아있음)이 열린다. 지금 안 터지는 건 순전히 에디터의 Addressables 언로드 지연 덕 — **구조적 보장이 아니다.** 빌드에서 번들이 내려가면 핑크 텍스처.

---

## 2. 핵심 설계 결정

### 결정 A — 티켓은 스코프가 아니라 레지스트리에 직접

풀이 ref를 **Scene 스코프를 통해** 잡으면 소용없다:
- `ResourceScope`는 키를 HashSet으로 중복 제거(`ResourceScope.cs:32`). GameScene이 이미 그 키를 Scene 스코프에 넣었으므로 풀의 acquire는 **no-op**.
- `ChangeSceneScope()`가 Scene 스코프를 통째로 Dispose(`ResourceScope.cs:64`) → 풀 티켓도 함께 소멸 → 위험 창 부활.

→ 풀은 **스코프와 별개로 `ResourceRegistry.RefCount`를 직접 +1** 한다. 이러면 `ChangeSceneScope`가 스코프 ref를 반납해도 **풀 티켓이 남아 refCount가 0에 못 닿고**, 에셋은 풀이 명시적으로 반납할 때까지 살아있다 → **순서 의존 소멸**.
`AssertSceneHandlesCleared`(`ResourceManager.cs:516`)는 Scene 스코프의 `.Count`만 보므로, 스코프 밖 티켓은 이 검사에 **안 걸린다**. ✔

### 결정 B — 풀링 대상 두 부류를 구분

| 부류 | 예시 | 로드 경로 | 티켓 |
|---|---|---|---|
| **A. 사전 로드 + 명시적 CreatePool** | 총알, 필드몬스터 3종 | `GameScene`이 `LoadAsync<GameObject>(key)` → Scene 스코프 핸들 존재 | **필요** — 레지스트리에 엔트리가 있으므로 acquire |
| **B. SO 직접 참조 + 지연 풀 생성** | `Lightning`(VFX) | `SpawnVFX._vfxPrefab`(`SpawnVFX.cs:14`) 직접 참조 → 레지스트리 엔트리 없음 | **불필요** — Addressables 관리 대상 아님, 하드 참조가 수명 보장 |

- 부류 B는 `AcquirePoolRef`가 **false 반환**(엔트리 없음) → 티켓 없이 진행. 핑크 위험 원천 부재.
- 참고: 총구화염 3종(`RifleFireEffect`/`RocketFireEffect`/`Aris_Charging`)은 **Poolable이 없어** 애초에 풀링 안 됨(`Object.Instantiate`/`Destroy`) → 무관.

> **결정 요지**: 실제 변경은 **부류 A만**. 부류 B(VFX)는 작업 0.

---

## 3. 구현 명세 (6곳)

### ① `ResourceRegistry` — 로드 없이 ref만 증가

```csharp
// ResourceRegistry.cs — 기존 Release(key)와 짝
public bool TryAddRef(ResourceKey key)
{
    if (!_entries.TryGetValue(key, out Entry entry)) return false; // 부류 B면 실패
    entry.RefCount++;
    return true;
}
```

### ② `ResourceManager` — 풀 전용 래퍼

```csharp
// GameObject 키잉 고정(풀 원본은 항상 GameObject)
public bool AcquirePoolRef(string key)
    => _registry.TryAddRef(new ResourceKey(key, typeof(GameObject)));
public void ReleasePoolRef(string key)
    => _registry.Release(new ResourceKey(key, typeof(GameObject)));
```

### ③ `PoolManager` — key 저장 + acquire/release

```csharp
// Pool: sourceKey 필드 추가
string _sourceKey;

// Pool.Init: key를 받아 저장 + 티켓 획득 (//_handle 주석 부활의 실체)
public void Init(GameObject original, string sourceKey, int count = 5)
{
    Original  = original;
    _sourceKey = sourceKey;
    if (!string.IsNullOrEmpty(_sourceKey))
        Managers.Resource.AcquirePoolRef(_sourceKey);   // 부류 A만 성공
    ...
}

// Pool.DestroyPool: Root가 이미 파괴됐어도 티켓은 반드시 반납
public void DestroyPool()
{
    if (!string.IsNullOrEmpty(_sourceKey))
        Managers.Resource.ReleasePoolRef(_sourceKey);   // refCount -1
    if (Root != null)
        Object.Destroy(Root.gameObject);
}

// CreatePool: key 인자 추가 (부류 B는 null로 들어옴)
public void CreatePool(GameObject original, string sourceKey = null, int count = 10)
{
    if (original == null || _pool.ContainsKey(original.name)) return;
    Pool pool = new Pool();
    pool.Init(original, sourceKey, count);
    pool.Root.SetParent(_root);
    _pool.Add(original.name, pool);
}
```
지연 경로(`PoolManager.cs:118` `Pop → CreatePool(original)`)는 key 없이 호출 → `sourceKey=null` → 티켓 없음(부류 B). 그대로 둔다.

### ④ `GameScene.CreatePool` 호출부 — key 전달

```csharp
// GameScene.cs:113/120/127/134 — 이미 위에서 LoadAsync한 그 key를 함께 넘김
GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(key);
if (prefab != null) Managers.Pool.CreatePool(prefab, key, 30);
```
총알은 `_preloadData.bullet`, 몬스터 3종은 `MonsterBaseData.AddressableKey`를 그대로 사용.

### ⑤ `PoolManager.Init` — DDOL 제거 + 재생성 가드

```csharp
public void Init()
{
    if (_root == null)
        _root = new GameObject { name = "@Pool_Root" }.transform;   // DontDestroyOnLoad 삭제
}

// CreatePool 진입부에 재생성 가드 (씬 언로드로 _root가 파괴됐을 수 있음)
if (_root == null)
    _root = new GameObject { name = "@Pool_Root" }.transform;
```
- `@Pool_Root`가 씬과 함께 자동 파괴 → 인스턴스가 씬 넘어 영생하지 않음.
- `DestroyPool`이 Root null 여부와 무관하게 티켓을 반납하므로(③), 자동 파괴 후 `Pool.Clear()`가 남은 티켓만 정산.

### ⑥ 잔존 인스턴스 경고 (신규 검증 지표)

```csharp
// Pool: 활성(IsUsing) 인스턴스 카운트
int _activeCount;
// Pop 성공 시 _activeCount++, Push 시 _activeCount--
// DestroyPool 진입 시:
if (_activeCount > 0)
    GameLog.LogWarning($"[PoolLeak] '{Original.name}' 반환 안 된 인스턴스 {_activeCount}개 — 스폰 후 회수 누락");
```
"스폰만 하고 회수 안 함" 누수를 씬 정리 시점에 조기 탐지. §5 성공 기준의 유일한 새 지표.

---

## 4. 순서 무관 보장 (수정 후 타임라인)

```
LoadScene("Loading")   → 게임 씬 언로드 → 풀 인스턴스 자동 파괴 (에셋은 아직 살아있음)
SceneManagerEx:82  ChangeSceneScope          → Scene 스코프 Release → refCount 2→1 (풀 티켓 남음, 언로드 X) ✔
SceneManagerEx:88  AssertSceneHandlesCleared → 새 스코프 빈 값 → 통과 ✔
SceneManagerEx:90  Pool.Clear → DestroyPool  → 티켓 반납 → refCount 1→0 → 이제 언로드 (인스턴스 이미 없음) ✔
```
`Pool.Clear`를 82 앞으로 옮기든 뒤에 두든 **결과 동일** = 위험 창 소멸. 순서 의존이 구조 보장으로 바뀐다.

---

## 5. 실행 순서 + 검증

각 단계 = **독립 커밋**. 매 단계 **Scene 버킷·Global 핸들 불변(회귀 없음)** 을 계측이 자동 확인.

1. **① + ②** 레지스트리/매니저 API (동작 무변경, 컴파일만).
2. **③ + ④** 풀 티켓 배선 + GameScene key 전달 — 부류 A 티켓 획득/반납 성립.
3. **⑤** DDOL 제거 + 재생성 가드 — 영생 소멸.
4. **⑥** 잔존 인스턴스 경고 — 신규 지표.

### 성공 기준 (대부분 불변 = 성공)

| 지표 | P4 예측 | 판정 |
|---|---|---|
| Global 핸들 수 / 팝업·던전 증가 / AtlasSpriteCache | 불변 | 변하면 실패(딴 걸 안 망가뜨림 증명) |
| 진입 직후 Global · 세션 합계 | 불변 | 풀 프리팹 이미 로드돼 큰 변동 없음 |
| Scene 버킷 · 평시 프레임 | 불변 | 회귀 없음 |
| **스코프/풀 Dispose 시 잔존 인스턴스 경고** | **0** | **신규 검증 항목** |

완료 기준: **위 불변 확인 + 잔존 경고 0 + 씬 왕복 왕복 후 핑크 없음(에디터).** 개발 빌드 실측은 리팩터 전체 완성 조건으로 이월.

---

## 6. 주의 / 함정

- **티켓은 스코프 밖**: 스코프를 통해 잡으면 무효(§2 결정 A). 반드시 `AcquirePoolRef`/`ReleasePoolRef` = 레지스트리 직접.
- **티켓은 풀당 한 장**: 인스턴스 개수와 무관. Pop/Push는 refCount를 건드리지 않는다(순수 기능 층).
- **DestroyPool은 Root null이어도 티켓 반납**: DDOL 제거로 Root가 씬 언로드 시 먼저 파괴될 수 있음 → null 가드와 무관하게 반납 실행.
- **부류 B(VFX)는 손대지 않음**: `AcquirePoolRef` false 반환으로 자연 처리. `SpawnVFX._vfxPrefab`은 하드 참조라 언로드 위험 없음.
- **key 일관성**: 풀 티켓 키는 `new ResourceKey(key, typeof(GameObject))` — GameScene의 `LoadAsync<GameObject>(key)`와 동일 키잉이어야 한다.
- **에디터 열림 시 배치 컴파일 불가** / PS 커밋 메시지 따옴표는 `-F` 파일 경유.
