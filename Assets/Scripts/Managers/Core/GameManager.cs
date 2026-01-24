using UnityEngine;

public class GameManager
{
    // 게임 전반에 걸쳐 유지되어야 할 데이터들
    public int _selectedSchoolIndex { get; set; } = 0;

    // 필요하다면 캐릭터 데이터 전체를 넘길 수도 있음
    // public CharacterData SelectedCharacter { get; set; }

    public void Init()
    {
        // 초기화 로직
    }

    public void Clear()
    {
        // 게임이 끝나고 로비로 돌아갈 때 초기화
        _selectedSchoolIndex = 0;
    }
}
