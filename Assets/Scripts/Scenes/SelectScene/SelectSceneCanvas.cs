using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

public class SelectSceneCanvas : MonoBehaviour
{
    [Header("Background Images")]
    [SerializeField]
    private Image _backgroundImage; // 배경 이미지 (Image)
    [SerializeField]
    private Sprite _backgroundSprite; // 배경 이미지 ( Sprite )

    [Header("UI Buttons")]
    [SerializeField]
    private Button _abydosSelectButton;
    [SerializeField]
    private Button _gehennaSelectButton;
    [SerializeField]
    private Button _millenniumSelectButton;
    [SerializeField]
    private Button _gameStartButton;

    [Header("UI Image Buttons")]
    [SerializeField]
    private Button _abydosSelectImageButton;
    [SerializeField]
    private Button _gehennaSelectImageButton;
    [SerializeField]
    private Button _millenniumSelectImageButton;

    [Header("ShowingGroup")]
    [SerializeField]
    private Image _schoolIcon;
    [SerializeField]
    private Image _schoolName;


    [Header("Spawn Points")]
    [SerializeField]
    private Transform[] _spawnPoints; // 4개 (각 학교별 4마리 위치)

    private GameObject[] _abydosModels;
    private GameObject[] _gehennaModels;
    private GameObject[] _millenniumModels;

    private int _currentSelectedSchool = -1;

    private string[] _schoolNames = new string[3]
    {
        "Abydos", "Gehenna", "Millennium"
    };

    private string[][] _characterNames = new string[3][]
    {
        new string[] { "Hoshino", "Nonomi", "Shiroko", "Serika" },
        new string[] { "Aru", "Hina", "Ako", "Iori" },
        new string[] { "Toki", "Karin", "Asuna", "Aris" }
    };

    private SelectScene _selectScene;

