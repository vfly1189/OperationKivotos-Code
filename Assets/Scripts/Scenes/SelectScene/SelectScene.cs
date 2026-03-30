using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
// [추가] UniTask 네임스페이스
using Cysharp.Threading.Tasks;

public class SelectScene : BaseScene
{
    private Dictionary<int, List<GameObject>> _schoolModels
        = new Dictionary<int, List<GameObject>>();

    [SerializeField] private GameObject _loadingCover; // 일반 프리팹 (Addressable이 아닌 Inspector 연결)
    [SerializeField] private SelectScenePreloadSO _preloadData;
    [SerializeField] private SchoolDataSO[] _schoolDatas;

    private GameObject _modelCamera;
    private SelectSceneCanvas _uiCanvas;
    private int _currentSchoolIdx = -1;
    private GameObject _loadingCoverInstance;

    // [핵심 변경 1] async UniTaskVoid로 선언 (유니티 생명주기에 맞춤)
    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Select;

        // [수정점 1] 동기 Instantiate 대신 유니티 기본 Instantiate 사용
        if (_loadingCover != null)
        {
            _loadingCoverInstance = Instantiate(_loadingCover);
            _loadingCoverInstance.SetActive(true);
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
            _loadingCoverInstance.GetComponent<LoadingSceneController>().SetValue(1f);
        }

        // [핵심 변경 2] UniTask.Delay 사용 (에디터 멈춤 방지)
        //await UniTask.Delay(500);

        // 비동기 작업들 대기 (await)
        await CreateModelCamera();
        await CreateMainUI();
        await LoadAllSchoolModels();

        //if (_uiCanvas == null)
        //{
        //    //_uiCanvas.Init(this);
        //    SelectSceneCanvas canvas = Managers.UI.ShowSceneUI<SelectSceneCanvas>("SelectSceneCanvas_New");
        //    canvas.Setup(this);
        //    SelectSchool(0);
        //}

