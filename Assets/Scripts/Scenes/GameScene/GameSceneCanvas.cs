using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneCanvas : MonoBehaviour
{
    [SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결

    [Header("Data Source")]
    [SerializeField] private SchoolDataSO[] _schoolDatas;

    [Header("Party Slots")]
    [SerializeField] private GameObject[] _partySlots;

    [Header("Name Slots")]
    [SerializeField] private TextMeshProUGUI[] _characterNameSlots;

    [Header("Portrait Slots")]
    [SerializeField] private Image[] _portraitSlots;

    [Header("HPBar Slots")]
    [SerializeField] private Slider[] _hpBars;

    [Header("UltimateReady Slots")]
    [SerializeField] private GameObject[] _ultimateReadySlots;

    [Header("RotatingLights")]
    [SerializeField] private UI_RotatingLight[] _rotatingLights;

    [Header("Skill UI")]
    [SerializeField] private Image _qSkillIcon;  // Q 스킬 아이콘 UI
    [SerializeField] private Image _eSkillIcon;  // E 스킬 아이콘 UI

    [Header("Ultimate Visuals")]
    [SerializeField] private Image _ultimateFillImage;   // 게이지 채워지는 이미지
    [SerializeField] private Image _ultimateGlowImage;   // 뒤에 일렁이는 이펙트 이미지

    private PartyManager _partyManager;

    int _schoolIdx;

    // 공통 회전 속도 (초당 180도)
    private float _rotationSpeed = 180f;
    private float _currentAngle = 0f;

    void Start()
    {
        _schoolIdx = Managers.Context.SchoolIdx;

        SetNames();
        SetPortraits();

        if (_partyManager != null)
        {
            // 캐릭터 바뀔 때마다 UpdateActiveCharacterUI 실행
            _partyManager.OnCharacterChanged += UpdateActiveCharacterUI;

            // 게임 시작 시 0번 캐릭터 기준으로 초기화
            UpdateActiveCharacterUI(0);
        }

        ConnectCharacterEvents();
    }

    void Update()
    {
        // 1. 공통 각도 계산 (0 ~ 360도 계속 회전)
        _currentAngle -= _rotationSpeed * Time.deltaTime; // 반시계 방향
        _currentAngle %= 360f;

        // 2. 켜져 있는 모든 슬롯에 동기화된 각도 전파
        if (_rotatingLights != null)
        {
            foreach (var light in _rotatingLights)
            {
                // 꺼져있는 애는 굳이 돌릴 필요 없지만, 켜질 때 튀는 게 싫다면 다 돌려도 됨
                if (light != null && light.gameObject.activeInHierarchy)
                {
                    light.SyncRotation(_currentAngle);
                }
            }
        }
    }

    public void SetPartyManager(PartyManager partyManager)
    {
        _partyManager = partyManager;
    }

    void SetNames()
    {
        for(int i=0; i<4; i++)
        {
            TextMeshProUGUI text = _characterNameSlots[i];

            //_schoolDatas[i].schoolNameKR[i]
            text.SetText(_schoolDatas[_schoolIdx].characters[i].nameKR);
            //text.SetText(charNamesKR[_schoolIdx][i]);
        }
    }

    void SetPortraits()
    {
        for (int i = 0; i < 4; i++)
        {
            Image image = _portraitSlots[i];

            Sprite emblem = Managers.Context.SelectedSchool.characters[i].Portrait;

            if(emblem != null ) image.sprite = emblem;
        }
    }

    public void SetUltimatePanelOn()
    {
        for(int i=0; i<4; i++)
        {
            _ultimateReadySlots[i].SetActive(true);
        }
    }

    void ConnectCharacterEvents()
    {
        List<BaseCharacter> members = _partyManager.PartyMembers;

        foreach (BaseCharacter member in members)
        {
            if(member.Stat == null)
            {
                Debug.Log($"{member.name}에 Stat이 없어요");
                continue;
            }
        }

        for (int i = 0; i < members.Count; i++)
        {
            if (i >= 4) break; // UI 슬롯보다 많으면 무시

            BaseCharacter character = members[i];

            // ★ C# 람다식으로 이벤트 연결 (Closure 문제 주의 -> index 복사)
            int slotIndex = i;

            character.Stat.OnEnergyChanged += (cur, max) =>
            {
                UpdateUltimateUI(slotIndex, cur, max);
            };
        }
    }

    // 실제 UI 갱신 로직
    void UpdateUltimateUI(int slotIndex, float currentEnergy, float maxEnergy)
    {
        bool isReady = currentEnergy >= maxEnergy;

        //Debug.Log($"현재 에너지 : {currentEnergy} , 최대 에너지 : {maxEnergy}");

        // 이미 켜져있는데 또 켜라고 하면 무시 (성능 최적화)
        if (_ultimateReadySlots[slotIndex].activeSelf != isReady)
        {
            _ultimateReadySlots[slotIndex].SetActive(isReady);

            if (isReady) Debug.Log($"Slot {slotIndex} 궁극기 준비 완료!");
        }
    }

    void UpdateActiveCharacterUI(int charIndex)
    {
        // 1. 현재 학교의 캐릭터 데이터 가져오기
        CharacterDataSO charData = Managers.Context.SelectedSchool.characters[charIndex];

        if (charData == null) return;

        // 2. 스킬 아이콘 교체
        if (_qSkillIcon != null) _qSkillIcon.sprite = charData.qSkillIcon;
        if (_eSkillIcon != null) _eSkillIcon.sprite = charData.eSkillIcon;
        // if (_ultimateIcon != null) _ultimateIcon.sprite = charData.ultimateIcon;

        // 3. 궁극기 UI 색상 교체 (SO에 색상 필드가 있다고 가정)
        // (CharacterDataSO에 public Color energyFillColor, ultimateGlowColor가 있어야 함)

        if (_ultimateFillImage != null)
            _ultimateFillImage.color = charData.energyFillColor;

        if (_ultimateGlowImage != null)
            _ultimateGlowImage.color = charData.ultimateGlowColor;

        Debug.Log($"[UI] {charData.nameKR} UI로 갱신 완료");
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (_partyManager != null)
            _partyManager.OnCharacterChanged -= UpdateActiveCharacterUI;
    }
}
