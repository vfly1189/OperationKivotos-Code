using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
// [추가] UniTask 네임스페이스
using Cysharp.Threading.Tasks;

public class BossDungeonScene : BaseScene
{
    [SerializeField] private BossDungeonScenePreloadSO _preloadData;
    [SerializeField] private GameObject _loadingCover;

    private GameObject _boss;
    private GameObject _curMap;
    private GameObject _mainUI;
    private GameObject _bossHPBar;
    private GameObject _loadingCoverInstance;
    private GameObject _clearUI;

    private Transform _spawnPoint;
    private Transform _cameraPoint;
    private Transform _bossSpawnPoint;

    private AudioClip _mainBGM;
    private AudioClip _successBGM;

    // [핵심 1] async UniTaskVoid 로 선언
    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.BossDungeon;

        if (_loadingCover != null)
        {
            _loadingCoverInstance = Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
            _loadingCoverInstance.GetComponent<LoadingSceneController>().SetValue(1f);
        }

        // [핵심 2] UniTask.Delay 사용
        await UniTask.Delay(1000);

        await CreateMap();
        CreateUI();
        SetupBGM();

        var poolTask = CreatePool();
        var effectTask = CreateEffectStage();
        var successBgmTask = CreateSuccessBGM();
        var clearUITask = CreateClearUI();

        await CreateBoss();
        await CreateBossHPBarUI();

        // [핵심 3] UniTask.WhenAll 사용
        await UniTask.WhenAll(poolTask, effectTask, successBgmTask, clearUITask);

        SetupBGM();
        PlayBGM();
        PlayBattleInVoice().Forget(); // Fire and Forget