    void Start()
    {
        _selectScene = GameObject.FindAnyObjectByType<SelectScene>();

        if (_selectScene == null)
        {
            Debug.LogError("SelectScene을 찾을 수 없습니다!");
            return;
        }

        InitializeUI();
        SetupButtonListeners();
        LoadAndInstantiateModels();
        SelectSchool(0);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void InitializeUI()
    {
        if (_backgroundImage != null && _backgroundSprite != null)
        {
            _backgroundImage.sprite = _backgroundSprite;
        }
    }

    void SetupButtonListeners()
    {
        _abydosSelectButton.onClick.AddListener(
            () => SelectSchool(0)
        );
        _gehennaSelectButton.onClick.AddListener(
            () => SelectSchool(1)
        );
        _millenniumSelectButton.onClick.AddListener(
            () => SelectSchool(2)
        );

        _abydosSelectImageButton.onClick.AddListener(
            () => SelectSchool(0)
        );
        _gehennaSelectImageButton.onClick.AddListener(
            () => SelectSchool(1)
        );
        _millenniumSelectImageButton.onClick.AddListener(
            () => SelectSchool(2)
        );

        _gameStartButton.onClick.AddListener(
            () =>
            {
                // 1. 선택된 학교 인덱스 저장 (GameManager에 저장)
                Managers.Game._selectedSchoolIndex = _currentSelectedSchool;

                // 2. 로딩할 리소스 목록 만들기 (짐 싸기)
                List<string> resourcesToLoad = new List<string>();

                // A. GameScene의 기본 필수 리소스 (맵 등)
                resourcesToLoad.AddRange(GameScene.REQUIRED_RESOURCES);

                // B. 선택된 학교의 캐릭터들 리소스 추가
                string schoolName = _schoolNames[_currentSelectedSchool];
                string[] students = _characterNames[_currentSelectedSchool];

                foreach (string student in students)
                {
                    // ★ 중요: ResourceManager.Instantiate가 "Prefabs/"를 붙이므로
                    // 여기서도 "Prefabs/"를 붙여서 로딩해야 키(Key)가 일치함.
                    // 예: "Prefabs/Characters/Abydos/Hoshino"

                    // (주의: SelectScene용 모델은 _Select가 붙었지만, 게임용 모델은 _Select가 없는지, 
                    //  아니면 경로가 다른지 확인 필요. 여기선 "_Select" 없는 버전이라고 가정)
                    string path = $"Prefabs/Characters/{schoolName}/{student}_InGame";
                    resourcesToLoad.Add(path);
                }

                // 3. 리소스 목록을 들고 GameScene으로 출발!
                Managers.SceneEx.LoadScene(Define.Scene.Game, resourcesToLoad.ToArray());

                Managers.Sound.StopAll();
            }
        );
    }

    void LoadAndInstantiateModels()
    {
        if (_selectScene.modelCamera == null)
            Debug.Log("카메라없음");

        Transform[] spawnPoints = new Transform[4];

        spawnPoints[0] = _selectScene.modelCamera.transform.Find("SpawnPoint1");
        spawnPoints[1] = _selectScene.modelCamera.transform.Find("SpawnPoint2");
        spawnPoints[2] = _selectScene.modelCamera.transform.Find("SpawnPoint3");
        spawnPoints[3] = _selectScene.modelCamera.transform.Find("SpawnPoint4");

        _abydosModels = new GameObject[4];
        _gehennaModels = new GameObject[4];
        _millenniumModels = new GameObject[4];

        LoadSchoolModels(0, "Abydos", _abydosModels, spawnPoints);
        LoadSchoolModels(1, "Gehenna", _gehennaModels, spawnPoints);
        LoadSchoolModels(2, "Millennium", _millenniumModels, spawnPoints);

        Debug.Log("모든 캐릭터 모델 Instantiate 완료");
    }

    void LoadSchoolModels(int schoolIdx, string schoolName, GameObject[] modelArray, Transform[] spawnPoints)
    {
        for (int i = 0; i < 4; i++)
        {
            string charName = _characterNames[schoolIdx][i];

            // ★ 수정 포인트: 경로 맞춰주기
            // SelectScene.REQUIRED_RESOURCES에 적힌 경로는 "Prefabs/Characters/.../_Select" 였음.
            // Managers.Resource.Instantiate는 "Prefabs/"를 자동으로 붙여주므로, 나머지 경로만 전달.
            string path = $"Characters/{schoolName}/{charName}_Select";

            // Managers.Resource.Instantiate 사용 (캐시에서 즉시 가져옴)
            GameObject go = Managers.Resource.Instantiate(path, spawnPoints[i]);

            if (go != null)
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.SetActive(false);
                modelArray[i] = go;
            }
            else
            {
                Debug.LogError($"모델 로드 실패: {path}");
            }
        }
    }


    void SelectSchool(int schoolIndex)
    {
        if (_currentSelectedSchool >= 0)
        {
            DeactivateSchoolModels(_currentSelectedSchool);
        }

        ActivateSchoolModels(schoolIndex);
        _currentSelectedSchool = schoolIndex;
        UpdateButtonStates(schoolIndex);

        ChangeSchoolIcon(schoolIndex);
        ChangeSchoolNameImageFont(schoolIndex);

        Managers.Sound.StopAll();
        PlayRandomSchoolVoice(schoolIndex);
        PlaySchoolTheme(schoolIndex);

        // 다음 씬에서 어떤거 불러왔는지 알 수 잇음
        //PlayerPrefs.SetInt("SelectedSchool", schoolIndex);
        //PlayerPrefs.Save();

        Managers.Game._selectedSchoolIndex = schoolIndex;
    }

    void ActivateSchoolModels(int schoolIndex)
    {
        GameObject[] models = schoolIndex switch
        {
            0 => _abydosModels,
            1 => _gehennaModels,
            2 => _millenniumModels,
            _ => null
        };

        if (models != null)
        {
            foreach (var model in models)
            {
                model.SetActive(true);
            }
        }
    }

