using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

public class SelectSceneCanvas : MonoBehaviour
{
    // ========================================================================
    // [1] 데이터 및 설정
    // ========================================================================
    [Header("School Data")]
    [SerializeField] private SchoolDataSO[] _schoolDatas;
    [SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결

    [Header("Background Images")]
    [SerializeField] private Image _backgroundImage; // 배경 이미지 (Image)
    [SerializeField] private Sprite _backgroundSprite; // 배경 이미지 ( Sprite )


    // ========================================================================
    // [2] UI 요소 그룹화 (버튼 및 표시부)
    // ========================================================================
    [System.Serializable]
    public class SchoolUIElements
    {
        public Button textButton;  
        public Button imageButton; 
    }

    [Header("UI Controls")]
    [SerializeField] private SchoolUIElements[] _schoolUIElements; // 0:아비도스, 1:게헨나, 2:밀레니엄
    [SerializeField] private Button _gameStartButton;


    [Header("Display Info")]
    [SerializeField] private Image _schoolIcon;
    [SerializeField] private Image _schoolName;

    // ========================================================================
    // [3] 내부 상태 변수
    // ========================================================================
    private GameObject _modelCamera;
    private readonly string[] _spawnPointNames = { "SpawnPoint1", "SpawnPoint2", "SpawnPoint3", "SpawnPoint4" };
    private int _currentSelectedSchool = -1;
    private List<GameObject[]> _loadedModels = new List<GameObject[]>();



    void Start()
    {
        InitializeUI();
        SetupButtonListeners();

        if(_modelCamera != null)
        {
            LoadAndInstantiateModels();
            SelectSchool(0);
        }     
    }

    public void SetModelCamera(GameObject modelCamera) { _modelCamera = modelCamera; }

    void InitializeUI()
    {
        if (_backgroundImage != null && _backgroundSprite != null)
        {
            _backgroundImage.sprite = _backgroundSprite;
        }
    }


    void SetupButtonListeners()
    {
        // 학교별 버튼 리스너 일괄 등록
        for (int i = 0; i < _schoolUIElements.Length; i++)
        {
            int index = i; // Closure 캡처
            _schoolUIElements[i].textButton.onClick.AddListener(() => SelectSchool(index));
            _schoolUIElements[i].imageButton.onClick.AddListener(() => SelectSchool(index));
        }

        // 게임 시작 버튼
        _gameStartButton.onClick.AddListener(OnGameStartClicked);
    }

    void OnGameStartClicked()
    {
        if (_currentSelectedSchool < 0) return;

        // Context 설정
        Managers.Context.SchoolIdx = _currentSelectedSchool;
        Managers.Context.SelectedSchool = _schoolDatas[_currentSelectedSchool];

        // 사운드 정리 후 씬 이동
        Managers.Sound.StopAll();
        Managers.SceneEx.LoadScene(Define.Scene.Game);
    }

    void LoadAndInstantiateModels()
    {
        Transform[] spawnPoints = GetSpawnPoints();
        _loadedModels.Clear();

        // 각 학교별로 모델 생성 후 리스트에 저장
        for (int i = 0; i < _schoolDatas.Length; i++)
        {
            GameObject[] models = CreateModelsForSchool(i, spawnPoints);
            _loadedModels.Add(models);
        }

        Debug.Log("모든 캐릭터 모델 로딩 완료");
    }

    Transform[] GetSpawnPoints()
    {
        Transform[] points = new Transform[_spawnPointNames.Length];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = _modelCamera.transform.Find(_spawnPointNames[i]);
        }
        return points;
    }

    GameObject[] CreateModelsForSchool(int schoolIdx, Transform[] spawnPoints)
    {
        GameObject[] models = new GameObject[4];
        CharacterDataSO[] chars = _schoolDatas[schoolIdx].characters;

        for (int i = 0; i < 4; i++)
        {
            if (i >= chars.Length) break;

            GameObject go = Instantiate(chars[i].selectPrefab, spawnPoints[i]);
            go.transform.localPosition = Vector3.zero;

            // 카메라 방향 바라보기 (Y축만 회전)
            if (_modelCamera != null)
            {
                Vector3 direction = _modelCamera.transform.position - go.transform.position;
                direction.y = 0; // 높이 차이는 무시 (수평 회전만)

                if (direction != Vector3.zero) // 안전장치
                {
                    go.transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            else
            {
                go.transform.localRotation = Quaternion.identity;
            }

            go.SetActive(false); // 기본은 꺼둠
            models[i] = go;
        }
        return models;
    }

    // ========================================================================
    // [4] 선택 로직 (SelectSchool)
    // ========================================================================
    void SelectSchool(int schoolIndex)
    {
        Debug.Log($"선택된 학교 index : {schoolIndex}");

        if (schoolIndex == _currentSelectedSchool) return;

        // 이전 모델 끄기
        if (_currentSelectedSchool >= 0 && _currentSelectedSchool < _loadedModels.Count)
            SetModelsActive(_currentSelectedSchool, false);

        // 새 모델 켜기
        if (schoolIndex >= 0 && schoolIndex < _loadedModels.Count)
            SetModelsActive(schoolIndex, true);

        _currentSelectedSchool = schoolIndex;

        // UI 갱신
        UpdateUIState(schoolIndex);

        // 사운드 재생
        PlaySchoolSound(schoolIndex);

        // Context 미리 업데이트 
        Managers.Context.SchoolIdx = schoolIndex;
    }

    void SetModelsActive(int schoolIdx, bool isActive)
    {
        foreach (var model in _loadedModels[schoolIdx])
        {
            if (model != null) model.SetActive(isActive);
        }
    }

    void UpdateUIState(int selectedIndex)
    {
        // 버튼 상태 갱신 
        for (int i = 0; i < _schoolUIElements.Length; i++)
        {
            bool isNotSelected = (i != selectedIndex);
            _schoolUIElements[i].textButton.interactable = isNotSelected;
            _schoolUIElements[i].imageButton.interactable = isNotSelected;
        }

        // 아이콘 및 폰트 변경
        if (_schoolDatas[selectedIndex] != null)
        {
            _schoolIcon.sprite = _schoolDatas[selectedIndex].schoolIcon;
            _schoolName.sprite = _schoolDatas[selectedIndex].schoolNameFont;
        }
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
}
