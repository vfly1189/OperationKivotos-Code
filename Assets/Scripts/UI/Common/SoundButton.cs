using UnityEngine;
using UnityEngine.UI;

public class SoundButton : MonoBehaviour
{
    [SerializeField] private Button _soundSettingButton;

    private void Start()
    {
        // 안전장치
        if (_soundSettingButton == null)
            _soundSettingButton = GetComponent<Button>();

        _soundSettingButton.onClick.AddListener(OnSoundSettingClicked);
    }

    public void OnSoundSettingClicked()
    {
        // 1. 이미 팝업이 떠 있는지 확인 (선택 사항)
        // 만약 중복으로 뜨는 걸 막고 싶다면 UIManager에 확인 로직 추가 필요
        // 하지만 지금 UIManager 구조상 계속 쌓이게(Stack) 되어 있음.

        // 2. 팝업 띄우기 (핵심!)
        // 제네릭 T에는 팝업 스크립트 클래스 이름을 넣고, 
        // 인자에는 "폴더 경로/프리팹 이름"을 넣습니다.
        // 프리팹이 Resources/UI/StartScene/UI_SoundSetting 에 있다고 가정
        Managers.UI.ShowPopupUI<UI_SoundSetting>("common/UI_SoundSetting");

        Debug.Log("Sound Setting Popup Opened!");
    }
}
