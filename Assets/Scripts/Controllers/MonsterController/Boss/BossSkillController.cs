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
    private Dictionary<string, GameObject> _skillDatabase = new Dictionary<string, GameObject>();

    // 생성된 인스턴스 캐싱 (String -> GameObject Instance)
    private Dictionary<string, GameObject> _activeSkills = new Dictionary<string, GameObject>();

    private BossRelicController _relicController;
    private Transform[] _monsterSpawnPoints;
    private Transform[] _lightningSpawnPoints;

    public void SetSpawnPoints(Transform[] points) { _monsterSpawnPoints = points; }

    public void SetLightningPoints(Transform[] points) { _lightningSpawnPoints = points; }

    private void Awake()
    {
        _relicController = GetComponent<BossRelicController>();

        // 리스트를 딕셔너리로 변환
        foreach (var data in _skillList)
        {
            if (string.IsNullOrEmpty(data.skillName)) continue;

            if (!_skillDatabase.ContainsKey(data.skillName))
            {
                _skillDatabase.Add(data.skillName, data.prefab);
            }
        }
    }

    // 타임라인 Signal Receiver에서 호출할 함수 (String사용)
    public void CastSkill(string skillName)
    {
        // 키가 존재하는지 확인
        if (!_skillDatabase.ContainsKey(skillName))
        {
            Debug.LogError($"[BossSkill] '{skillName}'라는 이름의 스킬을 찾을 수 없습니다! 오타를 확인하세요.");
            return;
        }

        // 인스턴스 없으면 생성
        if (!_activeSkills.ContainsKey(skillName) || _activeSkills[skillName] == null)
        {
            GameObject prefab = _skillDatabase[skillName];
            GameObject instance = Instantiate(prefab, transform);
            _activeSkills[skillName] = instance;
        }

        GameObject skillObj = _activeSkills[skillName];
        BossSkillBase skillLogic = skillObj.GetComponent<BossSkillBase>();
        if (skillLogic != null)
        {
            BossSkillContext context = new BossSkillContext()
            {
                _targetPosition = Managers.Party.GetCurrentCharacter().transform.position,
                _spawnPoints = _monsterSpawnPoints,
                _boss = this.gameObject,
                _lightningPoints = _lightningSpawnPoints,
                _bossRelicController = _relicController
            };
            skillLogic.Cast(context);
        }
        else
        {
            skillObj.GetComponent<ParticleSystem>().Stop();
            skillObj.GetComponent<ParticleSystem>().Play();
        }
    }
}
