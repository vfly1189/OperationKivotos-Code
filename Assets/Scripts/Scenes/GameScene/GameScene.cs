using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class GameScene : BaseScene
{
    //private enum ObjectType
    //{
    //    Character,
    //    UI,
    //    Map,
    //    BGM,
    //    Props
    //}

    // 씬이 관리하는 핵심 컨트롤러들
    //[SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결

    [SerializeField] private GameScenePreloadSO _preloadData;
    [SerializeField] private GameObject _loadingCover;

    private GameObject _loadingCoverInstance;
    private GameObject _map;
    private AudioClip _mainBGM;

    //private Dictionary<ObjectType, List<AsyncOperationHandle>> _spawnedHandles
    //= new Dictionary<ObjectType, List<AsyncOperationHandle>>();

    protected override async void Init()
    {
        base.Init();

        _sceneType = Define.Scene.Game;

        Managers.Input.OnEscapePressed -= HandleEscape;
        Managers.Input.OnEscapePressed += HandleEscape;

        // 로딩하는거 가려줄 화면
        if (_loadingCover != null)
        {
            _loadingCoverInstance = Object.Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            // 커버에도 CanvasGroup이 있어야 페이드 아웃 가능 (없으면 추가)
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
        }

        //await Task.Delay(1000);

        await CreatePool();

        var mapTask = CreateMainVillage();
       
        var bgmTask = SetupMainBGM();

        //위에 3개 끝날때까지 대기
        await Task.WhenAll(mapTask, bgmTask);

        // 포탈은 맵이 있어야됨
        //await CreatePortal();
        
        // 캐릭터 및 UI 생성
        await CreateCharacters();
        await SetupUI();
        await CreateShopMaster();

        SetupCamera();
        PlayMainBGM();

        StartCoroutine(FadeInSequence());
        
        Debug.Log("GameScene Init Complete");
    }

    // ========================================================================
    // [유틸리티] 중복 코드를 줄여주는 제네릭 로더
    // ========================================================================
    private async Task<GameObject> LoadAndSpawnAsync(AssetReferenceGameObject refObj, Transform parent = null)
    {
        if (refObj == null) return null;

        // 핸들 관리는 매니저가 하므로 씬에서는 결과물(GameObject)만 받아서 씁니다.
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

    async Task SetupUI()
    {
        // 1. 기존 UI 확인
        var existingUI = FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
        if (existingUI != null)
        {
            existingUI.gameObject.SetActive(true);
            existingUI.SetPartyManager();
            return;
        }

        // [주의 사항] GameSceneCanvas는 DontDestroyOnLoad(앱 종료 시까지 유지) 객체입니다.
        // ResourceManager에 핸들을 등록하면 씬 이동 시 Clear() 되면서 UI가 파괴될 수 있으므로,
        // 이 UI만큼은 예외적으로 직접 Addressables.LoadAssetAsync를 호출하여 독립적으로 로드합니다.
        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.gameSceneCanvas);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject ui = Instantiate(handle.Result);
            ui.name = "@GameSceneCanvas";
            DontDestroyOnLoad(ui);
            ui.GetComponent<GameSceneCanvas>()?.SetPartyManager();
        }
    }

    async Task CreatePool()
    {
        // PoolManager도 이제 핸들을 알 필요 없이 'GameObject 원본'만 받으면 됩니다.
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

    async System.Threading.Tasks.Task CreateMainVillage()
    {
        _map = await LoadAndSpawnAsync(_preloadData.mainVillage);
        if (_map != null)
        {
            _map.name = "@Map";
            _map.transform.position = Vector3.zero;
        }
    }
   
    async Task CreateCharacters()
    {
        Managers.Party.ClearParty();
        Transform container = Managers.Party.GetCharacterContainer();
        List<BaseCharacter> partyMembers = new List<BaseCharacter>();

        Transform spawnPoint = _map.GetComponent<BaseMap>().GetPlayerSpawnPoint();

        foreach (var data in Managers.Context.SelectedSchool.characters)
        {
            await LoadCharacterSequential(data, container, partyMembers, spawnPoint);
        }   
        Managers.Party.Init(partyMembers, spawnPoint);
    }

    // 캐릭터 개별 로딩 로직
    async Task LoadCharacterSequential(CharacterDataSO data, Transform parent, List<BaseCharacter> list, Transform spawnPoint)
    {  
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(data.inGamePrefab);

        if (prefab != null)
        {
            GameObject characterGO = Managers.Resource.Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parent);

            BaseCharacter character = characterGO.GetComponent<BaseCharacter>();

            var savedData = Managers.Context.LoadCharacterStat(data.id);
            if (savedData != null) character.Stat.ApplyRuntimeData(savedData);

            list.Add(character);
        }
    }

   
    async Task CreateShopMaster()
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

    void CreateEffectStage()
    {
        //GameObject root = new GameObject { name = "@Effect" };

        //if (_preloadData.effectStage != null)
        //{
        //    GameObject effectStage = Object.Instantiate(_preloadData.effectStage, root.transform);
        //    // 위치를 따로 잡고 싶다면 여기서 수정 (예: portalGroup 기준 상대 좌표)
        //    effectStage.SetActive(true);
        //}
    }

    async Task SetupMainBGM()
    {
        if (_preloadData.mainBGMs == null || _preloadData.mainBGMs.Length == 0) return;

        int rand = Random.Range(0, _preloadData.mainBGMs.Length);

        _mainBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.mainBGMs[rand]);
    }


    void SetupCamera()
    {
        // 1. 현재 파티의 리더(0번 캐릭터) 가져오기
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
    }

    IEnumerator FadeInSequence()
    {
        if (_loadingCoverInstance == null) yield break;

        CanvasGroup coverCG = _loadingCoverInstance.GetComponent<CanvasGroup>();
        if (coverCG == null)
        {
            Managers.Resource.Destroy(_loadingCoverInstance);
            yield break;
        }

        float timer = 0f;
        float duration = 0.5f; // 0.5초 동안 사라짐

        while (timer < duration)
        {
            timer += Time.deltaTime;
            // 1(불투명) -> 0(투명)으로 감소
            coverCG.alpha = Mathf.Lerp(1f, 0f, timer / duration);
            yield return null;
        }

        // 완전히 사라지면 삭제
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
