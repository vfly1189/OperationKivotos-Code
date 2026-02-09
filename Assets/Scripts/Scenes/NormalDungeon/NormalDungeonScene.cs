using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class NormalDungeonScene : BaseScene
{
    [SerializeField] private NormalDungeonScenePreloadSO _preloadData;

    private int _remainingMonsters = 0;

    private GameObject _curMap;
    private GameObject _mainUI;


    void Start()
    {

    }

    protected override void Init()
    {
        base.Init();
        _sceneType = Define.Scene.NormalDungeon;

        Debug.Log("Normal Dungeon Init 호출");


        //Camera.main.GetComponent<CameraController>().SetTarget(Managers.Party.GetCurrentCharacter().gameObject);

        CreateUI();
        CreateMap();
        
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
        switch (Managers.Context.SelectedDifficulty)
        {
            case Define.DungeonDifficulty.Easy:
                mapPrefab = _preloadData.normalDungeonEasy;
                break;
            case Define.DungeonDifficulty.Normal:
                mapPrefab = _preloadData.normalDungeonNormal;
                break;
            case Define.DungeonDifficulty.Hard:
                mapPrefab = _preloadData.normalDungeonHard;
                break;
        }

        if (mapPrefab != null)
        {
            GameObject map = Object.Instantiate(mapPrefab, root.transform);
            CountAndRegisterMonsters(map);
            _curMap = map;
            map.transform.position = Vector3.zero;
        }
        else
        {
            Debug.LogError("맵 프리팹이 할당되지 않았습니다!");
        }
    }

    void CreateUI()
    {
        //GameObject mainUI = Object.Instantiate(_preloadData.gameSceneCanvas);
        GameObject mainUI = GameObject.Find("@GameSceneCanvas");
        //mainUI.name = "@GameSceneCanvas";
        _mainUI = mainUI;

        //GameSceneCanvas canvas = mainUI.GetComponent<GameSceneCanvas>();
        //if (canvas != null)
        //    canvas.SetPartyManager(PartyManager.Instance); // 싱글톤 매니저 연결

    }

    void PlayBGM()
    {
        int randNum = Random.Range(0, 2);

        Managers.Sound.Play(_preloadData.fightingBgms[randNum], Define.Sound.Bgm);
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

    void PlayVictoryVoice()
    {
        List<BaseCharacter> partyMemebers = Managers.Party.GetMemeber();

        int randomNum_partyMembers = Random.Range(0, 4);
        AudioClip[] voices = partyMemebers[randomNum_partyMembers].Stat.GetBattleVictoryVoices();

        int randomNum_voice = Random.Range(0, 2);
        Debug.Log($"번호 : {randomNum_partyMembers} , {randomNum_voice}");
        Managers.Sound.Play(voices[randomNum_voice], Define.Sound.Effect);
    }

    void CreatePool()
    {
        //총알
        Managers.Pool.CreatePool(_preloadData.bullet, 40);
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

    void CountAndRegisterMonsters(GameObject map)
    {
        // 1. "Monsters" 라는 이름의 부모 오브젝트 찾기
        // (맵 프리팹 구조에 따라 경로가 다를 수 있으니 유의)
        // 가장 확실한 건 Tag를 쓰거나, GameObject.Find를 쓰는 것입니다.
        // 여기서는 생성된 맵(_map) 아래에서 찾는다고 가정합니다.

        // 만약 맵 전체를 뒤져야 한다면:
        GameObject monstersRoot = GameObject.Find("Monsters");

        if (monstersRoot == null)
        {
            Debug.LogWarning("맵에 'Monsters' 오브젝트가 없습니다! 클리어 조건 확인 필요.");
            //DungeonClear(); // 몬스터가 없으면 바로 클리어 처리?
            return;
        }

        // 2. 자식으로 있는 모든 몬스터(MonsterController) 찾기
        // GetComponentsInChildren을 쓰면 비활성화된 놈까지 찾을 수 있음(true 옵션)
        MonsterController[] monsters = monstersRoot.GetComponentsInChildren<MonsterController>(true);

        _remainingMonsters = monsters.Length;
        Debug.Log($"총 몬스터 수: {_remainingMonsters}");

        // 3. 각 몬스터에게 "죽으면 나한테 보고해"라고 이벤트 연결
        foreach (var monster in monsters)
        {
            // MonsterController나 그 Stat에 OnDead 이벤트가 있어야 함
            // 예: monster.Stat.OnDeadEvent += () => OnMonsterDead();

            // 만약 Stat이 Action<BaseCharacter> 형태라면:
            //monster.Stat.OnDead += (deadChar) => OnMonsterDead();
            monster.Stat.OnDead += OnMonsterDead;
        }
    }

    private void OnMonsterDead()
    {
        _remainingMonsters--;
        // ... 클리어 체크 ...
        Debug.Log($"남은 몬스터 수 : {_remainingMonsters}");
        if (_remainingMonsters <= 0)
        {
            Debug.Log("던전 클리어!");

            // 1. Party Time BGM 재생 (선택)
            Managers.Sound.Play(_preloadData.successBgm, Define.Sound.Bgm); // 실제 BGM 이름이나 AudioClip 필요

            // 2. PartyManager에게 승리 통보 (이게 핵심)
            Managers.Party.FinishGame(true); // true = Success
            StartCoroutine(CoVictoryPoze());
        }
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
        GameObject camObj = _curMap.GetComponent<NormalDungeonMap>().GetCameraPoint().gameObject;
        camObj.SetActive(true);
        // 카메라 줌 시작 (동시에)
        StartCoroutine(CoCameraZoomEffect(camObj.transform));

        Transform[] endingPositions = _curMap.GetComponent<NormalDungeonMap>().GetTransforms();
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