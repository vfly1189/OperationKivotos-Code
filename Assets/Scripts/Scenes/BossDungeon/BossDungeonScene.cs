// [추가] UniTask 네임스페이스
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class BossDungeonScene : BaseScene
{
    [SerializeField] private BossDungeonScenePreloadSO _preloadData;
    [SerializeField] private GameObject _loadingCover;

    private GameObject _boss;
    private GameObject _curMap;
    private GameSceneCanvas _mainUI;
    private GameObject _bossHPBar;
    private GameObject _loadingCoverInstance;
    private GameObject _clearUI;

    private Transform _spawnPoint;
    private Transform _cameraPoint;
    private Transform _bossSpawnPoint;

    private AudioClip _mainBGM;
    private AudioClip _successBGM;
    private AudioClip _victoryVoice;

    private DungeonSequenceDirector _clearDirector;

    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.BossDungeon;

        if (_loadingCover != null)
        {
            _loadingCoverInstance = Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
            _loadingCoverInstance.GetComponent<LoadingSceneController>().SetValue(1f);
        }

        await CreateMap();

        var mainUI = SetupUI();
        var poolTask = CreatePool();
        var effectTask = CreateEffectStage();
        var successBgmTask = CreateSuccessBGM();
        var victoryVoice = LoadVictoryVoice();

        await CreateBoss();
        await CreateBossHPBarUI();

        await UniTask.WhenAll(mainUI, poolTask, effectTask, successBgmTask, victoryVoice);


        if (_curMap.GetComponent<DungeonSequenceDirector>() == null)
        {
            _clearDirector = _curMap.AddComponent<DungeonSequenceDirector>();
            _clearDirector.SetupDirector(_curMap, _successBGM, _victoryVoice);
        }

      
        // 씬 자체적으로 던전 매니저의 결과를 구독
        Managers.Dungeon.OnDungeonCleared -= OnDungeonSuccess;
        Managers.Dungeon.OnDungeonCleared += OnDungeonSuccess;

        Managers.Dungeon.OnDungeonFailed -= OnDungeonFail;
        Managers.Dungeon.OnDungeonFailed += OnDungeonFail;

        SetupDungeonConditions();

        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Input.OnEscapePressed += HandleEscape;

        // Fire and Forget
        PlayBGM().Forget();
        PlayBattleInVoice().Forget(); 

        CoSafeTeleport().Forget();
        FadeInSequence().Forget();
    }

    
    async UniTask CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };

        // ResourceManager에 위임
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

    async UniTask CreateSuccessBGM()
    {
        if (_preloadData.successBgm == null) return;
        _successBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.successBgm);
    }

    private async UniTask SetupUI()
    {
        await Managers.Resource.LoadAsync<GameObject>("GameSceneCanvas_New");
        // 3. UIManager를 통해 Scene UI 생성
        // @Canvas_Scene 하위로 자동 배치 및 SetCanvas 됨
        GameSceneCanvas ui = Managers.UI.ShowSceneUI<GameSceneCanvas>("GameSceneCanvas_New");
        _mainUI = ui;
        // 4. Party Manager 연동
        if (ui != null)
        {
            ui.SetPartyManager();
        }
    }
    public async UniTask PlayBGM()
    {
        if (_preloadData.fightingBgms == null || _preloadData.fightingBgms.Length == 0) return;
        int rand = Random.Range(0, _preloadData.fightingBgms.Length);

        _mainBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.fightingBgms[rand]);

        if(_mainBGM != null)
        {
            Managers.Sound.Play(_mainBGM, Define.Sound.Bgm);
        }
    }

    async UniTaskVoid PlayBattleInVoice()
    {
        List<BaseCharacter> partyMembers = Managers.Party.GetMember();
        if (partyMembers.Count == 0) return;

        int randomNum = Random.Range(0, partyMembers.Count);
        AssetReferenceT<AudioClip>[] voices = partyMembers[randomNum].Stat.GetBattleInVoice();

        if (voices != null && voices.Length > 0)
        {
            var voiceRef = voices[Random.Range(0, voices.Length)];
            if (voiceRef != null && voiceRef.RuntimeKeyIsValid())
            {
                AudioClip clip = await Managers.Resource.LoadAsync<AudioClip>(voiceRef);
                if (clip != null) Managers.Sound.Play(clip, Define.Sound.Voice);
            }
        }
    }

    async UniTask CreatePool()
    {
        if (_preloadData.bullet != null)
        {
            GameObject p = await Managers.Resource.LoadAsync<GameObject>(_preloadData.bullet);
            if (p != null) Managers.Pool.CreatePool(p, 30);
        }
        if (_preloadData.monsterRL != null)
        {
            GameObject p = await Managers.Resource.LoadAsync<GameObject>(_preloadData.monsterRL);
            if (p != null) Managers.Pool.CreatePool(p, 16);
        }
    }

    async UniTask CreateEffectStage()
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

    async UniTask CreateBoss()
    {
        if (_bossSpawnPoint == null || _preloadData.boss == null) return;

        _bossSpawnPoint = _curMap.transform.Find("BossSpawnPoint");

        string addressableKey = "Hieronymus_Boss";

        GameObject boss = await MonsterFactory.CreateMonsterByAddressableKeyAsync(
            addressableKey,
            Managers.Context.CurrentDungeonID,
            _bossSpawnPoint
        );

        if (boss != null)
        {
            _boss = boss;
            _boss.transform.SetParent(_bossSpawnPoint);
            _boss.transform.localPosition = Vector3.zero;

            // (SetStat은 이제 팩토리에서 해줬으므로 여기서 중복으로 할 필요 없습니다! 코드가 훨씬 짧아집니다.)

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

    async UniTask CreateBossHPBarUI()
    {
        // [핵심 6] Task.Yield() 대신 안전한 UniTask.Yield() 사용 (에디터 멈춤 원천 차단)
        while (_boss == null) await UniTask.Yield();

        // 1. 이미 BossHPBar 컴포넌트 타입으로 받아옴
        BossHPBar hpScript = Managers.UI.ShowSceneUI<BossHPBar>("UI_BossHPBar_New");
        _bossHPBar = hpScript.gameObject;
        // 2. 받아온 컴포넌트가 널이 아니라면 즉시 데이터 주입
        if (hpScript != null)
        {
            hpScript.SetBoss(_boss);
        }
        else
        {
            Debug.LogError("BossHPBar UI를 로드하는데 실패했습니다! Addressable Key나 Prefab 상태를 확인하세요.");
        }
    }

 
    async UniTask LoadVictoryVoice()
    {
        List<BaseCharacter> partyMembers = Managers.Party.GetMember();
        if (partyMembers.Count == 0) return;

        int randomMemberIdx = Random.Range(0, partyMembers.Count);
        AssetReferenceT<AudioClip>[] voices = partyMembers[randomMemberIdx].Stat.GetBattleVictoryVoices();

        if (voices != null && voices.Length > 0)
        {
            var voiceRef = voices[Random.Range(0, voices.Length)];
            if (voiceRef != null && voiceRef.RuntimeKeyIsValid())
            {
                _victoryVoice = await Managers.Resource.LoadAsync<AudioClip>(voiceRef);
            }
        }
    }


    private async UniTaskVoid CoSafeTeleport()
    {
        await UniTask.Yield(); // 1프레임 대기

        Managers.Party.TeleportParty(_spawnPoint.position);
        Camera.main.transform.position = _cameraPoint.position;
        Camera.main.transform.rotation = _cameraPoint.rotation;
    }

    private void OnDungeonSuccess()
    {
        // 보상을 주고 UI를 띄우는 비동기 함수 호출
        ShowSuccessUIAsync().Forget();
        Managers.Party.FinishGame(true);
    }

    private async UniTaskVoid ShowSuccessUIAsync()
    {
        // [핵심] Director의 컷신 연출이 끝날 때까지 씬에서 대기 (예: 3.5초)
        // 이벤트로 콜백 받지 않고, 씬이 연출 시간을 알고 기다리는 방식이 훨씬 유지보수가 좋습니다.
        await UniTask.Delay(System.TimeSpan.FromSeconds(3.0f));

        BaseClearUI();

        // 던전 보상 로직
        DungeonGroup dungeonGroup = Managers.Data.GetData<int, DungeonGroup>(Managers.Context.CurrentDungeonGroupID);
        DungeonData dungeonData = dungeonGroup.DungeonDataByDifficulty[Managers.Context.SelectedDifficulty];

        int clearExp = 0, clearCredit = 0;
        List<InventorySlot> finalRewards = new List<InventorySlot>();

        if (dungeonData != null)
        {
            clearExp = dungeonData.ClearExp;
            clearCredit = dungeonData.ClearCredit;
            Managers.Party.AddExp(clearExp);
            Managers.Wallet.AddCurrency(CurrencyType.Credit, clearCredit);
            finalRewards = Managers.Drop.RollAndGiveDropItems(dungeonData.ClearDropTableID, false);
        }

        // 성공 UI 띄우기
        UI_DungeonClear clearUI = Managers.UI.ShowSceneUI<UI_DungeonClear>("UI_DungeonClear");
        if (clearUI != null) clearUI.SetInfo(clearCredit, clearExp, finalRewards);
    }

    private async void OnDungeonFail()
    {
        Managers.Party.FinishGame(false);
        // 실패는 컷신 대기 없이 바로 실패 팝업 띄우기
        await UniTask.Delay(System.TimeSpan.FromSeconds(3.5f));
        BaseClearUI();
    }

    private void BaseClearUI()
    {
        if (_mainUI != null)
        {
            _mainUI.gameObject.SetActive(false);
            Managers.Resource.Destroy(_mainUI.gameObject);
        }

        if (_clearUI != null) _clearUI.SetActive(true);

        if (_bossHPBar != null)
        {
            _bossHPBar.gameObject.SetActive(false);
            Managers.Resource.Destroy(_bossHPBar);
        }
    }

    private void SetupDungeonConditions()
    {
        // 1. 보스 처치 (승리) 조건 설정
        KillBossCondition clearCondition = _curMap.AddComponent<KillBossCondition>();
        clearCondition.SetBoss(_boss);
        clearCondition.SetUp();

        // 2. 파티 전멸 (패배) 조건 설정
        PartyWipeCondition failCondition = _curMap.AddComponent<PartyWipeCondition>();
        failCondition.SetUp();

        // 3. 매니저에 각각 등록
        Managers.Dungeon.AddClearCondition(clearCondition);
        Managers.Dungeon.AddFailCondition(failCondition);

        Managers.Dungeon.StartDungeon();
    }

    private async UniTaskVoid FadeInSequence()
    {
        if (_loadingCoverInstance == null) return;
        CanvasGroup coverCG = _loadingCoverInstance.GetComponent<CanvasGroup>();
        if (coverCG != null)
        {
            float timer = 0f;
            float duration = 0.5f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                coverCG.alpha = Mathf.Lerp(1f, 0f, timer / duration);
                await UniTask.Yield();
            }
        }
        Managers.Resource.Destroy(_loadingCoverInstance);
    }

   
    public override void Clear()
    {
        base.Clear();
        Managers.Sound.StopAll();

        Managers.Dungeon.ClearDungeonData();

        Managers.Dungeon.OnDungeonCleared -= OnDungeonSuccess;
        Managers.Dungeon.OnDungeonFailed -= OnDungeonFail;


        Managers.Input.OnEscapePressed -= HandleEscape;

        if (Managers.Save.IsReady)
            Managers.Save.SaveCurrentPartyAsync().Forget();
    }

}
