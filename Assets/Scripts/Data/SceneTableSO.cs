using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/SceneTable")]
public class SceneTableSO : ScriptableObject
{
    // 씬 Enum과 데이터를 매핑하는 구조체
    [System.Serializable]
    public struct SceneEntry
    {
        public Define.Scene sceneType;
        public SceneDataSO data;
    }

    public List<SceneEntry> scenes;

    // 편하게 찾기 위한 함수
    public SceneDataSO GetSceneData(Define.Scene type)
    {
        foreach (var entry in scenes)
        {
            if (entry.sceneType == type) return entry.data;
        }
        return null;
    }
}
