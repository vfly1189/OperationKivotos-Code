using System.Collections.Generic;
using UnityEngine;

public class GameSessionContext
{
    // 원본 에셋(SO) 참조. (이건 SO를 '읽기 전용'으로 가리키기만 하므로 OK)
    public SchoolDataSO SelectedSchool { get; set; }
    public List<CharacterDataSO> SelectedCharacters { get; set; }

    // 비동기 로딩된 3개 학교의 세이브 데이터 원본 (사전 로딩용 바구니)
    public Dictionary<string, PartySaveData> SavedDatas { get; private set; } = new Dictionary<string, PartySaveData>();

    // 현재 플레이 중인 학교의 세이브 데이터
    public PartySaveData SelectedSavedData { get; private set; }

    public bool ShouldLoadSaveData { get; set; } = true;
    public int SchoolIdx { get; set; }

    // 현재 플레이 중인 던전 정보
    public Define.DungeonDifficulty SelectedDifficulty { get; set; } = Define.DungeonDifficulty.Easy;
    public int CurrentDungeonID { get; set; }
    public int CurrentDungeonGroupID { get; set; }

    // StartScene에서 호출하여 미리 로딩된 데이터를 보관
    public void SetPreloadedSaveData(string schoolName, PartySaveData data)
    {
        SavedDatas[schoolName] = data;
    }

    

    // 이어하기 (Continue)
    public void LoadSchool(string schoolName)
    {
        if (SavedDatas.TryGetValue(schoolName, out var data))
        {
            if (data == null)
            {
                // 세이브 파일이 아예 없다면 새로 생성
                SelectedSavedData = Managers.Save.CreateNewSave(schoolName);
                Debug.Log($"[{schoolName}] 세이브가 없어 새로 생성 후 이어합니다.");
            }
            else
            {
                // 기존 데이터 딥카피
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                SelectedSavedData = Newtonsoft.Json.JsonConvert.DeserializeObject<PartySaveData>(json);
                Debug.Log($"[{schoolName}] 기존 세이브 데이터를 불러왔습니다.");
            }
        }
        else
        {
            Debug.LogError($"{schoolName} 키가 없습니다!");
        }
    }

    // 새로하기 (New Start)
    public void CreateNewSchool(string schoolName)
    {
        // 기존 세이브 파일(디스크) 삭제
        Managers.Save.DeleteSave(schoolName);

        // 완전히 깨끗한 새 세이브 데이터 생성하여 세션에 장착
        SelectedSavedData = Managers.Save.CreateNewSave(schoolName);

        // 캐싱된 딕셔너리 데이터도 null로 초기화 (StartScene으로 돌아갔을 때 방지)
        SavedDatas[schoolName] = null;

        Debug.Log($"[{schoolName}] 기존 데이터를 삭제하고 새로 시작합니다.");
    }

    // 게임 시작 시, 혹은 로비로 나갈 때 휘발성 데이터 싹 비우기
    public void Clear()
    {
        Debug.Log("[GameSession] 런타임 데이터 초기화");
        SelectedSchool = null;
        SelectedCharacters = null;
        SelectedSavedData = null;
        CurrentDungeonID = 0;
        CurrentDungeonGroupID = 0;
        ShouldLoadSaveData = true;
    }
}