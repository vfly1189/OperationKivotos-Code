using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
// [추가]
using Cysharp.Threading.Tasks;

public class NormalDungeonScene : BaseScene
{
    [SerializeField] private GameObject _loadingCover;
    [SerializeField] private NormalDungeonScenePreloadSO _preloadData;

    private int _remainingMonsters = 0;

    private GameObject _curMap;
    private GameObject _mainUI;
    private GameObject _loadingCoverInstance;
    private GameObject _clearUI;

    private AudioClip _battleInVoice;
    private AudioClip _victoryVoice;
    private AudioClip _successBGM;
    private AudioClip _mainBGM;

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

        await UniTask.Delay(500); // Task.Delay -> UniTask.Delay

        CreateUI();

        var mapTask = CreateMap();
        var poolTask = CreatePool();
        var effectStageTask = CreateEffectStage();
        var clearUI = CreateClearUI();
        var successBgm = CreateSuccessBGM();
        var victoryVoice = LoadVictoryVoice();
        var battleInVoice = LoadBattleInVoice();

        // UniTask.WhenAll 로 병렬 대기
        await UniTask.WhenAll(mapTask, poolTask, effectStageTask, clearUI, successBgm, victoryVoice, battleInVoice);

        SetupBGM();
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
            case Define.DungeonDifficulty.Easy: mapPrefabRef = _preloadData.normalDungeonEasy; break;
            case Define.DungeonDifficulty.Normal: mapPrefabRef = _preloadData.normalDungeonNormal; break;
            case Define.DungeonDifficulty.Hard: mapPrefabRef = _preloadData.normalDungeonHard; break;
        }

        if (mapPrefabRef != null)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(mapPrefabRef);
            GameObject mapPrefab = await handle.ToUniTask();
            if (mapPrefab != null)
            {
                _curMap = Instantiate(mapPrefab, root.transform);
                _curMap.transform.position = Vector3.zero;
                CountAndRegisterMonsters(_curMap);
            }
        }
    }

    void CreateUI() => _mainUI = GameObject.Find("@GameSceneCanvas");

    async UniTask CreateSuccessBGM()
    {
        if (_preloadData.successBgm == null) return;
        var handle = Addressables.LoadAssetAsync<AudioClip>(_preloadData.successBgm);
        _successBGM = await handle.ToUniTask();
    }

    async UniTask CreateClearUI()
    {
        if (_preloadData.dungeonClearUI == null) return;
        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.dungeonClearUI);
        GameObject prefab = await handle.ToUniTask();
        if (prefab != null)
        {
            _clearUI = Instantiate(prefab);
            _clearUI.SetActive(false);
        }
    }

    public void SetupBGM()
    {
        if (_preloadData.fightingBgms == null || _preloadData.fightingBgms.Length == 0) return;
        int rand = Random.Range(0, _preloadData.fightingBgms.Length);
        _mainBGM = Managers.Resource.GetLoadedAsset<AudioClip>(_preloadData.fightingBgms[rand]);
    }

    void PlayBGM()
    {
        if (_mainBGM != null) Managers.Sound.Play(_mainBGM, Define.Sound.Bgm);
    }

    void PlayBattleInVoice()
    {
        if (_battleInVoice != null) Managers.Sound.Play(_battleInVoice, Define.Sound.Voice);
    }

    void PlayVictoryVoice()
    {
        if (_victoryVoice != null) Managers.Sound.Play(_victoryVoice, Define.Sound.Voice);
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
                var handle = Addressables.LoadAssetAsync<AudioClip>(voiceRef);
                _battleInVoice = await handle.ToUniTask();
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
                var handle = Addressables.LoadAssetAsync<AudioClip>(voiceRef);
                _victoryVoice = await handle.ToUniTask();
            }
        }
    }

    async UniTask CreatePool()
    {
        if (_preloadData.bullet == null) return;
        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.bullet);
        GameObject prefab = await handle.ToUniTask();
        if (prefab != null) Managers.Pool.CreatePool(prefab, 30);
    }

    async UniTask CreateEffectStage()
    {
        if (_preloadData.effectStage == null) return;
        GameObject root = new GameObject { name = "@Effect" };
        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.effectStage);
        GameObject prefab = await handle.ToUniTask();

        if (prefab != null)
        {
            GameObject go = Instantiate(prefab, root.transform);
            go.SetActive(true);
        }
    }

    void CountAndRegisterMonsters(GameObject map)
    {
        GameObject monstersRoot = null;
        Transform t = map.transform.Find("Monsters");
        if (t != null) monstersRoot = t.gameObject;
        if (monstersRoot == null) monstersRoot = GameObject.Find("Monsters");

        if (monstersRoot == null)
        {
            _remainingMonsters = 0;
            return;
        }

        MonsterController[] monsters = monstersRoot.GetComponentsInChildren<MonsterController>(true);
        _remainingMonsters = monsters.Length;

        foreach (var monster in monsters)
        {
            monster.Stat.OnDead -= OnMonsterDead;
            monster.Stat.OnDead += OnMonsterDead;
        }
    }

    private void OnMonsterDead()
    {
        _remainingMonsters--;
        if (_remainingMonsters <= 0)
        {
            Managers.Sound.Play(_successBGM, Define.Sound.Bgm);
            Managers.Party.FinishGame(true);
            CoVictoryPoze().Forget(); // UniTaskVoid 호출
        }
    }

    private async UniTaskVoid CoVictoryPoze()
    {
        if (Managers.Party.PlayerController != null)
            Managers.Party.PlayerController.VictoryTime = true;

        await UniTask.Delay(System.TimeSpan.FromSeconds(3.0));

        PlayVictoryVoice();

        if (_mainUI != null) _mainUI.SetActive(false);
        if (_clearUI != null) _clearUI.SetActive(true);

        if (_curMap != null)
        {
            var mapScript = _curMap.GetComponent<NormalDungeonMap>();
            if (mapScript != null)
            {
                GameObject camObj = mapScript.GetCameraPoint().gameObject;
                if (camObj != null)
                {
                    camObj.SetActive(true);
                    CoCameraZoomEffect(camObj.transform).Forget();
                }

                Transform[] endingPositions = mapScript.GetTransforms();
                List<BaseCharacter> characters = Managers.Party.GetMemeber();
                int index = 0;

                foreach (BaseCharacter character in characters)
                {
                    character.gameObject.SetActive(true);

                    var agent = character.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null) agent.enabled = false;

                    var rb = character.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    if (index < endingPositions.Length)
                    {
                        character.transform.position = endingPositions[index].position;

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

    private async UniTaskVoid CoCameraZoomEffect(Transform camTr)
    {
        float duration = 4.0f;
        float timer = 0f;
        Vector3 startPos = camTr.position;
        Vector3 targetPos = startPos + (camTr.forward * 2.0f);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            float easeT = (t < 0.5f) ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            camTr.position = Vector3.Lerp(startPos, targetPos, easeT);
            await UniTask.Yield();
        }
        camTr.position = targetPos;
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
