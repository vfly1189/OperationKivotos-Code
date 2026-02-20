using UnityEngine;


[CreateAssetMenu(menuName = "Game/Preload/SceneData")]
public class SceneDataSO : ScriptableObject
{
    [Header("기본 설정")]
    public string sceneAddress;      // Addressable Scene Key ( 이거는 일단 나중에 )
    public Define.Scene sceneType; 

    [Header("메모리 관리")]
    public string[] preloadLabels;   // 미리 로드할 에셋 그룹 -> Label
    public bool clearPreviousMemory = true; // 이전 씬 메모리를 다 날릴지 여부
}
