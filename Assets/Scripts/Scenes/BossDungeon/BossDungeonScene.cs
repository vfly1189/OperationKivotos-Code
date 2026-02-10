using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossDungeonScene : BaseScene
{
    [SerializeField] private BossDungeonScenePreloadSO _preloadData;

    private GameObject _boss;
    private GameObject _curMap;
    private GameObject _mainUI;
    private GameObject _bossHPBar;

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


        _spawnPoint = _preloadData.bossDungeon.GetComponent<BossDungeonMap>().GetCharacterSpawnPoint();
        _cameraPoint = _preloadData.bossDungeon.GetComponent<BossDungeonMap>().GetCameraPoint();
        _bossSpawnPoint = _preloadData.bossDungeon.GetComponent<BossDungeonMap>().GetBossSpawnPoint();

        CreateMap();
        CreateUI();
        PlayBGM();
        PlayBattleInVoice();
        CreatePool();
        CreateEffectStage();
        CreateBoss();
        CreateBossHPBarUI();
       
        Managers.Party.TeleportParty(_spawnPoint.position);
        Camera.main.transform.position = _cameraPoint.position;
        Camera.main.transform.rotation = _cameraPoint.rotation;
    }

    void CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };

        GameObject map = Object.Instantiate(_preloadData.bossDungeon);
        map.transform.SetParent(root.transform);

        map.transform.position = new Vector3(0, 0, 0);

        _curMap = map;
    }

    void CreateUI()
    {
        //GameObject mainUI = Object.Instantiate(_preloadData.gameSceneCanvas);
        //mainUI.name = "@GameSceneCanvas";

        //GameSceneCanvas canvas = mainUI.GetComponent<GameSceneCanvas>();
        //if (canvas != null)
        //    canvas.SetPartyManager(); // 싱글톤 매니저 연결

        GameObject mainUI = GameObject.Find("@GameSceneCanvas");
        //mainUI.name = "@GameSceneCanvas";
        _mainUI = mainUI;
    }

    void PlayBGM()
    {
        Managers.Sound.Play(_preloadData.fightingBgms[0], Define.Sound.Bgm);
    }

    void PlayBattleInVoice()
    {
        List<BaseCharacter> partyMemebers = Managers.Party.GetMemeber();

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
        Managers.Pool.CreatePool(_preloadData.monsterRL, 10);
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

    void CreateBoss()
    {
        GameObject root = new GameObject { name = "@BossMonster" };
        root.transform.position = _bossSpawnPoint.transform.position;

        if (_preloadData.boss != null)
        {
            GameObject boss = Object.Instantiate(_preloadData.boss, root.transform);
            _boss = boss;
            _boss.GetComponent<BossMonsterController>().OnDead -= OnMonsterDead;
            _boss.GetComponent<BossMonsterController>().OnDead += OnMonsterDead;
            _boss.GetComponent<BossSkillController>().SetSpawnPoints(_curMap.GetComponent<BossDungeonMap>().GetMonsterSpawnPoints());
            _boss.GetComponent<BossSkillController>().SetLightningPoints(_curMap.GetComponent<BossDungeonMap>().GetLightningPoints());

        }
    }

    void CreateBossHPBarUI()
    {
        GameObject root = new GameObject { name = "@HPBarUI" };
        
        if(_preloadData.bossHPBar != null)
        {
            GameObject hpBarUI = Object.Instantiate(_preloadData.bossHPBar, root.transform);
            _bossHPBar = hpBarUI;
            hpBarUI.GetComponent<BossHPBar>().SetBoss(_boss);
        }
    }

    void PlayVictoryVoice()
    {
        List<BaseCharacter> partyMemebers = Managers.Party.GetMemeber();

        int randomNum_partyMembers = Random.Range(0, 4);
        AudioClip[] voices = partyMemebers[randomNum_partyMembers].Stat.GetBattleVictoryVoices();

        int randomNum_voice = Random.Range(0, 2);
        Debug.Log($"번호 : {randomNum_partyMembers} , {randomNum_voice}");
        Managers.Sound.Play(voices[randomNum_voice], Define.Sound.Effect);
    }


    private void OnMonsterDead()
    {
        // 1. Party Time BGM 재생 (선택)
        Managers.Sound.Play(_preloadData.successBgm, Define.Sound.Bgm); // 실제 BGM 이름이나 AudioClip 필요

        // 2. PartyManager에게 승리 통보 (이게 핵심)
        Managers.Party.FinishGame(true); // true = Success
        Managers.Destroy(_bossHPBar);
        StartCoroutine(CoVictoryPoze());     
    }



    private System.Collections.IEnumerator CoVictoryPoze()
    {
        Managers.Party.PlayerController.VictoryTime = true; // 승리화면에서는 공격(좌클릭) 불가능하게
        yield return new WaitForSeconds(3.0f);

        PlayVictoryVoice();

        if (_mainUI != null)
        {
            _mainUI.SetActive(false); // 파괴 대신 비활성화
        }

        // [중요] 일괄 활성화 함수(TurnOnAllMembers) 대신, 아래 루프에서 하나씩 정교하게 제어하는 것을 추천
        // PartyManager.Instance.TurnOnAllMembers(); (삭제)

        GameObject clearUI = Object.Instantiate(_preloadData.dungeonClearUI);

        // 1. 카메라 준비
        GameObject camObj = _curMap.GetComponent<BossDungeonMap>().GetEndingCameraPoint().gameObject;
        camObj.SetActive(true);
        // 카메라 줌 시작 (동시에)
        StartCoroutine(CoCameraZoomEffect(camObj.transform));

        Transform[] endingPositions = _curMap.GetComponent<BossDungeonMap>().GetEndingPoints();
        List<BaseCharacter> characters = Managers.Party.GetMemeber();

        int index = 0;
        foreach (BaseCharacter character in characters)
        {
            // [단계 1] 활성화 먼저 (스크립트가 동작해야 애니메이션 등 변경 가능)
            // 단, NavMeshAgent가 켜져있으면 위치 이동을 막거나 튕겨낼 수 있으니 주의
            character.gameObject.SetActive(true);

            // [단계 2] 물리/AI 무력화 (이동 방지 및 위치 고정)
            // (BaseCharacter에 StopMove나 DisableAI 같은 함수가 있다면 활용)
            var agent = character.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false; // [핵심] NavMesh가 위치 강제 보정하는 것 차단
            }
            var rb = character.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // [핵심] 물리 충돌로 밀려나는 것 차단
            }

            // [단계 3] 위치 및 회전 강제 지정
            if (index < endingPositions.Length)
            {
                character.transform.position = endingPositions[index].position;

                // 회전
                Vector3 targetPos = camObj.transform.position;
                targetPos.y = character.transform.position.y;
                Vector3 dir = targetPos - character.transform.position;

                if (dir != Vector3.zero)
                    character.transform.rotation = Quaternion.LookRotation(dir);

                index++;
            }

            // [단계 4] 승리 포즈 애니메이션 재생
            //character.ChangeState(BaseCharacter.PlayerState.Victory); //변경전
            character.Victory();
        }
    }

    private IEnumerator CoCameraZoomEffect(Transform camTr)
    {
        float duration = 4.0f;
        float timer = 0f;
        Vector3 startPos = camTr.position;
        Vector3 targetPos = startPos + (camTr.forward * 2.0f);

        while (timer < duration)
        {
            timer += Time.deltaTime;

            // 0 ~ 1 사이의 선형 진행도
            float t = timer / duration;

            // [핵심] 3차 함수(Cubic) 공식을 이용한 Ease In Out 변환
            // t가 0.5보다 작을 땐 4*t^3 (서서히 가속)
            // t가 0.5보다 클 땐 뒤집어서 감속
            float easeT = (t < 0.5f)
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            camTr.position = Vector3.Lerp(startPos, targetPos, easeT);
            yield return null;
        }

        // 최종 위치 보정
        camTr.position = targetPos;
    }
}
