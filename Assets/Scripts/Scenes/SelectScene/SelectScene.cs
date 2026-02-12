using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class SelectScene : BaseScene
{
    private enum ObjectType
    {
        Camera,
        UI,
        Character,
    }

    // 핸들 관리 (모델 포함)
    private Dictionary<ObjectType, List<AsyncOperationHandle>> _spawnedHandles
    = new Dictionary<ObjectType, List<AsyncOperationHandle>>();

    // 학교별 모델 핸들 리스트 (Key: School Index, Value: List of Handles)
    private Dictionary<int, List<GameObject>> _schoolModels
        = new Dictionary<int, List<GameObject>>();

    [SerializeField] private GameObject _loadingCover;
    [SerializeField] private SelectScenePreloadSO _preloadData;
    [SerializeField] private SchoolDataSO[] _schoolDatas; // 학교 데이터 여기로 이동

    private GameObject _modelCamera;
    private SelectSceneCanvas _uiCanvas;
    private int _currentSchoolIdx = -1;
    private GameObject _loadingCoverInstance;

    protected override async void Init()
    {
        base.Init();
        _sceneType = Define.Scene.Select;

        // 로딩하는거 가려줄 화면
        if (_loadingCover != null)
        {
            _loadingCoverInstance = Object.Instantiate(_loadingCover);
            _loadingCover.SetActive(true);
            // 커버에도 CanvasGroup이 있어야 페이드 아웃 가능 (없으면 추가)
            if (_loadingCoverInstance.GetComponent<CanvasGroup>() == null)
                _loadingCoverInstance.AddComponent<CanvasGroup>().alpha = 1f;
        }

        await CreateModelCamera();
        await CreateMainUI();
     
        await LoadAllSchoolModels();

        // 3. UI 초기화 및 이벤트 연결
        if (_uiCanvas != null)
        {
            _uiCanvas.Init(this); // Scene 참조 전달
            SelectSchool(0);      // 첫 학교 선택
        }

        StartCoroutine(FadeInSequence());
    }


    // 외부(Canvas)에서 호출할 함수
    public void SelectSchool(int index)
    {
        if (_currentSchoolIdx == index) return;

        // 이전 학교 끄기
        if (_currentSchoolIdx != -1 && _schoolModels.ContainsKey(_currentSchoolIdx))
        {
            foreach (var go in _schoolModels[_currentSchoolIdx])
                if (go != null) go.SetActive(false);
        }

        // 새 학교 켜기
        if (_schoolModels.ContainsKey(index))
        {
            foreach (var go in _schoolModels[index])
                if (go != null) go.SetActive(true);
        }

        PlaySchoolSound(index);

        _currentSchoolIdx = index;

        // Context 업데이트
        Managers.Context.SchoolIdx = index;
        Managers.Context.SelectedSchool = _schoolDatas[index];

        // UI 갱신 요청
        _uiCanvas.UpdateUIState(index, _schoolDatas[index]);
    }

    void PlaySchoolSound(int schoolIndex)
    {
        Managers.Sound.StopAll();

        SchoolDataSO data = _schoolDatas[schoolIndex];

        // 테마곡
        if (data.themeBGM != null)
            Managers.Sound.Play(data.themeBGM, Define.Sound.Bgm);

        // 랜덤 보이스
        if (data.characters.Length > 0)
        {
            var character = data.characters[Random.Range(0, data.characters.Length)];
            if (character.formationInVoices != null && character.formationInVoices.Length > 0)
            {
                var clip = character.formationInVoices[Random.Range(0, character.formationInVoices.Length)];
                Managers.Sound.Play(clip, Define.Sound.Effect);
            }
        }
    }

    // ========================================================================
    // [리소스 로딩 로직]
    // ========================================================================
    async Task LoadAllSchoolModels()
    {
        // 스폰 포인트 찾기
        string[] spawnPointNames = { "SpawnPoint1", "SpawnPoint2", "SpawnPoint3", "SpawnPoint4" };
        Transform[] points = new Transform[4];
        if (_modelCamera != null)
        {
            for (int i = 0; i < 4; i++)
                points[i] = _modelCamera.transform.Find(spawnPointNames[i]);
        }

        List<Task> loadingTasks = new List<Task>();

        for (int i = 0; i < _schoolDatas.Length; i++)
        {
            loadingTasks.Add(LoadSingleSchoolModels(i, points));
        }

        await Task.WhenAll(loadingTasks);
    }

    async Task LoadSingleSchoolModels(int schoolIdx, Transform[] spawnPoints)
    {
        _schoolModels[schoolIdx] = new List<GameObject>();
        var chars = _schoolDatas[schoolIdx].characters;

        for (int k = 0; k < 4; k++)
        {
            if (k >= chars.Length) break;

            var handle = chars[k].selectPrefab.InstantiateAsync(spawnPoints[k]);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject go = handle.Result;
                go.SetActive(false); // 기본 숨김

                // 카메라 보기 설정
                go.transform.localPosition = Vector3.zero;
                if (_modelCamera != null)
                {
                    Vector3 dir = _modelCamera.transform.position - go.transform.position;
                    dir.y = 0;
                    if (dir != Vector3.zero) go.transform.rotation = Quaternion.LookRotation(dir);
                }

                // 리스트에 추가
                _schoolModels[schoolIdx].Add(go);

                // 핸들 저장 (메모리 해제용)
                RegisterHandle(ObjectType.Character, handle);
            }
        }
    }


    async System.Threading.Tasks.Task CreateModelCamera()
    {
        var handle = _preloadData.modelCamera.InstantiateAsync();
        await handle.Task;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _modelCamera = handle.Result;
            _modelCamera.name = "@ModelCamera";
            RegisterHandle(ObjectType.Camera, handle);
        }
    }

    async System.Threading.Tasks.Task CreateMainUI()
    {
        var handle = _preloadData.mainUI.InstantiateAsync();
        await handle.Task;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject uiObj = handle.Result;
            uiObj.name = "@SelectSceneCanvas";
            _uiCanvas = uiObj.GetComponent<SelectSceneCanvas>();
            RegisterHandle(ObjectType.UI, handle);
        }
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
        _schoolModels.Clear();
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
