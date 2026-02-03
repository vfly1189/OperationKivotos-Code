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

        if (PartyManager.Instance == null)
        {
            // 최초 생성 (게임 처음 시작 시)
            GameObject go = new GameObject("@PartyManager");
            go.transform.position = new Vector3(0, 0, 0);
            _partyManager = go.AddComponent<PartyManager>();

            // PlayerController도 같이 붙여서 평생 함께 가게 함
            _playerController = go.AddComponent<PlayerController>();
            _partyManager._playerController = _playerController;

            // 파티 멤버 최초 생성 및 초기화
            //CreateCharacters();
        }
        else
        {
            // 이미 생성된 매니저 사용 (던전 갔다 돌아왔을 때 등)
            _partyManager = PartyManager.Instance;
            _playerController = _partyManager._playerController;

            // ★ 중요: 파티 멤버들이 비활성화되어 있을 수 있으므로 위치 잡고 활성화 처리 등 필요
            // (CreateCharacters는 호출하지 않음 - 이미 멤버가 있으니까)
            // 예를 들어 마을 스폰 포인트로 이동
            _partyManager.TeleportParty(new Vector3(0, 0, 0));
        }


        // 이미 로딩된 리소스들을 배치
        CreateMainVillage();
        CreateCharacters();
        CreatePortal();
        CreateShopMaster();
        CreateEffectStage();
        PlayMainBGM();


        SetupUI();
    }

    void SetupUI()
    {
        // UI도 씬마다 새로 만들 것인지, DDOL로 유지할 것인지 결정 필요.
        // 여기서는 "GameSceneCanvas는 씬마다 새로 만든다"고 가정 (가장 쉬운 접근)
        GameObject mainUI = Object.Instantiate(_preloadData.gameSceneCanvas);
        mainUI.name = "@GameSceneCanvas";

        GameSceneCanvas canvas = mainUI.GetComponent<GameSceneCanvas>();
        if (canvas != null)
            canvas.SetPartyManager(_partyManager); // 싱글톤 매니저 연결
    }

    void CreateMainVillage()
    {
        _map = Object.Instantiate(_preloadData.mainVillage);
        if (_map != null) _map.name = "@Map";

        _map.gameObject.transform.position = Vector3.zero;
    }

    void CreateCharacters()
    {
        // [추가] 기존 파티 멤버가 있다면 싹 다 삭제 (Destroy)
        _partyManager.ClearParty();

        // ... 기존 생성 로직 그대로 ...
        CharacterDataSO[] charactersToSpawn = Managers.Context.SelectedSchool.characters;
        List<BaseCharacter> partyMembers = new List<BaseCharacter>();

        for (int i = 0; i < charactersToSpawn.Length; i++)
        {
            GameObject go = Object.Instantiate(charactersToSpawn[i].inGamePrefab);
            BaseCharacter character = go.GetComponent<BaseCharacter>();
            character.Init();

            // [핵심 추가] 저장된 성장 데이터(레벨, 경험치)가 있으면 복구해라!
            CharacterRuntimeData savedData = Managers.Context.LoadCharacterStat(charactersToSpawn[i].id);
            if (savedData != null)
            {
                character.Stat.ApplyRuntimeData(savedData);
            }

            partyMembers.Add(character);
            go.transform.SetParent(_partyManager.transform);
        }

        // 매니저에게 "새 멤버들이다. 다시 관리해라"라고 넘김
        _partyManager.Init(partyMembers);
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

    void CreateEffectStage()
    {
        GameObject root = new GameObject { name = "@Effect" };

        if (_preloadData.effectStage != null)
        {
            GameObject effectStage = Object.Instantiate(_preloadData.effectStage, root.transform);
            // 위치를 따로 잡고 싶다면 여기서 수정 (예: portalGroup 기준 상대 좌표)
            effectStage.SetActive(true);
        }
    }

    void PlayMainBGM()
    {
        GameObject root = new GameObject { name = "@BGM" };

        if (_preloadData.mainBGMs != null)
        {
            int rand = Random.Range(0, 2);

            Managers.Sound.Play(_preloadData.mainBGMs[rand], Define.Sound.Bgm);
        }
    }


    // Update is called once per frame
    void Update()
    {
        
    }

    public override void Clear()
    {
        base.Clear();
        Managers.Sound.StopAll();
    }
}
