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

    // [Phase 3d] 핸들이 "실제로" 해제된 순간(refCount 0) 알림.
    //  핸들 바깥에 파생물을 들고 있는 쪽(아틀라스 스프라이트 캐시 등)이
    //  자기 것을 같이 버릴 수 있게 한다 — 이게 없으면 스코프를 아무리 정확히
    //  잡아도 캐시가 에셋을 붙잡아 회수가 무효화된다.
    public event System.Action<ResourceKey> OnReleased;

    // 특정 scope의 첫 참조일때만 incrementRef 값 증가
    public async UniTask<T> LoadAsync<T> (ResourceKey key, bool incrementRef, CancellationToken tok)
        where T : UnityEngine.Object
    {
        //이미 있는 경우
        if(_entries.TryGetValue(key, out Entry entry))
        {
            if (incrementRef) entry.RefCount++;

            try
            {
                //만약 아직 로딩 중이라면 핸들 공유 대기
                if (!entry.handle.IsDone)
                    await entry.handle.ToUniTask(cancellationToken: tok);
            }
            catch
            {
                // 대기 중 취소/실패 — 방금 더한 참조를 되돌린다 (스코프의 _acquired 롤백과 정산 일치).
                RollbackLoad(key, entry, incrementRef);
                throw;
            }

            return entry.handle.Result as T;
        }

        //entries에 없어서 로딩해야됨
        //  [계측] 여기가 "실제 Addressables 로드"의 유일한 지점 — 위 캐시 히트 경로는 세지 않는다.
        //  같은 키가 여러 번 세지면 그것이 곧 "회수 후 재로드"의 증거 (Baseline §7-3).
        var sw = System.Diagnostics.Stopwatch.StartNew();

        AsyncOperationHandle handle = Addressables.LoadAssetAsync<T>(key.Key);
        entry = new Entry { handle = handle, RefCount = incrementRef ? 1 : 0 };
        _entries[key] = entry;

        try
        {
            await handle.ToUniTask(cancellationToken:tok);
        }
        catch
        {
            // 취소/예외로 방금 만든 엔트리를 유령(어느 스코프도 소유 안 하는 refCount>0)으로 남기지 않는다.
            RollbackLoad(key, entry, incrementRef);
            throw;
        }

        ResourceMetrics.RecordLoad(key.Key, sw.ElapsedMilliseconds);

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            RollbackLoad(key, entry, incrementRef);
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

            try
            {
                if (!entry.handle.IsDone)
                    await entry.handle.ToUniTask(cancellationToken: tok);
            }
            catch
            {
                RollbackLoad(key, entry, incrementRef);   // 제네릭 오버로드와 동일 규약
                throw;
            }

            return entry.handle.Result as Object;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();   // [계측] 위와 동일 — 실제 로드만 집계

        AsyncOperationHandle handle = Addressables.LoadAssetAsync<Object>(location);   // location으로 로드

        entry = new Entry { handle = handle, RefCount = incrementRef ? 1 : 0 };
        _entries[key] = entry;

        try
        {
            await handle.ToUniTask(cancellationToken: tok);
        }
        catch
        {
            RollbackLoad(key, entry, incrementRef);
            throw;
        }

        ResourceMetrics.RecordLoad(key.Key, sw.ElapsedMilliseconds);

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            RollbackLoad(key, entry, incrementRef);
            return null;
        }

        return handle.Result as Object;
    }

    // [취소 정산] 취소·실패로 로드가 중단됐을 때 "이 호출이 더한 참조"만 되돌린다.
    //  되돌린 뒤 아무 소유자도 없으면(RefCount ≤ 0) 엔트리와 핸들을 함께 정리한다.
    //  - 다른 소유자가 같은 in-flight 핸들을 대기 중일 수 있으므로 무조건 Release하지 않고 RefCount로 판단한다.
    //  - cur == entry 확인: 그 사이 다른 로더가 엔트리를 이미 치웠거나 교체했으면 건드리지 않는다(재진입 안전).
    //  토큰 취소는 밑단 Addressables 로드를 멈추지 않으므로(핸들은 결국 완료됨), 소유자가 없을 때 Release가 정확하다.
    private void RollbackLoad(ResourceKey key, Entry entry, bool addedRef)
    {
        if (addedRef) entry.RefCount--;

        if (entry.RefCount <= 0 && _entries.TryGetValue(key, out Entry cur) && cur == entry)
        {
            _entries.Remove(key);
            if (entry.handle.IsValid()) Addressables.Release(entry.handle);
        }
    }

    // [Phase 4] 로드 없이 refCount만 +1. 이미 로드된 엔트리에 "추가 소유자"를 등록한다.
    //  풀이 원본 프리팹 핸들의 수명을 붙잡는 용도 — 엔트리가 없으면(부류 B: SO 직접 참조 등)
    //  false를 돌려주고 아무것도 하지 않는다(Addressables 관리 대상이 아니므로 잡을 것이 없음).
    public bool TryAddRef(ResourceKey key)
    {
        if (!_entries.TryGetValue(key, out Entry entry)) return false;
        entry.RefCount++;
        return true;
    }

    public void Release(ResourceKey key)
    {
        //없는걸 해제하려고 할때
        if (!_entries.TryGetValue(key, out Entry entry)) return;

        if(--entry.RefCount <= 0)
        {
            if(entry.handle.IsValid()) Addressables.Release(entry.handle);
            _entries.Remove(key);

            OnReleased?.Invoke(key);   // [Phase 3d] 파생물 보유자에게 통지 (제거 후에 호출 — 재진입 안전)
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
