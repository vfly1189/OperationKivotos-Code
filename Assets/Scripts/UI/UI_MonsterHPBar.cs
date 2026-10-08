using UnityEngine;
using UnityEngine.UI;

public class UI_MonsterHPBar : UI_Base
{
    private Camera _mainCamera; // 카메라 캐싱용 변수

    private Transform _targetTr; // 따라다닐 3D 오브젝트 (몬스터 머리 위)
    private MonsterStat _stat; // 몬스터 스탯 (HP 정보)

    private RectTransform _rectTransform;
    private Slider _slider;

    private void Awake()
    {
        // 1. 컴포넌트 캐싱
        _rectTransform = GetComponent<RectTransform>();
        _slider = GetComponent<Slider>();

        if (_slider == null)
            GameLog.LogError("UI_MonsterHPBar: Slider 컴포넌트를 찾을 수 없습니다! 프리팹을 확인하세요.");

        _mainCamera = Camera.main;
    }

    public override void Init()
    {
        //// 1. 컴포넌트 캐싱
        //_rectTransform = GetComponent<RectTransform>();
        //_slider = GetComponent<Slider>();

        //if (_slider == null)
        //    GameLog.LogError("UI_MonsterHPBar: Slider 컴포넌트를 찾을 수 없습니다! 프리팹을 확인하세요.");

        //_mainCamera = Camera.main;


    }

    // 몬스터 쪽에서 생성 직후 호출해줘야 함
    public void SetTarget(Transform target, MonsterStat stat)
    {
        _targetTr = target;
        _stat = stat;

        // 초기 갱신
        UpdateHpBar(stat.HealthComp.CurrentHp, stat.MaxHp.Value);
    }

    private void LateUpdate()
    {
        // 수명은 몬스터가 소유한다(OnSpawn 연결 / OnDespawn 해제) — 여기서는 스스로 파괴하지 않는다.
        //  타겟이 없으면(씬 언로드로 몬스터가 먼저 파괴된 뒤 Pool.Clear까지의 틈 등) 그리지 않고 넘긴다.
        if (_targetTr == null) return;

        // 카메라가 없으면 찾기 시도
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(_targetTr.position);

        // [보강] Z값 체크 (카메라 뒤쪽)
        if (screenPos.z <= 0)
        {
            // 그냥 캔버스 밖으로 날려버림
            screenPos = new Vector3(-1000, -1000, 0);
        }
        else
        {
            // [보강] Z값을 0으로 맞춰야 UI 캔버스 평면에 딱 붙음 (Overlay가 아닌 경우 중요)
            screenPos.z = 0;
        }

        _rectTransform.position = screenPos;
    }

    public void UpdateHpBar(float cur, float max)
    {
        if (_stat == null || _slider == null) return;

        // HP 비율 계산 (0 ~ 1)
        float ratio = 0f;
        if (_stat.MaxHp.Value > 0)
        {
            ratio = Mathf.Clamp01((float)_stat.HealthComp.CurrentHp / (float)_stat.MaxHp.Value);
        }

        _slider.value = ratio;
    }
}
