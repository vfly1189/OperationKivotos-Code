using UnityEngine;

public class BossRelicController : MonoBehaviour
{
    [Header("Relic Objects")]
    [SerializeField] private GameObject _redRelic;   // 빨간 렐릭 오브젝트(돌릴거)
    [SerializeField] private GameObject _greenRelic; // 초록 렐릭 오브젝트


    [SerializeField] private GameObject _redRelicEffect;   // 빨간 렐릭 이펙트(particle System 달린거)
    [SerializeField] private GameObject _greenRelicEffect; // 초록 렐릭 이펙트

    [Header("Settings")]
    [SerializeField] private float _rotateSpeed = 360f; // 1초당 회전 각도

    [Header("PingPong Settings")]
    [SerializeField] private float _floatSpeed = 2f;  // 위아래 움직이는 속도
    [SerializeField] private float _floatHeight = 1f; // 위아래 움직이는 높이 (0 ~ 1)

    private float _startY; // 원래 높이를 기억하기 위한 변수 (필요 시)

    private GameObject _activeObject;
    private GameObject _activeRelicEffect; // 현재 선택된 렐릭
    private bool _isSpinning = false;


    public bool CurrentIsRed { get; private set; }

    private void Start()
    {
        Init();
    }

    // 초기화: 둘 다 끄거나 대기 상태로
    public void Init()
    {
        if (_redRelicEffect) _redRelicEffect.GetComponent<ParticleSystem>().Stop();
        if (_greenRelicEffect) _greenRelicEffect.GetComponent<ParticleSystem>().Stop();
        _isSpinning = false;
    }

    // 1. 랜덤 활성화 및 회전 시작
    // isRed를 리턴해서 스킬 스크립트가 어떤 색인지 알게 함
    public bool ActivateRandomRelic()
    {
        // 0.5 확률로 빨강/초록 결정
        bool isRed = Random.value > 0.5f;
        CurrentIsRed = isRed;
        // 기존 켜진거 끄기
        if (_activeRelicEffect != null)
        {
            _activeRelicEffect.GetComponent<ParticleSystem>().Stop();
        }

        // 선택된 렐릭 켜기
        _activeRelicEffect = isRed ? _redRelicEffect : _greenRelicEffect;
        _activeObject = isRed ? _redRelic : _greenRelic;

        if (_activeRelicEffect != null)
        {
            _activeRelicEffect.GetComponent<ParticleSystem>().Play();
            _isSpinning = true;
        }

        return isRed; // 빨강이면 true, 초록이면 false 반환
    }

    // 매 프레임 돌리기
    private void Update()
    {
        if (_isSpinning && _activeObject != null)
        {
            // 1. 회전 (Y축 기준 빙글빙글)
            _activeObject.transform.Rotate(Vector3.up * _rotateSpeed * Time.deltaTime);

            // 2. 위아래 핑퐁 (Floating)
            // Sin 값은 -1 ~ 1 사이를 오가므로, 이를 0 ~ 1로 맞추려면 (Sin + 1) * 0.5 를 하면 됨
            // 하지만 단순히 현재 위치에서 위로 1만큼 올라갔다 내려오게 하려면 아래처럼 로컬 위치를 건드림

            float newY = Mathf.Abs(Mathf.Sin(Time.time * _floatSpeed)) * _floatHeight;

            // 만약 원래 위치(0)를 기준으로 0 ~ 1 사이를 왕복하고 싶다면:
            Vector3 pos = _activeObject.transform.localPosition;
            pos.y = newY; // 로컬 Y좌표를 0 ~ 1 사이 값으로 갱신
            _activeObject.transform.localPosition = pos;
        }

    }

    // 스킬 끝나면 멈추거나 끄기
    public void DeactivateRelic()
    {
        _isSpinning = false;
        if (_activeRelicEffect != null)
        {
            // 정면을 보게 리셋하고 끄기 (선택사항)
            _activeRelicEffect.transform.localRotation = Quaternion.identity;
            _activeObject.transform.localRotation = Quaternion.identity;

            Vector3 pos = _activeObject.transform.localPosition;
            pos.y = 0f; // Y값만 수정
            _activeObject.transform.localPosition = pos; // 다시 대입 (필수!)

            pos = _activeRelicEffect.transform.localPosition;
            pos.y = 0f;
            _activeRelicEffect.transform.localPosition = pos;

            _activeRelicEffect.GetComponent<ParticleSystem>().Stop();
        }
    }
}
