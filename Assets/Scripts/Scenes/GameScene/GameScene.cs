using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
// [추가] UniTask 네임스페이스
using Cysharp.Threading.Tasks;

public class GameScene : BaseScene
{
    [SerializeField] private GameScenePreloadSO _preloadData;
    [SerializeField] private GameObject _loadingCover;

    private GameObject _loadingCoverInstance;
    private GameObject _map;
    private AudioClip _mainBGM;

    // [핵심 변경 1] async void 사용
    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Game;

        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Input.OnEscapePressed += HandleEscape;

        if (_loadingCover != null)
        {
            _loadingCoverInstance = Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
            _loadingCoverInstance.GetComponent<LoadingSceneController>().SetValue(1f);
        }

        // [핵심 변경 2] UniTask.Delay 사용
        //await UniTask.Delay(500);

        var poolTask = CreatePool();
        var mapTask = CreateMainVillage();
        var bgmTask = LoadMainBGM(); // [추가] BGM 비동기 로드 분리

        // [핵심 변경 3] 병렬 로딩 최적화
        await UniTask.WhenAll(poolTask, mapTask, bgmTask);

        await CreateCharacters();
        await SetupUI();
        await CreateShopMaster();

        SetupCamera();
        PlayMainBGM();

        FadeInSequence().Forget(); // [변경] 코루틴 대신 UniTask 사용


        Managers.Inventory.AddItem(10002, ItemCategory.Equipment, 1);
        Managers.Inventory.AddItem(10010, ItemCategory.Equipment, 1);
        Managers.Inventory.AddItem(10015, ItemCategory.Equipment, 1);
        Managers.Inventory.AddItem(10016, ItemCategory.Equipment, 1);
        Managers.Inventory.AddItem(10018, ItemCategory.Equipment, 1);
        Managers.Inventory.AddItem(10019, ItemCategory.Equipment, 1);

        ////Managers.Inventory.AddItem(20000, ItemCategory.Consumable, 10);
        Managers.Inventory.AddItem(30000, ItemCategory.Material, 10);
        Managers.Inventory.AddItem(30001, ItemCategory.Material, 9989);
        Managers.Inventory.AddItem(30001, ItemCategory.Material, 39);

        Managers.Inventory.AddItem(30002, ItemCategory.Material, 50);
        Debug.Log("GameScene Init Complete");
    }

    // [핵심 변경 4] 공통 로드/스폰 함수: ResourceManager 활용
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

    // [핵심 변경 5] Addressables 직접 로드 제거
    private async UniTask SetupUI()
    {
        //var existingUI = FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
        //if (existingUI != null)
        //{
        //    existingUI.gameObject.SetActive(true);
        //    existingUI.SetPartyManager();
        //    return;
        //}

        //GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.gameSceneCanvas, true);

        //if (prefab != null)
        //{
        //    GameObject ui = Instantiate(prefab);
        //    ui.name = "@GameSceneCanvas";
        //    DontDestroyOnLoad(ui);
        //    ui.GetComponent<GameSceneCanvas>()?.SetPartyManager();
        //}

        // 1. 혹시 모를 씬 내에 이미 떠있는 UI가 있다면 UIManager 캐싱 및 활성화만 진행 (보통 던전에서 마을로 돌아올 때)
        // (단, 완벽한 프레임워크라면 씬에 UI를 수동으로 두지 않아야 함)
        var existingUI = FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
        if (existingUI != null)
        {
            existingUI.gameObject.SetActive(true);
            existingUI.SetPartyManager();
            return;
        }

        // 2. 어드레서블에서 UI 프리팹을 메모리에 비동기 로드
        // ShowSceneUI가 동기 Instantiate를 하기 때문에 로드가 선행되어야 합니다.
        await Managers.Resource.LoadAsync<GameObject>(_preloadData.gameSceneCanvas);

        // 3. UIManager를 통해 Scene UI 생성
        // @Canvas_Scene 하위로 자동 배치 및 SetCanvas 됨
        GameSceneCanvas ui = Managers.UI.ShowSceneUI<GameSceneCanvas>("GameSceneCanvas_New");

        // 4. Party Manager 연동
        if (ui != null)
        {
            ui.SetPartyManager();
        }
    }

    private async UniTask CreatePool()
    {
        if (_preloadData.bullet != null)
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.bullet);
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterAR != null)
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.monsterAR);
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterRL != null)
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.monsterRL);
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterTank != null)
        {
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.monsterTank);
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }
    }

    private async UniTask CreateMainVillage()
    {
        _map = await LoadAndSpawnAsync(_preloadData.mainVillage);
        if (_map != null)
        {
            _map.name = "@Map";
            _map.transform.position = Vector3.zero;
        }
    }

    private async UniTask CreateCharacters()
    {
        Transform spawnPoint = _map.GetComponent<BaseMap>().GetPlayerSpawnPoint();

        if (Managers.Party.GetMemeber() != null && Managers.Party.GetMemeber().Count > 0)
        {
            Managers.Party.ResetPartyForNewScene(spawnPoint);
            return;
        }

        Managers.Party.ClearParty();
        Transform container = Managers.Party.GetCharacterContainer();
        List<BaseCharacter> partyMembers = new List<BaseCharacter>();

        foreach (var data in Managers.Context.SelectedSchool.characters)
        {
            await LoadCharacterSequential(data, container, partyMembers, spawnPoint);
        }
        Managers.Party.Init(partyMembers, spawnPoint);
    }

    // [핵심 변경 6] 캐릭터 로드도 ResourceManager.LoadAsync 활용
    private async UniTask LoadCharacterSequential(CharacterDataSO data, Transform parent, List<BaseCharacter> list, Transform spawnPoint)
    {
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(data.inGamePrefab);

        if (prefab != null)
        {
            // 위치/회전 처리용 Instantiate 함수 호출
            GameObject characterGO = Managers.Resource.Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parent);

            BaseCharacter character = characterGO.GetComponent<BaseCharacter>();

            var savedData = Managers.Context.LoadCharacterStat(data.id);
            if (savedData != null) character.Stat.ApplyRuntimeData(savedData);

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

    // [핵심 변경 7] 동기 로드 함수(GetLoadedAsset) 완전 제거 및 비동기 LoadAsync 사용
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
        base.Clear();
        Managers.Sound.StopAll();
        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Sector.Clear();
    }

    // [핵심 변경 8] 코루틴(IEnumerator)에서 UniTaskVoid로 변경
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

    private void HandleEscape()
    {
        if (Managers.UI.IsPopupOpen)
        {
            Managers.UI.ClosePopupUI();
        }
    }
}
