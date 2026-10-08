// [추가] UniTask 네임스페이스
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StartScene : BaseScene
{
    [Header("PreloadData")]
    [SerializeField] private StartScenePreloadSO _preloadData;

    [SerializeField] private TextMeshProUGUI _loadingText; // "데이터를 준비중입니다..."
    [SerializeField] private GameObject _tapToStartGroup; // (시작 시 비활성화 상태)
    [SerializeField] private Button _startButton;
    [SerializeField] private GameObject _soundSettingButton;

    float _voiceDelay = 0.5f;

    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Start;

        // 이벤트 시스템 추가
        // 얘 없으면 UI들 작동안함
        MakeEventSystem();

        _startButton.onClick.AddListener(OnClick);

        DeActiveUI();

        var audioTask = PlayMainTitle();
        var popupTask = PreloadPopups();
        var saveTask = LoadingSaveDatas();

        var globalAssetTask = Managers.Resource.LoadAsyncPreload(
                                    new[] { "Global" },
                                    true,                       // Global 스코프
                                    (fileName, progress) => UpdateText(progress)
                                        );

        // [핵심 3] Task.WhenAll 대신 UniTask.WhenAll을 사용하여 스레드 데드락 방지
        await UniTask.WhenAll(audioTask, popupTask, globalAssetTask, saveTask);

        ActiveUI();

        // 보이스 재생 (Invoke 대신 딜레이를 직접 주거나 UniTask.Delay 사용 가능)
        // 여기서는 안전하게 Fire-and-forget 방식(UniTaskVoid)으로 백그라운드 재생
        PlayTitleVoiceWithDelay(_voiceDelay).Forget();    

        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Input.OnEscapePressed += HandleEscape;
    }

    public void DeActiveUI()
    {
        // 1. 초기 UI 상태 세팅
        _loadingText.gameObject.SetActive(true);
        if (_tapToStartGroup != null) _tapToStartGroup.SetActive(false);
        _startButton.interactable = false;
        _soundSettingButton.gameObject.SetActive(false);
    }

    public void ActiveUI()
    {
        // 4. 로딩 완료!
        _startButton.interactable = true;
        _soundSettingButton.gameObject.SetActive(true);

        // 5. 로딩 완료 처리 (유저 조작 허용)
        _loadingText.gameObject.SetActive(false);
        if (_tapToStartGroup != null) _tapToStartGroup.SetActive(true);
    }

    public void MakeEventSystem()
    {
        // 씬에 올려둔 EventSystem을 찾아서 파괴되지 않게 설정
        EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            DontDestroyOnLoad(eventSystem.gameObject);
        }
    }

    public void UpdateText(float progress)
    {
        _loadingText.text = $"Loading ... 진행률 : {progress * 100.0f}%";
    }

    private async UniTask PlayMainTitle()
    {
        if (_preloadData.mainTitleBgm != null && _preloadData.mainTitleBgm.RuntimeKeyIsValid())
        {
            AudioClip bgm = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.mainTitleBgm);

            if (bgm != null)
            {
                Managers.Sound.Play(bgm, Define.Sound.Bgm);
            }
        }
    }

    private async UniTask LoadingSaveDatas()
    {
        Managers.Context.SetPreloadedSaveData("Abydos", await Managers.Save.LoadPartyAsync("Abydos"));
        Managers.Context.SetPreloadedSaveData("Gehenna", await Managers.Save.LoadPartyAsync("Gehenna"));
        Managers.Context.SetPreloadedSaveData("Millennium", await Managers.Save.LoadPartyAsync("Millennium"));
    }

    // 딜레이를 주고 백그라운드에서 실행할 수 있도록 UniTaskVoid로 분리
    private async UniTaskVoid PlayTitleVoiceWithDelay(float delay)
    {
        // Invoke를 대체하는 UniTask의 강력한 시간 대기 (타임스케일 영향 받음)
        await UniTask.Delay(System.TimeSpan.FromSeconds(delay));

        int length = _preloadData.titleVoices.Length;
        if (length > 0)
        {
            int voiceNum = Random.Range(0, length);

            AudioClip voice = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.titleVoices[voiceNum]);

            if (voice != null)
                Managers.Sound.Play(voice, Define.Sound.Voice);
        }
    }

    private async UniTask PreloadPopups()
    {
        if (_preloadData.exitPopup != null && _preloadData.exitPopup.RuntimeKeyIsValid())
        {
            await Managers.Resource.LoadAsync<GameObject>(_preloadData.exitPopup);
        }
    }

    // 버튼 클릭 등의 이벤트에서 비동기를 띄울 때는 async UniTaskVoid 사용
    private async UniTaskVoid ShowExitPopup()
    {
        //var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.exitPopup);
        GameObject popupPrefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.exitPopup, ResourceScopeType.Global);

        if (popupPrefab != null)
        {
            UI_ExitPopUp popup = await Managers.UI.ShowPopupUIAsync<UI_ExitPopUp>("UI_ExitPopUp");
            popup.SetCallbacks(
                onConfirm: () => GameLog.Log("앱 종료"),
                onCancel: () => GameLog.Log("취소됨")
            );
        }
    }

    void OnClick()
    {  
        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Select);
    }

    protected override void HandleEscape()
    {
        GameLog.Log("Handle Escape 호출");
        if (Managers.UI.IsPopupOpen)
        {
            Managers.UI.ClosePopupUI();
        }
        else
        {
            ShowExitPopup().Forget();
        }
    }

    public override void Clear()
    {
        base.Clear();
        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Sound.Stop(Define.Sound.Bgm);
    }
}