    void DeactivateSchoolModels(int schoolIndex)
    {
        GameObject[] models = schoolIndex switch
        {
            0 => _abydosModels,
            1 => _gehennaModels,
            2 => _millenniumModels,
            _ => null
        };

        if (models != null)
        {
            foreach (var model in models)
            {
                model.SetActive(false);
            }
        }
    }
    void UpdateButtonStates(int selectedIndex)
    {
        _abydosSelectButton.interactable = (selectedIndex != 0);
        _gehennaSelectButton.interactable = (selectedIndex != 1);
        _millenniumSelectButton.interactable = (selectedIndex != 2);

        _abydosSelectImageButton.interactable = (selectedIndex != 0);
        _gehennaSelectImageButton.interactable = (selectedIndex != 1);
        _millenniumSelectImageButton.interactable = (selectedIndex != 2);
    }


    // ★ 랜덤 학생 음성 재생
    void PlayRandomSchoolVoice(int schoolIndex)
    {
        // 해당 학교 학생 이름 배열 가져오기
        string[] students = _characterNames[schoolIndex];

        // 랜덤 학생 선택 (0~3)
        int randomStudentIndex = Random.Range(0, students.Length);
        string selectedStudent = students[randomStudentIndex];


        string schoolName = null;
        if (schoolIndex == 0) schoolName = "Abydos";
        else if (schoolIndex == 1) schoolName = "Gehenna";
        else if (schoolIndex == 2) schoolName = "Millennium";

        int fileNum = Random.Range(1, 2);

        // 음성 파일 경로 생성
        // 예: "Sounds/SFX/Select/Hoshino_Select"
        string voicePath = $"Sounds/SFX/Selected/{schoolName}/{selectedStudent}_Formation_In_{fileNum}";

        // 음성 로드
        AudioClip voiceClip = Managers.Resource.Load<AudioClip>(voicePath);

        if (voiceClip != null)
        {
            // 음성 재생 (이전 음성 중단하고 새로 재생)
            Managers.Sound.Play(voiceClip, Define.Sound.Effect);
            Debug.Log($"{selectedStudent} 음성 재생");
        }
        else
        {
            Debug.LogWarning($"음성 파일을 찾을 수 없습니다: {voicePath}");
        }
    }

    void PlaySchoolTheme(int schoolIndex)
    {
        string schoolName = null;
        if (schoolIndex == 0) schoolName = "Abydos";
        else if (schoolIndex == 1) schoolName = "Gehenna";
        else if (schoolIndex == 2) schoolName = "Millennium";

        // 음성 파일 경로 생성
        // 예: "Sounds/SFX/Select/Hoshino_Select"
        string themePath = $"Sounds/BGM/Theme/{schoolName}_Theme";

        // 음성 로드
        AudioClip themeClip = Managers.Resource.Load<AudioClip>(themePath);

        if (themeClip != null)
        {
            // 음성 재생 (이전 음성 중단하고 새로 재생)
            Managers.Sound.Play(themeClip, Define.Sound.Bgm);
            Debug.Log($"{themeClip} 재생");
        }
        else
        {
            Debug.LogWarning($"음성 파일을 찾을 수 없습니다: {themeClip}");
        }
    }

    void ChangeSchoolIcon(int schoolIndex)
    { 
        // 경로: "Images/School_Icon/School_Icon_Abydos"
        // Managers.Resource.Load<Sprite> 사용 (이미 캐시됨)
        string path = $"Images/School_Icon/School_Icon_{_schoolNames[schoolIndex]}";
        Sprite icon = Managers.Resource.Load<Sprite>(path);

        if (icon != null) _schoolIcon.sprite = icon;
    }

    void ChangeSchoolNameImageFont(int schoolIndex)
    {
        string path = $"Images/ImageFont/{_schoolNames[schoolIndex]}_ImageFont";
        Sprite font = Managers.Resource.Load<Sprite>(path);

        if (font != null) _schoolName.sprite = font;
    }



}
