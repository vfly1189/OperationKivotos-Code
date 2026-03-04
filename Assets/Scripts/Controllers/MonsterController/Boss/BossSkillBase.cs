using UnityEngine;
using UnityEngine.EventSystems;

public class BossSkillContext
{
    public Vector3 _targetPosition;
    public Transform[] _spawnPoints;
    public GameObject _boss;
    public Transform[] _lightningPoints;
    public BossRelicController _bossRelicController;
}

public abstract class BossSkillBase : MonoBehaviour
{
    // 스킬이 실행될 때 호출되는 함수
    public abstract void Cast(BossSkillContext context);
}
