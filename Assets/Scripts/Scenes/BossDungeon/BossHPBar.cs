using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHPBar : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private TextMeshProUGUI _text;

    private GameObject _boss;
    private BossMonsterController _bossMonsterController;

    public void SetBoss(GameObject boss) {  _boss = boss; }

    public void Start()
    {
        Init();
    }


    public void Init()
    {
        _bossMonsterController = _boss.GetComponent<BossMonsterController>();

        if (_bossMonsterController != null)
        {
            _bossMonsterController.Stat.OnHpChanged -= UpdateHPBar;
            _bossMonsterController.Stat.OnHpChanged += UpdateHPBar;
            UpdateHPBar(_bossMonsterController.Stat.CurrentHp, _bossMonsterController.Stat.MaxHp.Value);
        }
    }

    void UpdateHPBar(float cur, float max)
    {
        Debug.Log($"{cur} , {max}");
        _slider.value = cur / max;
        _text.text = $"{cur.ToString("N0")} / {max.ToString("N0")}";
    }

    private void OnDestroy()
    {
        _bossMonsterController.Stat.OnHpChanged -= UpdateHPBar;
    }
}
