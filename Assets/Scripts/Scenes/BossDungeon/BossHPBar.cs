using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHPBar : UI_Scene
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _text;

    private BossMonsterController _bossMonsterController;

    // UI_Base의 Start -> Init 흐름은 UI 자체의 기본 세팅만 하도록 둡니다.
    public override void Init()
    {
        base.Init();
        // 슬라이더 초기화, 이벤트 바인딩 같은 보스와 무관한 UI 셋팅만 여기서 진행
    }

    //  데이터 주입과 이벤트를 여기서 한 번에 처리합니다.
    public void SetBoss(GameObject boss)
    {
        if (boss == null) return;

        _bossMonsterController = boss.GetComponent<BossMonsterController>();

        if (_bossMonsterController != null)
        {
            // 구독 중복 방지를 위해 뺐다가 넣기
            _bossMonsterController.Stat.HealthComp.OnHpChanged -= UpdateHPBar;
            _bossMonsterController.Stat.HealthComp.OnHpChanged += UpdateHPBar;

            // 처음 보여줄 체력 갱신
            UpdateHPBar(_bossMonsterController.Stat.HealthComp.CurrentHp, _bossMonsterController.Stat.MaxHp.Value);
        }
    }

    void UpdateHPBar(float cur, float max)
    {
        _slider.value = cur / max;
        _text.text = $"{cur.ToString("N0")} / {max.ToString("N0")}";
    }

    private void OnDestroy()
    {
        if (_bossMonsterController != null && _bossMonsterController.Stat != null)
        {
            _bossMonsterController.Stat.HealthComp.OnHpChanged -= UpdateHPBar;
        }
    }
}
