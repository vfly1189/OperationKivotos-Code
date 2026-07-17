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
        public string sceneName;

        public string[] preloadLabels;

        //public SceneDataSO data;
    }

    [SerializeField] private List<SceneEntry> _entries;

    public bool TryGet(Define.Scene type, out SceneEntry entry)
    {
        foreach (var e in _entries)
            if (e.sceneType == type) { entry = e; return true; }
        entry = default; return false;
    }
}
