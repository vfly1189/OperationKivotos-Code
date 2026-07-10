using UnityEngine;
using UnityEngine.VFX;

public class ArisBulletController : MonoBehaviour, IProjectile
{
    [Header("Settings")]
    [SerializeField] private float _speed = 3.0f;
    [SerializeField] private float _maxLifeTime = 3.0f;
    [SerializeField] private bool _isPenetrate = false; // 관통 여부 (아리스 레일건 느낌이면 true)

    [Header("Growth Settings (Scale)")]
    [SerializeField] private bool _useGrowth = true;           // 커지는 효과 사용 여부
    [SerializeField] private Vector3 _startScale = Vector3.one * 0.5f; // 시작 크기
    [SerializeField] private Vector3 _targetScale = Vector3.one * 1.0f; // 최종 크기
    [SerializeField] private float _growthDuration = 1.0f;     // 커지는 데 걸리는 시간
    [SerializeField] private AnimationCurve _growthCurve = AnimationCurve.Linear(0, 0, 1, 1); // 커지는 속도 그

    [Header("VFX")]
    [SerializeField] private VisualEffect _projectileVFX; // 투사체 자체의 VFX 컴포넌트
    [SerializeField] private GameObject _hitVFXPrefab;    // 충돌 시 생성될 폭발/스파크 이펙트 (프리팹)

    private GameObject _shooter;
    private DamageInfo _damageInfo;
    private Vector3 _direction;
    private Rigidbody _rb;
    private float _elapsedTime = 0f; // 경과 시간 체크용
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_projectileVFX == null) _projectileVFX = GetComponent<VisualEffect>();
    }

    private void OnEnable()
    {
        // 풀링 사용 시 초기화 로직
        Invoke(nameof(Despawn), _maxLifeTime);

        // VFX가 있다면 재생
        if (_projectileVFX != null)
        {
            _projectileVFX.Reinit(); // 그래프 초기화
            _projectileVFX.Play();
        }
    }

    public void Init(DamageInfo damage, GameObject shooter)
    {
        _damageInfo = damage;
        _shooter = shooter;
    }

    void Update()
    {
        // 1. 앞으로 이동 (에너지탄은 보통 직선 운동)
        // 리지드바디 velocity를 써도 되지만, 투사체는 Transform 이동이 제어가 편할 때가 많습니다.
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);

        // 2. 크기 성장 로직
        if (_useGrowth && _elapsedTime < _growthDuration)
        {
            _elapsedTime += Time.deltaTime;

            // 0~1 사이의 진행률 계산
            float t = _elapsedTime / _growthDuration;

            // AnimationCurve를 이용해 자연스러운 보간값(curveValue) 추출
            float curveValue = _growthCurve.Evaluate(t);

            // Lerp로 현재 크기 계산
            transform.localScale = Vector3.Lerp(_startScale, _targetScale, curveValue);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. 피아식별 (태그나 레이어 사용)
        // 예: Player, Bullet, Trigger 등은 무시
        if (other.CompareTag("Player")) return;

        // 2. 데미지 처리 — IDamageable 대상에 스냅샷 데미지 적용 (관통이면 여러 대상에 반복 적용)
        if (other.TryGetComponent<IDamageable>(out IDamageable target))
        {
            _damageInfo.HitPoint = other.ClosestPoint(transform.position);
            target.TakeDamage(_damageInfo);
        }

        // 3. 피격 이펙트 생성 (중요: 충돌 지점과 법선 벡터 활용)
        SpawnHitVFX(other);

        // 4. 관통이 아니면 투사체 삭제(반환)
        if (!_isPenetrate)
        {
            Despawn();
        }
    }

    private void SpawnHitVFX(Collider other)
    {
        if (_hitVFXPrefab == null) return;

        // 정확한 충돌 지점을 찾기 위해 ClosestPoint 사용
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        // 피격 이펙트가 적을 바라보거나, 반사각을 갖도록 설정 (여기서는 충돌 지점에서 내 쪽을 보게 설정)
        Quaternion hitRot = Quaternion.LookRotation(transform.position - hitPoint);

        // Managers.Resource.Instantiate 사용 (사용자 환경)
        GameObject hitObj = Managers.Resource.Instantiate(_hitVFXPrefab, hitPoint, hitRot);

        // 수명이 끝나면 자동으로 풀 반환/파괴 (스폰만 하고 회수 안 하던 누수 방지)
        Util.GetOrAddComponent<AutoReturnToPool>(hitObj);
    }

    private void Despawn()
    {
        CancelInvoke(); // 예약된 Despawn 취소

        // VFX Graph 특성상 바로 끄면 잔상이 뚝 끊길 수 있음.
        // 하지만 빠른 액션 게임에서는 보통 바로 반환하고, Hit VFX로 덮습니다.
        Managers.Resource.Destroy(gameObject);
    }
}
