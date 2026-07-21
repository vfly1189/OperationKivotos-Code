using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

public sealed class ResourceScope : IDisposable
{
    private readonly ResourceRegistry _registry;
    private readonly HashSet<ResourceKey> _acquired = new HashSet<ResourceKey>();

    private bool _disposed;

    public string Name { get; private set; }
    public ResourceScope(ResourceRegistry registry, string name)
    {
        _registry = registry;
        Name = name;
    }

    // [Phase 2 계측] 이 스코프가 소유(참조)한 키 수 / 특정 키 소유 여부 —
    //  ResourceManager가 레지스트리 엔트리에 Global/Scene 버킷 라벨을 붙일 때 사용.
    public int Count => _acquired.Count;
    public bool Contains(ResourceKey rk) => _acquired.Contains(rk);

    public async UniTask<T> LoadAsync<T>(string key, CancellationToken tok = default) where T : UnityEngine.Object
    {
        if (_disposed || string.IsNullOrEmpty(key)) return null;

        ResourceKey rk = new ResourceKey(key, typeof(T));
        bool isFirstTimeToLoad = _acquired.Add(rk);

        try
        {
            return await _registry.LoadAsync<T>(rk, isFirstTimeToLoad, tok);
        }
        catch
        {
            // 취소·실패 시 소유 기록을 되돌린다. 안 그러면 레지스트리엔 없는데 스코프만
            // 쥐고 있는 유령 소유가 남아, 계측에는 잡히고 Dispose는 헛돈다.
            if (isFirstTimeToLoad) _acquired.Remove(rk);
            throw;
        }
    }

    // ResourceScope — location 기반 추가
    public UniTask<UnityEngine.Object> LoadAsync(IResourceLocation location, CancellationToken tok = default)
    {
        if (_disposed || location == null) return UniTask.FromResult<UnityEngine.Object>(null);

        ResourceKey rk = new ResourceKey(location.PrimaryKey, location.ResourceType);   //실제 타입으로 키잉
        bool isFirstTimeToLoad = _acquired.Add(rk);

        return _registry.LoadAsync(rk, location, isFirstTimeToLoad, tok);
    }

    public void Dispose()
    {
        if(_disposed) return;
        _disposed = true;

        // TODO -> registry에 해제 요청
        foreach (ResourceKey key in _acquired) _registry.Release(key);

        _acquired.Clear();
    }
}
