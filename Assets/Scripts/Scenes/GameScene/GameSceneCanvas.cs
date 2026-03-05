using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneCanvas : UI_Scene
{
    [SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결

    [Header("Data Source")]
    [SerializeField] private SchoolDataSO[] _schoolDatas;

    [Header("파티 슬롯")]
    [SerializeField] private PartyHUD _partyHUD;

    [Header("스킬아이콘 및 체력경험치 HUD")]
    [SerializeField] 
    private ActiveCharacterHUD _activeCharacterHUD;

    [Header("Failed or Success")]
    [SerializeField] private Image _failedImageFont;
    [SerializeField] private Image _sucessImageFont;

    //private PartyManager _partyManager;
    private BaseCharacter _cachedActiveCharacter; // 현재 UI가 구독 중인 캐릭터
    int _schoolIdx;
    private bool _isShowingResult = false; // [추가] 중복 실행 방지

    private bool _isInit = false;
    public override void Init()
    {
        if (_isInit) return;
        base.Init(); 

        _schoolIdx = Managers.Context.SchoolIdx;

        // 1. UI 기본 정보 세팅 (이름, 초상화)
        if (_schoolDatas != null && _schoolDatas.Length > _schoolIdx)
        {
            _partyHUD.Init(_schoolDatas[_schoolIdx]);
        }

        // 시작할 때 이미지는 꺼두기
        if (_failedImageFont) _failedImageFont.gameObject.SetActive(false);
        if (_sucessImageFont) _sucessImageFont.gameObject.SetActive(false);

        _isInit = true;
    }

    public void SetPartyManager()
    {
        Init(); // [핵심] 외부(GameScene 등)에서 SetPartyManager를 Start보다 먼저 부를 경우를 대비해 확실히 초기화 보장

        // 중복 구독 방지를 위해 확실히 먼저 해제
        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= UpdateActiveCharacterUI;
            Managers.Party.OnGameFinished -= HandleGameFinished;

            Managers.Party.OnActiveCharacterChanged += UpdateActiveCharacterUI;
            Managers.Party.OnGameFinished += HandleGameFinished;
        }

        ConnectPermanentPartyEvents();

        BaseCharacter currentChar = Managers.Party?.GetCurrentCharacter();
        if (currentChar != null)
        {
            UpdateActiveCharacterUI(currentChar.gameObject);
        }


    }

    //void Start()
    //{
    //    _schoolIdx = Managers.Context.SchoolIdx;

    //    // 1. UI 기본 정보 세팅 (이름, 초상화)
    //    _partyHUD.Init(_schoolDatas[_schoolIdx]);

    //    // 시작할 때 이미지는 꺼두기
    //    if (_failedImageFont) _failedImageFont.gameObject.SetActive(false);
    //    if (_sucessImageFont) _sucessImageFont.gameObject.SetActive(false);
    //}

    

    void Update()
    {
        // [최적화] 매 프레임 도는 것은 오직 '쿨타임' 뿐
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            _activeCharacterHUD.UpdateCooldowns(_cachedActiveCharacter.Stat);
        }
    }

    // ========================================================================
    // [1] 파티 슬롯 (우측) - 한 번 연결하면 끝 (캐릭터가 파티에서 빠지지 않는 한)
    // ========================================================================


    void ConnectPermanentPartyEvents()
    {
        if (Managers.Party == null || Managers.Party.GetMemeber() == null)
            return;

        _partyHUD.ConnectPartyEvents(Managers.Party.GetMemeber());
    }

    // ========================================================================
    // [2] 활성 캐릭터 (메인 UI) - 교체될 때마다 갈아끼우기
    // ========================================================================
    void UpdateActiveCharacterUI(GameObject currentCharacter)
    {
        // [추가] null 체크
        if (currentCharacter == null) return;

        // --- 1. 기존 캐릭터 구독 해제 ---
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            _activeCharacterHUD.UnSubscribeEvent(_cachedActiveCharacter);
        }

        // --- 2. 새 캐릭터 가져오기 ---
        BaseCharacter newChar = currentCharacter.GetComponent<BaseCharacter>();

        if (newChar == null) return;
        if (newChar.Stat == null) return;


        CharacterDataSO charData = newChar.Stat.GetData();
        if (charData == null) return;


        _cachedActiveCharacter = newChar;

        if (_activeCharacterHUD != null)
        {
            _activeCharacterHUD.ChangeStaticData(charData);
            _activeCharacterHUD.SubscribeEvent(_cachedActiveCharacter);
        }
    }


    // ========================================================================
    // [3] 매 프레임 업데이트 (Update)
    // ========================================================================

    void OnDestroy()
    {
        // 1. PartyManager 이벤트 해제
        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= UpdateActiveCharacterUI;
            Managers.Party.OnGameFinished -= HandleGameFinished;
        }

        // 2. [핵심] 현재 보고 있던 캐릭터의 스탯 이벤트 해제 (이게 빠져서 문제였음)
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            _activeCharacterHUD.UnSubscribeEvent(_cachedActiveCharacter);
        }
    }

    // 게임 종료 핸들러
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


    // [핵심 1] 코루틴 -> UniTask 변경 및 토큰 적용
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
            if (isCanceled) return; // 씬 전환 등으로 파괴 시 안전 종료
        }

        targetImage.transform.localScale = Vector3.one;

        bool isWaitCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(2.0f), cancellationToken: token).SuppressCancellationThrow();
        if (isWaitCanceled) return;

        targetImage.gameObject.SetActive(false);
        _isShowingResult = false;
        gameObject.SetActive(false);
    }
}
