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

    public UniTask<T> LoadAsync<T>(string key, CancellationToken tok = default) where T : UnityEngine.Object
    {
        if (_disposed || string.IsNullOrEmpty(key)) return UniTask.FromResult<T>(null);

        ResourceKey rk = new ResourceKey(key, typeof(T));
        bool isFirstTimeToLoad = _acquired.Add(rk);

        return _registry.LoadAsync<T>(rk, isFirstTimeToLoad, tok);
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
