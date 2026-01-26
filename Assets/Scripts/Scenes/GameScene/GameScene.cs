using System.Collections.Generic;
using UnityEngine;

public class GameScene : BaseScene
{
    // 씬이 관리하는 핵심 컨트롤러들
    private PartyManager _partyManager;
    private PlayerController _playerController;
    [SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결
    [SerializeField] private GameScenePreloadSO _preloadData;

    //
    GameObject _map;

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
        CreatePortal();
        CreateShopMaster();

        //GameObject mainUI = Managers.Resource.Instantiate("UI/GameScene/GameSceneCanvas");
        //mainUI.name = "@GameSceneCanvas";


        GameObject mainUI = Object.Instantiate(_preloadData.gameSceneCanvas);
        mainUI.name = "@GameSceneCanvas";

        GameSceneCanvas canvas = mainUI.GetComponent<GameSceneCanvas>();
        if (canvas != null)
            canvas.SetPartyManager(_partyManager);
    }



    void CreateMainVillage()
    {
        _map = Object.Instantiate(_preloadData.mainVillage);
        if (_map != null) _map.name = "@Map";
    }

    void CreateCharacters()
    {
        GameObject root = new GameObject { name = "@Characters" };

        // GameManager에서 선택 정보 가져오기
        int schoolIdx = Managers.Context.SchoolIdx;
        CharacterDataSO[] charactersToSpawn = Managers.Context.SelectedSchool.characters;

        // 스폰 포인트 (임시)
        Vector3 spawnStartPos = new Vector3(0, 0, 0);
        List<BaseCharacter> partyMembers = new List<BaseCharacter>();

        for (int i = 0; i < charactersToSpawn.Length; i++)
        {
            GameObject go = Object.Instantiate(charactersToSpawn[i].inGamePrefab);
            go.transform.SetParent(root.transform);
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
                    //Debug.LogError($"프리팹 {students[i]}에 BaseCharacter 스크립트가 없습니다!");
                }
            }
            else
            {
                // Debug.LogError($"캐릭터 생성 실패: {path}");
            }
        }
        if (_partyManager != null)
        {
            _partyManager.Init(partyMembers);
        }
    }

    void CreatePortal()
    {
        // 1. 포탈 그룹 생성 (Map 밑에)
        GameObject portalGroup = new GameObject("Portals");

        if (_map != null)
            portalGroup.transform.SetParent(_map.transform);

        // 2. 포탈 생성 (Instantiate의 2번째 인자로 부모 지정)

        // 노말 던전 포탈
        if (_preloadData.normalDungeonPortal != null)
        {
            GameObject normalPortal = Object.Instantiate(_preloadData.normalDungeonPortal, portalGroup.transform);
            // 위치를 따로 잡고 싶다면 여기서 수정 (예: portalGroup 기준 상대 좌표)
            normalPortal.transform.localPosition = new Vector3(-3, 1, 0);
        }

        // 보스 던전 포탈
        if (_preloadData.bossDungeonPortal != null)
        {
            GameObject bossPortal = Object.Instantiate(_preloadData.bossDungeonPortal, portalGroup.transform);
            // 위치 수정
            bossPortal.transform.localPosition = new Vector3(3, 1, 0);
        }
    }

    void CreateShopMaster()
    {
        GameObject root = new GameObject { name = "@Shop" };

        if (_preloadData.shopMaster != null)
        {
            GameObject shopMaster = Object.Instantiate(_preloadData.shopMaster, root.transform);
            // 위치를 따로 잡고 싶다면 여기서 수정 (예: portalGroup 기준 상대 좌표)
            shopMaster.transform.localPosition = new Vector3(0, 0, 2);
            shopMaster.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
