using System.Collections.Generic;
using UnityEngine;

public class GameScene : BaseScene
{
    public static readonly string[] REQUIRED_RESOURCES = new string[]
    {
        "Prefabs/Map/MainVillage",
        "Images/Character_Emblem/Emblem_Icon_Favor_Ako",
        "Images/Character_Emblem/Emblem_Icon_Favor_Aris",
        "Images/Character_Emblem/Emblem_Icon_Favor_Asuna",
        "Images/Character_Emblem/Emblem_Icon_Favor_Karin",
        "Images/Character_Emblem/Emblem_Icon_Favor_Aru",
        "Images/Character_Emblem/Emblem_Icon_Favor_Hina",
        "Images/Character_Emblem/Emblem_Icon_Favor_Hoshino",
        "Images/Character_Emblem/Emblem_Icon_Favor_Iori",
        "Images/Character_Emblem/Emblem_Icon_Favor_Nonomi",
        "Images/Character_Emblem/Emblem_Icon_Favor_Shiroko",
        "Images/Character_Emblem/Emblem_Icon_Favor_Serika",
        "Images/Character_Emblem/Emblem_Icon_Favor_Toki"
    };
    // 씬이 관리하는 핵심 컨트롤러들
    private PartyManager _partyManager;
    private PlayerController _playerController;


    protected override void Init()
    {
        base.Init();

        _sceneType = Define.Scene.Game;

        // 1. 관리자용 GameObject 생성 (Hierarchy 정리용)
        GameObject go = new GameObject("@GameSystem");

        // 2. 컴포넌트 부착
        _partyManager = go.AddComponent<PartyManager>();
        _playerController = go.AddComponent<PlayerController>();

        // 3. 서로 연결 (Dependency Injection)
        _partyManager._playerController = _playerController;

        // 이미 로딩된 리소스들을 배치
        CreateMainVillage();
        CreateCharacters();


        GameObject mainUI = Managers.Resource.Instantiate("UI/GameScene/GameSceneCanvas");
        mainUI.name = "@GameSceneCanvas";
    }

    protected override string[] GetRequiredResources()
    {
        return REQUIRED_RESOURCES;
    }

    void CreateMainVillage()
    {
        // ResourceManager 캐시에 "Prefabs/Map/MainVillage"가 있으므로 즉시 생성됨
        GameObject map = Managers.Resource.Instantiate("Map/MainVillage");
        if (map != null) map.name = "Map";
    }

    void CreateCharacters()
    {
        // GameManager에서 선택 정보 가져오기
        int schoolIdx = Managers.Game._selectedSchoolIndex;

        // (데이터 매핑 - SelectSceneCanvas와 동일하게 사용)
        string[] schoolNames = new string[] { "Abydos", "Gehenna", "Millennium" };
        string[][] charNames = new string[][]
        {
            new string[] { "Hoshino", "Nonomi", "Shiroko", "Serika" },
            new string[] { "Aru", "Hina", "Ako", "Iori" },
            new string[] { "Toki", "Karin", "Asuna", "Aris" }
        };

        string schoolName = schoolNames[schoolIdx];
        string[] students = charNames[schoolIdx];

        // 스폰 포인트 (임시)
        Vector3 spawnStartPos = new Vector3(0, 0, 0);

        List<BaseCharacter> partyMembers = new List<BaseCharacter>();

        for (int i = 0; i < students.Length; i++)
        {
            // SelectSceneCanvas에서 로딩 요청했던 경로와 일치해야 함 ("Prefabs/" 제외하고 호출)
            string path = $"Characters/{schoolName}/{students[i]}_InGame";

            // 캐시에서 즉시 꺼냄
            GameObject go = Managers.Resource.Instantiate(path);

            if (go != null)
            {
                // 생성된 오브젝트에서 BaseCharacter 컴포넌트 추출
                // (NonomiCharacter 같은 자식 클래스도 BaseCharacter로 받아짐)
                BaseCharacter character = go.GetComponent<BaseCharacter>();

                if (character != null)
                {
                    character.Init();
                    // 리스트에 추가
                    partyMembers.Add(character);

                    // 초기 위치 설정 (일단 모두 같은 곳에 두거나, 안 보이는 곳에 둠)
                    // 어차피 PartyManager.Init()에서 1번만 남기고 나머지는 비활성화 시킬 것임
                    go.transform.position = spawnStartPos;
                }
                else
                {
                    Debug.LogError($"프리팹 {students[i]}에 BaseCharacter 스크립트가 없습니다!");
                }
            }
            else
            {
                Debug.LogError($"캐릭터 생성 실패: {path}");
            }

            //Camera.main.GetComponent<CameraController>()._player = go;

            //if (go != null)
            //{
            //    go.transform.position = spawnStartPos + new Vector3(i * 1.5f, 0, 0);
            //}
        }
        if (_partyManager != null)
        {
            _partyManager.Init(partyMembers);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
