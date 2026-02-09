using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class BossSkillController : MonoBehaviour
{
    [System.Serializable]
    public class SkillData
    {
        public string skillName; // 예: "Meteor", "Lightning"
        public GameObject prefab;
    }

    [Header("스킬 데이터 등록")]
    [SerializeField] private List<SkillData> _skillList;

    // 런타임 검색용 딕셔너리 (String -> GameObject)
    private Dictionary<string, GameObject> _skillMap = new Dictionary<string, GameObject>();

    // 생성된 인스턴스 캐싱 (String -> GameObject Instance)
    private Dictionary<string, GameObject> _spawnedInstances = new Dictionary<string, GameObject>();

    private void Awake()
    {
        // 1. 리스트를 딕셔너리로 변환 (검색 속도 UP)
        foreach (var data in _skillList)
        {
            if (string.IsNullOrEmpty(data.skillName)) continue;

            if (!_skillMap.ContainsKey(data.skillName))
            {
                _skillMap.Add(data.skillName, data.prefab);
            }
        }
    }

    // 타임라인 Signal Receiver에서 호출할 함수 (String 매개변수 사용)
    public void CastSkill(string skillName)
    {
        // 2. 안전장치: 키가 존재하는지 확인
        if (!_skillMap.ContainsKey(skillName))
        {
            Debug.LogError($"[BossSkill] '{skillName}'라는 이름의 스킬을 찾을 수 없습니다! 오타를 확인하세요.");
            return;
        }

        // 3. 인스턴스 없으면 생성 (풀링)
        if (!_spawnedInstances.ContainsKey(skillName) || _spawnedInstances[skillName] == null)
        {
            GameObject prefab = _skillMap[skillName];
            GameObject instance = Instantiate(prefab, transform);
            instance.SetActive(false);
            _spawnedInstances[skillName] = instance;
        }

        GameObject skillObj = _spawnedInstances[skillName];

        skillObj.transform.position = Managers.Party.GetCurrentCharacter().transform.position;

        // 5. 실행
        skillObj.SetActive(true);

        skillObj.GetComponent<ParticleSystem>().Stop();
        skillObj.GetComponent<ParticleSystem>().Play();
    }
}
