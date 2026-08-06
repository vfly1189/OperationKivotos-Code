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
            _connectedCharacter.Stat.HealthComp.OnHpChanged -= UpdateHP;
        }

        _connectedCharacter = character;

        if (_connectedCharacter != null && _connectedCharacter.Stat != null)
        {
            _connectedCharacter.Stat.HealthComp.OnHpChanged += UpdateHP;

            // 초기화 (HP는 이벤트, 궁 준비는 아래 Update 폴링이 매 프레임 갱신)
            UpdateHP(_connectedCharacter.Stat.HealthComp.CurrentHp, _connectedCharacter.Stat.MaxHp.Value);
            SetUltimateReady(_connectedCharacter.IsUltimateReady);
        }
    }

    // 궁 준비 글로우 폴링 — 벤치(스왑아웃) 멤버도 Q 쿨(절대시각)이 끝나면 점등돼야 하는데
    // 러너 쿨엔 "끝남" 이벤트가 없다. 슬롯 UI는 벤치 중에도 활성이라 여기서 폴링한다.
    private void Update()
    {
        if (_connectedCharacter != null)
            SetUltimateReady(_connectedCharacter.IsUltimateReady);
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
