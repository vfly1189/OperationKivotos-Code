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

public class ResourceManager
{
    // Addressables 핸들 관리용 딕셔너리 2개 분리
    // 1. 글로벌: 게임 종료 시까지 절대 해제되지 않음 (플레이어 캐릭터, UI, 공통 VFX 등)
    private Dictionary<string, AsyncOperationHandle> _globalHandles = new Dictionary<string, AsyncOperationHandle>();

    // 2. 씬: 씬 이동(Clear) 시마다 모두 해제되어 메모리 확보 (맵, 몬스터, 환경음 등)
    private Dictionary<string, AsyncOperationHandle> _sceneHandles = new Dictionary<string, AsyncOperationHandle>();

    //아틀라스 파편(Sprite) 보호용 강력한 글로벌 캐시
    private Dictionary<string, Sprite> _atlasSpriteCache = new Dictionary<string, Sprite>();



    public void Init()
    {
        //global은 계속 살려둘거임
        _sceneHandles.Clear();

        
    }

    // =========================================================================
    // 1. AssetReference를 인자로 받는 LoadAsync (씬에서 주로 사용)
    // =========================================================================
    public async UniTask<T> LoadAsync<T>(AssetReference assetRef, bool isGlobal = false, CancellationToken token = default) where T : UnityEngine.Object
    {
        if (assetRef == null || !assetRef.RuntimeKeyIsValid())
            return null;

        // AssetReference의 런타임 키를 string으로 변환해서 내부 처리 함수로 넘김
        return await LoadAsync<T>(assetRef.RuntimeKey.ToString(), isGlobal, token);
    }
  
