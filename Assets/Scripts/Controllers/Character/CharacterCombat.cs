using UnityEngine;

public class CharacterCombat
{
    private readonly CharacterStat _stat;
    private readonly float _attackRate;
    private float _lastAttackTime = -99f;

    public bool CanAttack => Time.time - _lastAttackTime >= _attackRate;

    public CharacterCombat(CharacterStat stat, float attackRate)
    {
        this._stat = stat;
        this._attackRate = attackRate;
    }

    public bool TryAttack()
    {
        if (!CanAttack) return false;
        _lastAttackTime = Time.time;
        return true;
    }

    public bool TryUseSkillQ() => _stat.TryUseSkillQ();
    public bool TryUseSkillE() => _stat.TryUseSkillE();
}