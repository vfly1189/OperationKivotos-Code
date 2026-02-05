using System.Collections;
using UnityEngine;
using UnityEngine.Diagnostics;

public class BulletController : MonoBehaviour
{
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _lifeTime = 1f;

    private GameObject _shooter;

    private float _damage;
    private Coroutine _lifeTimeCoroutine; // 실행 중인 코루틴 저장용

    // 성능을 위해 레이어 인덱스는 Awake에서 한 번만 찾아둠
    private int _layerPlayer;
    private int _layerMonster;
    private int _layerPlayerBullet;
    private int _layerMonsterBullet;

    void Awake()
    {
        _layerPlayer = LayerMask.NameToLayer("Player");
        _layerMonster = LayerMask.NameToLayer("Monster");
        _layerPlayerBullet = LayerMask.NameToLayer("PlayerBullet");
        _layerMonsterBullet = LayerMask.NameToLayer("MonsterBullet");
    }

    public void Init(float damage, GameObject shooter)
    {
        _damage = damage;
        _shooter = shooter;

        // [핵심] 쏘는 사람 레이어에 따라 내 레이어 결정 -> 매트릭스 설정에 따라 충돌 자동 필터링
        if (shooter.layer == _layerPlayer)
        {
            gameObject.layer = _layerPlayerBullet;
            Util.SetLayerRecursively(gameObject, _layerPlayerBullet);
        }
        else if (shooter.layer == _layerMonster)
        {
            gameObject.layer = _layerMonsterBullet;
            Util.SetLayerRecursively(gameObject, _layerMonsterBullet);
        }

        // 기존에 돌던 코루틴이 있다면 멈춤 (재사용 시 안전장치)
        if (_lifeTimeCoroutine != null) StopCoroutine(_lifeTimeCoroutine);
        // 수명 카운트 시작
        _lifeTimeCoroutine = StartCoroutine(CoLifeTimeTimer());
    }

    // [중요] 풀링되어 비활성화될 때 코루틴도 확실히 꺼줌
    private void OnDisable()
    {
        if (_lifeTimeCoroutine != null) StopCoroutine(_lifeTimeCoroutine);
    }

    void Update()
    {
        // 앞으로 전진
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 물리 매트릭스 덕분에 아군은 이미 걸러졌음. 
        // 여기 들어온 건 [적] 아니면 [벽]임.

        // 1. 데미지를 줄 수 있는 대상인가? (다형성 활용)
        if (other.TryGetComponent<IDamageable>(out IDamageable target))
        {
            // 정확한 타격 위치 계산 (이펙트 용)
            Vector3 hitPoint = other.ClosestPoint(transform.position);

            // 인터페이스 메서드 호출 (상대가 Player든 Monster든 상관 안 함)
            target.TakeDamage(new DamageInfo(_damage, _shooter, hitPoint));

            Managers.Resource.Destroy(gameObject);
        }
        else
        {
            Debug.Log($"other : {other.gameObject.name}");
            // 데미지 대상은 아닌데 부딪힘 -> 벽(Wall)이나 장애물
            Managers.Resource.Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        // 1. 트레일 초기화
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail != null) trail.Clear();

    }

        // 일정 시간(_lifeTime) 지나면 자동 반납
    IEnumerator CoLifeTimeTimer()
    {
        yield return new WaitForSeconds(_lifeTime);
        Managers.Resource.Destroy(gameObject);
    }
}
