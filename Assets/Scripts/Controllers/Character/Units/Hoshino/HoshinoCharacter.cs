using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class HoshinoCharacter : BaseCharacter
{
    [Header("ESkill Fire Point")]
    [SerializeField] private Transform _eSkillFirePoint;
    [SerializeField] private ParticleSystem _eSkillFireEffect;

    public override void Init()
    {
        base.Init();
    }

    // 평타(샷건)는 BaseCharacter 기본(TryUseAbility(0)) → Hoshino_Spread Effect로 이관됨.
    // 호시노는 E스킬 발사만 고유 로직으로 남김.
    protected override void OnESkillEvent(AudioClip sfx)
    {
        // 1. 풀링으로 총알 생성 (위치/회전은 총구 기준)
        GameObject bulletObj = Managers.Resource.Instantiate(_bulletPrefab, _eSkillFirePoint.position, _eSkillFirePoint.rotation);

        bulletObj.transform.position = _eSkillFirePoint.position;
        // 캐릭터가 바라보는 방향 기준으로 회전
        bulletObj.transform.rotation = transform.rotation;
        // 2. 데미지 주입
        BulletController bulletScript = bulletObj.GetComponent<BulletController>();
        if (bulletScript != null && Stat != null)
        {
            bulletScript.Init(CalculatedDamage(), this.gameObject);
        }
        PlayESkillFireEffect();
    }

    protected virtual void PlayESkillFireEffect()
    {
        if (_eSkillFireEffect == null) return;
        _eSkillFireEffect.Stop();
        _eSkillFireEffect.Play();
    }
}
