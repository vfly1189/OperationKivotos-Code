using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class StartScene : BaseScene
{
    AudioClip _mainTitleBgm;
    AudioClip _titleVoice;

    float _voiceDelay = 1.5f;
    protected override void Init()
    {
        base.Init();

        _sceneType = Define.Scene.Start;

        // 코루틴으로 순차 실행
        StartCoroutine(InitializeScene());
    }

    private IEnumerator InitializeScene()
    {
        // 1. 배경 생성
        CreateBackgroundSlideShow();
        yield return null; // 1프레임 대기 (생성 완료 보장)

        // 2. TabToStart 생성
        CreateTapToStart();
        yield return null;

        // 3. Logo 생성
        CreateLogo();
        yield return null;

        // 4. BGM 먼저 재생 (부드럽게 시작)
        _mainTitleBgm = Managers.Resource.Load<AudioClip>("Sounds/BGM/MainTitle/MainTitle");
        Managers.Sound.Play(_mainTitleBgm, Define.Sound.Bgm);

        // 5. 약간의 여유 시간 후 타이틀 보이스 재생
        yield return new WaitForSeconds(_voiceDelay); // 0.5초 대기

        // 6. 타이틀 보이스 재생
        PlayTitleVoice();

        // 7. 입력 이벤트 등록
        Managers.Input.OnEscapePressed += HandleEscape;
    }

    private void PlayTitleVoice()
    {
        int voiceNum = Random.Range(160, 171);
        string voiceSFXFileName = "CH0" + voiceNum + "_Title";

        _titleVoice = Managers.Resource.Load<AudioClip>($"Sounds/SFX/Title/{voiceSFXFileName}");
        Managers.Sound.Play(_titleVoice, Define.Sound.Narration);
    }

    void Update()
    {
        if (Keyboard.current.qKey.isPressed)
        {
            Managers.Scene.LoadScene(Define.Scene.Select);
        }
    }

    public override void Clear()
    {
        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Sound.Stop(Define.Sound.Bgm);
        Debug.Log("StartScene Clear");
    }

    private void CreateBackgroundSlideShow()
    {
        GameObject bgSlideshow = Managers.Resource.Instantiate("UI/StartScene/UI_BackgroundSlide");
        bgSlideshow.name = "@Background_Slideshow";

        Canvas canvas = bgSlideshow.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = -10;
    }

    private void CreateLogo()
    {
        GameObject logo = Managers.Resource.Instantiate("Logo/Logo");
        logo.name = "@Logo";

        Canvas canvas = logo.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = -9;
    }

    private void CreateTapToStart()
    {
        GameObject tapToStart = Managers.Resource.Instantiate("UI/StartScene/TapToStartGroup");
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
        UI_ExitPopUp popup = Managers.UI.ShowPopupUI<UI_ExitPopUp>("StartScene/UI_ExitPopUp");
        popup.SetCallbacks(
            onConfirm: () => Debug.Log("앱 종료"),
            onCancel: () => Debug.Log("취소됨")
        );
    }
}
