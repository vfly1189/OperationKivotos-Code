
using System.Collections.Generic;
using UnityEngine;

public class BossMonsterPatternSelector : MonoBehaviour
{
    [SerializeField] private List<NormalPatternSlot> _normalPatterns;
    [SerializeField] private List<GimmickPatternSlot> _gimmickPatterns;

    private HashSet<GimmickPatternSlot> _finishedGimmick = new HashSet<GimmickPatternSlot>();

    private SkillSystem _ss;

    public void Awake()
    {
        _gimmickPatterns.Sort((x, y) => y._startLine.CompareTo(x._startLine));

        _ss = GetComponent<SkillSystem>();

        foreach (var p in _normalPatterns) _ss.Grant(p._pattern);
        foreach (var p in _gimmickPatterns) _ss.Grant(p._pattern);
    }

    // 새 생애 — 풀 재사용 시 이미 쓴 기믹이 다시 나오게
    public void ResetState()
    {
        _finishedGimmick.Clear();
    }

    public bool TryPick(float currentHpLine, out Pattern pattern)
    {
        //기믹 패턴 사용가능 여부 확인
        foreach(GimmickPatternSlot gps  in _gimmickPatterns)
        {
            if (_finishedGimmick.Contains(gps)) continue;

            if(currentHpLine < gps._startLine && _ss.CanUse(gps._pattern))
            {
                pattern = gps._pattern;
                _finishedGimmick.Add(gps);
                return true;
            }
        }


        //일반 패턴 가중치 기반 선택
        List<NormalPatternSlot> _candidates = new List<NormalPatternSlot>();
        int total = 0;
        foreach(NormalPatternSlot nps in _normalPatterns)
        {
            bool b = _ss.CanUse(nps._pattern) && nps._weight > 0;
            if (!b) continue;

            _candidates.Add(nps);
            total += nps._weight;
        }

        int r = Random.Range(0, total);
        foreach(NormalPatternSlot nps in _candidates)
        {
            r -= nps._weight;
            if(r < 0) { pattern = nps._pattern; return true; }
        }

        pattern = null;
        return false;
    }


}
