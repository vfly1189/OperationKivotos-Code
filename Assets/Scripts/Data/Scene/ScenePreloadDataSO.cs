using UnityEngine;


//정적인 것들 preloading용 SO

[CreateAssetMenu(menuName = "Game/ScenePreloadData")]
public class ScenePreloadDataSO : ScriptableObject
{
    [Header("필수 로드 프리팹 (맵, 시스템 등)")]
    public GameObject[] _preloadingObjects;

    [Header("필수 로드 스프라이트")]
    public Sprite[] _preloadingSprites;
}
