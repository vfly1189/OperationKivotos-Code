using UnityEngine;
using UnityEngine.UI;

public class UI_MonsterHPBar : UI_Base
{
    private Transform _targetTr; // 따라다닐 3D 오브젝트 (몬스터 머리 위)
    private MonsterStat _stat; // 몬스터 스탯 (HP 정보)

    private RectTransform _rectTransform;
    private Slider _slider;

    public override void Init()
    {
        // 1. 컴포넌트 캐싱
        _rectTransform = GetComponent<RectTransform>();
        _slider = GetComponent<Slider>();

        if (_slider == null)
            Debug.LogError("UI_MonsterHPBar: Slider 컴포넌트를 찾을 수 없습니다! 프리팹을 확인하세요.");
    }

    // 몬스터 쪽에서 생성 직후 호출해줘야 함
    public void SetTarget(Transform target, MonsterStat stat)
    {
        _targetTr = target;
        _stat = stat;

        // 초기 갱신
        UpdateHpBar(stat.CurrentHp, stat.MaxHp.Value);
    }

    private void LateUpdate()
    {
        // 타겟이 사라지면(몬스터 죽음) UI도 스스로 파괴하거나 비활성화
        if (_targetTr == null)
        {
            Managers.Resource.Destroy(gameObject);
            return;
        }

        // [핵심] 3D 좌표(월드) -> 2D 좌표(스크린) 변환
        Vector3 screenPos = Camera.main.WorldToScreenPoint(_targetTr.position);

        // [화면 밖 처리] 카메라 뒤쪽에 있는 경우 숨김
        if (screenPos.z < 0)
        {
            // 화면 밖으로 멀리 보내거나 scale을 0으로
            // gameObject.SetActive(false); // SetActive는 비용이 좀 있으므로 좌표 이동 추천
            screenPos = new Vector3(-1000, -1000, 0);
        }

        // 위치 적용
        _rectTransform.position = screenPos;

        // (선택) 거리 비례 크기 조절: 멀리 있으면 작게 보이게
        // float dist = Vector3.Distance(Camera.main.transform.position, _targetTr.position);
        // float scale = Mathf.Clamp(20.0f / dist, 0.5f, 1.5f);
        // _rectTransform.localScale = Vector3.one * scale;
    }

    public void UpdateHpBar(float cur, float max)
    {
        if (_stat == null || _slider == null) return;

        // HP 비율 계산 (0 ~ 1)
        float ratio = 0f;
        if (_stat.MaxHp.Value > 0)
        {
            ratio = Mathf.Clamp01((float)_stat.CurrentHp / (float)_stat.MaxHp.Value);
        }

        _slider.value = ratio;
    }
}
