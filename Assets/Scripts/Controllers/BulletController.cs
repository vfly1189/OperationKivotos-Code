using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _lifeTime = 1f;

    private GameObject _shooter;
    private DamageInfo _damageInfo;

    // 코루틴(Coroutine)을 대체할 취소 토큰 소스
    private CancellationTokenSource _lifeTimeCts;

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

    public void Init(DamageInfo damageInfo, GameObject shooter)
    {
        _damageInfo = damageInfo;
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

        // 기존에 돌던 UniTask가 있다면 안전하게 취소 및 정리
        CancelLifeTimeTimer();

        // 새로운 토큰 발급 및 수명 카운트 시작 (비동기 메서드 호출)
        _lifeTimeCts = new CancellationTokenSource();
        LifeTimeTimerAsync(_lifeTimeCts.Token).Forget();
    }

    // [중요] 풀링되어 비활성화될 때 실행 중인 UniTask도 확실히 꺼줌
    private void OnDisable()
    {
        CancelLifeTimeTimer();
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
            _damageInfo.HitPoint = hitPoint;
            // 인터페이스 메서드 호출 (상대가 Player든 Monster든 상관 안 함)
            target.TakeDamage(_damageInfo);


            Managers.Resource.Destroy(gameObject);
        }
        else
        {
            Debug.Log($"other : {other.gameObject.name}");
            // 데미지 대상은 아닌데 부딪힘 -> 벽(Wall)이나 장애물

            if (other.gameObject.layer == LayerMask.NameToLayer("Wall"))
                Managers.Resource.Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        // 1. 트레일 초기화
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail != null) trail.Clear();
    }

    private async UniTaskVoid LifeTimeTimerAsync(CancellationToken token)
    {
        // SuppressCancellationThrow를 사용하면, 총알이 중간에 벽에 부딪혀 비활성화되면서
        // 토큰이 취소(Cancel)되었을 때 에러(TaskCanceledException) 로그가 콘솔에 찍히는 것을 방지합니다.
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_lifeTime), cancellationToken: token).SuppressCancellationThrow();

        // 중간에 Cancel되었다면 바로 종료 (오브젝트 풀로 돌아감)
        if (isCanceled) return;

        // 지정된 시간이 무사히 다 지났다면 반납
        Managers.Resource.Destroy(gameObject);
    }

   

    // 토큰 소스 취소 및 메모리 해제 헬퍼 함수
    private void CancelLifeTimeTimer()
    {
        if (_lifeTimeCts != null)
        {
            _lifeTimeCts.Cancel();
            _lifeTimeCts.Dispose();
            _lifeTimeCts = null;
        }
    }
}