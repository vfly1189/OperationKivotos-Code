using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
// [추가]
using Cysharp.Threading.Tasks;
using NPOI.HSSF.Record.PivotTable;

public class NormalDungeonScene : BaseScene
{
    [SerializeField] private GameObject _loadingCover;
    [SerializeField] private NormalDungeonScenePreloadSO _preloadData;

    private GameObject _curMap;
    private GameSceneCanvas _mainUI;
    private GameObject _loadingCoverInstance;
    private GameObject _clearUI;

    private AudioClip _battleInVoice;
    private AudioClip _victoryVoice;
    private AudioClip _successBGM;
    private AudioClip _mainBGM;

    private DungeonSequenceDirector _clearDirector;
    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.NormalDungeon;


        GameObject spawnPointObj = new GameObject("TempSpawn");
        spawnPointObj.transform.position = Vector3.zero;
        Managers.Party.ResetPartyForNewScene(spawnPointObj.transform);
        Destroy(spawnPointObj);

        if (_loadingCover != null)
        {
            _loadingCoverInstance = Instantiate(_loadingCover);
            _loadingCover.SetActive(true);

            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
            _loadingCoverInstance.GetComponent<LoadingSceneController>().SetValue(1f);
        }

        var mainUI = SetupUI();
        var mapTask = CreateMap();
        var poolTask = CreatePool();
        var effectStageTask = CreateEffectStage();
        var clearUI = CreateClearUI();
        var successBgm = CreateSuccessBGM();
        var victoryVoice = LoadVictoryVoice();
        var battleInVoice = LoadBattleInVoice();
        var mainBgmTask = LoadMainBgm();

        // UniTask.WhenAll 로 병렬 대기
        await UniTask.WhenAll(mainUI, mapTask, poolTask, effectStageTask, clearUI, successBgm, victoryVoice, battleInVoice);


        var clearCondition = _curMap.AddComponent<KillAllMonstersCondition>();
        clearCondition.SetupCondition(_curMap);

        if (_curMap.GetComponent<DungeonSequenceDirector>() == null)
        {
            _clearDirector = _curMap.AddComponent<DungeonSequenceDirector>();
            _clearDirector.SetupDirector(_curMap, _successBGM, _victoryVoice);

            _clearDirector.OnClearUI -= ClearUI;
            _clearDirector.OnClearUI += ClearUI;
        }

        Managers.Dungeon.StartDungeon(clearCondition);

        PlayBGM();
        PlayBattleInVoice();

