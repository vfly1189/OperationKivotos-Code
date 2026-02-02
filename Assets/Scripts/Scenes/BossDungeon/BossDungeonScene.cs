using System.Collections.Generic;
using UnityEngine;

public class BossDungeonScene : BaseScene
{
    [SerializeField] private BossDungeonScenePreloadSO _preloadData;

    private Transform _spawnPoint;
    private Transform _cameraPoint;
    private Transform _bossSpawnPoint;

    void Start()
    {

    }

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.BossDungeon;

        Debug.Log("Boss Dungeon Init 호출");


        Camera.main.GetComponent<CameraController>().SetTarget(PartyManager.Instance.GetCurrentCharacter().gameObject);


        CreateMap();
        CreateUI();
        PlayBGM();
        //PlayBattleInVoice();
        //CreatePool();
        CreateEffectStage();

        _spawnPoint = _preloadData.bossDungeon.GetComponent<BossDungeonMap>().GetCharacterSpawnPoint();
        _cameraPoint = _preloadData.bossDungeon.GetComponent<BossDungeonMap>().GetCameraPoint();
        _bossSpawnPoint = _preloadData.bossDungeon.GetComponent<BossDungeonMap>().GetBossSpawnPoint();

        PartyManager.Instance.TeleportParty(_spawnPoint.position);
        Camera.main.transform.position = _cameraPoint.position;
        Camera.main.transform.rotation = _cameraPoint.rotation;
    }

    // Update is called once per frame
    void Update()
    {

    }



    void CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };

        GameObject map = Object.Instantiate(_preloadData.bossDungeon);
        map.transform.SetParent(root.transform);

        map.transform.position = new Vector3(0, 0, 0);
    }

    void CreateUI()
    {
        GameObject mainUI = Object.Instantiate(_preloadData.gameSceneCanvas);
        mainUI.name = "@GameSceneCanvas";

        GameSceneCanvas canvas = mainUI.GetComponent<GameSceneCanvas>();
        if (canvas != null)
            canvas.SetPartyManager(PartyManager.Instance); // 싱글톤 매니저 연결
    }

    void PlayBGM()
    {
        Managers.Sound.Play(_preloadData.fightingBgms[0], Define.Sound.Bgm);
    }

    //void PlayBattleInVoice()
    //{
    //    List<BaseCharacter> partyMemebers = PartyManager.Instance.PartyMembers;

    //    int randomNum_partyMembers = Random.Range(0, 4);
    //    AudioClip[] voices = partyMemebers[randomNum_partyMembers].Stat.GetBattleInVoice();

    //    int randomNum_voice = Random.Range(0, 2);
    //    Debug.Log($"번호 : {randomNum_partyMembers} , {randomNum_voice}");
    //    Managers.Sound.Play(voices[randomNum_voice], Define.Sound.Effect);
    //}

    //void CreatePool()
    //{
    //    //총알
    //    Managers.Pool.CreatePool(_preloadData.bullet, 60);
    //    //몬스터
    //    Managers.Pool.CreatePool(_preloadData.monsterAR, 20);
    //}

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

    void SpawnCharacter()
    {
        if (PartyManager.Instance != null)
        {

        }
    }
}
