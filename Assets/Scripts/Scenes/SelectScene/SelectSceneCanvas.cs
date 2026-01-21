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

        //리소스 준비 확인
        if (!_selectScene.IsResourcesReady)
        {
            Debug.LogError("SelectSceneCanvas: Resources not ready!");
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
            () => Managers.SceneEx.LoadScene(Define.Scene.Game)
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

        //Abydos - SelectScene에서 리소스 가져오기
        for (int i = 0; i < 4; i++)
        {
            GameObject prefab = _selectScene.GetResource<GameObject>(_characterNames[0][i]);

            if (prefab == null)
            {
                Debug.LogError($"Abydos {_characterNames[0][i]} 프리팹을 찾을 수 없습니다!");
                continue;
            }

            _abydosModels[i] = Instantiate(prefab, spawnPoints[i].position, new Quaternion(0,0,0,0));
            _abydosModels[i].SetActive(false);
        }

        //Gehenna - SelectScene에서 리소스 가져오기
        for (int i = 0; i < 4; i++)
        {
            GameObject prefab = _selectScene.GetResource<GameObject>(_characterNames[1][i]);

            if (prefab == null)
            {
                Debug.LogError($"Gehenna {_characterNames[1][i]} 프리팹을 찾을 수 없습니다!");
                continue;
            }

            _gehennaModels[i] = Instantiate(prefab, spawnPoints[i].position, new Quaternion(0, 0, 0, 0));
            _gehennaModels[i].SetActive(false);
        }

        //Millennium - SelectScene에서 리소스 가져오기
        for (int i = 0; i < 4; i++)
        {
            GameObject prefab = _selectScene.GetResource<GameObject>(_characterNames[2][i]);

            if (prefab == null)
            {
                Debug.LogError($"Millennium {_characterNames[2][i]} 프리팹을 찾을 수 없습니다!");
                continue;
            }

            _millenniumModels[i] = Instantiate(prefab, spawnPoints[i].position, new Quaternion(0, 0, 0, 0));
            _millenniumModels[i].SetActive(false);
        }

        Debug.Log("모든 캐릭터 모델 Instantiate 완료");
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
        PlayerPrefs.SetInt("SelectedSchool", schoolIndex);
        PlayerPrefs.Save();
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
        Sprite schoolIcon = _selectScene.GetResource<Sprite>("School_Icon_" + _schoolNames[schoolIndex]);
        _schoolIcon.sprite = schoolIcon;
    }

    void ChangeSchoolNameImageFont(int schoolIndex)
    {
        Sprite schoolNameImageFont = _selectScene.GetResource<Sprite>(_schoolNames[schoolIndex] + "_ImageFont");
        _schoolName.sprite = schoolNameImageFont;
    }



}
