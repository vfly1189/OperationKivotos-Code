using System.Collections.Generic;
using UnityEngine;

public class PatternTask : MonoBehaviour
{
    // 어떤 신호를 감지할지
    [SerializeField] private GameplayTagSO _tag;

    // 조건(선택) — 비우면 항상. 분기는 Event가 아니라 여기서: 같은 태그에 Task 두 개 + 조건만 다르게
    [SerializeField] private string _ifKey;
    [SerializeField] private int _ifValue;

    // 신호가 오면 실행할 반응들 — 목록 순서대로. 기다리지 않는 Event는 Fire 안에서 바로 끝나므로
    // 앞 Event가 memo에 적은 값(ChooseEvent)을 뒤 Event가 바로 읽을 수 있다.
    [SerializeField] private List<PatternEvent> _events;

    public GameplayTagSO Tag => _tag;   // { get { return _tag; } }와 같음

    public bool Matches(GameplayTagSO tag, PatternMemo memo)
    {
        if (tag != _tag) return false;
        if (string.IsNullOrEmpty(_ifKey)) return true;
        return memo != null && memo.TryGetInt(_ifKey, out int v) && v == _ifValue;
    }

    public void Fire(PatternContext ctx)
    {
        foreach (var e in _events)
            if (e != null) e.Fire(ctx);
    }
}
