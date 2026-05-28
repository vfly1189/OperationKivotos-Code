using UnityEngine;
using UnityEngine.EventSystems;

public class BossSkillContext
{
    public Vector3 _targetPosition;
    public Transform[] _spawnPoints;
    public GameObject _boss;
    public Transform[] _lightningPoints;
    public BossRelicController _bossRelicController;
    // (보스 컨트롤러 참조를 캐싱해두면 편합니다)
    public BossMonsterController _bossController;
}

public abstract class BossSkillBase : MonoBehaviour
{
    // 스킬이 실행될 때 호출되는 함수
    public abstract void Cast(BossSkillContext context);
}
