using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneCanvas : MonoBehaviour
{
    //private static GameSceneCanvas _instance;


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


    public void SetPartyManager()
    {
        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged -= UpdateActiveCharacterUI;
            Managers.Party.OnGameFinished -= HandleGameFinished;
        }

        // 파티 슬롯 이벤트 재연결
        ConnectPermanentPartyEvents();

        // 파티 매니저 이벤트 재연결
        if (Managers.Party != null)
        {
            Managers.Party.OnActiveCharacterChanged += UpdateActiveCharacterUI;
            Managers.Party.OnGameFinished += HandleGameFinished;
        }

        // UI 갱신
        BaseCharacter currentChar = Managers.Party?.GetCurrentCharacter();
        if (currentChar != null)
        {
            UpdateActiveCharacterUI(currentChar.gameObject);
        }
    }

    void Start()
    {
        _schoolIdx = Managers.Context.SchoolIdx;

        // 1. UI 기본 정보 세팅 (이름, 초상화)
        _partyHUD.Init(_schoolDatas[_schoolIdx]);

        // 시작할 때 이미지는 꺼두기
        if (_failedImageFont) _failedImageFont.gameObject.SetActive(false);
        if (_sucessImageFont) _sucessImageFont.gameObject.SetActive(false);
    }

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
        if (currentCharacter == null)
        {
            return;
        }

        // --- 1. 기존 캐릭터 구독 해제 ---
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            _activeCharacterHUD.UnSubscribeEvent(_cachedActiveCharacter);
        }

        // --- 2. 새 캐릭터 가져오기 ---
        BaseCharacter newChar = currentCharacter.GetComponent<BaseCharacter>();

        if (newChar == null)
        { 
            return;
        }

        if (newChar.Stat == null)
        {
            return;
        }

        CharacterDataSO charData = newChar.Stat.GetData();

        if (charData == null)
        {
            return;
        }

        _cachedActiveCharacter = newChar;

        // [핵심 수정] 정적 데이터 먼저 교체
        if (_activeCharacterHUD != null)
        {
            _activeCharacterHUD.ChangeStaticData(charData);
        }
        else
        {
            return;
        }

        // --- 3. 새 캐릭터 구독 및 초기화 ---
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
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

        if (isSuccess)
        {
            if (_sucessImageFont != null)
                StartCoroutine(CoShowResultEffect(_sucessImageFont));
        }
        else
        {
            if (_failedImageFont != null)
                StartCoroutine(CoShowResultEffect(_failedImageFont));
        }
    }

    // [추가] 점점 커지는 연출 코루틴
    private IEnumerator CoShowResultEffect(Image targetImage)
    {
        targetImage.gameObject.SetActive(true);
        targetImage.transform.localScale = Vector3.zero; // 0에서 시작

        float duration = 0.5f; // 0.5초 동안 커짐
        float timer = 0f;

        // 약간 튕기는 듯한 연출을 위한 Overshoot 커브 (선택사항)
        // AnimationCurve.EaseInOut(0,0,1,1) 등을 써도 됨

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            // 부드러운 보간 (Lerp)
            // t * (2 - t)는 EaseOut 효과 (빠르게 시작해서 천천히 도착)
            float scale = Mathf.Lerp(0f, 1f, t * (2 - t));

            targetImage.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        targetImage.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(2.0f);
        targetImage.gameObject.SetActive(false);

        _isShowingResult = false; // 플래그 리셋
    }
}
