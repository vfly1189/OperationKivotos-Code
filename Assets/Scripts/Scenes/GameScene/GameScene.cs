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
            _loadingCoverInstance = Managers.Resource.Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
            _loadingCoverInstance.GetComponent<LoadingSceneController>().SetValue(1f);
        }

        // [핵심 변경 2] UniTask.Delay 사용
        await UniTask.Delay(500);

        await CreatePool();

        // [핵심 변경 3] Task.WhenAll 대신 UniTask.WhenAll
        var mapTask = CreateMainVillage();
        await UniTask.WhenAll(mapTask);

        await CreateCharacters();
        await SetupUI();
        await CreateShopMaster();

        SetupCamera();
        SetupMainBGM();
        PlayMainBGM();

        StartCoroutine(FadeInSequence());

        Debug.Log("GameScene Init Complete");
    }

    // [핵심 변경 4] Task<GameObject> -> UniTask<GameObject>
    private async UniTask<GameObject> LoadAndSpawnAsync(AssetReferenceGameObject refObj, Transform parent = null)
    {
        if (refObj == null) return null;

        var handle = Addressables.LoadAssetAsync<GameObject>(refObj);
        GameObject prefab = await handle.ToUniTask();

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

    // [핵심 변경 5] Task -> UniTask
    private async UniTask SetupUI()
    {
        var existingUI = FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
        if (existingUI != null)
        {
            existingUI.gameObject.SetActive(true);
            existingUI.SetPartyManager();
            return;
        }

        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.gameSceneCanvas);
        await handle.ToUniTask(); // .ToUniTask() 사용

        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            GameObject ui = Instantiate(handle.Result);
            ui.name = "@GameSceneCanvas";
            DontDestroyOnLoad(ui);
            ui.GetComponent<GameSceneCanvas>()?.SetPartyManager();
        }
    }

    private async UniTask CreatePool()
    {
        if (_preloadData.bullet != null)
        {
            var h = Addressables.LoadAssetAsync<GameObject>(_preloadData.bullet);
            GameObject prefab = await h.ToUniTask();
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterAR != null)
        {
            var h = Addressables.LoadAssetAsync<GameObject>(_preloadData.monsterAR);
            GameObject prefab = await h.ToUniTask();
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterRL != null)
        {
            var h = Addressables.LoadAssetAsync<GameObject>(_preloadData.monsterRL);
            GameObject prefab = await h.ToUniTask();
            if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
        }

        if (_preloadData.monsterTank != null)
        {
            var h = Addressables.LoadAssetAsync<GameObject>(_preloadData.monsterTank);
            GameObject prefab = await h.ToUniTask();
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
        var handle = Addressables.LoadAssetAsync<GameObject>(data.inGamePrefab);
        await handle.ToUniTask();

        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            GameObject prefab = handle.Result;
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

    public void SetupMainBGM()
    {
        if (_preloadData.mainBGMs == null || _preloadData.mainBGMs.Length == 0) return;

        int rand = Random.Range(0, _preloadData.mainBGMs.Length);
        _mainBGM = Managers.Resource.GetLoadedAsset<AudioClip>(_preloadData.mainBGMs[rand]);
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

    IEnumerator FadeInSequence()
    {
        // ... 기존 코루틴 로직 동일 ...
        if (_loadingCoverInstance == null) yield break;

        CanvasGroup coverCG = _loadingCoverInstance.GetComponent<CanvasGroup>();
        if (coverCG == null)
        {
            Managers.Resource.Destroy(_loadingCoverInstance);
            yield break;
        }

        float timer = 0f;
        float duration = 0.5f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            coverCG.alpha = Mathf.Lerp(1f, 0f, timer / duration);
            yield return null;
        }
        Managers.Resource.Destroy(_loadingCoverInstance);
    }

    private void HandleEscape()
    {
        if (Managers.UI.IsPopupOpen)
        {
            Managers.UI.ClosePopupUI();
        }
    }
}
