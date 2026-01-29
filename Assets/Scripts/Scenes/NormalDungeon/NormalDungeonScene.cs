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
    }

    // Update is called once per frame
    void Update()
    {
        
    }



    void CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };

        GameObject map = Object.Instantiate(_preloadData.normalDungeon);
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
        Managers.Pool.CreatePool(_preloadData.monsterAR, 20);
    }
   

    void SpawnCharacter()
    {
        if(PartyManager.Instance != null)
        {

        }
    }
}
