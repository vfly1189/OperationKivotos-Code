using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameSceneCanvas : MonoBehaviour
{
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

    int _schoolIdx;
    // (데이터 매핑 - SelectSceneCanvas와 동일하게 사용)
    string[] schoolNames = new string[] { "Abydos", "Gehenna", "Millennium" };
    string[][] charNamesEN = new string[][]
    {
            new string[] { "Hoshino", "Nonomi", "Shiroko", "Serika" },
            new string[] { "Aru", "Hina", "Ako", "Iori" },
            new string[] { "Toki", "Karin", "Asuna", "Aris" }
    };
    string[][] charNamesKR = new string[][]
    {
            new string[] { "호시노", "노노미", "시로코", "세리카" },
            new string[] { "아루", "히나", "아코", "이오리" },
            new string[] { "토키", "카린", "아스나", "아리스" }
    };

    // 공통 회전 속도 (초당 180도)
    private float _rotationSpeed = 180f;
    private float _currentAngle = 0f;

    void Start()
    {
        _schoolIdx = Managers.Game._selectedSchoolIndex;

        SetNames();
        SetPortraits();
     

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

    void SetNames()
    {
        for(int i=0; i<4; i++)
        {
            TextMeshProUGUI text = _characterNameSlots[i];

            text.SetText(charNamesKR[_schoolIdx][i]);
        }
    }

    void SetPortraits()
    {
        for (int i = 0; i < 4; i++)
        {
            Image image = _portraitSlots[i];

            string path = $"Images/Character_Emblem/Emblem_Icon_Favor_{charNamesEN[_schoolIdx][i]}";
            //Debug.Log($"경로 : {path}");
            Sprite emblem = Managers.Resource.Load<Sprite>(path);

            if(emblem != null ) image.sprite = emblem;
        }
    }

    void SetUltimatePanelOn()
    {
        for(int i=0; i<4; i++)
        {
            _ultimateReadySlots[i].SetActive(true);
        }
    }

    void ConnectCharacterEvents()
    {
        // PartyManager에서 현재 멤버 리스트 가져오기
        // (Managers.Game.CurrentParty나 Scene 내 PartyManager 참조 필요)
        var partyManager = FindAnyObjectByType<PartyManager>();
        if (partyManager == null)
            return;


        List<BaseCharacter> members = partyManager.PartyMembers;

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
}