    public UniTask<T> LoadAsync<T>(string key, bool isGlobal = false, CancellationToken token = default) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key)) return UniTask.FromResult<T>(null);

        RecordLoadSource(key); // [Phase 0 계측] 최초 로드 요청자 기록 (에디터/개발 빌드 전용, 동작 무변경)

        if (_globalHandles.TryGetValue(key, out var gh) && gh.IsDone && gh.Result is T gResult)
            return UniTask.FromResult(gResult);

        if (_sceneHandles.TryGetValue(key, out var sh) && sh.IsDone && sh.Result is T sResult)
            return UniTask.FromResult(sResult);

        return LoadAsyncInternal<T>(key, isGlobal, token);
    }

    // 실제 비동기 로직 분리
    private async UniTask<T> LoadAsyncInternal<T>(string key, bool isGlobal, CancellationToken token = default) where T : UnityEngine.Object
    {

        bool wasGlobal = false;
        AsyncOperationHandle handleToRelease = default;

        // 1. 타입 불일치 핸들 제거 및 원래 글로벌 소속이었는지 기억하기
        if (_globalHandles.TryGetValue(key, out var gh) && gh.IsDone && !(gh.Result is T))
        {
            wasGlobal = true; // 아! 얘는 처음에 Global로 프리로드 했던 애구나!
            _globalHandles.Remove(key);
            handleToRelease = gh; // 즉시 해제하면 메모리가 날아갈 수 있으니 임시 보관
        }
        if (_sceneHandles.TryGetValue(key, out var sh) && sh.IsDone && !(sh.Result is T))
        {
            _sceneHandles.Remove(key);
            handleToRelease = sh;
        }

        // 로딩 중인 핸들 있으면 기다리기
        if (_globalHandles.TryGetValue(key, out var pendingGlobal) && !pendingGlobal.IsDone)
        {
            await pendingGlobal.ToUniTask(cancellationToken: token);
            return pendingGlobal.Result as T;
        }
        if (_sceneHandles.TryGetValue(key, out var pendingScene) && !pendingScene.IsDone)
        {
            await pendingScene.ToUniTask(cancellationToken: token);
            return pendingScene.Result as T;
        }

        // 2. 새로운 타입(예: Sprite)으로 다시 로드
        var handle = Addressables.LoadAssetAsync<T>(key);


        if (isGlobal || wasGlobal) _globalHandles[key] = handle;
        else _sceneHandles[key] = handle;

        await handle.ToUniTask(cancellationToken: token);

        // 새 핸들 로드가 완전히 끝난 후 예전 핸들 해제 
        if (handleToRelease.IsValid())
        {
            Addressables.Release(handleToRelease);
        }

        if (handle.Status == AsyncOperationStatus.Succeeded) return handle.Result as T;

        GameLog.LogError($"[ResourceManager] Load Failed: {key}");
        if (isGlobal || wasGlobal) _globalHandles.Remove(key);
        else _sceneHandles.Remove(key);
        return null;
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
    // 프리로딩 전용 함수
    // =========================================================================
    public async UniTask LoadDependenciesAsync(IEnumerable<string> labels, bool isGlobal = false, System.Action<string, float> onProgress = null)
    {
        // [Phase 0 계측] 프리로드 소요 시간 측정 (Baseline: P6 순차 로드의 Before 수치)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        string labelText = string.Join(",", labels);

        var locationsHandle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union);
        await locationsHandle.ToUniTask();

        if (locationsHandle.Status != AsyncOperationStatus.Succeeded) return;

        var locations = locationsHandle.Result;
        int totalCount = locations.Count;

        for (int i = 0; i < totalCount; i++)
        {
            var location = locations[i];
            string key = location.PrimaryKey;

            GameLog.Log($"로딩 키 : {key}");

            // 이미 딕셔너리에 있으면 스킵 (중복 로드 방지)
            if (_globalHandles.ContainsKey(key) || _sceneHandles.ContainsKey(key))
            {
                onProgress?.Invoke(key, (i + 1f) / totalCount);
                continue;
            }

            //  Object 타입으로 로드하되 딕셔너리에 저장
            var handle = Addressables.LoadAssetAsync<Object>(location);

            if (isGlobal) _globalHandles[key] = handle;
            else _sceneHandles[key] = handle;

            SetLoadSource(key, $"Preload({labelText})"); // [Phase 0 계측]

            while (!handle.IsDone)
            {
                onProgress?.Invoke(key, (i + handle.PercentComplete) / totalCount);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            onProgress?.Invoke(key, (i + 1f) / totalCount);
        }

        Addressables.Release(locationsHandle);

        stopwatch.Stop();
        GameLog.Log($"[Preload] {totalCount}개 로드 완료 — {stopwatch.ElapsedMilliseconds}ms (라벨: {labelText}, isGlobal: {isGlobal})");
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

    //  Addressable Key 문자열을 받아 위치/회전까지 맞춰주는 Instantiate 함수
    public GameObject Instantiate(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        // 캐시에서 찾기 (글로벌 우선, 그 다음 씬)
        AsyncOperationHandle handle;
        bool found = _globalHandles.TryGetValue(key, out handle) || _sceneHandles.TryGetValue(key, out handle);

        if (found && handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject original = handle.Result as GameObject;
            if (original != null)
            {
                // 찾았으면 기존 안전한 Instantiate(GameObject) 활용
                return Instantiate(original, position, rotation, parent);
            }
        }

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



    // 메모리 정리 (씬 이동 시 호출)
    public void Clear()
    {
        RemoveSceneLoadSources(); // [Phase 0 계측] 해제될 씬 키들의 출처 기록 정리

        // _globalHandles는 건드리지 않고, _sceneHandles만 Release하여 메모리 확보
        foreach (var handle in _sceneHandles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle); // Addressable 레퍼런스 카운트 감소 (메모리 해제)
            }
        }
        _sceneHandles.Clear();
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

    public int GlobalHandleCount => _globalHandles.Count;
    public int SceneHandleCount => _sceneHandles.Count;
    public int AtlasSpriteCacheCount => _atlasSpriteCache.Count;

    // 디버그 창이 매 프레임 호출해도 부담 없도록 버퍼 재사용 방식
    public void GetHandleSnapshot(List<ResourceHandleDebugInfo> buffer)
    {
        buffer.Clear();
        AppendSnapshot(buffer, _globalHandles, "Global");
        AppendSnapshot(buffer, _sceneHandles, "Scene");
    }

    private void AppendSnapshot(List<ResourceHandleDebugInfo> buffer, Dictionary<string, AsyncOperationHandle> handles, string bucket)
    {
        foreach (var kv in handles)
        {
            var h = kv.Value;
            bool done = h.IsValid() && h.IsDone && h.Result != null;
            buffer.Add(new ResourceHandleDebugInfo
            {
                Key = kv.Key,
                Bucket = bucket,
                TypeName = done ? h.Result.GetType().Name : "(loading)",
                IsDone = h.IsValid() && h.IsDone,
                Source = _loadSources.TryGetValue(kv.Key, out var src) ? src : "?",
                Asset = done ? h.Result as Object : null,
            });
        }
    }

    // 씬 전환 시점에 살아있는 핸들 전체를 로그로 남김 (Baseline 측정용)
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void LogAliveReport(string tag)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[ResourceReport] ({tag}) global={_globalHandles.Count} scene={_sceneHandles.Count} atlasSprites={_atlasSpriteCache.Count}");
        foreach (var kv in _globalHandles)
            sb.AppendLine($"  [G] {kv.Key}  ←  {(_loadSources.TryGetValue(kv.Key, out var g) ? g : "?")}");
        foreach (var kv in _sceneHandles)
            sb.AppendLine($"  [S] {kv.Key}  ←  {(_loadSources.TryGetValue(kv.Key, out var s) ? s : "?")}");
        GameLog.Log(sb.ToString());
    }

    // 씬 버킷이 비었어야 하는 시점의 누수 검출
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void AssertSceneHandlesCleared(string context)
    {
        if (_sceneHandles.Count == 0) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[ResourceLeak] 씬 버킷에 핸들 {_sceneHandles.Count}개 잔존! ({context})");
        foreach (var kv in _sceneHandles)
            sb.AppendLine($"  [S] {kv.Key}  ←  {(_loadSources.TryGetValue(kv.Key, out var s) ? s : "?")}");
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

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void RemoveSceneLoadSources()
    {
        foreach (var key in _sceneHandles.Keys)
            _loadSources.Remove(key);
    }

    #endregion
}
