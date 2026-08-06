using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneCanvas : UI_Scene
{
    [Header("Data Source")]
    [SerializeField] private SchoolDataSO[] _schoolDatas;

    [Header("파티 슬롯")]
    [SerializeField] private PartyHUD _partyHUD;

    [Header("스킬아이콘 및 체력경험치 HUD")]
    [SerializeField] private ActiveCharacterHUD _activeCharacterHUD;

    [Header("Failed or Success")]
    [SerializeField] private Image _failedImageFont;
    [SerializeField] private Image _sucessImageFont;

    private BaseCharacter _cachedActiveCharacter;
    private int _schoolIdx;
    private bool _isShowingResult = false;
    private bool _isInit = false;

    public override void Init()
    {
        if (_isInit) return;
        base.Init(); // UI_Scene의 Init 호출

        _schoolIdx = Managers.Context.SchoolIdx;

        if (_failedImageFont) _failedImageFont.gameObject.SetActive(false);
        if (_sucessImageFont) _sucessImageFont.gameObject.SetActive(false);

        _isInit = true;

        // 2. 비동기 로딩 작업을 Fire & Forget으로 던져놓음
        InitAsync().Forget();
    }

    // 3. 실제 비동기 로딩을 담당하는 내부 함수
    private async UniTaskVoid InitAsync()
    {
        // UI 기본 정보 세팅 (비동기 대기)
        if (_schoolDatas != null && _schoolDatas.Length > _schoolIdx)
        {
            await _partyHUD.Init(_schoolDatas[_schoolIdx]);
        }
    }

    public void SetPartyManager()
    {
        // Init()이 안 불렸다면 여기서 호출
        if (!_isInit)
        {
            Init();
        }

        // 이벤트 중복 방지
        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= OnActiveCharacterChanged;
            Managers.Party.OnGameFinished -= HandleGameFinished;

            Managers.Party.OnActiveCharacterChanged += OnActiveCharacterChanged;
            Managers.Party.OnGameFinished += HandleGameFinished;

            ConnectPermanentPartyEvents();

            BaseCharacter currentChar = Managers.Party.GetCurrentCharacter();
            if (currentChar != null)
            {
                OnActiveCharacterChanged(currentChar.gameObject);
            }
        }
    }



    void Update()
    {
        if (_cachedActiveCharacter != null)
        {
            _activeCharacterHUD.UpdateCooldowns(_cachedActiveCharacter);
        }
    }

    void ConnectPermanentPartyEvents()
    {
        if (Managers.Party == null || Managers.Party.GetMember() == null) return;
        _partyHUD.ConnectPartyEvents(Managers.Party.GetMember());
    }

    // 델리게이트와 시그니처를 맞추기 위한 동기 래퍼 함수
    private void OnActiveCharacterChanged(GameObject currentCharacter)
    {
        UpdateActiveCharacterUIAsync(currentCharacter).Forget();
    }

    private async UniTaskVoid UpdateActiveCharacterUIAsync(GameObject currentCharacter)
    {
        if (currentCharacter == null) return;

        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            _activeCharacterHUD.UnSubscribeEvent(_cachedActiveCharacter);
        }

        BaseCharacter newChar = currentCharacter.GetComponent<BaseCharacter>();
        if (newChar == null || newChar.Stat == null) return;

        CharacterDataSO charData = newChar.Stat.GetData();
        if (charData == null) return;

        _cachedActiveCharacter = newChar;

        if (_activeCharacterHUD != null)
        {
            await _activeCharacterHUD.ChangeStaticDataAsync(charData);
            _activeCharacterHUD.SubscribeEvent(_cachedActiveCharacter);
        }
    }

    void OnDestroy()
    {
        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= OnActiveCharacterChanged;
            Managers.Party.OnGameFinished -= HandleGameFinished;
        }

        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            _activeCharacterHUD.UnSubscribeEvent(_cachedActiveCharacter);
        }
    }

    private void HandleGameFinished(bool isSuccess)
    {
        if (_isShowingResult) return;
        _isShowingResult = true;

        if (isSuccess && _sucessImageFont != null)
        {
            gameObject.SetActive(true);
            ShowResultEffectAsync(_sucessImageFont, this.GetCancellationTokenOnDestroy()).Forget();
        }
        else if (!isSuccess && _failedImageFont != null)
        {
            gameObject.SetActive(true);
            ShowResultEffectAsync(_failedImageFont, this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    private async UniTaskVoid ShowResultEffectAsync(Image targetImage, System.Threading.CancellationToken token)
    {
        targetImage.gameObject.SetActive(true);
        targetImage.transform.localScale = Vector3.zero;

        float duration = 0.5f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            float scale = Mathf.Lerp(0f, 1f, t * (2 - t));

            targetImage.transform.localScale = Vector3.one * scale;

            bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (isCanceled) return;
        }

        targetImage.transform.localScale = Vector3.one;

        bool isWaitCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(2.0f), cancellationToken: token).SuppressCancellationThrow();
        if (isWaitCanceled) return;

        targetImage.gameObject.SetActive(false);
        _isShowingResult = false;
        gameObject.SetActive(false);
    }
}
