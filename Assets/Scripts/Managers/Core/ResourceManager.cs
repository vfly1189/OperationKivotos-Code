using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

using Cysharp.Threading.Tasks;
using UnityEngine.U2D;

// [Phase 0 계측] 디버그 창/리포트가 읽는 핸들 스냅샷 한 줄
public struct ResourceHandleDebugInfo
{
    public string Key;
    public string Bucket;    // "Global" / "Scene"
    public string TypeName;  // 로드 완료 시 실제 타입, 로딩 중이면 "(loading)"
    public bool IsDone;
    public string Source;    // 최초 로드를 요청한 호출자 (에디터/개발 빌드에서만 수집)
    public Object Asset;     // 로드 완료된 에셋 참조 (에디터 창의 메모리 측정용)
}

// 리소스 수명 계층 (Phase 3). 열거 순서 = 수명이 긴 것부터.
//  Global/Scene은 Init에서 상시 생성, 그 외는 도메인 경계에서 CreateScope로 생성·Dispose.
public enum ResourceScopeType
{
    Global,   // 부팅 → 종료. 절대 Dispose 안 함
    Scene,    // 씬 진입 → 다음 전환 (ChangeSceneScope가 자동 회전)
    Party,    // 파티 구성 → 해체/교체 (씬 전환을 넘어 생존) — Phase 3c
    Popup,    // 팝업 스택이 비어있지 않은 동안 (0→1에서 생성, 1→0에서 Dispose) — Phase 3b
}

public class ResourceManager
{
    //아틀라스 파편(Sprite) 보호용 강력한 글로벌 캐시
    private Dictionary<string, Sprite> _atlasSpriteCache = new Dictionary<string, Sprite>();


    private ResourceRegistry _registry;
    private Dictionary<ResourceScopeType, ResourceScope> _scopes = new Dictionary<ResourceScopeType, ResourceScope>();

    public void Init()
    {
        _registry = new ResourceRegistry();
        _scopes[ResourceScopeType.Global] = new ResourceScope(_registry, "Global");
        _scopes[ResourceScopeType.Scene] = new ResourceScope(_registry, "Scene");
    }

    #region 리소스 관리 신버전

    // 스코프 회전: 새 스코프로 교체하고 **이전 것은 반드시 Dispose**.
    //  Dispose를 빠뜨리면 이전 스코프가 쥔 refCount가 영영 안 풀려 조용히 누수된다
    //  (레지스트리에는 남고 소유 스코프는 사라지므로 LogAliveReport에 [?]로 잡힘).
    public ResourceScope CreateScope(ResourceScopeType type, string scopeName)
    {
        if (type == ResourceScopeType.Global)
        {
            GameLog.LogError("[Resource] Global 스코프는 회전할 수 없다 (수명 = 게임 전체).");
            return _scopes[ResourceScopeType.Global];
        }

        if (_scopes.TryGetValue(type, out ResourceScope old) && old != null)
            old.Dispose();

        return _scopes[type] = new ResourceScope(_registry, scopeName);
    }

    // 아직 생성되지 않은 스코프(Party 등)는 null — 호출부가 판단하도록 예외 대신 null 반환.
    public ResourceScope GetScope(ResourceScopeType type)
        => _scopes.TryGetValue(type, out ResourceScope scope) ? scope : null;

    // 도메인 경계에서 스코프를 닫는다 (파티 해체·던전 퇴장 등). Global은 무시.
    public void DisposeScope(ResourceScopeType type)
    {
        if (type == ResourceScopeType.Global) return;
        if (!_scopes.TryGetValue(type, out ResourceScope scope) || scope == null) return;

        scope.Dispose();
        _scopes.Remove(type);
    }

    public void ChangeSceneScope()
    {
        ResourceScope old = _scopes[ResourceScopeType.Scene];
        _scopes[ResourceScopeType.Scene] = new ResourceScope(_registry, "Scene");
        old.Dispose();
    }

