using UnityEngine;

public class CharacterCombat
{
    private CharacterStat _stat;
    private float _attackRate;
    private float _lastAttackTime = -99f;

    public bool CanAttack => Time.time - _lastAttackTime >= _attackRate;

    public CharacterCombat(CharacterStat stat, float attackRate)
    {
        _stat = stat;
        _attackRate = attackRate;
    }

    public bool TryAttack()
    {
        if (!CanAttack) return false;

        _lastAttackTime = Time.time;
        return true;
    }

    public bool TryUseSkillQ()
    {
        return _stat.TryUseSkillQ();
    }

    public bool TryUseSkillE()
    {
        return _stat.TryUseSkillE();
    }
}
