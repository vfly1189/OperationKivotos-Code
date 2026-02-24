using UnityEngine;

public class UpgradeStone : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        // Space.Self 기준으로 돌아야 눕혀진 각도를 유지한 채로 회전합니다.
        transform.Rotate(0, 180f * Time.deltaTime, 0, Space.Self);
    }
}
