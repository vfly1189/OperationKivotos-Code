using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class BossDungeonScene : BaseScene
{
    [SerializeField] private BossDungeonScenePreloadSO _preloadData;
    [SerializeField] private GameObject _loadingCover;


    private GameObject _boss;
    private GameObject _curMap;
    private GameObject _mainUI;
    private GameObject _bossHPBar;
    private GameObject _loadingCoverInstance;
    private GameObject _clearUI;

    private Transform _spawnPoint;
    private Transform _cameraPoint;
    private Transform _bossSpawnPoint;

    private AudioClip _mainBGM;
    private AudioClip _successBGM;


    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.BossDungeon;

        // 로딩하는거 가려줄 화면
        if (_loadingCover != null)
        {
            _loadingCoverInstance = Object.Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            // 커버에도 CanvasGroup이 있어야 페이드 아웃 가능 (없으면 추가)
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
        }

        await Task.Delay(1000);

        // 2. 맵 생성 (스폰 포인트 확보를 위해 가장 먼저 필수)
        await CreateMap();

        // 3. UI 및 기타 리소스 병렬 로드
        CreateUI(); // 동기 호출 (찾기만 함)

        var bgmTask = SetupBGM();
        var poolTask = CreatePool();
        var effectTask = CreateEffectStage();
        var successBgmTask = CreateSuccessBGM();
        var clearUITask = CreateClearUI();

        // 4. 보스 및 HP Bar 생성 (순서 중요: 보스 -> HP Bar)
        await CreateBoss();
        await CreateBossHPBarUI();

        // 5. 나머지 로딩 대기
        await Task.WhenAll(bgmTask, poolTask, effectTask, successBgmTask, clearUITask);

        // 6. 게임 시작 처리
        PlayBGM();
        //PlayBattleInVoice();
        StartCoroutine(CoSafeTeleport());
        StartCoroutine(FadeInSequence());
    }

    async Task CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };

        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.bossDungeon);
        if (prefab != null)
        {
            _curMap = Instantiate(prefab, root.transform);
            _curMap.transform.position = Vector3.zero;

            var mapScript = _curMap.GetComponent<BossDungeonMap>();
            if (mapScript != null)
            {
                _spawnPoint = mapScript.GetCharacterSpawnPoint();
                _cameraPoint = mapScript.GetCameraPoint();
                _bossSpawnPoint = mapScript.GetBossSpawnPoint();
            }
        }
    }

    async Task CreateSuccessBGM()
    {
        if (_preloadData.successBgm == null) return;
        _successBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.successBgm);
    }
    void CreateUI()
    {
        _mainUI = GameObject.Find("@GameSceneCanvas");
    }

    async Task SetupBGM()
    {
        if (_preloadData.fightingBgms == null || _preloadData.fightingBgms.Length == 0) return;

        int rand = Random.Range(0, _preloadData.fightingBgms.Length);
        _mainBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.fightingBgms[rand]);
    }

    void PlayBGM()
    {
        if (_mainBGM != null)
            Managers.Sound.Play(_mainBGM, Define.Sound.Bgm);
    }

    void PlayBattleInVoice()
    {
        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
        if (partyMembers.Count == 0) return;

        int randomNum_partyMembers = Random.Range(0, partyMembers.Count);
        AudioClip[] voices = partyMembers[randomNum_partyMembers].Stat.GetBattleInVoice();

        if (voices != null && voices.Length > 0)
        {
            int randomNum_voice = Random.Range(0, voices.Length);
            Managers.Sound.Play(voices[randomNum_voice], Define.Sound.Voice);
        }
    }

    async Task CreatePool()
    {
        // PoolManager는 이제 원본 GameObject만 받으면 알아서 풀링을 해줍니다.
        if (_preloadData.bullet != null)
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.bullet);
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterRL != null)
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.monsterRL);
            if (prefab != null) Managers.Pool.CreatePool(prefab, 16);
        }
    }

    async Task CreateEffectStage()
    {
        if (_preloadData.effectStage == null) return;

        GameObject root = new GameObject { name = "@Effect" };
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.effectStage);

        if (prefab != null)
        {
            GameObject go = Instantiate(prefab, root.transform);
            go.SetActive(true);
        }
    }

    async Task CreateClearUI()
    {
        if (_preloadData.dungeonClearUI == null) return;

        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.dungeonClearUI);
        if (prefab != null)
        {
            _clearUI = Instantiate(prefab);
            _clearUI.SetActive(false);
        }
    }

    async Task CreateBoss()
    {
        //GameObject root = new GameObject { name = "@BossMonster" };
        //root.transform.position = _bossSpawnPoint.transform.position;

        //if (_preloadData.boss != null)
        //{
        //    var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.boss);

        //    await handle.Task;
        //    if(handle.Status == AsyncOperationStatus.Succeeded)
        //    {
        //        GameObject boss = Object.Instantiate(handle.Result, root.transform);
        //        _boss = boss;
        //        _boss.GetComponent<BossMonsterController>().OnDead -= OnMonsterDead;
        //        _boss.GetComponent<BossMonsterController>().OnDead += OnMonsterDead;
        //        _boss.GetComponent<BossSkillController>().SetSpawnPoints(_curMap.GetComponent<BossDungeonMap>().GetMonsterSpawnPoints());
        //        _boss.GetComponent<BossSkillController>().SetLightningPoints(_curMap.GetComponent<BossDungeonMap>().GetLightningPoints());
        //        RegisterHandle(ObjectType.Monster, handle);
        //    }
        //}


        //if (_bossSpawnPoint == null)
        //{
        //    Debug.LogError("Boss Spawn Point is NULL. Cannot create boss.");
        //    return;
        //}

        //if (_preloadData.boss == null) return;

        //GameObject root = new GameObject { name = "@BossMonster" };
        //root.transform.position = _bossSpawnPoint.position; // 위치 설정

        //var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.boss);
        //await handle.Task;

        //if (handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    GameObject boss = Object.Instantiate(handle.Result, root.transform);
        //    boss.transform.localPosition = Vector3.zero; // 루트 기준 0,0,0
        //    _boss = boss;

        //    // 이벤트 및 설정
        //    var ctrl = _boss.GetComponent<BossMonsterController>();
        //    if (ctrl != null)
        //    {
        //        ctrl.OnDead -= OnMonsterDead;
        //        ctrl.OnDead += OnMonsterDead;
        //    }

        //    if (_curMap != null)
        //    {
        //        var skill = _boss.GetComponent<BossSkillController>();
        //        var mapScript = _curMap.GetComponent<BossDungeonMap>();
        //        if (skill != null && mapScript != null)
        //        {
        //            skill.SetSpawnPoints(mapScript.GetMonsterSpawnPoints());
        //            skill.SetLightningPoints(mapScript.GetLightningPoints());
        //        }
        //    }

        //    RegisterHandle(ObjectType.Monster, handle);
        //}


        if (_bossSpawnPoint == null || _preloadData.boss == null) return;

        GameObject root = new GameObject { name = "@BossMonster" };
        root.transform.position = _bossSpawnPoint.position;

        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.boss);
        if (prefab != null)
        {
            _boss = Instantiate(prefab, root.transform);
            _boss.transform.localPosition = Vector3.zero;

            var ctrl = _boss.GetComponent<BossMonsterController>();
            if (ctrl != null)
            {
                ctrl.OnDead -= OnMonsterDead;
                ctrl.OnDead += OnMonsterDead;
            }

            if (_curMap != null)
            {
                var skill = _boss.GetComponent<BossSkillController>();
                var mapScript = _curMap.GetComponent<BossDungeonMap>();
                if (skill != null && mapScript != null)
                {
                    skill.SetSpawnPoints(mapScript.GetMonsterSpawnPoints());
                    skill.SetLightningPoints(mapScript.GetLightningPoints());
                }
            }
        }
    }

    async Task CreateBossHPBarUI()
    {
        //GameObject root = new GameObject { name = "@HPBarUI" };

        ////if (_preloadData.bossHPBar != null)
        ////{
        ////    GameObject hpBarUI = Object.Instantiate(_preloadData.bossHPBar, root.transform);
        ////    _bossHPBar = hpBarUI;
        ////    hpBarUI.GetComponent<BossHPBar>().SetBoss(_boss);
        ////}

        //var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.bossHPBar);

        //await handle.Task;
        //if (handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    GameObject bossHPBar = Object.Instantiate(handle.Result, root.transform);
        //    _bossHPBar = bossHPBar;
        //    _bossHPBar.GetComponent<BossHPBar>().SetBoss(_boss);
        //    RegisterHandle(ObjectType.UI, handle);
        //}


        //// 보스가 생성될 때까지 대기
        //while (_boss == null) await Task.Yield();

        //if (_preloadData.bossHPBar == null) return;

        //GameObject root = new GameObject { name = "@HPBarUI" };

        //var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.bossHPBar);
        //await handle.Task;

        //if (handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    GameObject hpBar = Object.Instantiate(handle.Result, root.transform);
        //    _bossHPBar = hpBar;

        //    var hpScript = hpBar.GetComponent<BossHPBar>();
        //    if (hpScript != null)
        //        hpScript.SetBoss(_boss);

        //    RegisterHandle(ObjectType.UI, handle);
        //}


        // 보스가 할당될 때까지 안전하게 대기
        while (_boss == null) await Task.Yield();

        if (_preloadData.bossHPBar == null) return;

        GameObject root = new GameObject { name = "@HPBarUI" };
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.bossHPBar);

        if (prefab != null)
        {
            _bossHPBar = Instantiate(prefab, root.transform);
            var hpScript = _bossHPBar.GetComponent<BossHPBar>();
            if (hpScript != null)
                hpScript.SetBoss(_boss);
        }
    }

    void PlayVictoryVoice()
    {
        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
        if (partyMembers.Count == 0) return;

        int randomNum_partyMembers = Random.Range(0, partyMembers.Count);
        AudioClip[] voices = partyMembers[randomNum_partyMembers].Stat.GetBattleVictoryVoices();

        if (voices != null && voices.Length > 0)
        {
            int randomNum_voice = Random.Range(0, voices.Length);
            Managers.Sound.Play(voices[randomNum_voice], Define.Sound.Voice);
        }
    }


    private void OnMonsterDead()
    {
        //// 1. Party Time BGM 재생 (선택)
        //Managers.Sound.Play(_successBGM, Define.Sound.Bgm); // 실제 BGM 이름이나 AudioClip 필요

        //// 2. PartyManager에게 승리 통보 (이게 핵심)
        //Managers.Party.FinishGame(true); // true = Success
        //Managers.Destroy(_bossHPBar);
        //StartCoroutine(CoVictoryPoze());

        if (_successBGM != null)
            Managers.Sound.Play(_successBGM, Define.Sound.Bgm);

        Managers.Party.FinishGame(true);
        Managers.Resource.Destroy(_bossHPBar);
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

        //GameObject clearUI = Object.Instantiate(_preloadData.dungeonClearUI);
        _clearUI.SetActive(true);

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


    private IEnumerator CoSafeTeleport()
    {
        // 맵의 콜라이더들이 물리 엔진에 등록될 시간을 줌 (1프레임)
        yield return null;

        // 그 다음 안전하게 이동
        Managers.Party.TeleportParty(_spawnPoint.position);

        // 카메라 이동
        Camera.main.transform.position = _cameraPoint.position;
        Camera.main.transform.rotation = _cameraPoint.rotation;
    }
    private IEnumerator FadeInSequence()
    {
        if (_loadingCoverInstance == null) yield break;

        CanvasGroup coverCG = _loadingCoverInstance.GetComponent<CanvasGroup>();
        if (coverCG != null)
        {
            float timer = 0f;
            float duration = 0.5f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                coverCG.alpha = Mathf.Lerp(1f, 0f, timer / duration);
                yield return null;
            }
        }
        Managers.Resource.Destroy(_loadingCoverInstance);
    }
}
