using UnityEngine;

// 일반몹 선택기 — 탄이 있으면 공격, 없으면 장전.
// 탄 수는 패턴을 넘는 상태(memo는 패턴마다 비워짐)라 고르는 쪽이 든다.
// 확정(탄 소모 · 채움)은 TryPick 안에서 — SS가 같은 CanUse로 같은 프레임에 받아들이므로 어긋나지 않는다(보스 기믹과 같은 방식).
public class AmmoPatternSelector : MonoBehaviour
{
    [SerializeField] private Pattern _attack;
    [SerializeField] private Pattern _reload;       // 비우면 장전 없이 공격만
    [SerializeField] private int _maxAmmo = 3;

    private int _ammo;
    private SkillSystem _ss;

    private void Awake()
    {
        _ss = GetComponent<SkillSystem>();
        if (_attack != null) _ss.Grant(_attack);
        if (_reload != null) _ss.Grant(_reload);
        _ammo = _maxAmmo;
    }

    // 새 생애 · 리쉬 귀환 완료
    public void ResetState() => _ammo = _maxAmmo;

    public bool TryPick(out Pattern pattern)
    {
        pattern = (_ammo > 0 || _reload == null) ? _attack : _reload;
        if (pattern == null || !_ss.CanUse(pattern)) { pattern = null; return false; }

        if (pattern == _attack) _ammo--;
        else _ammo = _maxAmmo;
        return true;
    }
}
