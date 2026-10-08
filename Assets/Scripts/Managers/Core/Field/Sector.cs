using UnityEngine;

// 구역 하나 — 영역(BoxCollider)과 그 안의 스포너 id만 가진다. 켜고 끄는 판정은 ① · ② 정책(SectorActivation ·
//  AdjacentSectorActivation)이 매 프레임 플레이어 위치로 한다. 섹터마다 독립이라 겹침 구간에선 둘 다 안일 수 있다.
public class Sector : MonoBehaviour
{
    [SerializeField] public int _sectorID;

    // 정책은 스포너 id만 안다. 몬스터 · 풀 · 팩토리는 SpawnerManager 몫.
    private int[] _spawnerIds;
    private bool _isInitialized = false;

    private BoxCollider _area;

    // ① · ② 정책이 각자(그림자 포함) 부르므로 두 번째부터는 무시한다.
    public void Init()
    {
        if (_isInitialized) return;

        MonsterSpawner[] spawners = GetComponentsInChildren<MonsterSpawner>(true);
        _spawnerIds = new int[spawners.Length];
        for (int i = 0; i < spawners.Length; i++)
            _spawnerIds[i] = spawners[i].SpawnerID;

        _area = GetComponent<BoxCollider>();
        _isInitialized = true;
    }

    // 위치가 영역 안인가 — 수평(XZ)만 본다. 박스 높이가 1m라 계단·경사에서 높이로 빠지지 않게
    //  y는 박스 중심 높이로 맞춘다. 회전된 박스도 ClosestPoint가 처리(안이면 입력 위치 그대로 반환).
    public bool Contains(Vector3 position)
    {
        if (_area == null) return false;

        position.y = _area.bounds.center.y;
        return (_area.ClosestPoint(position) - position).sqrMagnitude < 0.0001f;
    }

    public int[] GetSpawnersID()
    {
        return _spawnerIds;
    }

#if UNITY_EDITOR
    // 씬 뷰에서 Sector의 영역을 시각적으로 확인하기 위한 기즈모
    private void OnDrawGizmos()
    {
        DrawGizmoArea(Color.green);
    }

    // 활성화 정책도 같은 박스를 다른 색으로 덮어 그린다 (서 있는 섹터 · 인접 섹터)
    public void DrawGizmoArea(Color color)
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(color.r, color.g, color.b, 0.2f); // 반투명
        Gizmos.DrawCube(col.center, col.size);
        Gizmos.color = color;
        Gizmos.DrawWireCube(col.center, col.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
