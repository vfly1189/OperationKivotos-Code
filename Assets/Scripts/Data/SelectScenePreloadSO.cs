using UnityEngine;
[CreateAssetMenu(menuName = "Preload/SelectScenePreloadData")]
public class SelectScenePreloadSO : ScriptableObject
{
    [Header("모델들 찍을 카메라")]
    public GameObject modelCamera;

    [Header("선택창 메인 UI")]
    public GameObject mainUI;
}
