using Cysharp.Threading.Tasks;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_EscapeMenu : UI_PopUp
{
    [SerializeField] private Image[] _decoImages;
    [SerializeField] private Button _continueBtn;
    [SerializeField] private Button _saveGameBtn;
    [SerializeField] private Button _soundSettingBtn;
    [SerializeField] private Button _gameExitBtn;

    [SerializeField] private Image _imageFont;
    [SerializeField] private GameObject[] _standingImages;

    public override void Init()
    {
        base.Init();

        Time.timeScale = 0f;
        //Managers.UI.SetActiveSystemCavas(false);

        SettingDecos().Forget();
        SettingFont().Forget();

        SettingStandingImages();
        SettingListener();
    }

    private async UniTask SettingDecos()
    {
        if (_decoImages != null)
        {
            string[] dir =
            {
                "Left", "Middle", "Right"
            };

            // 0 1 2 순서대로 Left, Middle, Right
            string schoolName = Managers.Context.SelectedSchool.schoolNameEN;
            for(int i= 0; i < 3; i++)
            {
                string key = schoolName + "_Deco_" + dir[i];
                // [Phase 0.5c] 아틀라스 해체 — 학교 1곳 데코 3장만 개별 로드 (9장 전량 로드 제거)
                Sprite img = await Managers.Resource.LoadAsync<Sprite>(key, isGlobal: true);
                _decoImages[i].sprite = img;
            }
        }

    }

    private void SettingStandingImages()
    {
        if (_standingImages != null)
        {
            int schoolIndex = Managers.Context.SchoolIdx;

            for (int i = 0; i < _standingImages.Length; i++)
            {
                if (i == schoolIndex) _standingImages[i].SetActive(true);
                else _standingImages[i].SetActive(false);
            }
        }
    }

    private async UniTask SettingFont()
    {
        if(_imageFont != null)
        {
            string schoolName = Managers.Context.SelectedSchool.schoolNameEN;
            string key = schoolName + "_ImageFont";
            Sprite img = await Managers.Resource.GetSpriteFromAtlasAsync("ImageFontsAtlas", key);
            _imageFont.sprite = img;
        }
    }

    private void SettingListener()
    {
        _continueBtn.onClick.AddListener(OnContinue);
        _gameExitBtn.onClick.AddListener(OnGameExit);
        _soundSettingBtn.onClick.AddListener(OnSoundSetting);
        _saveGameBtn.onClick.AddListener(OnSave);
    }

    private void OnGameExit()
    {
        // 게임 종료
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    private void OnContinue()
    {
        if (Managers.UI.IsPopupOpen)
            Managers.UI.ClosePopupUI();
    }

    private async void OnSoundSetting()
    {
        await Managers.UI.ShowPopupUIAsync<UI_SoundSetting>("UI_SoundSetting");
    }

    private void OnSave()
    {
        if (Managers.Save.IsReady)
            Managers.Save.SaveCurrentPartyAsync().Forget();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        //Managers.UI.SetActiveSystemCavas(true);
    }
}
