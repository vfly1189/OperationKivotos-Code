using UnityEngine;

public class UI_RotatingLight : MonoBehaviour
{
    // 외부에서 회전값을 주입받음
    public void SyncRotation(float angle)
    {
        // Z축 회전 적용
        transform.localRotation = Quaternion.Euler(0, 0, angle);
    }
}
