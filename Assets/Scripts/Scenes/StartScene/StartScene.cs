using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class StartScene : BaseScene
{
    [Header("PreloadData")]
    [SerializeField] private StartScenePreloadSO _preloadData;

    float _voiceDelay = 1.5f;
    protected override void Init()
    {
        base.Init();

        _sceneType = Define.Scene.Start;

        CreateBackgroundSlideShow();
        CreateTapToStart();
        CreateSoundSettingIcon();
        CreateLogo();

        PlayMainTitle();

        Invoke(nameof(PlayTitleVoice), _voiceDelay);

        Managers.Input.OnEscapePressed += HandleEscape;
    }


    private void PlayMainTitle()
    {
        Managers.Sound.Play(_preloadData.mainTitleBgm, Define.Sound.Bgm);
    }

    private void PlayTitleVoice()
    {
        int length = _preloadData.titleVoices.Count;
        if (length > 0)
        {
            int voiceNum = Random.Range(0, length); 
            Managers.Sound.Play(_preloadData.titleVoices[voiceNum], Define.Sound.Narration);
        }
    }

    public override void Clear()
    {
        base.Clear();
        CancelInvoke(); // Invoke 취소

        Managers.Input.OnEscapePressed -= HandleEscape;

        Managers.Sound.Stop(Define.Sound.Bgm);
    }

    private void CreateBackgroundSlideShow()
    {
        GameObject bgSlideshow = Object.Instantiate(_preloadData.backgroundSlideShow);

        bgSlideshow.name = "@Background_Slideshow";

        Canvas canvas = bgSlideshow.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = -10;
    }

    private void CreateLogo()
    {
        GameObject logo = Object.Instantiate(_preloadData.logo);

        logo.name = "@Logo";

        Canvas canvas = logo.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = -9;
    }

    private void CreateSoundSettingIcon()
    {
        GameObject icon = Object.Instantiate(_preloadData.soundSettingIcon);
        icon.name = "@SoundSettingIcon";

        Canvas canvas = icon.GetComponent<Canvas>();
        if (canvas == null)
            canvas = icon.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -7;

        // SoundButton에 Prefab 전달
        SoundButton soundBtn = icon.GetComponent<SoundButton>();
        if (soundBtn != null && _preloadData.soundSettingPopup != null)
        {
            soundBtn.SetPopupPrefab(_preloadData.soundSettingPopup);
        }
    }

    private void CreateTapToStart()
    {
        GameObject tapToStart = Object.Instantiate(_preloadData.tapToStart);
        tapToStart.name = "@TapToStartGroup";

        Canvas canvas = tapToStart.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = -8;
    }

    private void HandleEscape()
    {
        if (Managers.UI.IsPopupOpen)
        {
            Managers.UI.ClosePopupUI();
        }
        else
        {
            ShowExitPopup();
        }
    }

    private void ShowExitPopup()
    {
        UI_ExitPopUp popup = Managers.UI.ShowPopupUI<UI_ExitPopUp>(_preloadData.exitPopup);
        popup.SetCallbacks(
            onConfirm: () => Debug.Log("앱 종료"),
            onCancel: () => Debug.Log("취소됨")
        );
    }
}
