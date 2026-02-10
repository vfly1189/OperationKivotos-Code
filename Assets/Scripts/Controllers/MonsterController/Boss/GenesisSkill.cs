using System.Collections;
using UnityEngine;

public class GenesisSkill : BossSkillBase
{
    [Header("설정")]
    [SerializeField] private GameObject _genesisEffect;
    [SerializeField] private AudioClip _sfx;

    public override void Cast(BossSkillContext context)
    {
        // 프리팹이 활성화될 때 코루틴 시작
        StartCoroutine(ProcessSkillRoutine(context._targetPosition));
    }

    private IEnumerator ProcessSkillRoutine(Vector3 centerPos)
    {
        GameObject go = Object.Instantiate(_genesisEffect, centerPos, Quaternion.identity);

        go.transform.position = centerPos;
        go.GetComponent<ParticleSystem>().Play();

        yield return new WaitForSeconds(5.0f);
        Managers.Destroy(go);
    }
}
