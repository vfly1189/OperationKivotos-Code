using System.Collections.Generic;
using UnityEngine;

public class GameScene : BaseScene
{
    // 씬이 관리하는 핵심 컨트롤러들
    private PlayerController _playerController;
    [SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결
    [SerializeField] private GameScenePreloadSO _preloadData;

    //
    GameObject _map;

    protected override void Init()
    {
        base.Init();

        _sceneType = Define.Scene.Game;

        
        _playerController = Managers.Party.PlayerController;

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
        // 이미 존재하는지 확인
        GameObject existingUI = GameObject.Find("@GameSceneCanvas");
        if (existingUI != null)
        {
            existingUI.SetActive(true); // ← 추가: 던전에서 숨긴 경우 다시 활성화
            GameSceneCanvas canvas = existingUI.GetComponent<GameSceneCanvas>();
            canvas.SetPartyManager();
            return;
        }

        // 없으면 새로 생성
        GameObject mainUI = Object.Instantiate(_preloadData.gameSceneCanvas);
        mainUI.name = "@GameSceneCanvas";
        DontDestroyOnLoad(mainUI); // DDOL 적용

        GameSceneCanvas newCanvas = mainUI.GetComponent<GameSceneCanvas>();
        if (newCanvas != null)
        {
            newCanvas.SetPartyManager();
        }
    }

    void CreateMainVillage()
    {
        _map = Object.Instantiate(_preloadData.mainVillage);
        if (_map != null) _map.name = "@Map";

        _map.gameObject.transform.position = Vector3.zero;
    }

    void CreateCharacters()
    {
        Managers.Party.ClearParty();

        CharacterDataSO[] charactersToSpawn = Managers.Context.SelectedSchool.characters;
        List<BaseCharacter> partyMembers = new List<BaseCharacter>();

        // [핵심] PartyManager의 전용 컨테이너 사용
        Transform partyContainer = Managers.Party.GetCharacterContainer();

        for (int i = 0; i < charactersToSpawn.Length; i++)
        {
            GameObject go = Object.Instantiate(charactersToSpawn[i].inGamePrefab);
            BaseCharacter character = go.GetComponent<BaseCharacter>();
            character.Init();

            CharacterRuntimeData savedData = Managers.Context.LoadCharacterStat(charactersToSpawn[i].id);
            if (savedData != null)
            {
                character.Stat.ApplyRuntimeData(savedData);
            }

            partyMembers.Add(character);

            // 파티 전용 컨테이너의 자식으로 설정
            go.transform.SetParent(partyContainer);
        }

        Managers.Party.Init(partyMembers);
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


    public override void Clear()
    {
        base.Clear();
        Managers.Sound.StopAll();
    }
}
