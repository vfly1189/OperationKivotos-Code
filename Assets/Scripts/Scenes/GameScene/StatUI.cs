using TMPro;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class StatUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private Slider _hpBar;

    [SerializeField] private TextMeshProUGUI _expText;
    [SerializeField] private Slider _expBar;

    public void Initialize(CharacterStat stat)
    {
        SetLevel(stat.CurLevel);
        SetHp(stat.CurrentHp, stat.MaxHp.Value);
        SetExp(stat.CurrentExp, stat.MaxExp.Value);
    }

    // [2] 부분 갱신 (이벤트 연결용)
    public void SetHp(float cur, float max)
    {
        _hpBar.value = (max > 0) ? cur / max : 0;
        _hpText.text = $"{cur:F0} / {max:F0}"; // 소수점 제거 포맷팅
    }

    public void SetExp(float cur, float max)
    {
        _expBar.value = (max > 0) ? cur / max : 0;
        _expText.text = $"{cur:F0} / {max:F0}";
    }

    public void SetLevel(float level)
    {
        _levelText.text = $"LV. {level}";
    }
}
