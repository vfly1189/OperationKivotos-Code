using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class GameScene : BaseScene
{
    private enum ObjectType
    {
        Character,
        UI,
        Map,
        BGM,
        Props
    }

    // 씬이 관리하는 핵심 컨트롤러들
    //[SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결

    [SerializeField] private GameScenePreloadSO _preloadData;
    [SerializeField] private GameObject _loadingCover;

    private GameObject _loadingCoverInstance;
    private GameObject _map;
    private AudioClip _mainBGM;

    private Dictionary<ObjectType, List<AsyncOperationHandle>> _spawnedHandles
    = new Dictionary<ObjectType, List<AsyncOperationHandle>>();

    protected override async void Init()
    {
        base.Init();

        _sceneType = Define.Scene.Game;

        // 로딩하는거 가려줄 화면
        if (_loadingCover != null)
        {
            _loadingCoverInstance = Object.Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            // 커버에도 CanvasGroup이 있어야 페이드 아웃 가능 (없으면 추가)
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
        }

        var mapTask = CreateMainVillage();
        var shopTask = CreateShopMaster();
        var bgmTask = SetupMainBGM();

        //위에 3개 끝날때까지 대기
        await Task.WhenAll(mapTask, shopTask, bgmTask);

        // 포탈은 맵이 있어야됨
        await CreatePortal();
        
        // 캐릭터 및 UI 생성
        await CreateCharacters();
        await SetupUI();

        SetupCamera();
        StartCoroutine(FadeInSequence());
        PlayMainBGM();
    }

    // ========================================================================
    // [유틸리티] 중복 코드를 줄여주는 제네릭 로더
    // ========================================================================
    private async Task<GameObject> LoadAndSpawnAsync(AssetReferenceGameObject refObj, ObjectType type, Transform parent = null)
    {
        if (refObj == null) return null;

        // [변경] InstantiateAsync -> LoadAssetAsync
        var handle = Addressables.LoadAssetAsync<GameObject>(refObj);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            // Instantiate 실행
            GameObject go = Object.Instantiate(handle.Result, parent);

            // 핸들 저장 (Clear에서 해제용)
            if (!_spawnedHandles.ContainsKey(type))
                _spawnedHandles[type] = new List<AsyncOperationHandle>();
            _spawnedHandles[type].Add(handle);

            return go;
        }
        return null;
    }

    void PlayMainBGM() { Managers.Sound.Play(_mainBGM, Define.Sound.Bgm); }
    async Task SetupUI()
    {
        //// 이미 존재하는지 확인
        //GameSceneCanvas existingUI = Object.FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
        //if (existingUI != null)
        //{
        //    existingUI.gameObject.SetActive(true); // ← 추가: 던전에서 숨긴 경우 다시 활성화
        //    GameSceneCanvas canvas = existingUI.GetComponent<GameSceneCanvas>();
        //    canvas.SetPartyManager();
        //    return;
        //}

        //if (!_spawnedHandles.ContainsKey(ObjectType.UI))
        //    _spawnedHandles[ObjectType.UI] = new List<AsyncOperationHandle>();

        //var handle = _preloadData.gameSceneCanvas.InstantiateAsync();

        //await handle.Task;

        ////// 없으면 새로 생성
        ////GameObject mainUI = Object.Instantiate(_preloadData.gameSceneCanvas);
        ////mainUI.name = "@GameSceneCanvas";
        ////DontDestroyOnLoad(mainUI); // DDOL 적용

        ////GameSceneCanvas newCanvas = mainUI.GetComponent<GameSceneCanvas>();
        ////if (newCanvas != null)
        ////{
        ////    newCanvas.SetPartyManager();
        ////}

        //if(handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    GameObject mainUI = handle.Result;
        //    mainUI.name = "@GameSceneCanvas";
        //    DontDestroyOnLoad(mainUI); // DDOL 적용

        //    GameSceneCanvas newCanvas = mainUI.GetComponent<GameSceneCanvas>();
        //    if (newCanvas != null)
        //    {
        //        newCanvas.SetPartyManager();
        //    }

        //    _spawnedHandles[ObjectType.UI].Add(handle);
        //}



        // 1. 기존 UI 확인
        var existingUI = FindAnyObjectByType<GameSceneCanvas>(FindObjectsInactive.Include);
        if (existingUI != null)
        {
            existingUI.gameObject.SetActive(true);
            existingUI.SetPartyManager();
            return;
        }

        //// 2. 없으면 새로 생성
        //var handle = _preloadData.gameSceneCanvas.LoadAssetAsync();
        //await handle.Task;

        //if (handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    GameObject ui = Object.Instantiate(handle.Result);
        //    ui.name = "@GameSceneCanvas";
        //    DontDestroyOnLoad(ui);

        //    ui.GetComponent<GameSceneCanvas>()?.SetPartyManager();

        //    // 프리팹 에셋만 유지, GameObject는 일반 오브젝트로 관리
        //    Addressables.Release(handle); // 안전하게 release 가능
        //}


        // 2. 없으면 새로 생성 (LoadAndSpawnAsync 사용)
        GameObject ui = await LoadAndSpawnAsync(_preloadData.gameSceneCanvas, ObjectType.UI);
        if (ui != null)
        {
            ui.name = "@GameSceneCanvas";
            DontDestroyOnLoad(ui);
            ui.GetComponent<GameSceneCanvas>()?.SetPartyManager();
        }
    }

    async System.Threading.Tasks.Task CreateMainVillage()
    {
        //_map = Object.Instantiate(_preloadData.mainVillage);
        //if (_map != null) _map.name = "@Map";

        //_map.gameObject.transform.position = Vector3.zero;



        //if(!_spawnedHandles.ContainsKey(ObjectType.Map))
        //    _spawnedHandles[ObjectType.Map] = new List<AsyncOperationHandle>();

        //var handle = _preloadData.mainVillage.InstantiateAsync();

        //await handle.Task;

        //if(handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    _map = handle.Result;

        //    if (_map != null) _map.name = "@Map";
        //    _map.gameObject.transform.position = Vector3.zero;

        //    _spawnedHandles[ObjectType.Map].Add(handle);
        //}



        _map = await LoadAndSpawnAsync(_preloadData.mainVillage, ObjectType.Map);
        if (_map != null)
        {
            _map.name = "@Map";
            _map.transform.position = Vector3.zero;
        }
    }

    //async System.Threading.Tasks.Task CreateCharacters()
    //{
    //    Managers.Party.ClearParty();

    //    CharacterDataSO[] charactersToSpawn = Managers.Context.SelectedSchool.characters;
    //    List<BaseCharacter> partyMembers = new List<BaseCharacter>();

    //    // [핵심] PartyManager의 전용 컨테이너 사용
    //    Transform partyContainer = Managers.Party.GetCharacterContainer();

    //    // 아직 로드 안 된 학교라면 리스트 초기화
    //    if (!_spawnedHandles.ContainsKey(ObjectType.Character))
    //        _spawnedHandles[ObjectType.Character] = new List<AsyncOperationHandle>();

    //    for (int i = 0; i < charactersToSpawn.Length; i++)
    //    {
    //        //GameObject go = Object.Instantiate(charactersToSpawn[i].inGamePrefab);
    //        var handle = charactersToSpawn[i].inGamePrefab.InstantiateAsync();

    //        await handle.Task;

    //        //BaseCharacter character = go.GetComponent<BaseCharacter>();
    //        //character.Init();

    //        //CharacterRuntimeData savedData = Managers.Context.LoadCharacterStat(charactersToSpawn[i].id);
    //        //if (savedData != null)
    //        //{
    //        //    character.Stat.ApplyRuntimeData(savedData);
    //        //}

    //        //partyMembers.Add(character);

    //        //// 파티 전용 컨테이너의 자식으로 설정
    //        //go.transform.SetParent(partyContainer);
    //        if (handle.Status == AsyncOperationStatus.Succeeded)
    //        {
    //            BaseCharacter character = handle.Result.GetComponent<BaseCharacter>();
    //            character.Init();

    //            CharacterRuntimeData savedData = Managers.Context.LoadCharacterStat(charactersToSpawn[i].id);
    //            if (savedData != null)
    //            {
    //                character.Stat.ApplyRuntimeData(savedData);
    //            }

    //            partyMembers.Add(character);

    //            // 파티 전용 컨테이너의 자식으로 설정
    //            handle.Result.transform.SetParent(partyContainer);

    //            // 핸들 저장 (나중에 Release 하기 위해)
    //            _spawnedHandles[ObjectType.Character].Add(handle);
    //        }
    //    }

    //    Managers.Party.Init(partyMembers);
    //}

    async Task CreateCharacters()
    {
        Managers.Party.ClearParty();
        Transform container = Managers.Party.GetCharacterContainer();
        List<BaseCharacter> partyMembers = new List<BaseCharacter>();
        //List<Task> tasks = new List<Task>();

        foreach (var data in Managers.Context.SelectedSchool.characters)
        {
            //tasks.Add(LoadCharacterInternal(data, container, partyMembers));
            await LoadCharacterSequential(data, container, partyMembers);
        }

        //await Task.WhenAll(tasks);
        Managers.Party.Init(partyMembers);
    }

    // 캐릭터 개별 로딩 로직
    async Task LoadCharacterSequential(CharacterDataSO data, Transform parent, List<BaseCharacter> list)
    {
        // [변경] InstantiateAsync -> LoadAssetAsync
        var handle = Addressables.LoadAssetAsync<GameObject>(data.inGamePrefab);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject characterGO = Object.Instantiate(handle.Result, parent);
            BaseCharacter character = characterGO.GetComponent<BaseCharacter>();        
            character.Init();

            // 데이터 로드
            var savedData = Managers.Context.LoadCharacterStat(data.id);
            if (savedData != null) character.Stat.ApplyRuntimeData(savedData);

            // 리스트 추가
            list.Add(character);

            // 핸들 저장 (중요: 캐릭터 핸들은 보관해야 나중에 해제 가능)
            if (!_spawnedHandles.ContainsKey(ObjectType.Character))
                _spawnedHandles[ObjectType.Character] = new List<AsyncOperationHandle>();
            _spawnedHandles[ObjectType.Character].Add(handle);

            //Addressables.Release(handle);

            //// 핸들 저장
            //if (!_spawnedHandles.ContainsKey(ObjectType.Character))
            //    _spawnedHandles[ObjectType.Character] = new List<AsyncOperationHandle>();
            //_spawnedHandles[ObjectType.Character].Add(handle);
        }
    }

    async System.Threading.Tasks.Task CreatePortal()
    { 
        //if (_map != null)
        //    portalGroup.transform.SetParent(_map.transform);

        //// 1. 포탈 그룹 생성 (Map 밑에)
        //GameObject portalGroup = new GameObject("Portals");

        //if (_map != null)
        //    portalGroup.transform.SetParent(_map.transform);

        //// 2. 포탈 생성 (Instantiate의 2번째 인자로 부모 지정)

        //// 노말 던전 포탈
        //if (_preloadData.normalDungeonPortal != null)
        //{
        //    GameObject normalPortal = Object.Instantiate(_preloadData.normalDungeonPortal, portalGroup.transform);
        //    // 위치를 따로 잡고 싶다면 여기서 수정 (예: portalGroup 기준 상대 좌표)
        //    normalPortal.transform.localPosition = new Vector3(-3, 1, 0);
        //}

        //// 보스 던전 포탈
        //if (_preloadData.bossDungeonPortal != null)
        //{
        //    GameObject bossPortal = Object.Instantiate(_preloadData.bossDungeonPortal, portalGroup.transform);
        //    // 위치 수정
        //    bossPortal.transform.localPosition = new Vector3(3, 1, 0);
        //}




        //if (!_spawnedHandles.ContainsKey(ObjectType.Map))
        //    _spawnedHandles[ObjectType.Map] = new List<AsyncOperationHandle>();

        //if(_preloadData.normalDungeonPortal != null)
        //{
        //    var handle = _preloadData.normalDungeonPortal.InstantiateAsync();
            
        //    await handle.Task;

        //    GameObject normalDungeonPortal = handle.Result;
        //    normalDungeonPortal.transform.localPosition = new Vector3(-3, 1, 0);
        //    _spawnedHandles[ObjectType.Map].Add(handle);
        //}

        //if(_preloadData.bossDungeonPortal != null)
        //{
        //    var handle = _preloadData.bossDungeonPortal.InstantiateAsync();
            
        //    await handle.Task;

        //    GameObject bossDungeonPortal = handle.Result;
        //    bossDungeonPortal.transform.localPosition = new Vector3(3, 1, 0);
        //    _spawnedHandles[ObjectType.Map].Add(handle);
        //}




        if (_map == null) return;

        // 포탈 그룹 생성
        GameObject portalGroup = new GameObject("Portals");
        portalGroup.transform.SetParent(_map.transform);
        portalGroup.transform.localPosition = Vector3.zero;

        // 병렬로 포탈 생성
        var t1 = LoadAndSpawnAsync(_preloadData.normalDungeonPortal, ObjectType.Map, portalGroup.transform);
        var t2 = LoadAndSpawnAsync(_preloadData.bossDungeonPortal, ObjectType.Map, portalGroup.transform);

        await Task.WhenAll(t1, t2);

        // 위치 설정 (결과가 null이 아닐 때만)
        if (t1.Result != null) t1.Result.transform.localPosition = new Vector3(-3, 1, 0);
        if (t2.Result != null) t2.Result.transform.localPosition = new Vector3(3, 1, 0);
    }

    async Task CreateShopMaster()
    {
        //if (!_spawnedHandles.ContainsKey(ObjectType.Character))
        //    _spawnedHandles[ObjectType.Character] = new List<AsyncOperationHandle>();

        //var handle = _preloadData.shopMaster.InstantiateAsync();
        //await handle.Task;

        //if(handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    GameObject root = new GameObject { name = "@Shop" };

        //    if (_preloadData.shopMaster != null)
        //    {
        //        GameObject shopMaster = handle.Result;
        //        shopMaster.transform.SetParent(root.transform);
        //        // 위치를 따로 잡고 싶다면 여기서 수정 (예: portalGroup 기준 상대 좌표)
        //        shopMaster.transform.localPosition = new Vector3(0, 0, 2);
        //        shopMaster.transform.localRotation = Quaternion.Euler(0, 180, 0);
        //    }
        //}

        //GameObject root = new GameObject { name = "@Shop" };

        //if (_preloadData.shopMaster != null)
        //{
        //    GameObject shopMaster = Object.Instantiate(_preloadData.shopMaster, root.transform);
        //    // 위치를 따로 잡고 싶다면 여기서 수정 (예: portalGroup 기준 상대 좌표)
        //    shopMaster.transform.localPosition = new Vector3(0, 0, 2);
        //    shopMaster.transform.localRotation = Quaternion.Euler(0, 180, 0);
        //}


        GameObject shop = await LoadAndSpawnAsync(_preloadData.shopMaster, ObjectType.Character);
        if (shop != null)
        {
            GameObject root = new GameObject("@Shop");
            shop.transform.SetParent(root.transform);
            shop.transform.localPosition = new Vector3(0, 0, 2);
            shop.transform.localRotation = Quaternion.Euler(0, 180, 0);
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
        //GameObject root = new GameObject { name = "@BGM" };

        //if (_preloadData.mainBGMs != null)
        //{
        //    int rand = Random.Range(0, 2);

        //    //Managers.Sound.Play(_preloadData.mainBGMs[rand], Define.Sound.Bgm);
        //}


        //if (!_spawnedHandles.ContainsKey(ObjectType.BGM))
        //    _spawnedHandles[ObjectType.BGM] = new List<AsyncOperationHandle>();

        //if (_preloadData.mainBGMs != null && _preloadData.mainBGMs.Length > 0)
        //{
        //    int rand = Random.Range(0, _preloadData.mainBGMs.Length);
        //    var bgmRef = _preloadData.mainBGMs[rand];

        //    // 3. [핵심] 오디오 클립 비동기 로드 (메모리에 올림)
        //    var handle = bgmRef.LoadAssetAsync();

        //    await handle.Task; // 로딩 대기

        //    if (handle.Status == AsyncOperationStatus.Succeeded)
        //    {
        //        AudioClip clip = handle.Result;

        //        // 4. 사운드 매니저로 재생
        //        Managers.Sound.Play(clip, Define.Sound.Bgm);
        //    }
        //}


        if (_preloadData.mainBGMs == null || _preloadData.mainBGMs.Length == 0) return;

        int rand = Random.Range(0, _preloadData.mainBGMs.Length);
        var bgmRef = _preloadData.mainBGMs[rand];

        // [변경] Addressables.LoadAssetAsync 사용
        var handle = Addressables.LoadAssetAsync<AudioClip>(bgmRef);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _mainBGM = handle.Result;
            //Managers.Sound.Play(handle.Result, Define.Sound.Bgm);

            // 핸들 저장
            if (!_spawnedHandles.ContainsKey(ObjectType.BGM))
                _spawnedHandles[ObjectType.BGM] = new List<AsyncOperationHandle>();
            _spawnedHandles[ObjectType.BGM].Add(handle);
        }
    }


    void SetupCamera()
    {
        // 1. 현재 파티의 리더(0번 캐릭터) 가져오기
        BaseCharacter leader = Managers.Party.GetCurrentCharacter();

        Camera.main.GetComponent<CameraController>().SetTarget(leader.gameObject);
    }


    public override void Clear()
    {
        base.Clear();
        Managers.Sound.StopAll();

        // [중요] 모든 핸들 해제 (메모리 정리)
        foreach (var list in _spawnedHandles.Values)
        {
            foreach (var handle in list)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }
        _spawnedHandles.Clear();
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
}
