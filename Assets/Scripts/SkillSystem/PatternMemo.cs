using System.Collections.Generic;
using UnityEngine;

// 이번 패턴 실행 동안만 쓰는 칠판 — 저장한 점(PointEmitter._saveAs)과 고른 값(ChooseEvent).
// Runner가 하나를 들고 재사용하고, 패턴이 시작될 때 비운다(할당 0).
public class PatternMemo
{
    private readonly Dictionary<string, List<Pose>> _points = new Dictionary<string, List<Pose>>();
    private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();

    public void SavePoints(string key, List<Pose> src)
    {
        if (!_points.TryGetValue(key, out var list)) _points[key] = list = new List<Pose>(src.Count);
        list.Clear();
        list.AddRange(src);
    }

    public bool TryGetPoints(string key, out List<Pose> points)
        => _points.TryGetValue(key, out points) && points.Count > 0;

    public void SetInt(string key, int value) => _ints[key] = value;
    public bool TryGetInt(string key, out int value) => _ints.TryGetValue(key, out value);

    // 리스트는 남겨 두고 비우기만 — 다음 패턴이 재사용
    public void Clear()
    {
        foreach (var list in _points.Values) list.Clear();
        _ints.Clear();
    }
}
