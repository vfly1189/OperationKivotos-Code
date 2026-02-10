using System.Collections;
using UnityEngine;

public class SummonMonsterSkill : BossSkillBase
{
    [Header("설정")]
    [SerializeField] private GameObject _monsterRL;
    [SerializeField] private AudioClip _sfx;

    public override void Cast(BossSkillContext context)
    {
        // 프리팹이 활성화될 때 코루틴 시작
        StartCoroutine(ProcessSkillRoutine(context._spawnPoints));
    }

    private IEnumerator ProcessSkillRoutine(Transform[] monsterSpawnPoints)
    {
       // GameObject root = new GameObject { name = "@Test" }; 
        for(int i=0; i<monsterSpawnPoints.Length; i++)
        {
            // 1. 생성 시 위치/회전 지정 (이게 제일 안전함)
            GameObject monster = Managers.Resource.Instantiate(_monsterRL, monsterSpawnPoints[i].position, Quaternion.identity);

            //// 2. 만약 NavMeshAgent가 있다면 Warp로 강제 이동
            //UnityEngine.AI.NavMeshAgent agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
            //if (agent != null)
            //{
            //    // Instantiate 직후에 NavMeshAgent가 켜져 있으면 위치 할당이 씹힐 수 있음
            //    // 확실하게 Warp로 이동
            //    agent.Warp(monsterSpawnPoints[i].position);
            //}

            yield return null;
        }
    }
}