        // [추가] 3D 모델과 UI 로딩이 완벽히 끝난 후, 초기 학교(0번)를 선택 상태로 만듦
        SelectSchool(0);
        // [수정점 2] 코루틴 제거 -> UniTaskVoid 호출 (Fire and Forget)
        FadeInSequence().Forget();
    }

    public void SelectSchool(int index)
    {
        if (_currentSchoolIdx == index) return;

        if (_currentSchoolIdx != -1 && _schoolModels.ContainsKey(_currentSchoolIdx))
        {
            foreach (var go in _schoolModels[_currentSchoolIdx])
                if (go != null) go.SetActive(false);
        }

        if (_schoolModels.ContainsKey(index))
        {
            foreach (var go in _schoolModels[index])
                if (go != null) go.SetActive(true);
        }

        // 비동기 사운드 재생 (Fire and Forget)
        PlaySchoolSoundAsync(index).Forget();

        _currentSchoolIdx = index;
        Managers.Context.SchoolIdx = index;
        Managers.Context.SelectedSchool = _schoolDatas[index];

        _uiCanvas.UpdateUIState(index, _schoolDatas[index]);
    }

    // [핵심 변경 3] async void -> async UniTaskVoid 로 변경
    private async UniTaskVoid PlaySchoolSoundAsync(int schoolIndex)
    {
        Managers.Sound.StopAll();
        SchoolDataSO data = _schoolDatas[schoolIndex];

        if (data.themeBGM != null)
            Managers.Sound.Play(data.themeBGM, Define.Sound.Bgm);

        if (data.characters.Length > 0)
        {
            var character = data.characters[Random.Range(0, data.characters.Length)];

            if (character.formationInVoices != null && character.formationInVoices.Length > 0)
            {
                var voiceRef = character.formationInVoices[Random.Range(0, character.formationInVoices.Length)];

                if (voiceRef != null && voiceRef.RuntimeKeyIsValid())
                {
                    // [수정점 3] Addressables 직접 로드 제거 -> ResourceManager 위임
                    AudioClip clip = await Managers.Resource.LoadAsync<AudioClip>(voiceRef);

                    if (clip != null) Managers.Sound.Play(clip, Define.Sound.Voice);
                }
            }
        }
    }

    // [핵심 변경 4] Task -> UniTask 로 반환형 변경
    private async UniTask LoadAllSchoolModels()
    {
        string[] spawnPointNames = { "SpawnPoint1", "SpawnPoint2", "SpawnPoint3", "SpawnPoint4" };
        Transform[] points = new Transform[4];
        if (_modelCamera != null)
        {
            for (int i = 0; i < 4; i++)
                points[i] = _modelCamera.transform.Find(spawnPointNames[i]);
        }

        // UniTask.WhenAll 사용
        var loadingTasks = new List<UniTask>();

        for (int i = 0; i < _schoolDatas.Length; i++)
        {
            loadingTasks.Add(LoadSingleSchoolModels(i, points));
        }

        await UniTask.WhenAll(loadingTasks);
    }

    private async UniTask LoadSingleSchoolModels(int schoolIdx, Transform[] spawnPoints)
    {
        _schoolModels[schoolIdx] = new List<GameObject>();
        var chars = _schoolDatas[schoolIdx].characters;

        for (int k = 0; k < 4; k++)
        {
            if (k >= chars.Length) break;

            // [수정점 4] ResourceManager로 위임하여 씬 단위 메모리 관리 보장
            GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(chars[k].selectPrefab);

            if (prefab != null)
            {
                GameObject go = Instantiate(prefab, spawnPoints[k]);
                go.SetActive(false);
                go.transform.localPosition = Vector3.zero;

                if (_modelCamera != null)
                {
                    Vector3 dir = _modelCamera.transform.position - go.transform.position;
                    dir.y = 0;
                    if (dir != Vector3.zero) go.transform.rotation = Quaternion.LookRotation(dir);
                }

                _schoolModels[schoolIdx].Add(go);
            }
        }
    }

    private async UniTask CreateModelCamera()
    {
        // [수정점 5] Handle 로직 제거
        GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.modelCamera);

        if (prefab != null)
        {
            _modelCamera = Instantiate(prefab);
            _modelCamera.name = "@ModelCamera";
        }
    }

    private async UniTask CreateMainUI()
    {
        //// [수정점 6] Handle 로직 제거
        //GameObject prefab = await Managers.Resource.LoadAsync<GameObject>(_preloadData.mainUI);


        //if (prefab != null)
        //{
        //    GameObject uiObj = Instantiate(prefab);
        //    uiObj.name = "@SelectSceneCanvas";
        //    _uiCanvas = uiObj.GetComponent<SelectSceneCanvas>();
        //}

        // 1. 리소스 매니저를 통해 UI 프리팹을 메모리에 비동기 로드
        // (ShowSceneUI의 동기 Instantiate가 실패하지 않도록 보장)
        await Managers.Resource.LoadAsync<GameObject>("SelectSceneCanvas_New");

        // 2. UIManager를 통해 SceneUI 생성 
        // 클래스명("SelectSceneCanvas")과 Addressable Key가 같다면 인자 생략 가능.
        // 만약 Key가 다르다면 ShowSceneUI<SelectSceneCanvas>("SelectSceneCanvas_New") 처럼 문자열을 넣으세요.
        _uiCanvas = Managers.UI.ShowSceneUI<SelectSceneCanvas>("SelectSceneCanvas_New");

        // 3. 생성된 UI에 Scene 객체 주입 및 이벤트 바인딩
        if (_uiCanvas != null)
        {
            _uiCanvas.Setup(this);
            _uiCanvas.OnStarted -= OnStartButtonClicked;
            _uiCanvas.OnStarted += OnStartButtonClicked;
        }
    }

    //스타트 버튼의 이벤트로써 호출될 함수
    //저장된 세이브 파일 불러오기
    private void OnStartButtonClicked()
    {
        PartySaveData savedData;
        string partyID = null;

        switch(_currentSchoolIdx)
        {
            case 0: partyID = "Abydos";
                break;
            case 1: partyID = "Gehenna";
                break;
            case 2: partyID = "Millennium";
                break;
        }
        Managers.Save.SetCurrentParty(partyID);
        Managers.Save.TryLoadParty(partyID, out savedData);
        Managers.Context.PartySaveData = savedData;
    }

    public override void Clear()
    {
        base.Clear();

        _uiCanvas.OnStarted -= OnStartButtonClicked;

        _schoolModels.Clear();
    }

    // [수정점 7] IEnumerator 코루틴을 UniTaskVoid로 변경
    private async UniTaskVoid FadeInSequence()
    {
        if (_loadingCoverInstance == null) return;

        CanvasGroup coverCG = _loadingCoverInstance.GetComponent<CanvasGroup>();
        if (coverCG == null)
        {
            Destroy(_loadingCoverInstance); // 자체 인스턴스화이므로 일반 Destroy 사용
            return;
        }

        float timer = 0f;
        float duration = 0.5f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            coverCG.alpha = Mathf.Lerp(1f, 0f, timer / duration);
            await UniTask.Yield(); // yield return null 대체
        }

        Destroy(_loadingCoverInstance);
    }

}
