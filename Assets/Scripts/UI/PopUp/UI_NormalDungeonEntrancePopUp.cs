using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_NormalDungeonEntrancePopUp : UI_PopUp
{
    [SerializeField] private Button _enterButton;
    [SerializeField] private Button _cancelButton;

    [Header("Difficulty Buttons")]
    [SerializeField] private Button _easyButton;
    [SerializeField] private Button _normalButton;
    [SerializeField] private Button _hardButton;

    // 텍스트 컴포넌트 캐싱용 (버튼 자식에 있는 텍스트)
    private TextMeshProUGUI _easyText;
    private TextMeshProUGUI _normalText;
    private TextMeshProUGUI _hardText;

    // 색상 정의 (ColorUtility로 파싱)
    private Color _colEasy;
    private Color _colNormal;
    private Color _colHard;
    private Color _colDeactive = new Color(0.7f, 0.7f, 0.7f, 1f); // 비활성 색상 (회색)

    public override void Init()
    {
        base.Init();

        // 1. 컴포넌트 찾기 (버튼 아래의 텍스트)
        _easyText = _easyButton.GetComponentInChildren<TextMeshProUGUI>();
        _normalText = _normalButton.GetComponentInChildren<TextMeshProUGUI>();
        _hardText = _hardButton.GetComponentInChildren<TextMeshProUGUI>();

        // 2. 색상 파싱 (# 코드를 Color 객체로 변환)
        ColorUtility.TryParseHtmlString("#00C800", out _colEasy);   // Easy (Green)
        ColorUtility.TryParseHtmlString("#D4B200", out _colNormal); // Normal (Gold)
        ColorUtility.TryParseHtmlString("#FF4500", out _colHard);   // Hard (Red)

        // 3. 이벤트 연결
        _enterButton.onClick.AddListener(OnEnterClicked);
        _cancelButton.onClick.AddListener(OnCancelClicked);

        _easyButton.onClick.AddListener(() => OnDifficultySelected(Define.DungeonDifficulty.Easy));
        _normalButton.onClick.AddListener(() => OnDifficultySelected(Define.DungeonDifficulty.Normal));
        _hardButton.onClick.AddListener(() => OnDifficultySelected(Define.DungeonDifficulty.Hard));

        // 4. 초기값 설정 (Easy 선택 상태로 시작)
        OnDifficultySelected(0);
    }

    // 난이도 선택 처리 (0: Easy, 1: Normal, 2: Hard)
    void OnDifficultySelected(Define.DungeonDifficulty difficulty)
    {
        // 1. 매니저에 저장
        Managers.Context.SelectedDifficulty = difficulty;

        // 2. 버튼 색상 갱신
        UpdateButtonColors(difficulty);
    }

    void UpdateButtonColors(Define.DungeonDifficulty selectedDifficulty)
    {
        // 로직: 선택된 놈은 자기 색깔, 안 된 놈은 회색(_colDeactive)

        // Easy 버튼
        if (selectedDifficulty == Define.DungeonDifficulty.Easy)
        {
            SetButtonColor(_easyButton, _easyText, _colEasy, true);
        }
        else
        {
            SetButtonColor(_easyButton, _easyText, _colDeactive, false);
        }

        // Normal 버튼
        if (selectedDifficulty == Define.DungeonDifficulty.Normal)
        {
            SetButtonColor(_normalButton, _normalText, _colNormal, true);
        }
        else
        {
            SetButtonColor(_normalButton, _normalText, _colDeactive, false);
        }

        // Hard 버튼
        if (selectedDifficulty == Define.DungeonDifficulty.Hard)
        {
            SetButtonColor(_hardButton, _hardText, _colHard, true);
        }
        else
        {
            SetButtonColor(_hardButton, _hardText, _colDeactive, false);
        }
    }

    // 버튼과 텍스트 색상 일괄 적용 함수
    void SetButtonColor(Button btn, TextMeshProUGUI txt, Color color, bool isSelected)
    {
        if (txt != null)
        {
            txt.color = color; // 텍스트 색상 변경
        }
    }

    void OnCancelClicked()
    {
        ClosePopupUI();
    }

    void OnEnterClicked()
    {
        Managers.SceneEx.LoadScene(Define.Scene.NormalDungeon);
        ClosePopupUI();
    }
}
