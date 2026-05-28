using UnityEngine;

public class TrailTest : MonoBehaviour
{
    public float width = 5.0f;
    public float height = 5.0f;
    public float baseSpeed = 5.0f; // 기본 속도 (500은 너무 큽니다, 5정도로 시작하세요)

    [Range(0.1f, 3f)]
    public float horizontalSpeedMult = 1.0f; // 가로 이동 속도 배율
    [Range(0.1f, 3f)]
    public float verticalSpeedMult = 1.0f;   // 세로 이동 속도 배율

    private Vector3[] _corners;
    private int _targetIndex = 0;

    void Start()
    {
        UpdateCorners(); // 시작할 때 코너 계산
        transform.localPosition = _corners[0];
    }

    // 인스펙터에서 값 바꾸면 실시간으로 코너 위치 갱신되게 (테스트용)
    void OnValidate()
    {
        UpdateCorners();
    }

    void UpdateCorners()
    {
        float w = width / 2;
        float h = height / 2;

        _corners = new Vector3[]
        {
            new Vector3(w, h, 2),   // 0: 우상 (다음 목표가 우하 -> 세로 이동)
            new Vector3(w, -h, 2),  // 1: 우하 (다음 목표가 좌하 -> 가로 이동)
            new Vector3(-w, -h, 2), // 2: 좌하 (다음 목표가 좌상 -> 세로 이동)
            new Vector3(-w, h, 2)   // 3: 좌상 (다음 목표가 우상 -> 가로 이동)
        };
    }

    void Update()
    {
        Vector3 target = _corners[_targetIndex];

        // 현재 어느 방향으로 가고 있는지에 따라 속도 다르게 적용
        // _targetIndex가 1(우하로 감)이거나 3(좌상으로 감)이면 '세로 이동' 중
        // _targetIndex가 2(좌하로 감)이거나 0(우상으로 감)이면 '가로 이동' 중

        // 코너 인덱스: 0(우상) -> 1(우하) : 세로 이동
        // 코너 인덱스: 1(우하) -> 2(좌하) : 가로 이동
        // 코너 인덱스: 2(좌하) -> 3(좌상) : 세로 이동
        // 코너 인덱스: 3(좌상) -> 0(우상) : 가로 이동

        float currentSpeed = baseSpeed;

        // 타겟 인덱스에 따라 현재 이동 방향 판단
        if (_targetIndex == 1 || _targetIndex == 3)
            currentSpeed *= verticalSpeedMult; // 세로 이동 중
        else
            currentSpeed *= horizontalSpeedMult; // 가로 이동 중

        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, currentSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.localPosition, target) < 0.01f)
        {
            _targetIndex = (_targetIndex + 1) % 4;
        }
    }
}
