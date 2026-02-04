using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneCanvas : MonoBehaviour
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


    private PartyManager _partyManager;
    private BaseCharacter _cachedActiveCharacter; // 현재 UI가 구독 중인 캐릭터
    int _schoolIdx;

    public void SetPartyManager(PartyManager partyManager)
    {
        // 기존 파티 매니저 이벤트 해제
        if (_partyManager != null)
        {
            _partyManager.OnCharacterChanged -= UpdateActiveCharacterUI;
            _partyManager.OnGameFinished -= HandleGameFinished;
        }

        _partyManager = partyManager;

        if (_partyManager != null)
        {
            // [핵심] 캐릭터가 재생성되었을 수 있으므로 파티 슬롯 이벤트 재연결
            ConnectPermanentPartyEvents();

            // 파티 매니저 이벤트 재연결
            _partyManager.OnCharacterChanged += UpdateActiveCharacterUI;
            _partyManager.OnGameFinished += HandleGameFinished;

            // 현재 캐릭터로 UI 갱신 (OnSceneLoaded에서도 호출되지만 명시적으로)
            UpdateActiveCharacterUI(_partyManager.PartyMembers.FindIndex(
                c => c.gameObject.activeSelf
            ));
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
        if (_partyManager == null || _partyManager.PartyMembers == null)
            return;

        _partyHUD.ConnectPartyEvents(_partyManager.PartyMembers);
    }

    // ========================================================================
    // [2] 활성 캐릭터 (메인 UI) - 교체될 때마다 갈아끼우기
    // ========================================================================
    void UpdateActiveCharacterUI(int charIndex)
    {
        // --- 1. 기존 캐릭터 구독 해제 ---
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            _activeCharacterHUD.UnSubscribeEvent(_cachedActiveCharacter);
        }

        // --- 2. 새 캐릭터 가져오기 ---
        BaseCharacter newChar = _partyManager.PartyMembers[charIndex];
        CharacterDataSO charData = Managers.Context.SelectedSchool.characters[charIndex];
        _cachedActiveCharacter = newChar;

        // 아이콘 등 정적 데이터 교체
        if (charData != null)
        {
            _activeCharacterHUD.ChangeStaticData(charData);
        }

        // --- 3. 새 캐릭터 구독 및 초기화 ---
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            CharacterStat newStat = _cachedActiveCharacter.Stat;
            _activeCharacterHUD.SubscribeEvent(_cachedActiveCharacter);
        }
    }



    // ========================================================================
    // [3] 매 프레임 업데이트 (Update)
    // ========================================================================

    void OnDestroy()
    {
        // 1. PartyManager 이벤트 해제
        if (_partyManager != null)
        {
            _partyManager.OnCharacterChanged -= UpdateActiveCharacterUI;
            _partyManager.OnGameFinished -= HandleGameFinished;
        }

        // 2. [핵심] 현재 보고 있던 캐릭터의 스탯 이벤트 해제 (이게 빠져서 문제였음)
        if (_cachedActiveCharacter != null && _cachedActiveCharacter.Stat != null)
        {
            _activeCharacterHUD.UnSubscribeEvent(_cachedActiveCharacter);
        }
    }

    // [추가] 게임 종료 핸들러
    private void HandleGameFinished(bool isSuccess)
    {
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
    }
}