        FadeInSequence().Forget();
    }


    async UniTask CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };
        AssetReferenceGameObject mapPrefabRef = null;


        switch (Managers.Context.SelectedDifficulty)
        {
            case Define.DungeonDifficulty.Easy:
                mapPrefabRef = _preloadData.normalDungeonEasy;
                break;
            case Define.DungeonDifficulty.Normal:
                mapPrefabRef = _preloadData.normalDungeonNormal;
                break;
            case Define.DungeonDifficulty.Hard:
                mapPrefabRef = _preloadData.normalDungeonHard;
                break;
        }

        if (mapPrefabRef != null)
        {
            GameObject mapPrefab = await Managers.Resource.LoadAsync<GameObject>(mapPrefabRef);

            if (mapPrefab != null)
            {
                _curMap = Instantiate(mapPrefab, root.transform);
                _curMap.transform.position = Vector3.zero;

                // [변경] 스폰 방식으로 몬스터 등록 변경
                SpawnAndRegisterMonsters(_curMap);
            }
        }
    }

    private void SpawnAndRegisterMonsters(GameObject map)
    {
        Transform spawnPointsRoot = map.transform.Find("SpawnPoints");
        if (spawnPointsRoot == null)
        {
            Debug.LogError("맵 프리팹에 'SpawnPoints' 오브젝트가 없습니다!");
            return;
        }

        Transform monstersRoot = map.transform.Find("Monsters");
        if (monstersRoot == null)
        {
            GameObject go = new GameObject("Monsters");
            go.transform.SetParent(map.transform);
            monstersRoot = go.transform;
        }

        MapMonsterConfig mapConfig = Managers.Data.GetData<int, MapMonsterConfig>(Managers.Context.CurrentDungeonID);
        if (mapConfig == null)
        {
            Debug.LogError($"맵 정보({Managers.Context.CurrentDungeonGroupID})가 없습니다.");
            return;
        }


        // 1. 데이터 매니저에서 몬스터 전체 딕셔너리를 가져옵니다.
        Dictionary<int, MonsterBaseData> monsterDict = Managers.Data.GetDict<int, MonsterBaseData>();
        if (monsterDict == null) return;

        // 2. SpawnPoints 바로 아래의 자식들 (AddressableKey 이름의 노드들) 순회
        for (int i = 0; i < spawnPointsRoot.childCount; i++)
        {
            Transform groupNode = spawnPointsRoot.GetChild(i);
            string addressableKey = groupNode.name; // 노드 이름 = AddressableKey

            // 3. AddressableKey가 일치하면서, 던전용 몬스터(SpawnType.Dungeon)인 데이터를 찾습니다.
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
                continue;
            }

            // 4. 그룹 노드 아래의 실제 Point들 순회하며 스폰
            for (int j = 0; j < groupNode.childCount; j++)
            {
                Transform point = groupNode.GetChild(j);

                // 노드 이름(addressableKey)을 그대로 프리팹 로드에 사용
                GameObject monsterObj = Managers.Resource.Instantiate(
                    addressableKey,
                    point.position,
                    point.rotation
                );

                if (monsterObj == null) continue;

                monsterObj.transform.SetParent(monstersRoot);

                MonsterController monsterCtrl = monsterObj.GetComponent<MonsterController>();
                MonsterStat monsterStat = monsterObj.GetComponent<MonsterStat>();

                if (monsterStat != null && monsterCtrl != null)
                {
                    // 레벨 계산 및 스탯 주입
                    int spawnLevel = (baseData.Grade == MonsterDefine.MonsterGrade.Elite)
                        ? mapConfig.EliteMonsterLevel : mapConfig.NormalMonsterLevel;

                    MonsterLevelByStat levelStat = Managers.Data.GetData<int, MonsterLevelByStat>(spawnLevel);

                    monsterStat.Init(baseData, levelStat);
                }
            }
        }
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

        //await Managers.UI.GetOrMakeLootPanelAsync();
    }

    async UniTask CreateSuccessBGM()
    {
        if (_preloadData.successBgm == null) return;
        _successBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.successBgm);
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

    async UniTask LoadMainBgm()
    {
        if (_preloadData.fightingBgms == null || _preloadData.fightingBgms.Length == 0) return;
        int rand = Random.Range(0, _preloadData.fightingBgms.Length);

        _mainBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.fightingBgms[rand]);
    }

    void PlayBGM()
    {
        if (_mainBGM != null) Managers.Sound.Play(_mainBGM, Define.Sound.Bgm);
    }

    void PlayBattleInVoice()
    {
        if (_battleInVoice != null) Managers.Sound.Play(_battleInVoice, Define.Sound.Voice);
    }

    async UniTask LoadBattleInVoice()
    {
        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
        if (partyMembers.Count == 0) return;

        int randomMemberIdx = Random.Range(0, partyMembers.Count);
        AssetReferenceT<AudioClip>[] voices = partyMembers[randomMemberIdx].Stat.GetBattleInVoice();

        if (voices != null && voices.Length > 0)
        {
            var voiceRef = voices[Random.Range(0, voices.Length)];
            if (voiceRef != null && voiceRef.RuntimeKeyIsValid())
            {
                _battleInVoice = await Managers.Resource.LoadAsync<AudioClip>(voiceRef);
            }
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

    async UniTask CreatePool()
    {
        if (_preloadData.bullet == null) return;
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.bullet);
        if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
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


    private void ClearUI()
    {
        if (_mainUI != null)
        {
            //_mainUI.gameObject.SetActive(false);
            Managers.Resource.Destroy(_mainUI.gameObject);
        }
        if (_clearUI != null) _clearUI.SetActive(true);

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

        Managers.Resource.Destroy(_clearUI);

        if (Managers.Save.IsReady)
            Managers.Save.SaveCurrentPartyAsync().Forget();
    }


}
