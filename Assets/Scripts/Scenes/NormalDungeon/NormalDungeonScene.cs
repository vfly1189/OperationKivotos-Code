using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class NormalDungeonScene : BaseScene
{
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

        Managers.Party.TeleportParty(new Vector3(0, 0, 0));

        // 로딩하는거 가려줄 화면
        if (_loadingCover != null)
        {
            _loadingCoverInstance = Object.Instantiate(_loadingCover);
            _loadingCover.SetActive(true);

            // 커버에도 CanvasGroup이 있어야 페이드 아웃 가능 (없으면 추가)
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
        }

        await Task.Delay(1000);

        CreateUI();

        var mapTask = CreateMap();
        var poolTask =  CreatePool();
        var effectStageTask = CreateEffectStage();
        var bgmTask = SetupBGM();
        var clearUI = CreateClearUI();
        var successBgm = CreateSuccessBGM();

        await Task.WhenAll(mapTask, poolTask, bgmTask, effectStageTask, clearUI, successBgm);

        PlayBattleInVoice();
        PlayBGM();
        StartCoroutine(FadeInSequence());
    }

    async Task CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };
        AssetReferenceGameObject mapPrefabRef = null;

        // 난이도에 따른 맵 프리팹 선택
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

                CountAndRegisterMonsters(_curMap);
            }
        }
        else
        {
            Debug.LogError("맵 프리팹이 할당되지 않았습니다!");
        }
    }

    void CreateUI()
    {
        _mainUI = GameObject.Find("@GameSceneCanvas");
    }

    async Task CreateSuccessBGM()
    {
        if (_preloadData.successBgm == null) return;
        _successBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.successBgm);
    }

    async Task CreateClearUI()
    {
        if (_preloadData.dungeonClearUI == null) return;

        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.dungeonClearUI);
        if (prefab != null)
        {
            _clearUI = Instantiate(prefab);
            _clearUI.SetActive(false);
        }
    }

    async Task SetupBGM()
    {
        if (_preloadData.fightingBgms == null || _preloadData.fightingBgms.Length == 0) return;

        int rand = Random.Range(0, _preloadData.fightingBgms.Length);
        _mainBGM = await Managers.Resource.LoadAsync<AudioClip>(_preloadData.fightingBgms[rand]);
    }

    void PlayBGM()
    {
        if (_mainBGM != null)
            Managers.Sound.Play(_mainBGM, Define.Sound.Bgm);
    }

    void PlayBattleInVoice()
    {
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
        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
        if (partyMembers.Count == 0) return;

        int randomMemberIdx = Random.Range(0, partyMembers.Count);
        AudioClip[] voices = partyMembers[randomMemberIdx].Stat.GetBattleVictoryVoices();

        if (voices != null && voices.Length > 0)
        {
            int randomVoiceIdx = Random.Range(0, voices.Length);
            Managers.Sound.Play(voices[randomVoiceIdx], Define.Sound.Voice);
        }
    }

    async Task CreatePool()
    {
        if (_preloadData.bullet == null) return;

        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.bullet);
        if (prefab != null)
        {
            Managers.Pool.CreatePool(prefab, 30);
        }
    }

    async Task CreateEffectStage()
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

    void CountAndRegisterMonsters(GameObject map)
    {      
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


    public override void Clear()
    {
        base.Clear();
        Managers.Sound.StopAll();

        // 씬이 강제로 종료될 경우(중도 포기 등)를 대비해 남아있는 몬스터들의 이벤트 구독 해제
        if (_curMap != null)
        {
            MonsterController[] monsters = _curMap.GetComponentsInChildren<MonsterController>(true);
            foreach (var monster in monsters)
            {
                if (monster != null && monster.Stat != null)
                {
                    monster.Stat.OnDead -= OnMonsterDead;
                }
            }
        }
    }
}