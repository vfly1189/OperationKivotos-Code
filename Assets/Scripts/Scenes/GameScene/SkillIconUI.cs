using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillIconUI : MonoBehaviour
{
    [SerializeField] private Image _readyGlow;
    [SerializeField] private Image _energyFill;

    [SerializeField] private Image _icon;
    [SerializeField] private Image _coolTimeRing;
    [SerializeField] private TextMeshProUGUI _coolTimeText;

    public void SetIcon(Sprite icon) => _icon.sprite = icon;
    public void SetEnergyFillColor(Color color) { if (_energyFill) _energyFill.color = color; }
    public void SetReadyGlowColor(Color color) { if (_readyGlow) _readyGlow.color = color; }

    public void SetCoolTimeText(string text)
    {
        _coolTimeText.text = text;
    }


    // --- [Update용] 매 프레임 호출: 쿨타임 표시 ---
    public void UpdateCooldown(float currentCool, float maxCool)
    {
        if (currentCool > 0)
        {
            if (!_coolTimeText.gameObject.activeSelf) _coolTimeText.gameObject.SetActive(true);

            // [핵심 4] ToString() 대신 TMP의 SetText 포맷팅 사용 (가비지 0 또는 극소화)
            _coolTimeText.SetText("{0:0.0}", currentCool);
            _coolTimeRing.fillAmount = currentCool / maxCool;
        }
        else
        {
            if (_coolTimeText.gameObject.activeSelf)
            {
                _coolTimeText.gameObject.SetActive(false);
                _coolTimeRing.fillAmount = 0;
            }
        }
    }

    // --- [이벤트용] 에너지 변경 시 호출 ---
    public void UpdateEnergy(float currentEnergy, float maxEnergy)
    {
        //Debug.Log($"에너지 변동 : {currentEnergy} , {maxEnergy}");
        if (_energyFill != null && maxEnergy > 0)
        {
            _energyFill.fillAmount = currentEnergy / maxEnergy;
        }
    }

    // --- [이벤트용] 궁극기 준비 상태 변경 시 호출 ---
    public void SetUltimateReady(bool isReady)
    {
        if (_readyGlow != null && _readyGlow.gameObject.activeSelf != isReady)
        {
            _readyGlow.gameObject.SetActive(isReady);
        }
    }

    // [2] 이벤트용: 에너지 & 준비 상태 갱신 (가끔 호출됨)
    // 쿨타임이 끝났을 때도 상태 체크를 위해 호출해줘야 함
    public void UpdateEnergyState(float currentEnergy, float maxEnergy, bool isSkillReady)
    {
        if (_energyFill != null && maxEnergy > 0)
        {
            _energyFill.fillAmount = currentEnergy / maxEnergy;
        }

        if (_readyGlow != null)
        {
            // 이미 켜져있으면 건드리지 않음 (SetActive 최적화)
            if (_readyGlow.gameObject.activeSelf != isSkillReady)
                _readyGlow.gameObject.SetActive(isSkillReady);
        }
    }

    public void RefreshUI(float currentCool, float maxCool, float currentEnergy = -1, float maxEnergy = -1)
    {
        // 1. 쿨타임 처리
        if (currentCool > 0)
        {
            //_cooldownCover.fillAmount = currentCool / maxCool;
            _coolTimeText.gameObject.SetActive(true);
            _coolTimeText.text = currentCool.ToString("0.0"); // 소수점 1자리
            _coolTimeRing.fillAmount = currentCool / maxCool;
        }
        else
        {
            //_cooldownCover.fillAmount = 0;
            _coolTimeText.gameObject.SetActive(false);
            _coolTimeRing.fillAmount = currentCool / maxCool;
        }

        // 2. 에너지 처리 (Q스킬 궁극기용)
        if (_energyFill != null && maxEnergy > 0)
        {
            float ratio = currentEnergy / maxEnergy;
            _energyFill.fillAmount = ratio;

            //Debug.Log($"SkillUI: 에너지값{_energyFill.fillAmount}");

            // 궁극기 준비 완료 (쿨타임 X + 에너지 O)
            bool isReady = (currentCool <= 0) && (ratio >= 1.0f);

            if (_readyGlow != null)
                _readyGlow.gameObject.SetActive(isReady);
        }
    }
}
