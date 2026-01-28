using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_SoundSetting : UI_PopUp
{
    [Header("Slider")]
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("Button")]
    [SerializeField] private Button _confirmButton;

    private Action _onConfirm;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void Init()
    {
        base.Init();

        // 1. 현재 사운드 매니저의 볼륨값으로 슬라이더 초기화
        if (_bgmSlider != null)
        {
            _bgmSlider.value = Managers.Sound.BgmVolume;
            _bgmSlider.onValueChanged.AddListener(OnBgmValueChanged);
        }

        if (_sfxSlider != null)
        {
            _sfxSlider.value = Managers.Sound.SfxVolume;
            _sfxSlider.onValueChanged.AddListener(OnSfxValueChanged);
        }

        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    // 슬라이더를 드래그할 때마다 호출됨 (Update 필요 없음!)
    private void OnBgmValueChanged(float value)
    {
        Managers.Sound.SetBgmVolume(value);
    }

    private void OnSfxValueChanged(float value)
    {
        Managers.Sound.SetSfxVolume(value);
    }

    private void OnConfirmClicked()
    {
        // 팝업 닫기
        ClosePopupUI();

        // (선택) 여기서 PlayerPrefs.Save() 등을 호출하여 설정 영구 저장 가능
    }
}
