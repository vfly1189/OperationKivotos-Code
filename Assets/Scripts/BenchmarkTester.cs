using UnityEngine;

public class BenchmarkTester : MonoBehaviour
{
    private void Update()
    {
        // 전체 켜기 (비교 대상)
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Managers.Sector.ActivateAllSectors();
            Debug.Log("[Benchmark] 전체 Sector 활성화");
        }

        // Sector 모드 복귀 (전체 비활성화 → 플레이어가 구역 재진입으로 자동 켜짐)
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Managers.Sector.DeactivateAllSectors();
            Debug.Log("[Benchmark] 전체 Sector 비활성화 → 구역 진입 대기");
        }
    }
}
