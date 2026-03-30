using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class SoundButton : MonoBehaviour
{
    [SerializeField] private Button _soundSettingButton;
    

    private void Start()
    {
        if (_soundSettingButton == null)
            _soundSettingButton = GetComponent<Button>();

        if (_soundSettingButton != null)
            _soundSettingButton.onClick.AddListener(OnSoundSettingClicked);
    }

    private async void OnSoundSettingClicked()
    {
        // Prefab을 직접 전달
        await Managers.UI.ShowPopupUIAsync<UI_SoundSetting>("UI_SoundSetting");
    }

    private void OnDestroy()
    {
        if (_soundSettingButton != null)
            _soundSettingButton.onClick.RemoveListener(OnSoundSettingClicked);
    }
}
