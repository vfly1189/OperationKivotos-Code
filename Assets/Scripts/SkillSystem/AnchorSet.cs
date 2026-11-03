using System;
using System.Collections.Generic;
using UnityEngine;

public interface IAnchorSet
{
    bool TryGet(string key, out Transform t);
}

// 보스 프리팹 안에서 기준점이 될 Transform들을 이름표와 함께 들고 있는 컴포넌트.
// Transform은 계층 어디에 있어도 된다 — 손처럼 움직이는 점은 본 밑에, 유물 · 스폰포인트는 고정 오브젝트 밑에.
public class AnchorSet : MonoBehaviour, IAnchorSet
{
    [Serializable] struct Entry { public string key; public Transform point; }

    [SerializeField] private List<Entry> _entries;   // 인스펙터에서 키 + Transform 지정
    private Dictionary<string, Transform> _map;

    public bool TryGet(string key, out Transform t) => Map.TryGetValue(key, out t);

    // 실행 중 등록 — 프리팹 밖(맵)에 있는 점. 같은 키가 인스펙터에 있으면 덮어쓴다
    public void Register(string key, Transform t)
    {
        if (t != null && !string.IsNullOrEmpty(key)) Map[key] = t;
    }

    // 처음 쓸 때 만든다 — 풀 첫 스폰은 꺼진 채 OnSpawn이 먼저 불려 Awake 전에 쓰일 수 있고,
    // 씬이 Register를 TryGet보다 먼저 부를 수도 있다
    private Dictionary<string, Transform> Map
    {
        get
        {
            if (_map == null)
            {
                _map = new Dictionary<string, Transform>(_entries.Count);
                foreach (var e in _entries)
                    if (e.point != null && !string.IsNullOrEmpty(e.key)) _map[e.key] = e.point;
            }
            return _map;
        }
    }
}
