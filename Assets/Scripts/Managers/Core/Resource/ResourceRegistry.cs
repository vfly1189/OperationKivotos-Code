using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public sealed class ResourceRegistry
{
    public sealed class Entry
    {
        public AsyncOperationHandle handle;
        public int RefCount;
    }

    private readonly Dictionary<ResourceKey, Entry> _entries = new Dictionary<ResourceKey, Entry>();

    // 특정 scope의 첫 참조일때만 incrementRef 값 증가
    public async UniTask<T> LoadAsync<T> (ResourceKey key, bool incrementRef, CancellationToken tok)
        where T : UnityEngine.Object
    {
        //이미 있는 경우
        if(_entries.TryGetValue(key, out Entry entry))
        {
            if (incrementRef) entry.RefCount++;

            //만약 아직 로딩 중이라면 핸들 공유 대기
            if (!entry.handle.IsDone)
                await entry.handle.ToUniTask(cancellationToken: tok);

            return entry.handle.Result as T;
        }

        //entries에 없어서 로딩해야됨
        AsyncOperationHandle handle = Addressables.LoadAssetAsync<T>(key.Key);
        entry = new Entry { handle = handle, RefCount = incrementRef ? 1 : 0 };
        _entries[key] = entry;
        await handle.ToUniTask(cancellationToken:tok);

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            _entries.Remove(key);
            return null;
        }
        else
            return handle.Result as T;
    }

    // ResourceRegistry — location 기반 오버로드 추가
    public async UniTask<Object> LoadAsync(ResourceKey key, IResourceLocation location, bool incrementRef, CancellationToken tok)
    {
        if (_entries.TryGetValue(key, out Entry entry))
        {
            if (incrementRef) entry.RefCount++;

            if (!entry.handle.IsDone) 
                await entry.handle.ToUniTask(cancellationToken: tok);

            return entry.handle.Result as Object;
        }

        AsyncOperationHandle handle = Addressables.LoadAssetAsync<Object>(location);   // location으로 로드

        entry = new Entry { handle = handle, RefCount = incrementRef ? 1 : 0 };
        _entries[key] = entry;
        await handle.ToUniTask(cancellationToken: tok);

        if (handle.Status != AsyncOperationStatus.Succeeded) 
        { 
            _entries.Remove(key); 
            return null; 
        }

        return handle.Result as Object;
    }

    public void Release(ResourceKey key)
    {
        //없는걸 해제하려고 할때
        if (!_entries.TryGetValue(key, out Entry entry)) return;

        if(--entry.RefCount <= 0)
        {
            if(entry.handle.IsValid()) Addressables.Release(entry.handle);
            _entries.Remove(key);
        }
    }

    public bool TryGetAsset<T>(string key, out T asset) where T : UnityEngine.Object
    {
        asset = null;
        if (_entries.TryGetValue(new ResourceKey(key, typeof(T)), out var e)
            && e.handle.IsValid() && e.handle.IsDone && e.handle.Result is T t)
        { asset = t; return true; }
        return false;
    }

    // =========================================================================
    // [Phase 2 계측] 디버그 창/리포트가 읽는 스냅샷.
    //  레지스트리가 "살아있는 핸들"의 단일 진실 — 옛 2버킷 딕셔너리를 대체한다.
    //  버킷(Global/Scene) 라벨은 소유 스코프가 정하므로 여기선 refCount만 노출.
    // =========================================================================
    public int Count => _entries.Count;

    public struct DebugEntry
    {
        public ResourceKey Key;
        public int RefCount;      // 이 리소스를 소유한 서로 다른 스코프 수
        public bool IsDone;
        public string TypeName;   // 완료 시 실제 타입, 로딩 중이면 "(loading)"
        public UnityEngine.Object Asset;
    }

    public void GetSnapshot(List<DebugEntry> buffer)
    {
        buffer.Clear();
        foreach (var kv in _entries)
        {
            var h = kv.Value.handle;
            bool done = h.IsValid() && h.IsDone && h.Result != null;
            buffer.Add(new DebugEntry
            {
                Key = kv.Key,
                RefCount = kv.Value.RefCount,
                IsDone = h.IsValid() && h.IsDone,
                TypeName = done ? h.Result.GetType().Name : "(loading)",
                Asset = done ? h.Result as UnityEngine.Object : null,
            });
        }
    }
}
