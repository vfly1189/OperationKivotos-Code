using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartySlotUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Image _portraitImage;
    [SerializeField] private Slider _hpBar;
    [SerializeField] private GameObject _ultimateReadyObj;
    //[SerializeField] private UI_RotatingLight _rotatingLight;

    // 외부에서 접근하기 위한 프로퍼티 (필요시)
    //public UI_RotatingLight RotatingLight => _rotatingLight;

    // 데이터 갱신 메서드들을 이곳으로 이동
    public void SetCharacterName(string name) => _nameText.text = name;
    public void SetPortrait(Sprite sprite) => _portraitImage.sprite = sprite;

    // [이벤트용] HP 갱신
    public void UpdateHP(float current, float max)
    {
        if (_hpBar != null && max > 0)
            _hpBar.value = current / max;
    }

    // [이벤트용] 궁극기 준비 효과 On/Off
    public void SetUltimateReady(bool isReady)
    {
        if (_ultimateReadyObj != null && _ultimateReadyObj.activeSelf != isReady)
        {
            _ultimateReadyObj.SetActive(isReady);
        }
    }
}
