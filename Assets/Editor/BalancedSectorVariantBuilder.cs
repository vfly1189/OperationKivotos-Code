using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// 비교 실험용 변형 맵 — 원본 BackStreetMap은 그대로 두고, 섹터별 몬스터 수를 고르게 맞춘 복사본을 만든다.
//  규칙(측정 전에 고정): S1에서 1묶음, S4에서 3묶음을 S3로 옮긴다.
//    원본 102 / 68 / 6 / 119 / 68  →  변형 85 / 68 / 74 / 68 / 68
//  섹터 박스 · 스포너 ID · 데이터 표(pointIndex)는 그대로. 바뀌는 건 옮긴 묶음의 위치와 소속(부모)뿐이다.
//  옮길 자리는 NavMesh로 검사한다: 묶음의 모든 포인트가 메시 위 · S3 박스 안 · 다른 묶음과 12m 이상 · 플레이어 시작점에서 경로 있음.
//  docs/FieldEntity/03_Comparison_Plan.md
public static class BalancedSectorVariantBuilder
{
    const string SrcPath = "Assets/Resources_moved/Prefabs/Map/BackStreetMap.prefab";
    const string DstPath = "Assets/Resources_moved/Prefabs/Map/BackStreetMap_Balanced.prefab";

    static readonly int[] MoveIds = { 1009, 1012, 1013, 1014 };   // S1 1개 · S4 3개
    const int TargetSectorId = 3;

    const float Step = 1f;            // 후보 격자 간격
    const float MinGroupGap = 12f;    // 묶음 중심 사이 최소 거리 (묶음 반경 5.5m × 2 + 여유)
    const float OnMeshTolerance = 0.3f;

    [MenuItem("Tools/Field/Build Balanced Sector Variant")]
    static void Build()
    {
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(SrcPath);
        if (src == null) { Debug.LogError($"[Variant] 원본 없음: {SrcPath}"); return; }

        // 씬에 꺼내야 NavMeshSurface(ExecuteAlways)가 메시를 등록하고 콜라이더 판정이 된다. 끝나면 지운다.
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        try
        {
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            inst.transform.position = Vector3.zero;     // GameScene도 원점에 놓는다
            Physics.SyncTransforms();

            MonsterSpawner[] spawners = inst.GetComponentsInChildren<MonsterSpawner>(true);
            Sector target = inst.GetComponentsInChildren<Sector>(true).FirstOrDefault(s => s._sectorID == TargetSectorId);
            if (target == null) { Debug.LogError($"[Variant] Sector {TargetSectorId} 없음"); return; }
            target.Init();

            if (!NavMesh.SamplePosition(spawners[0].transform.GetChild(0).position, out _, 1f, NavMesh.AllAreas))
            {
                Debug.LogError("[Variant] NavMesh가 등록되지 않음 — 맵 프리팹의 NavMeshSurface 확인");
                return;
            }

            // 옮긴 묶음은 S3의 기존 스포너와 같은 부모 밑으로 (소속 = 섹터의 자식)
            Transform targetParent = spawners.First(s => s.GetComponentInParent<Sector>() == target).transform.parent;
            Vector3 start = inst.GetComponent<BaseMap>().GetPlayerSpawnPoint().position;
            BoxCollider box = target.GetComponent<BoxCollider>();

            List<Vector3> occupied = spawners.Where(s => !MoveIds.Contains(s.SpawnerID)).Select(s => s.transform.position).ToList();

            foreach (int id in MoveIds)
            {
                MonsterSpawner sp = spawners.FirstOrDefault(s => s.SpawnerID == id);
                if (sp == null) { Debug.LogError($"[Variant] 스포너 {id} 없음"); return; }

                Vector3 origin = sp.transform.position;
                Vector3[] offsets = new Vector3[sp.transform.childCount];
                for (int i = 0; i < offsets.Length; i++)
                    offsets[i] = sp.transform.GetChild(i).position - origin;

                if (!TryFindSpot(target, box.bounds, offsets, occupied, start, out Vector3 center))
                {
                    Debug.LogError($"[Variant] 스포너 {id}를 놓을 자리를 S{TargetSectorId}에서 못 찾음 — 중단(저장 안 함)");
                    return;
                }

                sp.transform.SetParent(targetParent, true);
                sp.transform.position = center;
                occupied.Add(center);
                Debug.Log($"[Variant] 스포너 {id}: ({origin.x:F1}, {origin.z:F1}) → ({center.x:F1}, {center.z:F1})");
            }

            PrefabUtility.SaveAsPrefabAsset(inst, DstPath);
            Debug.Log($"[Variant] 저장: {DstPath} — Addressable 등록 후 GameScenePreloadSO.mainVillage를 이 프리팹으로 바꾸면 변형 맵으로 실행");
        }
        finally
        {
            Object.DestroyImmediate(inst);
        }
    }

    // 박스 안을 격자로 훑어, 조건을 모두 통과한 자리 중 다른 묶음과 가장 멀리 떨어진 곳을 고른다(고르게 퍼지게).
    static bool TryFindSpot(Sector target, Bounds b, Vector3[] offsets, List<Vector3> occupied, Vector3 start, out Vector3 best)
    {
        best = default;
        float bestScore = -1f;
        NavMeshPath path = new NavMeshPath();

        for (float x = b.min.x; x <= b.max.x; x += Step)
        for (float z = b.min.z; z <= b.max.z; z += Step)
        {
            if (!NavMesh.SamplePosition(new Vector3(x, b.center.y, z), out NavMeshHit centerHit, 1f, NavMesh.AllAreas)) continue;
            Vector3 c = centerHit.position;

            float gap = occupied.Count == 0 ? float.MaxValue : occupied.Min(o => Horizontal(o, c));
            if (gap < MinGroupGap || gap <= bestScore) continue;

            if (!AllPointsValid(target, c, offsets)) continue;

            if (!NavMesh.CalculatePath(start, c, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            bestScore = gap;
            best = c;
        }
        return bestScore > 0f;
    }

    static bool AllPointsValid(Sector target, Vector3 center, Vector3[] offsets)
    {
        foreach (Vector3 off in offsets)
        {
            Vector3 p = center + off;
            if (!target.Contains(p)) return false;                                   // 자기 섹터 박스 안
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas)) return false;
            if (Horizontal(hit.position, p) > OnMeshTolerance) return false;         // 메시 가장자리 밖으로 밀려나면 실패
        }
        return true;
    }

    static float Horizontal(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