    public async UniTask LoadAsyncPreload(
    string[] labels, bool isGlobal = false,
    System.Action<string, float> onProgress = null, CancellationToken token = default)
    {
        // Preload 할때 Global은 어차피 StartScene에서 할거임
        // 따라서 중간에 불리는 이 함수들은 무조건 Scope가 Scene에 한정됨.

        // Global 프리로드는 StartScene, 씬 중간 호출은 Scene — 스코프만 다르므로 먼저 결정
        ResourceScope scope = isGlobal ? _scopes[ResourceScopeType.Global]
                             : _scopes[ResourceScopeType.Scene];

        string labelText = string.Join(",", labels); // [Phase 2 계측] 출처 라벨

        // "이 라벨이 붙은 물건들의 '주소 카드'를 다 뽑아줘" (카드만! 물건 자체는 안 꺼냄)
        var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);
        try
        {
            await locationsHandle.ToUniTask(cancellationToken: token);
            if (locationsHandle.Status != AsyncOperationStatus.Succeeded) return;

            var locations = locationsHandle.Result;
            int total = locations.Count;
            for (int i = 0; i < total; i++)
            {
                var loc = locations[i];
                SetLoadSource(loc.PrimaryKey, $"Preload({labelText})"); // [Phase 2 계측]
                await scope.LoadAsync(loc, token);                    // (key, ResourceType)로 스코프에 acquire
                onProgress?.Invoke(loc.PrimaryKey, (i + 1f) / total);
            }
        }
        finally
        {
            if (locationsHandle.IsValid()) Addressables.Release(locationsHandle);
        }
    }

    #endregion


    // =========================================================================
    // 1. AssetReference를 인자로 받는 LoadAsync (씬에서 주로 사용)
    // =========================================================================
    //public async UniTask<T> LoadAsync<T>(AssetReference assetRef, bool isGlobal = false, CancellationToken token = default) where T : UnityEngine.Object
    //{
    //    if (assetRef == null || !assetRef.RuntimeKeyIsValid())
    //        return null;

    //    // AssetReference의 런타임 키를 string으로 변환해서 내부 처리 함수로 넘김
    //    return await LoadAsync<T>(assetRef.RuntimeKey.ToString(), isGlobal, token);
    //}

    public UniTask<T> LoadAsync<T>(AssetReference assetRef, bool isGlobal = false, CancellationToken token = default)
        where T : UnityEngine.Object
        => (assetRef == null || !assetRef.RuntimeKeyIsValid())
            ? UniTask.FromResult<T>(null)
            : LoadAsync<T>(assetRef.RuntimeKey.ToString(), isGlobal, token);

    // [Phase 3] AssetReference + 명시 스코프 (SO가 프리팹을 AssetReference로 들고 있는 경로용)
    public UniTask<T> LoadAsync<T>(AssetReference assetRef, ResourceScopeType scopeType, CancellationToken token = default)
        where T : UnityEngine.Object
        => (assetRef == null || !assetRef.RuntimeKeyIsValid())
            ? UniTask.FromResult<T>(null)
            : LoadAsync<T>(assetRef.RuntimeKey.ToString(), scopeType, token);

    public UniTask<T> LoadAsync<T>(string key, bool isGlobal = false, CancellationToken token = default)
        where T : UnityEngine.Object
    {
        RecordLoadSource(key); // [Phase 2 계측] 최초 로드 요청자 기록 (에디터/개발 빌드 전용, 동작 무변경)
        ResourceScope scope = isGlobal ? _scopes[ResourceScopeType.Global] : _scopes[ResourceScopeType.Scene];
        return scope.LoadAsync<T>(key, token);
    }

    // [Phase 3] 수명 스코프를 명시하는 로드. bool isGlobal은 2버킷 시절의 잔재라
    //  Party/Popup/Dungeon 같은 새 수명을 표현할 수 없다 — 신규 호출부는 이쪽을 쓴다.
    //  해당 스코프가 아직 없으면(생성 훅 누락) 로드하지 않고 에러 — 조용히 Global로
    //  새는 것보다 즉시 드러나는 편이 낫다(2버킷 도피의 재발 방지).
    public UniTask<T> LoadAsync<T>(string key, ResourceScopeType scopeType, CancellationToken token = default)
        where T : UnityEngine.Object
    {
        ResourceScope scope = GetScope(scopeType);
        if (scope == null)
        {
            GameLog.LogError($"[Resource] {scopeType} 스코프가 없다 — 생성 훅 누락. key={key}");
            return UniTask.FromResult<T>(null);
        }

        RecordLoadSource(key);
        return scope.LoadAsync<T>(key, token);
    }

    // =========================================================================
    // 3. NoCache 로드 (문자열 string Key 기반) -> DataManager에서 JSON 부를 때 사용
    // =========================================================================
    public async UniTask<T> LoadAsyncNoCache<T>(string key) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key)) return null;

        // string key를 사용해 로드
        var handle = Addressables.LoadAssetAsync<T>(key);

        // ToUniTask로 대기할 때 에러가 나면 잡을 수 있도록 안전하게 처리
        T result = null;
        try
        {
            result = await handle.ToUniTask();
        }
        catch (Exception e)
        {
            GameLog.LogError($"[ResourceManager] NoCache Load Exception: {key} / {e.Message}");
        }

        if (handle.Status == AsyncOperationStatus.Succeeded && result != null)
        {
            Addressables.Release(handle);
            return result;
        }
        else
        {
            GameLog.LogError($"[ResourceManager] Addressable NoCache Load Failed: {key}");
            // 실패했을 때도 핸들이 유효하면 메모리 해제
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }
    }

    // =========================================================================
    //  SpriteAtlas 특화 로드 및 추출 함수
    // =========================================================================
    public async UniTask<Sprite> GetSpriteFromAtlasAsync(string atlasKey, string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;

        // 1. 방어 캐시에 안전하게 보관 중이라면 반환
        if (_atlasSpriteCache.TryGetValue(spriteName, out Sprite cachedSprite))
        {
            if (cachedSprite != null) return cachedSprite;
        }

        // 2. 캐시에 없으면 아틀라스 자체를 어드레서블로 로드
        SpriteAtlas atlas = await LoadAsync<SpriteAtlas>(atlasKey, isGlobal: true);

        if (atlas != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var expandSw = System.Diagnostics.Stopwatch.StartNew(); // [Phase 0.5 계측] 전량 전개 = 메인 스레드 히치 후보
#endif
            // 3. 아틀라스를 여는 순간, 내부의 모든 Sprite 조각을 캐시에 등록
            Sprite[] allSprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(allSprites);

            foreach (var s in allSprites)
            {
                // (Clone) 글자 떼기
                string cleanName = s.name.Replace("(Clone)", "");

                // 캐시에 등록
                if (!_atlasSpriteCache.ContainsKey(cleanName))
                {
                    _atlasSpriteCache.Add(cleanName, s);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            expandSw.Stop();
            ResourceMetrics.RecordAtlasExpansion(atlasKey, allSprites.Length, expandSw.ElapsedMilliseconds); // [Phase 0.5 계측]
#endif

            // 4. 이제 안전하게 캐시에서 꺼내서 반환
            if (_atlasSpriteCache.TryGetValue(spriteName, out Sprite targetSprite))
            {
                return targetSprite;
            }
            else
            {
                GameLog.LogWarning($"[ResourceManager] '{atlasKey}' 아틀라스에 '{spriteName}' 이미지가 없습니다.");
            }
        }
        return null;
    }


    // =========================================================================
    // [실무 패턴] 로드-필요시-생성 통합 API
    // 동기 Instantiate(프리로드 필수)와 async Load를 손으로 잇던 걸 한 호출로 통합.
    // 취소 토큰을 로드까지 전파하여 씬 이탈/디스폰 중 use-after-teardown 방지.
    // =========================================================================
    public async UniTask<GameObject> InstantiateAsync(
        string key, Vector3 position, Quaternion rotation,
        Transform parent = null, bool isGlobal = false, CancellationToken token = default)
    {
        GameObject prefab;
        try
        {
            prefab = await LoadAsync<GameObject>(key, isGlobal, token);
        }
        catch (OperationCanceledException)
        {
            // 씬 이탈/디스폰으로 취소되면 예외 대신 null로 흡수 (호출부의 == null 방어와 일관)
            return null;
        }

        if (prefab == null || token.IsCancellationRequested) return null;

        return Instantiate(prefab, position, rotation, parent);
    }


    public GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        // 1. 원본 프리팹의 활성화 상태를 잠시 끄고 복사
        // (이렇게 하면 생성될 때 Awake는 돌지만 OnEnable과 물리 처리는 돌지 않음)
        bool wasActive = original.activeSelf;
        if (wasActive) original.SetActive(false);
        // 1. 생성 (풀링 혹은 인스턴스화)
        GameObject go = Instantiate(original, parent); // 기존 Instantiate(GameObject) 활용


        // 원상 복구
        if (wasActive) original.SetActive(true);


        // go가 제대로 생성되었을 때만 처리 (안전망)
        if (go != null)
        {
            // 2. 위치/회전 설정
            var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();

            if (agent != null)
            {
                // [Agent가 있는 경우] 
                // 위치는 무조건 Warp로 이동시켜야 씹히지 않음
                agent.Warp(position);
                // 단, Warp는 회전을 처리해주지 않으므로 회전은 따로 적용
                go.transform.rotation = rotation;
            }
            else
            {
                // [Agent가 없는 경우 (플레이어 등)] 
                // 일반적인 Transform 방식으로 위치와 회전 모두 적용
                go.transform.position = position;
                go.transform.rotation = rotation;
            }

            // 3. 모든 세팅이 완벽히 끝난 후 오브젝트 활성화
            go.SetActive(true);
        }

        return go;
    }


    public GameObject Instantiate(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (_registry.TryGetAsset<GameObject>(key, out var original))
            return Instantiate(original, position, rotation, parent);

        GameLog.LogError($"[ResourceManager] 에셋이 로드되지 않았거나 찾을 수 없습니다. Key: {key}\n" +
                         $"미리 LoadAsync로 로딩해두었는지 확인하세요.");
        return null;
    }


    public GameObject Instantiate(GameObject original, Transform parent = null)
    {
        // 1. Poolable이 붙어있으면 풀 매니저에게 위임
        if (original.GetComponent<Poolable>() != null)
        {
            return Managers.Pool.Pop(original, parent).gameObject;
        }

        // 2. 아니면 그냥 생성
        GameObject go = Object.Instantiate(original, parent);
        go.name = original.name; // (Clone) 떼기
        return go;
    }



    public void Destroy(GameObject go)
    {
        if (go == null)
            return;

        //만약에 풀링이 필요한 아이라면 -> 풀링 매니저한테 위탁
        Poolable poolable = go.GetComponent<Poolable>();
        if (poolable != null)
        {
            Managers.Pool.Push(poolable);
            return;
        }

        Object.Destroy(go);
    }

    // =========================================================================
    // [Phase 0 계측] 디버그 스냅샷 · 로드 출처 추적 · 씬 전환 리포트
    //  - 동작 무변경: 읽기 전용 관찰 + 로그만. 출처 수집은 에디터/개발 빌드 전용.
    //  - 리팩토링(Phase 2) 후에는 레지스트리를 가리키도록 유지되는 영구 자산.
    // =========================================================================
    #region Phase 0 계측

    // key → 최초 로드를 요청한 호출자. 에디터/개발 빌드에서만 채워짐
    private readonly Dictionary<string, string> _loadSources = new Dictionary<string, string>();

    // 디버그 창/리포트가 매 프레임 호출해도 부담 없도록 재사용하는 레지스트리 스냅샷 버퍼
    private readonly List<ResourceRegistry.DebugEntry> _regSnapshot = new List<ResourceRegistry.DebugEntry>();

    // [Phase 2] 카운트는 이제 스코프(버킷) 소유 수 / 레지스트리 고유 핸들 수를 가리킨다.
    public int GlobalHandleCount => _scopes[ResourceScopeType.Global].Count;
    public int SceneHandleCount => _scopes[ResourceScopeType.Scene].Count;

    // [Phase 3] 현재 살아있는 스코프 이름 → 소유 핸들 수. 버킷이 2개로 고정이 아니게 되면서
    //  디버그 창/리포트가 하드코딩 대신 이걸 순회한다 (새 스코프가 Scene에 오분류되던 문제 차단).
    public void GetScopeCounts(Dictionary<string, int> buffer)
    {
        buffer.Clear();
        foreach (var kv in _scopes)
            if (kv.Value != null) buffer[kv.Value.Name] = kv.Value.Count;
    }
    public int RegistryHandleCount => _registry.Count;   // 서로 다른 (key,type) 핸들 총수
    public int AtlasSpriteCacheCount => _atlasSpriteCache.Count;

    // 레지스트리 엔트리에 Global/Scene 버킷 라벨 부여 — 어느 스코프가 이 키를 소유하는가
    private string ResolveBucket(ResourceKey rk)
    {
        foreach (var kv in _scopes)
            if (kv.Value.Contains(rk)) return kv.Value.Name;
        return "?"; // 소유 스코프 없음(과도기/직접 로드) — 정상 흐름에선 나오지 않음
    }

    // 디버그 창이 매 프레임 호출해도 부담 없도록 버퍼 재사용 방식.
    //  Global 버킷을 먼저 방출 — 디버그 창 MeasureMemory가 공유 의존성(아틀라스 텍스처 등)을
    //  "먼저 나온 버킷"에 한 번만 귀속시키므로, Global 우선 순서를 유지해야 Baseline v2와 대조된다.
    public void GetHandleSnapshot(List<ResourceHandleDebugInfo> buffer)
    {
        buffer.Clear();
        _registry.GetSnapshot(_regSnapshot);

        for (int pass = 0; pass < 2; pass++) // pass 0 = Global, pass 1 = 나머지(Scene 등)
        {
            foreach (var e in _regSnapshot)
            {
                string bucket = ResolveBucket(e.Key);
                bool isGlobal = bucket == "Global";
                if (pass == 0 ? !isGlobal : isGlobal) continue;

                buffer.Add(new ResourceHandleDebugInfo
                {
                    Key = e.Key.Key,
                    Bucket = bucket,
                    TypeName = e.TypeName,
                    IsDone = e.IsDone,
                    Source = _loadSources.TryGetValue(e.Key.Key, out var src) ? src : "?",
                    Asset = e.Asset,
                });
            }
        }
    }

    // 씬 전환 시점에 살아있는 핸들 전체를 로그로 남김 (Baseline 측정용)
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void LogAliveReport(string tag)
    {
        _registry.GetSnapshot(_regSnapshot);

        var sb = new System.Text.StringBuilder();
        sb.Append($"[ResourceReport] ({tag}) handles={_registry.Count}");
        foreach (var kv in _scopes)
            if (kv.Value != null) sb.Append($" {kv.Value.Name.ToLowerInvariant()}={kv.Value.Count}");
        sb.AppendLine($" atlasSprites={_atlasSpriteCache.Count}");
        foreach (var e in _regSnapshot)
        {
            string bucket = ResolveBucket(e.Key);
            string src = _loadSources.TryGetValue(e.Key.Key, out var s) ? s : "?";
            sb.AppendLine($"  [{bucket}] {e.Key.Key}  (ref {e.RefCount}, {e.TypeName})  ←  {src}");
        }
        GameLog.Log(sb.ToString());
    }

    // 씬 스코프가 비었어야 하는 시점의 누수 검출
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void AssertSceneHandlesCleared(string context)
    {
        ResourceScope sceneScope = _scopes[ResourceScopeType.Scene];
        if (sceneScope.Count == 0) return;

        _registry.GetSnapshot(_regSnapshot);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[ResourceLeak] 씬 스코프에 핸들 {sceneScope.Count}개 잔존! ({context})");
        foreach (var e in _regSnapshot)
        {
            if (!sceneScope.Contains(e.Key)) continue;
            string src = _loadSources.TryGetValue(e.Key.Key, out var s) ? s : "?";
            sb.AppendLine($"  [S] {e.Key.Key}  (ref {e.RefCount})  ←  {src}");
        }
        GameLog.LogError(sb.ToString());
    }

    // 스택에서 ResourceManager/플러밍 프레임을 걷어내고 실제 호출자를 찾음
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void RecordLoadSource(string key)
    {
        if (_loadSources.ContainsKey(key)) return;

        var st = new System.Diagnostics.StackTrace(1, false);
        for (int i = 0; i < st.FrameCount; i++)
        {
            var method = st.GetFrame(i).GetMethod();
            var type = method?.DeclaringType;
            if (type == null) continue;

            // async 상태머신은 컴파일러 생성 중첩 타입 → 바깥 타입이 실제 소유자
            var owner = type.DeclaringType ?? type;
            if (owner == typeof(ResourceManager)) continue;
            if (owner.Namespace != null &&
                (owner.Namespace.StartsWith("Cysharp") || owner.Namespace.StartsWith("System") || owner.Namespace.StartsWith("UnityEngine")))
                continue;

            // "<LoadAsync>d__7" 같은 상태머신 타입명에서 원래 메서드명 복원
            string methodName = method.Name;
            if (type.Name.StartsWith("<"))
            {
                int end = type.Name.IndexOf('>');
                if (end > 1) methodName = type.Name.Substring(1, end - 1);
            }

            _loadSources[key] = $"{owner.Name}.{methodName}";
            return;
        }
        _loadSources[key] = "(async 연결점 추적 불가)";
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void SetLoadSource(string key, string source)
    {
        if (!_loadSources.ContainsKey(key)) _loadSources[key] = source;
    }

    // [Phase 0.5 계측] 아틀라스 캐시 스프라이트가 실제로 바인딩한 텍스처 목록
    //  - 텍스처 이름이 "SpriteAtlasTexture-..."면 페이지 바인딩(정상 패킹), 개별 이름이면 원본 폴백
    public void GetAtlasCacheTextures(Dictionary<Texture2D, List<string>> buffer)
    {
        buffer.Clear();
        foreach (var kv in _atlasSpriteCache)
        {
            var tex = kv.Value != null ? kv.Value.texture : null;
            if (tex == null) continue;
            if (!buffer.TryGetValue(tex, out var names))
            {
                names = new List<string>();
                buffer[tex] = names;
            }
            names.Add(kv.Key);
        }
    }

    #endregion
}
