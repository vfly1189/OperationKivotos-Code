using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

public class StartScene : BaseScene
{
    [Header("PreloadData")]
    [SerializeField] private StartScenePreloadSO _preloadData;

    float _voiceDelay = 1.5f;
 
    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Start;

        // 비동기 로딩 태스크들을 병렬로 실행하여 로딩 속도 최적화
        var bgTask = CreateBackgroundSlideShow();
        var logoTask = CreateLogo();
        var tapTask = CreateTapToStart();
        var soundIconTask = CreateSoundSettingIcon();

        // BGM과 Voice 로딩 및 재생
        var bgmTask = PlayMainTitle();

        // 모든 필수 시각적 요소가 로드될 때까지 대기
        await Task.WhenAll(bgTask, logoTask, tapTask, soundIconTask, bgmTask);

        // 보이스는 조금 있다가 재생
        Invoke(nameof(PlayTitleVoice), _voiceDelay);

        Managers.Input.OnEscapePressed += HandleEscape;
    }


    private async Task PlayMainTitle()
    {
        if (_preloadData.mainTitleBgm != null && _preloadData.mainTitleBgm.RuntimeKeyIsValid())
        {
            AudioClip bgm = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.mainTitleBgm);
            if (bgm != null)
                Managers.Sound.Play(bgm, Define.Sound.Bgm);
        }
    }

    private async void PlayTitleVoice()
    {
        int length = _preloadData.titleVoices.Length;
        if (length > 0)
        {
            int voiceNum = Random.Range(0, length);
            AudioClip voice = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.titleVoices[voiceNum]);
            if (voice != null)
                Managers.Sound.Play(voice, Define.Sound.Voice);
        }
    }
    private async Task CreateBackgroundSlideShow()
    {
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.backgroundSlideShow);
        if (prefab != null)
        {
            GameObject bgSlideshow = Instantiate(prefab);
            bgSlideshow.name = "@Background_Slideshow";

            Canvas canvas = bgSlideshow.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = -10;
        }
    }

    private async Task CreateLogo()
    {
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.logo);
        if (prefab != null)
        {
            GameObject logo = Instantiate(prefab);
            logo.name = "@Logo";

            Canvas canvas = logo.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = -9;
        }
    }

    private async Task CreateSoundSettingIcon()
    {
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.soundSettingIcon);
        if (prefab != null)
        {
            GameObject icon = Instantiate(prefab);
            icon.name = "@SoundSettingIcon";

            Canvas canvas = icon.GetComponent<Canvas>();
            if (canvas == null) canvas = icon.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -7;

            SoundButton soundBtn = icon.GetComponent<SoundButton>();
            // 팝업 프리팹은 클릭 시 로드하도록 하거나, SO에 레퍼런스로 들고 있게 수정
            soundBtn.SetPopupPrefab(await Managers.Resource.LoadAsync<GameObject>(_preloadData.soundSettingPopup));
        }
    }

    private async Task CreateTapToStart()
    {
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.tapToStart);
        if (prefab != null)
        {
            GameObject tapToStart = Instantiate(prefab);
            tapToStart.name = "@TapToStartGroup";

            Canvas canvas = tapToStart.GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = -8;
        }
    }


    private async void ShowExitPopup()
    {
        // 팝업 역시 띄울 때 비동기로 로드해서 보여줌
        GameObject popupPrefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.exitPopup);
        if (popupPrefab != null)
        {
            UI_ExitPopUp popup = Managers.UI.ShowPopupUI<UI_ExitPopUp>(popupPrefab);
            popup.SetCallbacks(
                onConfirm: () => Debug.Log("앱 종료"),
                onCancel: () => Debug.Log("취소됨")
            );
        }
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

    public override void Clear()
    {
        base.Clear();
        CancelInvoke(); // Invoke 취소

        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Sound.Stop(Define.Sound.Bgm);
    }
}
