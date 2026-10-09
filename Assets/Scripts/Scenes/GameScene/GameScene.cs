// [추가] UniTask 네임스페이스
using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class GameScene : BaseScene
{
    [SerializeField] private GameScenePreloadSO _preloadData;
    [SerializeField] private GameObject _loadingCover;

    [Header("존재 판정 (씬 로드 전에 고른다 — 런타임 전환 없음)")]
    [Tooltip("기본 = ⑤ Distance (2026-10-10 채택, 03_Comparison_Plan 8절). 나머지는 비교 · 측정용으로 남겨 둔 것.")]
    [SerializeField] private ActivationPolicyType _activationPolicy = ActivationPolicyType.Distance;
    [Tooltip("나머지 6개를 그림자(계산 · 로그만)로 같이 돌린다. 동등성 · 집합 지표용 측정 도구 — 평소엔 끈다.")]
    [SerializeField] private bool _activationShadows = false;

    // 초기화가 끝났는가 — 측정 하네스(-harness)가 이 뒤에 자동 시작한다.
    public bool IsReady { get; private set; }

    private GameObject _loadingCoverInstance;
    private GameObject _map;
    private AudioClip _mainBGM;

    // [핵심 변경 1] async void 사용
    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Game;

        //Managers.Input.OnEscapePressed -= HandleEscape;
        //Managers.Input.OnEscapePressed += HandleEscape;

        if (_loadingCover != null)
        {
            _loadingCoverInstance = Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
            _loadingCoverInstance.GetComponent<LoadingSceneController>().SetValue(1f);
        }

        var poolTask = CreatePool();
        var mapTask = CreateMainVillage();
        var bgmTask = LoadMainBGM();

        await UniTask.WhenAll(poolTask, mapTask, bgmTask);

        await CreateCharacters();
        Managers.Spawner.Run();   // 파티 준비 완료 → 스폰 가동 (몬스터 OnSpawn이 파티를 읽는다)
        // Party.Init() 이후에 하는게 나음.
        // 현재 CreateCharacters에서 하고 있음.
        ApplySaveOrTestData(); 

        await SetupUI();
        await CreateShopMaster();

        Transform spawnPoint = _map.GetComponent<BaseMap>().GetPlayerSpawnPoint();
        Managers.Field.Init(spawnPoint);


        //SetupFieldConditions();
        SetupCamera();
        PlayMainBGM();

        FadeInSequence().Forget();

        await UI_LootNotification.PreloadAsync();
        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Input.OnEscapePressed += HandleEscape;

        IsReady = true;
    }

    private async UniTask<GameObject> LoadAndSpawnAsync(AssetReferenceGameObject refObj, Transform parent = null)
    {
        if (refObj == null) return null;

        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(refObj);

        if (prefab != null)
        {
            return Instantiate(prefab, parent);
        }
        return null;
    }

    void PlayMainBGM()
    {
        if (_mainBGM != null)
            Managers.Sound.Play(_mainBGM, Define.Sound.Bgm);
    }

    private async UniTask SetupUI()
    {
        //var existingUI = FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
        //if (existingUI != null)
        //{
        //    existingUI.gameObject.SetActive(true);
        //    existingUI.SetPartyManager();
        //    return;
        //}

        await Managers.Resource.LoadAsync<GameObject>("GameSceneCanvas_New");

        GameSceneCanvas ui = Managers.UI.ShowSceneUI<GameSceneCanvas>("GameSceneCanvas_New");

        if (ui != null)
        {
            ui.SetPartyManager();
        }

        // 임시
        //await Managers.UI.GetOrMakeLootPanelAsync();
    }

    private async UniTask CreatePool()
    {
        if (_preloadData.bullet != null)
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.bullet);
            if (prefab != null) Managers.Pool.CreatePool(prefab, _preloadData.bullet.RuntimeKey.ToString(), 200);   // [Phase 4] key 전달 → 풀이 refCount 티켓 획득 (LoadAsync(AssetReference)와 동일 키잉)
        }

        if (_preloadData.monsterAR != null)
        {
            string addressableKey = Managers.Data.GetData<int, MonsterBaseData>(2000).AddressableKey;
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey);
            if (prefab != null) Managers.Pool.CreatePool(prefab, addressableKey, 200);   // [Phase 4] key 전달 → 풀이 refCount 티켓 획득
        }

        if (_preloadData.monsterRL != null)
        {
            string addressableKey = Managers.Data.GetData<int, MonsterBaseData>(2001).AddressableKey;
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey);
            if (prefab != null) Managers.Pool.CreatePool(prefab, addressableKey, 200);   // [Phase 4] key 전달 → 풀이 refCount 티켓 획득
        }

        if (_preloadData.monsterTank != null)
        {
            string addressableKey = Managers.Data.GetData<int, MonsterBaseData>(2002).AddressableKey;
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey);
            if (prefab != null) Managers.Pool.CreatePool(prefab, addressableKey, 200);   // [Phase 4] key 전달 → 풀이 refCount 티켓 획득
        }

        //HPBar 풀링
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>("MonsterHPBar");
            if (prefab != null) Managers.Pool.CreatePool(prefab, "MonsterHPBar", 200);
        }
    }

    private async UniTask CreateMainVillage()
    {
        _map = await LoadAndSpawnAsync(_preloadData.mainVillage);
        if (_map == null)
        {
            GameLog.LogError("[GameScene] 맵 생성 실패 — 스포너 등록을 건너뜀");
            return;
        }

        _map.name = "@Map";
        _map.transform.position = Vector3.zero;

        BaseMap map = _map.GetComponent<BaseMap>();
        List<SpawnerRecord> records = new List<SpawnerRecord>();
         
        foreach(MonsterSpawner spawner in map.GetSpawners())
        {
            SpawnerData data = Managers.Data.GetData<int, SpawnerData>(spawner.SpawnerID);
            SpawnerRecord record = spawner.Build(data);
            if(record != null) records.Add(record);
        }

        //record 들을 매니저한테 넘겨줘야됨
        Managers.Spawner.Init(map.MapID, records);
        // 실행 인자(-policy · -shadows)가 있으면 인스펙터 값 대신 — 빌드 하나로 정책을 바꿔 측정 (HarnessArgs)
        Managers.Activation.Init(new ActivationLayout { _sectors = map.GetSectors(), _records = records.ToArray() },
                                 HarnessArgs.Policy ?? _activationPolicy, HarnessArgs.Shadows ?? _activationShadows);
    }

    private async UniTask CreateCharacters()
    {
        Transform spawnPoint = _map.GetComponent<BaseMap>().GetPlayerSpawnPoint();
        Managers.Party.TeleportParty(spawnPoint.position);

        // 파티가 이미 있으면 재사용 — 파티는 씬 전환을 넘어 생존하므로 Party 스코프도 그대로 둔다.
        if (Managers.Party.GetMember() != null && Managers.Party.GetMember().Count > 0)
        {
            Managers.Party.ReturnToTownForNewScene(spawnPoint);
            return;
        }

        // [Phase 3c] 여기가 유일한 파티 구성 경계 = Party 스코프의 회전 지점.
        //  순서 주의: 인스턴스 파괴(ClearParty)를 먼저, 핸들 반납(CreateScope)을 나중에.
        //  살아있는 인스턴스가 프리팹의 메시/텍스처를 참조하는 동안 핸들만 반납하면
        //  refCount는 풀려도 실제 언로드가 일어나지 않는다.
        Managers.Party.ClearParty();
        Managers.Resource.CreateScope(ResourceScopeType.Party, "Party");   // 이전 파티 스코프 Dispose + 새 스코프

        Transform container = Managers.Party.GetCharacterContainer();
        List<BaseCharacter> partyMembers = new List<BaseCharacter>();

        foreach (var data in Managers.Context.SelectedSchool.characters)
        {
            await LoadCharacterSequential(data, container, partyMembers, spawnPoint);
        }
        Managers.Party.Init(partyMembers, spawnPoint);
    }

    private async UniTask LoadCharacterSequential(CharacterDataSO data, Transform parent, List<BaseCharacter> list, Transform spawnPoint)
    {
        // [Phase 3c] Global(영구 상주) → Party 스코프. 파티 교체 시 이전 파티 프리팹이 회수된다.
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(data.inGamePrefab, ResourceScopeType.Party);

        if (prefab != null)
        {
            GameObject characterGO = Managers.Resource.Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parent);

            BaseCharacter character = characterGO.GetComponent<BaseCharacter>();

            //var savedData = Managers.Context.LoadCharacterStat(data.id);
            //if (savedData != null) character.Stat.ApplyRuntimeData(savedData);

            list.Add(character);
        }
    }

    private async UniTask CreateShopMaster()
    {
        GameObject shop = await LoadAndSpawnAsync(_preloadData.shopMaster);
        if (shop != null)
        {
            GameObject root = new GameObject("@Shop");
            shop.transform.SetParent(root.transform);

            Transform tr = _map.GetComponent<BaseMap>().GetShopMasterTr();
            shop.transform.position = tr.position;
            shop.transform.rotation = tr.rotation;
        }
    }

    
    private async UniTask LoadMainBGM()
    {
        if (_preloadData.mainBGMs == null || _preloadData.mainBGMs.Length == 0) return;

        int rand = Random.Range(0, _preloadData.mainBGMs.Length);
        _mainBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.mainBGMs[rand]);
    }

    void SetupCamera()
    {
        BaseCharacter leader = Managers.Party.GetCurrentCharacter();

        if (leader != null)
        {
            Camera.main.GetComponent<CameraController>().SetTarget(leader.gameObject);
        }
    }

    public override void Clear()
    {
        IsReady = false;
        base.Clear();
        Managers.Sound.StopAll();
        Managers.Input.OnEscapePressed -= HandleEscape;

        Managers.Field.Clear();
        Managers.Activation.Clear();
        Managers.Spawner.Clear();
    }


    private async UniTaskVoid FadeInSequence()
    {
        if (_loadingCoverInstance == null) return;

        CanvasGroup coverCG = _loadingCoverInstance.GetComponent<CanvasGroup>();
        if (coverCG == null)
        {
            Destroy(_loadingCoverInstance); // 자체 인스턴스화했으므로 일반 Destroy 사용
            return;
        }

        float timer = 0f;
        float duration = 0.5f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            coverCG.alpha = Mathf.Lerp(1f, 0f, timer / duration);
            await UniTask.Yield(); // 1프레임 대기
        }

        Destroy(_loadingCoverInstance);
    }

   
   

    private void ApplySaveOrTestData()
    {
        //방금 게임을 켜서 파일에서 데이터를 읽어야 할 때만 실행
        if (Managers.Context.ShouldLoadSaveData)
        {
            PartySaveData saveData = Managers.Context.SelectedSavedData;

            if (saveData != null)
            {
                // 저장된 데이터 복원
                Managers.Save.ApplySaveDataToManagers(saveData);
            }
            else
            {
                // 세이브 데이터가 없는 새 게임(Fresh Start)일 경우
                Managers.Party.InitFromContext(null);
            }

            // 핵심: 한 번 로드했으면 플래그를 끄기 (던전에서 돌아올 땐 안 읽게 됨)
            Managers.Context.ShouldLoadSaveData = false;
        }
        else
        {
            // 던전에서 돌아온 경우 (메모리에 있는 Managers.Party 상태를 그대로 유지)
            GameLog.Log("[GameScene] 던전에서 귀환: 기존 메모리 상태 유지");

            // 주의: 파티 스폰 위치 등 물리적 리셋이 필요하다면 여기서 처리 (이미 CreateCharacters에서 하고 계시긴 합니다)
        }
    }

    private void SetupFieldConditions()
    {
        // 1. 실패 조건(파티 전멸) 생성
        PartyWipeCondition failCondition = _map.AddComponent<PartyWipeCondition>();
        failCondition.SetUp(); // Manager 이벤트 구독

        // 2. 매니저에 실패 조건 등록
        Managers.Dungeon.AddFailCondition(failCondition);

        // 3. 필드 전용 실패 연출 등록 (스폰 지점으로 부활)
        Managers.Dungeon.OnDungeonFailed += HandleFieldPartyWipe;
    }

    private void HandleFieldPartyWipe()
    {
        // 스폰 지점 가져오기
        Transform spawnPoint = _map.GetComponent<BaseMap>().GetPlayerSpawnPoint();

        // 파티 부활 및 스폰 지점으로 이동 처리
        Managers.Party.ResetPartyForNewScene(spawnPoint);
        // (선택) 여기에 페이드인/아웃 연출을 추가할 수 있습니다.
    }

    private void ApplyTestData()
    {
        for (int i = 0; i < 40; i++)
        {
            int randNum = Random.Range(10000, 10090);
            Managers.Inventory.AddEquipmentSlot(EquipmentFactory.CreateEquipment(randNum));
        }
        Managers.Inventory.AddEquipmentSlot(EquipmentFactory.CreateEquipment(10010));
        Managers.Inventory.AddEquipmentSlot(EquipmentFactory.CreateEquipment(10010));
        Managers.Inventory.AddEquipmentSlot(EquipmentFactory.CreateEquipment(10010));
        Managers.Inventory.AddEquipmentSlot(EquipmentFactory.CreateEquipment(10010));

        Managers.Inventory.AddItem(30000, ItemCategory.Material, 9999);
        Managers.Inventory.AddItem(30001, ItemCategory.Material, 9999);
        Managers.Inventory.AddItem(30002, ItemCategory.Material, 9999);
        Managers.Inventory.AddItem(30003, ItemCategory.Material, 1000);
        Managers.Inventory.AddItem(30004, ItemCategory.Material, 1000);
        Managers.Inventory.AddItem(30005, ItemCategory.Material, 1000);
        Managers.Inventory.AddItem(30000, ItemCategory.Material, 500);
    }
}
