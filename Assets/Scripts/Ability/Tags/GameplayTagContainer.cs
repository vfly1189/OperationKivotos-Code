using System.Collections.Generic;

// 캐스터/대상이 "지금 보유한 태그"의 집합. 카운트형 멀티셋 —
// 같은 태그를 여러 소스가 걸 수 있으므로(스턴 2중첩 등) count>0 일 때만 보유로 친다.
// 소스 하나가 풀려도 다른 소스가 남아 있으면 태그는 유지된다.
// 순수 C#(MonoBehaviour 아님) — 저장은 GameplayTagOwner가, 만료는 StatusRunner가, 판독은 AbilityRunner가 담당.
public class GameplayTagContainer
{
    private readonly Dictionary<GameplayTagSO, int> _counts = new();

    // 태그 1중 추가(+1). 같은 태그 재부착 시 카운트만 오른다.
    public void Add(GameplayTagSO tag)
    {
        if (tag == null) return;
        _counts.TryGetValue(tag, out int c);
        _counts[tag] = c + 1;
    }

    // 태그 1중 제거(−1). 0 이하가 되면 키 자체를 지운다.
    public void Remove(GameplayTagSO tag)
    {
        if (tag == null || !_counts.TryGetValue(tag, out int c)) return;
        if (c <= 1) _counts.Remove(tag);
        else _counts[tag] = c - 1;
    }

    public bool Has(GameplayTagSO tag) => tag != null && _counts.ContainsKey(tag);

    // 요구 태그를 전부 보유하는가? (null/빈 목록 = 제약 없음 → true)
    public bool HasAll(IReadOnlyList<GameplayTagSO> tags)
    {
        if (tags == null) return true;
        for (int i = 0; i < tags.Count; i++)
            if (tags[i] != null && !Has(tags[i])) return false;
        return true;
    }

    // 차단 태그를 하나도 보유하지 않는가? (null/빈 목록 = 제약 없음 → true)
    public bool HasNone(IReadOnlyList<GameplayTagSO> tags)
    {
        if (tags == null) return true;
        for (int i = 0; i < tags.Count; i++)
            if (tags[i] != null && Has(tags[i])) return false;
        return true;
    }

    public void Clear() => _counts.Clear();
}
