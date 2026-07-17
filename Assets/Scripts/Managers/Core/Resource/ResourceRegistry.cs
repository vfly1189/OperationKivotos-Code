using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
}
