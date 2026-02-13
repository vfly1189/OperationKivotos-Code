using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class NormalDungeonScene : BaseScene
{
    private enum ObjectType
    {
        Map,
        Monster,
        UI,
        BGM,
        Pool
    }

    // 핸들 관리 (모델 포함)
    private Dictionary<ObjectType, List<AsyncOperationHandle>> _spawnedHandles
    = new Dictionary<ObjectType, List<AsyncOperationHandle>>();

    [SerializeField] private GameObject _loadingCover;
    [SerializeField] private NormalDungeonScenePreloadSO _preloadData;

    private int _remainingMonsters = 0;

    private GameObject _curMap;
    private GameObject _mainUI;
    private GameObject _loadingCoverInstance;
    private GameObject _clearUI;

    private AudioClip _successBGM;
    private AudioClip _mainBGM;
    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.NormalDungeon;


        // 로딩하는거 가려줄 화면
        if (_loadingCover != null)
        {
            _loadingCoverInstance = Object.Instantiate(_loadingCover);
            _loadingCover.SetActive(true);

            // 커버에도 CanvasGroup이 있어야 페이드 아웃 가능 (없으면 추가)
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
        }

        CreateUI();
        var mapTask = CreateMap();
        var poolTask =  CreatePool();
        var effectStageTask = CreateEffectStage();
        var bgmTask = SetupBGM();
        var clearUI = CreateClearUI();
        var successBgm = CreateSuccessBGM();

        await Task.WhenAll(mapTask, poolTask, effectStageTask, bgmTask, clearUI, successBgm);

        PlayBattleInVoice();
        PlayBGM();
        StartCoroutine(FadeInSequence());
    }

    async Task CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };
        AssetReferenceGameObject mapPrefab = null;

        // Enum에 따라 프리팹 선택
        switch (Managers.Context.SelectedDifficulty)
        {
            case Define.DungeonDifficulty.Easy:
                mapPrefab = _preloadData.normalDungeonEasy;
                break;
            case Define.DungeonDifficulty.Normal:
                mapPrefab = _preloadData.normalDungeonNormal;
                break;
            case Define.DungeonDifficulty.Hard:
                mapPrefab = _preloadData.normalDungeonHard;
                break;
        }

        if (mapPrefab != null)
        {
            //GameObject map = Object.Instantiate(mapPrefab, root.transform);
            //CountAndRegisterMonsters(map);
            //_curMap = map;
            //map.transform.position = Vector3.zero;

            //var handle = mapPrefab.InstantiateAsync();
            //await handle.Task;

            //if(handle.Status == AsyncOperationStatus.Succeeded)
            //{
            //    GameObject map = handle.Result;
            //    CountAndRegisterMonsters(map);
            //    _curMap = map;
            //    _curMap.transform.position = Vector3.zero;
            //    RegisterHandle(ObjectType.Map, handle);
            //}

            // [변경] InstantiateAsync -> LoadAssetAsync + Instantiate
            var handle = Addressables.LoadAssetAsync<GameObject>(mapPrefab);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _curMap = Object.Instantiate(handle.Result, root.transform); // 부모 설정
                _curMap.transform.position = Vector3.zero;

                CountAndRegisterMonsters(_curMap);
                RegisterHandle(ObjectType.Map, handle);
            }
        }
        else
        {
            Debug.LogError("맵 프리팹이 할당되지 않았습니다!");
        }
    }

    void CreateUI()
    {
        GameObject mainUI = GameObject.Find("@GameSceneCanvas");
        _mainUI = mainUI;
    }

    async Task CreateSuccessBGM()
    {
        //var handle = Addressables.LoadAssetAsync<AudioClip>(_preloadData.successBgm);


        //await handle.Task;

        //if (handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    _successBGM = Object.Instantiate(handle.Result);

        //    RegisterHandle(ObjectType.BGM, handle);
        //}

        if (_preloadData.successBgm == null) return;

        // [유지] Addressables.LoadAssetAsync 사용
        var handle = Addressables.LoadAssetAsync<AudioClip>(_preloadData.successBgm);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _successBGM = handle.Result; // Instantiate 불필요 (AudioClip은 리소스임)
            RegisterHandle(ObjectType.BGM, handle);
        }
    }

    async Task CreateClearUI()
    {
        //var handle = _preloadData.dungeonClearUI.InstantiateAsync();

        //await handle.Task;
        //if(handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    _clearUI = handle.Result;
        //    _clearUI.SetActive(false);

        //    RegisterHandle(ObjectType.UI, handle);
        //}

        if (_preloadData.dungeonClearUI == null) return;

        // [변경] InstantiateAsync -> LoadAssetAsync + Instantiate
        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.dungeonClearUI);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _clearUI = Object.Instantiate(handle.Result);
            _clearUI.SetActive(false);
            RegisterHandle(ObjectType.UI, handle);
        }
    }

    async Task SetupBGM()
    {
        ////int randNum = Random.Range(0, 2);

        ////Managers.Sound.Play(_preloadData.fightingBgms[randNum], Define.Sound.Bgm);

        //if (_preloadData.fightingBgms == null || _preloadData.fightingBgms.Length == 0) return;

        //int rand = Random.Range(0, _preloadData.fightingBgms.Length);
        //var bgmRef = _preloadData.fightingBgms[rand];

        //// BGM 로드 (Instantiate 아님!)
        //var handle = Addressables.LoadAssetAsync<AudioClip>(bgmRef);
        //await handle.Task;

        //if (handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    Managers.Sound.Play(handle.Result, Define.Sound.Bgm);

        //    // 핸들 저장
        //    RegisterHandle(ObjectType.BGM, handle);
        //}

        if (_preloadData.fightingBgms == null || _preloadData.fightingBgms.Length == 0) return;

        int rand = Random.Range(0, _preloadData.fightingBgms.Length);
        var bgmRef = _preloadData.fightingBgms[rand];

        // [유지] Addressables.LoadAssetAsync 사용
        var handle = Addressables.LoadAssetAsync<AudioClip>(bgmRef);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            //Managers.Sound.Play(handle.Result, Define.Sound.Bgm);
            _mainBGM = handle.Result;
            RegisterHandle(ObjectType.BGM, handle);
        }
    }
    void PlayBGM() { Managers.Sound.Play(_mainBGM, Define.Sound.Bgm); }

    void PlayBattleInVoice()
    {
        //List<BaseCharacter> partyMemebers = Managers.Party.GetMemeber();

        //int randomNum_partyMembers = Random.Range(0, 4);
        //AudioClip[] voices = partyMemebers[randomNum_partyMembers].Stat.GetBattleInVoice();

        //int randomNum_voice = Random.Range(0, 2);

        //Managers.Sound.Play(voices[randomNum_voice], Define.Sound.Effect);

        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
        if (partyMembers.Count == 0) return;

        int randomMemberIdx = Random.Range(0, partyMembers.Count);
        AudioClip[] voices = partyMembers[randomMemberIdx].Stat.GetBattleInVoice();

        if (voices != null && voices.Length > 0)
        {
            int randomVoiceIdx = Random.Range(0, voices.Length);
            Managers.Sound.Play(voices[randomVoiceIdx], Define.Sound.Voice);
        }
    }

    void PlayVictoryVoice()
    {
        List<BaseCharacter> partyMemebers = Managers.Party.GetMemeber();

        int randomNum_partyMembers = Random.Range(0, 4);
        AudioClip[] voices = partyMemebers[randomNum_partyMembers].Stat.GetBattleVictoryVoices();

        int randomNum_voice = Random.Range(0, 2);
        Debug.Log($"번호 : {randomNum_partyMembers} , {randomNum_voice}");
        Managers.Sound.Play(voices[randomNum_voice], Define.Sound.Voice);
    }

    async Task CreatePool()
    {
        //AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.bullet);

        //await handle.Task;

        //if(handle.Status == AsyncOperationStatus.Succeeded)
        //{
        //    Managers.Pool.CreatePool(handle.Result, handle, 30);
        //    //RegisterHandle(ObjectType.Pool, handle);


        //}

        ////총알
        ////Managers.Pool.CreatePool(_preloadData.bullet, 40);
        ////몬스터
        ////Managers.Pool.CreatePool(_preloadData.monsterAR, 20);


        if (_preloadData.bullet == null) return;

        // [유지] Addressables.LoadAssetAsync 사용 (PoolManager 전달용)
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.bullet);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            // PoolManager에 GameObject 원본과 Handle 전달
            Managers.Pool.CreatePool(handle.Result, handle, 30);

            // 주의: Handle을 씬에서 RegisterHandle 하지 않음 (PoolManager가 관리하므로)
        }
    }

    async Task CreateEffectStage()
    {
        //GameObject root = new GameObject { name = "@Effect" };

        //if (_preloadData.effectStage != null)
        //{
        //    //GameObject effectStage = Object.Instantiate(_preloadData.effectStage, root.transform);
        //    // 위치를 따로 잡고 싶다면 여기서 수정 (예: portalGroup 기준 상대 좌표)
        //    //effectStage.SetActive(true);

        //    var handle = _preloadData.effectStage.InstantiateAsync();

        //    await handle.Task;

        //    if(handle.Status == AsyncOperationStatus.Succeeded)
        //    {
        //        handle.Result.SetActive(true);
        //        handle.Result.transform.SetParent(root.transform);

        //        RegisterHandle(ObjectType.UI, handle);
        //    }
        //}

        if (_preloadData.effectStage == null) return;

        GameObject root = new GameObject { name = "@Effect" };

        // [변경] InstantiateAsync -> LoadAssetAsync + Instantiate
        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.effectStage);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject go = Object.Instantiate(handle.Result, root.transform);
            go.SetActive(true);
            RegisterHandle(ObjectType.UI, handle);
        }
    }

    void CountAndRegisterMonsters(GameObject map)
    {
        //// 만약 맵 전체를 뒤져야 한다면:
        //GameObject monstersRoot = GameObject.Find("Monsters");

        //if (monstersRoot == null)
        //{
        //    Debug.LogWarning("맵에 'Monsters' 오브젝트가 없습니다! 클리어 조건 확인 필요.");
        //    //DungeonClear(); // 몬스터가 없으면 바로 클리어 처리?
        //    return;
        //}

        //// 2. 자식으로 있는 모든 몬스터(MonsterController) 찾기
        //// GetComponentsInChildren을 쓰면 비활성화된 놈까지 찾을 수 있음(true 옵션)
        //MonsterController[] monsters = monstersRoot.GetComponentsInChildren<MonsterController>(true);

        //_remainingMonsters = monsters.Length;
        //Debug.Log($"총 몬스터 수: {_remainingMonsters}");

        //// 3. 각 몬스터에게 "죽으면 나한테 보고해"라고 이벤트 연결
        //foreach (var monster in monsters)
        //{
        //    // MonsterController나 그 Stat에 OnDead 이벤트가 있어야 함
        //    // 예: monster.Stat.OnDeadEvent += () => OnMonsterDead();

        //    // 만약 Stat이 Action<BaseCharacter> 형태라면:
        //    //monster.Stat.OnDead += (deadChar) => OnMonsterDead();
        //    monster.Stat.OnDead += OnMonsterDead;
        //}

        GameObject monstersRoot = null;

        // 1. 맵 루트 바로 아래에서 찾기 시도
        Transform t = map.transform.Find("Monsters");
        if (t != null) monstersRoot = t.gameObject;

        // 2. 없으면 전체 검색 (비효율적일 수 있으니 주의)
        if (monstersRoot == null) monstersRoot = GameObject.Find("Monsters");

        if (monstersRoot == null)
        {
            Debug.LogWarning("맵에 'Monsters' 오브젝트가 없습니다! 몬스터 0마리로 시작.");
            _remainingMonsters = 0;
            return;
        }

        MonsterController[] monsters = monstersRoot.GetComponentsInChildren<MonsterController>(true);
        _remainingMonsters = monsters.Length;
        Debug.Log($"총 몬스터 수: {_remainingMonsters}");

        foreach (var monster in monsters)
        {
            monster.Stat.OnDead -= OnMonsterDead; // 중복 방지
            monster.Stat.OnDead += OnMonsterDead;
        }
    }

    private void OnMonsterDead()
    {
        _remainingMonsters--;
        // ... 클리어 체크 ...
        Debug.Log($"남은 몬스터 수 : {_remainingMonsters}");
        if (_remainingMonsters <= 0)
        {
            Debug.Log("던전 클리어!");

            // 1. Party Time BGM 재생 (선택)
            Managers.Sound.Play(_successBGM, Define.Sound.Bgm); // 실제 BGM 이름이나 AudioClip 필요


            // 2. PartyManager에게 승리 통보 (이게 핵심)
            Managers.Party.FinishGame(true); // true = Success
            StartCoroutine(CoVictoryPoze());
        }
    }



    //private System.Collections.IEnumerator CoVictoryPoze()
    //{
    //    Managers.Party.PlayerController.VictoryTime = true; // 승리화면에서는 공격(좌클릭) 불가능하게
    //    yield return new WaitForSeconds(3.0f);

    //    //PlayVictoryVoice();

    //    if (_mainUI != null)
    //    {
    //        _mainUI.SetActive(false); // 파괴 대신 비활성화
    //    }

    //    // [중요] 일괄 활성화 함수(TurnOnAllMembers) 대신, 아래 루프에서 하나씩 정교하게 제어하는 것을 추천
    //    // PartyManager.Instance.TurnOnAllMembers(); (삭제)

    //    //GameObject clearUI = Object.Instantiate(_preloadData.dungeonClearUI);
    //    _clearUI.SetActive(true);


    //    // 1. 카메라 준비
    //    GameObject camObj = _curMap.GetComponent<NormalDungeonMap>().GetCameraPoint().gameObject;
    //    camObj.SetActive(true);
    //    // 카메라 줌 시작 (동시에)
    //    StartCoroutine(CoCameraZoomEffect(camObj.transform));

    //    Transform[] endingPositions = _curMap.GetComponent<NormalDungeonMap>().GetTransforms();
    //    List<BaseCharacter> characters = Managers.Party.GetMemeber();

    //    int index = 0;
    //    foreach (BaseCharacter character in characters)
    //    {
    //        // [단계 1] 활성화 먼저 (스크립트가 동작해야 애니메이션 등 변경 가능)
    //        // 단, NavMeshAgent가 켜져있으면 위치 이동을 막거나 튕겨낼 수 있으니 주의
    //        character.gameObject.SetActive(true);

    //        // [단계 2] 물리/AI 무력화 (이동 방지 및 위치 고정)
    //        // (BaseCharacter에 StopMove나 DisableAI 같은 함수가 있다면 활용)
    //        var agent = character.GetComponent<UnityEngine.AI.NavMeshAgent>();
    //        if (agent != null)
    //        {
    //            agent.enabled = false; // [핵심] NavMesh가 위치 강제 보정하는 것 차단
    //        }
    //        var rb = character.GetComponent<Rigidbody>();
    //        if (rb != null)
    //        {
    //            rb.isKinematic = true; // [핵심] 물리 충돌로 밀려나는 것 차단
    //        }

    //        // [단계 3] 위치 및 회전 강제 지정
    //        if (index < endingPositions.Length)
    //        {
    //            character.transform.position = endingPositions[index].position;

    //            // 회전
    //            Vector3 targetPos = camObj.transform.position;
    //            targetPos.y = character.transform.position.y;
    //            Vector3 dir = targetPos - character.transform.position;

    //            if (dir != Vector3.zero)
    //                character.transform.rotation = Quaternion.LookRotation(dir);

    //            index++;
    //        }

    //        // [단계 4] 승리 포즈 애니메이션 재생
    //        //character.ChangeState(BaseCharacter.PlayerState.Victory); //변경전
    //        character.Victory();
    //    }
    //}

    //private IEnumerator CoCameraZoomEffect(Transform camTr)
    //{
    //    float duration = 4.0f;
    //    float timer = 0f;
    //    Vector3 startPos = camTr.position;
    //    Vector3 targetPos = startPos + (camTr.forward * 2.0f);

    //    while (timer < duration)
    //    {
    //        timer += Time.deltaTime;

    //        // 0 ~ 1 사이의 선형 진행도
    //        float t = timer / duration;

    //        // [핵심] 3차 함수(Cubic) 공식을 이용한 Ease In Out 변환
    //        // t가 0.5보다 작을 땐 4*t^3 (서서히 가속)
    //        // t가 0.5보다 클 땐 뒤집어서 감속
    //        float easeT = (t < 0.5f)
    //            ? 4f * t * t * t
    //            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    //        camTr.position = Vector3.Lerp(startPos, targetPos, easeT);
    //        yield return null;
    //    }

    //    // 최종 위치 보정
    //    camTr.position = targetPos;
    //}

    private IEnumerator CoVictoryPoze()
    {
        if (Managers.Party.PlayerController != null)
            Managers.Party.PlayerController.VictoryTime = true;

        yield return new WaitForSeconds(3.0f);

        PlayVictoryVoice();

        if (_mainUI != null) _mainUI.SetActive(false);
        if (_clearUI != null) _clearUI.SetActive(true);

        if (_curMap != null)
        {
            var mapScript = _curMap.GetComponent<NormalDungeonMap>();
            if (mapScript != null)
            {
                // 카메라 연출
                GameObject camObj = mapScript.GetCameraPoint().gameObject;
                if (camObj != null)
                {
                    camObj.SetActive(true);
                    StartCoroutine(CoCameraZoomEffect(camObj.transform));
                }

                // 캐릭터 승리 포즈
                Transform[] endingPositions = mapScript.GetTransforms();
                List<BaseCharacter> characters = Managers.Party.GetMemeber();
                int index = 0;

                foreach (BaseCharacter character in characters)
                {
                    character.gameObject.SetActive(true);

                    // 물리/AI 끄기
                    var agent = character.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null) agent.enabled = false;

                    var rb = character.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    // 위치 이동
                    if (index < endingPositions.Length)
                    {
                        character.transform.position = endingPositions[index].position;

                        // 카메라 바라보기
                        if (camObj != null)
                        {
                            Vector3 targetPos = camObj.transform.position;
                            targetPos.y = character.transform.position.y;
                            Vector3 dir = targetPos - character.transform.position;
                            if (dir != Vector3.zero)
                                character.transform.rotation = Quaternion.LookRotation(dir);
                        }
                        index++;
                    }
                    character.Victory();
                }
            }
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
            float t = timer / duration;

            // EaseInOut Cubic
            float easeT = (t < 0.5f) ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            camTr.position = Vector3.Lerp(startPos, targetPos, easeT);
            yield return null;
        }
        camTr.position = targetPos;
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


    // 제네릭 핸들 저장 함수
    private void RegisterHandle(ObjectType type, AsyncOperationHandle handle)
    {
        if (!_spawnedHandles.ContainsKey(type))
            _spawnedHandles[type] = new List<AsyncOperationHandle>();
        _spawnedHandles[type].Add(handle);
    }

    public override void Clear()
    {
        base.Clear();

        // 씬 나갈 때 모든 핸들 해제
        foreach (var list in _spawnedHandles.Values)
        {
            foreach (var handle in list)
                if (handle.IsValid()) Addressables.Release(handle);
        }
        _spawnedHandles.Clear();
    }
}