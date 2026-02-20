using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Context/CurrentGameData")]
public class CurrentGameDataSO : ScriptableObject
{
    // 이전 씬(SelectScene)에서 선택한 정보가 여기에 저장됨
    [Header("Runtime Data (Do not edit manually)")]
    public SchoolDataSO _selectedSchool; // 선택된 학교
    public List<CharacterDataSO> SelectedCharacters; // 선택된 캐릭터들 (필요하다면)
    public int SchoolIdx;
    public int textidx;

    // 캐릭터 ID를 키로 사용하여 런타임 데이터 저장
    public Dictionary<int, CharacterRuntimeData> SavedCharacterStats = new Dictionary<int, CharacterRuntimeData>();
    public Define.DungeonDifficulty SelectedDifficulty { get; set; } = Define.DungeonDifficulty.Easy;


    public SchoolDataSO SelectedSchool
    {
        get { return _selectedSchool; }
        set
        {
            _selectedSchool = value;
            if (value == null)
            {
                // ★ 여기가 핵심: 누가 null로 바꿨는지 스택 추적 로그 출력
                //Debug.LogError($"[SO] 데이터가 NULL로 초기화됨! 호출 스택:\n{System.Environment.StackTrace}");
            }
            else
            {
                //Debug.Log($"[SO] 데이터 설정됨: {value.name}");
            }
        }
    }

    // 게임 시작 시(혹은 앱 시작 시) 초기화하는 메서드

    public void Clear()
    {
        Debug.Log("[SO] Clear() 호출됨");
        SelectedSchool = null;
        // SelectedCharacters.Clear();
    }

    // 유니티 생명주기함수 체크
    private void OnDisable()
    {
        Debug.Log("[SO] OnDisable 호출됨 (씬 전환 시 호출될 수 있음)");
        // 여기서 초기화하면 안 됨!
    }


    // 던전 진입 전이나, 레벨업 할 때마다 저장
    public void SaveCharacterStat(int charId, int level, float exp, int weaponLevel)
    {
        if (!SavedCharacterStats.ContainsKey(charId))
            SavedCharacterStats[charId] = new CharacterRuntimeData();

        SavedCharacterStats[charId].level = level;
        SavedCharacterStats[charId].currentExp = exp;
        SavedCharacterStats[charId].weaponLevel = weaponLevel;
    }

    // 캐릭터 생성 시 불러오기
    public CharacterRuntimeData LoadCharacterStat(int charId)
    {
        if (SavedCharacterStats.ContainsKey(charId))
            return SavedCharacterStats[charId];
        return null; // 저장된 게 없으면 1레벨
    }
}