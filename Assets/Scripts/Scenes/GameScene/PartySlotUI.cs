using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartySlotUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Image _emblemImage;
    [SerializeField] private Slider _hpBar;
    [SerializeField] private GameObject _ultimateReadyObj;

    private BaseCharacter _connectedCharacter;

    // 데이터 갱신 메서드들을 이곳으로 이동
    public void SetCharacterName(string name) => _nameText.text = name;
    public void SetEmblem(Sprite sprite) => _emblemImage.sprite = sprite;


    // [핵심 3] 슬롯 스스로가 캐릭터를 기억하고 이벤트를 안전하게 구독/해제함
    public void SubscribeToCharacter(BaseCharacter character)
    {
        // 기존 연결 해제
        if (_connectedCharacter != null && _connectedCharacter.Stat != null)
        {
            _connectedCharacter.Stat.OnHpChanged -= UpdateHP;
            _connectedCharacter.Stat.OnUltimateStateChanged -= SetUltimateReady;
        }

        _connectedCharacter = character;

        if (_connectedCharacter != null && _connectedCharacter.Stat != null)
        {
            _connectedCharacter.Stat.OnHpChanged += UpdateHP;
            _connectedCharacter.Stat.OnUltimateStateChanged += SetUltimateReady;

            // 초기화
            UpdateHP(_connectedCharacter.Stat.CurrentHp, _connectedCharacter.Stat.MaxHp.Value);
            bool isReady = (_connectedCharacter.Stat.CurrentQSkillCoolTime <= 0) &&
                           (_connectedCharacter.Stat.CurrentEnergy >= _connectedCharacter.Stat.MaxEnergy.Value);
            SetUltimateReady(isReady);
        }
    }

    // 람다 대신 쓸 정식 이벤트 핸들러들
    private void UpdateHP(float current, float max)
    {
        if (_hpBar != null && max > 0)
            _hpBar.value = current / max;
    }

    private void SetUltimateReady(bool isReady)
    {
        if (_ultimateReadyObj != null && _ultimateReadyObj.activeSelf != isReady)
            _ultimateReadyObj.SetActive(isReady);
    }
}
