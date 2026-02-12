using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

public class SelectSceneCanvas : MonoBehaviour
{
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


    private SelectScene _scene; // Scene 참조

    public void Init(SelectScene scene)
    {
        _scene = scene;

        // 버튼 리스너 연결
        for (int i = 0; i < _schoolUIElements.Length; i++)
        {
            int idx = i;
            _schoolUIElements[i].imageButton.onClick.AddListener(() => _scene.SelectSchool(idx));
            _schoolUIElements[i].textButton.onClick.AddListener(() => _scene.SelectSchool(idx));
        }

        _gameStartButton.onClick.AddListener(() =>
        {
            //Managers.SceneEx.SetActiveCover(true);

            Managers.Sound.StopAll();
            Managers.SceneEx.LoadScene(Define.Scene.Game);
        });
    }

    public void UpdateUIState(int index, SchoolDataSO data)
    {
        // 1. 아이콘 변경
        if (data != null)
        {
            _schoolIcon.sprite = data.schoolIcon;
            _schoolName.sprite = data.schoolNameFont;
        }

        // 2. 버튼 활성/비활성 처리
        for (int i = 0; i < _schoolUIElements.Length; i++)
        {
            _schoolUIElements[i].imageButton.interactable = (i != index);
            _schoolUIElements[i].textButton.interactable = (i != index);
        }
    }
}
