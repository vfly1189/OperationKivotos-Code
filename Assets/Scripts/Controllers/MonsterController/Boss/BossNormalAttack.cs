using System.Collections;
using UnityEngine;

public class BossNormalAttack : BossSkillBase
{
    [SerializeField] private GameObject _lightningSubPrefab;
    [SerializeField] private AudioClip _sfx;

    public override void Cast(BossSkillContext context)
    {
        StartCoroutine(ProcessSkillRoutine(context._targetPosition));
    }

    private IEnumerator ProcessSkillRoutine(Vector3 targetPosition)
    {
        GameObject go = Managers.Resource.Instantiate(_lightningSubPrefab, targetPosition, Quaternion.identity);

        go.transform.position = targetPosition;
        go.GetComponent<ParticleSystem>().Play();

        yield return new WaitForSeconds(2.0f);
        Managers.Destroy(go);
    }
}
