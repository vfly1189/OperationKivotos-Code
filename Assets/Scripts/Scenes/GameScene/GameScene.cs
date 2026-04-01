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
        // Party.Init() 이후에 하는게 나음.
        // 현재 CreateCharacters에서 하고 있음.
        ApplySaveOrTestData(); 

        await SetupUI();
        await CreateShopMaster();

        

        SetupCamera();
        PlayMainBGM();

        FadeInSequence().Forget();

        await UI_LootNotification.PreloadAsync();
        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Input.OnEscapePressed += HandleEscape;

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
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterAR != null)
        {
            string addressableKey = Managers.Data.GetData<int, MonsterBaseData>(2000).AddressableKey;
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey);
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterRL != null)
        {
            string addressableKey = Managers.Data.GetData<int, MonsterBaseData>(2001).AddressableKey;
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey);
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterTank != null)
        {
            string addressableKey = Managers.Data.GetData<int, MonsterBaseData>(2002).AddressableKey;
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(addressableKey);
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

    private async UniTask LoadCharacterSequential(CharacterDataSO data, Transform parent, List<BaseCharacter> list, Transform spawnPoint)
    {
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(data.inGamePrefab);

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
        base.Clear();
        Managers.Sound.StopAll();
        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Sector.Clear();
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
            Debug.Log("[GameScene] 던전에서 귀환: 기존 메모리 상태 유지");

            // 주의: 파티 스폰 위치 등 물리적 리셋이 필요하다면 여기서 처리 (이미 CreateCharacters에서 하고 계시긴 합니다)
        }
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

    //private void HandleInfo()
    //{
    //    int index = Managers.Party.GetCurrentCharacterIndex();

    //    if(_prevIndex != index && _prevIndex != -1) 
    //        _infoModels[_prevIndex].SetActive(false);

    //    _infoModels[index].SetActive(true);

    //    _prevIndex = index;
    //}

    //private void HandleEscape()
    //{
    //    if (Managers.UI.IsPopupOpen)
    //    {
    //        Debug.Log("닫기 시작 ");
    //        Managers.UI.ClosePopupUI();
    //    }
    //    else
    //    {
    //        Debug.Log("또 열기");
    //        ShowEscapeMenu().Forget();
    //    }
    //}

    //// 버튼 클릭 등의 이벤트에서 비동기를 띄울 때는 async UniTaskVoid 사용
    //private async UniTaskVoid ShowEscapeMenu()
    //{
    //    //var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.exitPopup);
    //    UI_EscapeMenu popupPrefab = await Managers.UI.ShowPopupUIAsync<UI_EscapeMenu>("UI_EscapeMenu");
    //}
}