        // [핵심 4] 코루틴들을 통일성을 위해 UniTask로 변경하여 await
        CoSafeTeleport().Forget();
        FadeInSequence().Forget();
    }

    // [핵심 5] 모든 Task 반환형을 UniTask로 변경
    async UniTask CreateMap()
    {
        GameObject root = new GameObject { name = "@Map" };
        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.bossDungeon);
        GameObject prefab = await handle.ToUniTask();

        if (prefab != null)
        {
            _curMap = Instantiate(prefab, root.transform);
            _curMap.transform.position = Vector3.zero;

            var mapScript = _curMap.GetComponent<BossDungeonMap>();
            if (mapScript != null)
            {
                _spawnPoint = mapScript.GetCharacterSpawnPoint();
                _cameraPoint = mapScript.GetCameraPoint();
                _bossSpawnPoint = mapScript.GetBossSpawnPoint();
            }
        }
    }

    async UniTask CreateSuccessBGM()
    {
        if (_preloadData.successBgm == null) return;
        var handle = Addressables.LoadAssetAsync<AudioClip>(_preloadData.successBgm);
        _successBGM = await handle.ToUniTask();
    }

    void CreateUI() => _mainUI = GameObject.Find("@GameSceneCanvas");

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

    async UniTaskVoid PlayBattleInVoice()
    {
        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
        if (partyMembers.Count == 0) return;

        int randomNum = Random.Range(0, partyMembers.Count);
        AssetReferenceT<AudioClip>[] voices = partyMembers[randomNum].Stat.GetBattleInVoice();

        if (voices != null && voices.Length > 0)
        {
            var voiceRef = voices[Random.Range(0, voices.Length)];
            if (voiceRef != null && voiceRef.RuntimeKeyIsValid())
            {
                var handle = Addressables.LoadAssetAsync<AudioClip>(voiceRef);
                AudioClip clip = await handle.ToUniTask();
                if (clip != null) Managers.Sound.Play(clip, Define.Sound.Voice);
            }
        }
    }

    async UniTask CreatePool()
    {
        if (_preloadData.bullet != null)
        {
            var h = Addressables.LoadAssetAsync<GameObject>(_preloadData.bullet);
            GameObject p = await h.ToUniTask();
            if (p != null) Managers.Pool.CreatePool(p, 30);
        }
        if (_preloadData.monsterRL != null)
        {
            var h = Addressables.LoadAssetAsync<GameObject>(_preloadData.monsterRL);
            GameObject p = await h.ToUniTask();
            if (p != null) Managers.Pool.CreatePool(p, 16);
        }
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

    async UniTask CreateBoss()
    {
        if (_bossSpawnPoint == null || _preloadData.boss == null) return;

        GameObject root = new GameObject { name = "@BossMonster" };
        root.transform.position = _bossSpawnPoint.position;

        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.boss);
        GameObject prefab = await handle.ToUniTask();

        if (prefab != null)
        {
            _boss = Instantiate(prefab, root.transform);
            _boss.transform.localPosition = Vector3.zero;

            var ctrl = _boss.GetComponent<BossMonsterController>();
            if (ctrl != null)
            {
                ctrl.OnDead -= OnMonsterDead;
                ctrl.OnDead += OnMonsterDead;
            }

            if (_curMap != null)
            {
                var skill = _boss.GetComponent<BossSkillController>();
                var mapScript = _curMap.GetComponent<BossDungeonMap>();
                if (skill != null && mapScript != null)
                {
                    skill.SetSpawnPoints(mapScript.GetMonsterSpawnPoints());
                    skill.SetLightningPoints(mapScript.GetLightningPoints());
                }
            }
        }
    }

    async UniTask CreateBossHPBarUI()
    {
        // [핵심 6] Task.Yield() 대신 안전한 UniTask.Yield() 사용 (에디터 멈춤 원천 차단)
        while (_boss == null) await UniTask.Yield();

        if (_preloadData.bossHPBar == null) return;

        GameObject root = new GameObject { name = "@HPBarUI" };
        var handle = Addressables.LoadAssetAsync<GameObject>(_preloadData.bossHPBar);
        GameObject prefab = await handle.ToUniTask();

        if (prefab != null)
        {
            _bossHPBar = Instantiate(prefab, root.transform);
            var hpScript = _bossHPBar.GetComponent<BossHPBar>();
            if (hpScript != null) hpScript.SetBoss(_boss);
        }
    }

    async UniTaskVoid PlayVictoryVoice()
    {
        List<BaseCharacter> partyMembers = Managers.Party.GetMemeber();
        if (partyMembers.Count == 0) return;

        int randomNum = Random.Range(0, partyMembers.Count);
        AssetReferenceT<AudioClip>[] voices = partyMembers[randomNum].Stat.GetBattleVictoryVoices();

        if (voices != null && voices.Length > 0)
        {
            var voiceRef = voices[Random.Range(0, voices.Length)];
            if (voiceRef != null && voiceRef.RuntimeKeyIsValid())
            {
                var handle = Addressables.LoadAssetAsync<AudioClip>(voiceRef);
                AudioClip clip = await handle.ToUniTask();
                if (clip != null) Managers.Sound.Play(clip, Define.Sound.Voice);
            }
        }
    }

    private void OnMonsterDead()
    {
        if (_successBGM != null) Managers.Sound.Play(_successBGM, Define.Sound.Bgm);

        Managers.Party.FinishGame(true);
        Managers.Resource.Destroy(_bossHPBar);
        CoVictoryPoze().Forget(); // 코루틴 대신 UniTask 호출
    }

    // [핵심 7] 승리 연출 코루틴을 UniTask로 변경
    private async UniTaskVoid CoVictoryPoze()
    {
        Managers.Party.PlayerController.VictoryTime = true;

        // WaitForSeconds 대신 UniTask.Delay(초) 사용
        await UniTask.Delay(System.TimeSpan.FromSeconds(3.0));

        PlayVictoryVoice().Forget();

        if (_mainUI != null) _mainUI.SetActive(false);
        _clearUI.SetActive(true);

        GameObject camObj = _curMap.GetComponent<BossDungeonMap>().GetEndingCameraPoint().gameObject;
        camObj.SetActive(true);

        // 카메라 줌 시작
        CoCameraZoomEffect(camObj.transform).Forget();

        Transform[] endingPositions = _curMap.GetComponent<BossDungeonMap>().GetEndingPoints();
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

                Vector3 targetPos = camObj.transform.position;
                targetPos.y = character.transform.position.y;
                Vector3 dir = targetPos - character.transform.position;

                if (dir != Vector3.zero) character.transform.rotation = Quaternion.LookRotation(dir);
                index++;
            }
            character.Victory();
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

            // yield return null; -> await UniTask.Yield();
            await UniTask.Yield();
        }
        camTr.position = targetPos;
    }

    private async UniTaskVoid CoSafeTeleport()
    {
        await UniTask.Yield(); // 1프레임 대기

        Managers.Party.TeleportParty(_spawnPoint.position);
        Camera.main.transform.position = _cameraPoint.position;
        Camera.main.transform.rotation = _cameraPoint.rotation;
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
}
