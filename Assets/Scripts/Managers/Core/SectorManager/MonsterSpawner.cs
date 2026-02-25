using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();
    // 현재 이 스포너가 관리중인 활성 몬스터 리스트
    private List<BaseCharacter> _activeMonsters = new List<BaseCharacter>();

    public void SpawnMonsters()
    {
        // TODO: Managers.Resource (풀링 시스템)을 연동하여 몬스터 소환 뼈대
        
        for(int i = 0; i < _spawnPoints.Count; i++)
        {
           
        }     
    }

    public void DespawnMonsters()
    {
        // TODO: 몬스터들을 풀에 반환하거나, 제자리로 돌려보내는 로직(Tethering)
        foreach (var monster in _activeMonsters)
        {
            Managers.Resource.Destroy(monster.gameObject);
        }
        _activeMonsters.Clear();
    }

#if UNITY_EDITOR
    // 에디터에서 스폰 구역을 시각적으로 확인하기 위한 기즈모
    private void OnDrawGizmos()
    {
    }
#endif
}
