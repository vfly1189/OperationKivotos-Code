using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class SoundButton : MonoBehaviour
{
    [SerializeField] private Button _soundSettingButton;
    
    
    private GameObject _soundSettingPopupPrefab;

    // 외부에서 Prefab 주입
    public void SetPopupPrefab(GameObject prefab)
    {
        _soundSettingPopupPrefab = prefab;
    }

    private void Start()
    {
        if (_soundSettingButton == null)
            _soundSettingButton = GetComponent<Button>();

        if (_soundSettingButton != null)
            _soundSettingButton.onClick.AddListener(OnSoundSettingClicked);
    }

    private void OnSoundSettingClicked()
    {
        if (_soundSettingPopupPrefab == null)
        {
            Debug.LogError("SoundSetting Popup Prefab이 연결되지 않았습니다!");
            return;
        }

        // Prefab을 직접 전달
       // Managers.UI.ShowPopupUI<UI_SoundSetting>(_soundSettingPopupPrefab);
    }

    private void OnDestroy()
    {
        if (_soundSettingButton != null)
            _soundSettingButton.onClick.RemoveListener(OnSoundSettingClicked);
    }
}
