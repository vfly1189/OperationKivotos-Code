// [추가] UniTask 네임스페이스
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using NPOI.SS.Formula.Functions;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    // [핵심 1] async UniTaskVoid 로 선언
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
        //var clearUITask = CreateClearUI();
        var victoryVoice = LoadVictoryVoice();

        await CreateBoss();
        await CreateBossHPBarUI();

        // [핵심 3] UniTask.WhenAll 사용
        await UniTask.WhenAll(mainUI, poolTask, effectTask, successBgmTask, victoryVoice);

        var clearCondition = _curMap.AddComponent<KillBossCondition>();
        clearCondition.SetupCondition(_curMap);

        if (_curMap.GetComponent<DungeonSequenceDirector>() == null)
        {
            _clearDirector = _curMap.AddComponent<DungeonSequenceDirector>();
            _clearDirector.SetupDirector(_curMap, _successBGM, _victoryVoice);

            _clearDirector.OnClearUI -= ClearUI;
            _clearDirector.OnClearUI += ClearUI;
        }

        Managers.Dungeon.StartDungeon(clearCondition);


        // Fire and Forget
        //비동기 작업 끝날때까지 기다리는게 아니라 다음꺼 실행
        PlayBGM().Forget();
        PlayBattleInVoice().Forget(); 

        // [핵심 4] 코루틴들을 통일성을 위해 UniTask로 변경하여 await
        CoSafeTeleport().Forget();
        FadeInSequence().Forget();
    }

    // [핵심 5] 모든 Task 반환형을 UniTask로 변경
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

    //void CreateUI() => _mainUI = GameObject.Find("@GameSceneCanvas");

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
        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
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

    async UniTask CreateClearUI()
    {
        if (_preloadData.dungeonClearUI == null) return;

        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.dungeonClearUI);

        if (prefab != null)
        {
            _clearUI = Instantiate(prefab);
            _clearUI.SetActive(false);
        }
    }

    async UniTask CreateBoss()
    {
        if (_bossSpawnPoint == null || _preloadData.boss == null) return;

        _bossSpawnPoint = _curMap.transform.Find("BossSpawnPoint");

        Dictionary<int, MonsterBaseData> monsterDict = Managers.Data.GetDict<int, MonsterBaseData>();
        if (monsterDict == null) return;

        string addressableKey = "Hieronymus_Boss";
        MonsterBaseData baseData = null;
        foreach (var data in monsterDict.Values)
        {
            if (data.AddressableKey == addressableKey && data.SpawnType == MonsterDefine.MonsterSpawnType.Dungeon)
            {
                baseData = data;
                break;
            }
        }
        if (baseData == null)
        {
            Debug.LogWarning($"던전용 몬스터 중 AddressableKey가 '{addressableKey}'인 데이터를 찾을 수 없습니다.");
            return;
        }


        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.boss);

        if (prefab != null)
        {
            _boss = Instantiate(prefab, _bossSpawnPoint.transform);
            _boss.transform.localPosition = Vector3.zero;

            var ctrl = _boss.GetComponent<BossMonsterController>();
            if (ctrl != null)
            {
                MonsterStat monsterStat = _boss.GetComponent<MonsterStat>();
                if (monsterStat != null && ctrl != null)
                {
                    // 레벨 계산 및 스탯 주입
                    int spawnLevel = 1;

                    MonsterLevelByStat levelStat = Managers.Data.GetData<int, MonsterLevelByStat>(spawnLevel);

                    monsterStat.Init(baseData, levelStat);
                }

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
        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
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

    private void ClearUI()
    {
        if (_mainUI != null)
        {
            //_mainUI.gameObject.SetActive(false);
            Managers.Resource.Destroy(_mainUI.gameObject);
        }
        if (_clearUI != null) _clearUI.SetActive(true);
        if (_bossHPBar != null) Managers.Resource.Destroy(_bossHPBar);

        // 1. 던전 테이블에서 현재 맵(_currentMapId)의 클리어 보상 정보 가져오기
        // (이름은 실제 프로젝트의 던전 테이블 구조에 맞게 변경하세요)
        //DungeonTable dungeonTable = Managers.Data.GetData<int, DungeonTable>(_currentMapId);
        DungeonGroup dungeonGroup = Managers.Data.GetData<int, DungeonGroup>(Managers.Context.CurrentDungeonGroupID);
        DungeonData dungeonData = dungeonGroup.DungeonDataByDifficulty[Managers.Context.SelectedDifficulty];

        int clearExp = 0;
        int clearCredit = 0;
        List<InventorySlot> finalRewards = new List<InventorySlot>();

        if (dungeonData != null)
        {
            clearExp = dungeonData.ClearExp;
            clearCredit = dungeonData.ClearCredit;

            // 2. 실제 플레이어/파티에 경험치와 재화 지급
            Managers.Party.AddExp(clearExp);
            Managers.Wallet.AddCurrency(CurrencyType.Credit, clearCredit);

            // 3. 드랍 테이블 ID로 주사위를 굴리고 획득한 아이템 목록 받아오기
            // (클리어 보상이므로 우측 하단 토스트 팝업은 안 띄우도록 showToast: false 전달)
            finalRewards = Managers.Drop.RollAndGiveDropItems(dungeonData.ClearDropTableID, false);
        }

        // 4. 결과 UI 띄우고 데이터 꽂아주기
        UI_DungeonClear clearUI = Managers.UI.ShowSceneUI<UI_DungeonClear>("UI_DungeonClear");
        if (clearUI != null)
        {
            clearUI.SetInfo(clearCredit, clearExp, finalRewards);
        }
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
        _clearDirector.OnClearUI -= ClearUI;
    }
}
