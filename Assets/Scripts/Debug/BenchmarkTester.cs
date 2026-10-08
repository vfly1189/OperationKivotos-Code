using UnityEngine;

public class BenchmarkTester : MonoBehaviour
{
    private void Update()
    {
        // 전체 켜기 (비교 대상)
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Managers.Activation.SetForceAll(true);
            GameLog.Log("[Benchmark] 전체 스포너 활성화");
        }

        // 정책 복귀 (정책이 켠 스포너만 남기고 끔)
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Managers.Activation.SetForceAll(false);
            GameLog.Log("[Benchmark] 전체 활성화 해제 → 정책이 켠 스포너만 남김");
        }

        // 리쉬 + 교전 유지 켜기/끄기 — Before 녹화(원래 Sector)용. 리쉬 없이 교전 유지만 켜면 교전이 안 끝날 수 있어 같이 묶는다.
        if (Input.GetKeyDown(KeyCode.F3))
        {
            bool on = !NormalMonsterController.LeashEnabled;
            NormalMonsterController.LeashEnabled = on;
            Managers.Spawner._keepEngaged = on;
            GameLog.Log($"[Benchmark] 리쉬 · 교전 유지 {(on ? "켜짐" : "꺼짐")}");
        }
    }
}
