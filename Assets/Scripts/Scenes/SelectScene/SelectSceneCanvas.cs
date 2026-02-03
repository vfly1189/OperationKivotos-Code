using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

public class SelectSceneCanvas : MonoBehaviour
{
    [Header("School Data")]
    [SerializeField] private SchoolDataSO[] _schoolDatas;
    [SerializeField] private CurrentGameDataSO _currentGameContext; // 인스펙터 연결

    [Header("Background Images")]
    [SerializeField] private Image _backgroundImage; // 배경 이미지 (Image)
    [SerializeField] private Sprite _backgroundSprite; // 배경 이미지 ( Sprite )

    [Header("UI Buttons")]
    [SerializeField] private Button _abydosSelectButton;
    [SerializeField] private Button _gehennaSelectButton;
    [SerializeField] private Button _millenniumSelectButton;
    [SerializeField] private Button _gameStartButton;

    [Header("UI Image Buttons")]
    [SerializeField] private Button _abydosSelectImageButton;
    [SerializeField] private Button _gehennaSelectImageButton;
    [SerializeField] private Button _millenniumSelectImageButton;

    [Header("ShowingGroup")]
    [SerializeField] private Image _schoolIcon;
    [SerializeField] private Image _schoolName;


    [Header("Spawn Points")]
    [SerializeField] private Transform[] _spawnPoints; // 4개 (각 학교별 4마리 위치)

    private GameObject[] _abydosModels;
    private GameObject[] _gehennaModels;
    private GameObject[] _millenniumModels;

    private int _currentSelectedSchool = -1;
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
                //선택된 학교 인덱스 검증
                if (_currentSelectedSchool < 0)
                {
                    Debug.LogWarning("학교가 선택되지 않았습니다.");
                    return;
                }

                Managers.Context.SchoolIdx = _currentSelectedSchool;
                Managers.Context.SelectedSchool = _schoolDatas[_currentSelectedSchool];             

                // 3. 리소스 목록을 들고 GameScene으로 출발!
                Managers.SceneEx.LoadScene(Define.Scene.Game);

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

        LoadSchoolModels(0, _abydosModels, spawnPoints);
        LoadSchoolModels(1, _gehennaModels, spawnPoints);
        LoadSchoolModels(2, _millenniumModels, spawnPoints);

        Debug.Log("모든 캐릭터 모델 Instantiate 완료");
    }

    void LoadSchoolModels(int schoolIdx, GameObject[] modelArray, Transform[] spawnPoints)
    {
        for (int i = 0; i < 4; i++)
        {
            string charName = _schoolDatas[schoolIdx].characters[i].nameEN;

            GameObject go = Object.Instantiate(_schoolDatas[schoolIdx].characters[i].selectPrefab, spawnPoints[i].transform);

            if (go != null)
            {
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.SetActive(false);
                modelArray[i] = go;
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

        Managers.Context.SchoolIdx = schoolIndex;
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
        // 1. 현재 학교 데이터 가져오기
        SchoolDataSO school = _schoolDatas[schoolIndex];

        // 2. 랜덤 학생 데이터 가져오기 (문자열 필요 없음!)
        int randomCharIndex = Random.Range(0, school.characters.Length);
        CharacterDataSO character = school.characters[randomCharIndex];

        // 3. 그 학생의 보이스 목록 중 하나 랜덤 재생
        if (character.formationInVoices != null && character.formationInVoices.Length > 0)
        {
            int randomVoiceIdx = Random.Range(0, character.formationInVoices.Length);
            AudioClip clip = character.formationInVoices[randomVoiceIdx];

            Managers.Sound.Play(clip, Define.Sound.Effect);
            Debug.Log($"{character.nameEN} 음성 재생");
        }
    }

    void PlaySchoolTheme(int schoolIndex)
    {
        // 바로 꺼내 쓰면 됨
        AudioClip theme = _schoolDatas[schoolIndex].themeBGM;

        if (theme != null)
        {
            Managers.Sound.Play(theme, Define.Sound.Bgm);
        }
    }

    void ChangeSchoolIcon(int schoolIndex)
    {
        _schoolIcon.sprite = _schoolDatas[schoolIndex].schoolIcon;
    }

    void ChangeSchoolNameImageFont(int schoolIndex)
    {
        _schoolName.sprite = _schoolDatas[schoolIndex].schoolNameFont;
    }



}
