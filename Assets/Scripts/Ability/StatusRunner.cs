using System.Collections.Generic;
using UnityEngine;

// 지속시간 스탯 modifier(버프/방깎/가드)를 소유하고 만료 시 자동 해제한다.
// Effect는 stateless(공유 SO)로 두고, "언제 해제되나"라는 상태는 이 컴포넌트(소유자)가 갖는다.
// 소유자 규칙: 시전자/대상 스코프 = 해당 엔티티, 파티 스코프 = 영속 파티 컨테이너(비활성 멤버도 만료되게).
public class StatusRunner : MonoBehaviour
{
    private class Timed
    {
        public Stat Stat;
        public StatModifier Mod;
        public float Remaining;
    }

    private readonly List<Timed> _active = new();

    public static StatusRunner EnsureOn(GameObject go)
    {
        if (go == null) return null;
        return go.GetComponent<StatusRunner>() ?? go.AddComponent<StatusRunner>();
    }

    // 대상 스탯에 지속시간 modifier 적용. duration 후 자동 해제. (mod는 대상마다 새로 생성해 넘길 것)
    public void ApplyTimed(Stat stat, StatModifier mod, float duration)
    {
        if (stat == null || mod == null) return;

        stat.AddModifier(mod);
        _active.Add(new Timed { Stat = stat, Mod = mod, Remaining = Mathf.Max(duration, 0f) });
    }

    private void Update()
    {
        if (_active.Count == 0) return;

        float dt = Time.deltaTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Timed t = _active[i];
            t.Remaining -= dt;
            if (t.Remaining <= 0f)
            {
                t.Stat.RemoveModifier(t.Mod);
                _active.RemoveAt(i);
            }
        }
    }

    // 비활성/파괴 시 걸린 것 전부 해제 (풀 재사용 이월·누수 방지)
    private void OnDisable()
    {
        for (int i = 0; i < _active.Count; i++)
            _active[i].Stat.RemoveModifier(_active[i].Mod);
        _active.Clear();
    }
}
