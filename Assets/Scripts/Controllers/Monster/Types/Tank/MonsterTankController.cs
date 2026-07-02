using Cysharp.Threading.Tasks;
using UnityEngine;

// 포격형 몬스터: 실제 폭격 로직은 AreaStrike(Effect)로 이관됨.
// 이 컨트롤러는 "착탄점을 정해(flag로 번갈아) 어빌리티를 발동"하는 역할만 한다.
public class MonsterTankController : RangedMonsterController
{
    [Header("Artillery Aim")]
    [SerializeField] private float _randomSpread = 1.5f;   // 탄착군 범위(랜덤 산포)
    [SerializeField] private Vector3[] _aimOffsets;        // flag로 번갈아 쓸 착탄 오프셋 (예: 왼/오)

    // 여러 번의 공격에 걸쳐 번갈아 도는 상태 → 캐스터(이 컨트롤러)가 소유
    private int _flag = 0;

    // 공격 애니메이션 이벤트에서 호출됨 (총 2발 → 2번 호출)
    protected override void PerformAttackAction()
    {
        if (_target == null || _abilities == null || _abilities.Count == 0) return;

        // flag로 오프셋 선택 (0 1 0 1 ...)
        Vector3 offset = (_aimOffsets != null && _aimOffsets.Length > 0)
            ? _aimOffsets[_flag % _aimOffsets.Length]
            : Vector3.zero;

        // 착탄점 = 타겟 + flag 오프셋 + 랜덤 산포
        Vector2 rnd = UnityEngine.Random.insideUnitCircle * _randomSpread;
        Vector3 point = _target.position + offset + new Vector3(rnd.x, 0f, rnd.y);

        var ctx = new AbilityContext
        {
            Caster      = this,
            CasterGO    = gameObject,
            CasterStat  = Stat,
            Target      = _target.gameObject,
            TargetPoint = point,        // ★ 착탄 지점 (AreaStrike가 여기 폭격)
        };
        _abilityRunner.TryCast(_abilities[0], ctx, _monsterCts.Token).Forget();

        // 0 1 0 1 순환
        int n = (_aimOffsets != null && _aimOffsets.Length > 0) ? _aimOffsets.Length : 2;
        _flag = (_flag + 1) % n;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_target == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_target.position, _randomSpread);   // 탄착군 범위 미리보기
    }
#endif
}
