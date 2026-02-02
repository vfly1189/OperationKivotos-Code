using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class NormalDungeonScene : BaseScene
{

    [SerializeField] private NormalDungeonScenePreloadSO _preloadData;

    void Start()
    {

    }

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.NormalDungeon;

        Debug.Log("Normal Dungeon Init 호출");


        Camera.main.GetComponent<CameraController>().SetTarget(PartyManager.Instance.GetCurrentCharacter().gameObject);


        CreateMap();
        CreateUI();
        PlayBGM();
        PlayBattleInVoice();
        CreatePool();
        CreateEffectStage();
    }

    // Update is called once per frame
    void Update()
    {
        
    }



    void CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };
        GameObject mapPrefab = null;

        // Enum에 따라 프리팹 선택
        switch (Managers.Game.SelectedDifficulty)
        {
            case DungeonDifficulty.Easy:
                mapPrefab = _preloadData.normalDungeonEasy;
                break;
            case DungeonDifficulty.Normal:
                mapPrefab = _preloadData.normalDungeonNormal;
                break;
            case DungeonDifficulty.Hard:
                mapPrefab = _preloadData.normalDungeonHard;
                break;
        }

        if (mapPrefab != null)
        {
            GameObject map = Object.Instantiate(mapPrefab, root.transform);
            map.transform.position = Vector3.zero;
        }
        else
        {
            Debug.LogError("맵 프리팹이 할당되지 않았습니다!");
        }
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
        int randNum = Random.Range(0, 2);

        Managers.Sound.Play(_preloadData.fightingBgms[randNum], Define.Sound.Bgm);
    }

    void PlayBattleInVoice()
    {
        List<BaseCharacter> partyMemebers = PartyManager.Instance.PartyMembers;

        int randomNum_partyMembers = Random.Range(0, 4);
        AudioClip[] voices = partyMemebers[randomNum_partyMembers].Stat.GetBattleInVoice();

        int randomNum_voice = Random.Range(0, 2);
        Debug.Log($"번호 : {randomNum_partyMembers} , {randomNum_voice}");
        Managers.Sound.Play(voices[randomNum_voice], Define.Sound.Effect);
    }

    void CreatePool()
    {
        //총알
        Managers.Pool.CreatePool(_preloadData.bullet, 60);
        //몬스터
        //Managers.Pool.CreatePool(_preloadData.monsterAR, 20);
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

    void SpawnCharacter()
    {
        if(PartyManager.Instance != null)
        {

        }
    }
}
