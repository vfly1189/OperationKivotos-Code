using System.Collections.Generic;
using UnityEngine;

// ⑦ CullingGroup 거리 밴드 — 판정은 ⑤와 같고(켜기 20 / 끄기 25m), 거리 계산을 엔진 컬링 단계에 맡긴다.
// 밴드 0(< 20m) 켬 · 1(20~25m) 이전 상태 유지 · 2(≥ 25m) 끔 → 밴드 1이 ⑤의 히스테리시스 구간.
// 컬링은 렌더링 때 돌고 결과는 다음 프레임에 읽으므로 ⑤보다 1프레임 늦다(P10). 카메라가 렌더링하지 않으면 갱신도 멈춘다.
public class CullingGroupActivation : ActivationPolicy
{
    private const int BandOn = 0;
    private const int BandOff = 2;

    private readonly float[] _distances;

    private CullingGroup _group;
    private BoundingSphere[] _spheres;   // 엔진이 이 배열을 참조로 쥐고 있으므로 필드로 살려 둔다.
    private int[] _spawnerIds;           // 구 인덱스 → 스포너 id

    // 켜진 스포너. 콜백(컬링 중)이 바꿔 두고, 다음 SelectSpawners가 그대로 내보낸다.
    private readonly HashSet<int> _on = new HashSet<int>();

    private int _firstFrame;
    private bool _synced;

    public CullingGroupActivation(float onRadius, float offRadius)
    {
        _distances = new[] { onRadius, offRadius };
    }

    public override void Init(ActivationLayout layout)
    {
        Clear();

        SpawnerRecord[] records = layout._records;
        _spheres = new BoundingSphere[records.Length];
        _spawnerIds = new int[records.Length];

        // ⑤와 같은 바닥(x·z) 거리로 재도록 구와 기준점 모두 y = 0. 반경 0 = 스포너 중심점.
        for (int i = 0; i < records.Length; i++)
        {
            Vector3 c = records[i]._center;
            _spheres[i] = new BoundingSphere(new Vector3(c.x, 0f, c.z), 0f);
            _spawnerIds[i] = records[i]._spawnerId;
        }

        _group = new CullingGroup();
        _group.targetCamera = Camera.main;
        _group.SetBoundingSpheres(_spheres);
        _group.SetBoundingSphereCount(_spheres.Length);
        _group.SetBoundingDistances(_distances);
        _group.onStateChanged = OnStateChanged;

        if (_group.targetCamera == null)
            GameLog.LogError("[CullingGroupActivation] Camera.main 없음 — 컬링이 돌지 않아 아무것도 켜지지 않는다");
    }

    public override void SelectSpawners(Vector3 playerPos, HashSet<int> current, HashSet<int> result)
    {
        if (_group == null) return;

        // 이번 프레임 렌더링 때 이 기준점으로 컬링 → 결과는 다음 프레임에 반영.
        _group.SetDistanceReferencePoint(new Vector3(playerPos.x, 0f, playerPos.z));

        // 콜백은 밴드가 "바뀐" 구만 온다. 처음엔 기준점이 들어간 첫 컬링 이후 GetDistance로 1회 맞춘다.
        if (!_synced)
        {
            if (_firstFrame < 0) _firstFrame = Time.frameCount;
            else if (Time.frameCount > _firstFrame) Sync();
        }

        foreach (int id in _on) result.Add(id);
    }

    private void Sync()
    {
        for (int i = 0; i < _spawnerIds.Length; i++) Apply(i, _group.GetDistance(i));
        _synced = true;
    }

    private void OnStateChanged(CullingGroupEvent e) => Apply(e.index, e.currentDistance);

    private void Apply(int index, int band)
    {
        if (band == BandOn) _on.Add(_spawnerIds[index]);
        else if (band >= BandOff) _on.Remove(_spawnerIds[index]);
        // 밴드 1: 이전 상태 유지
    }

    public override string ToString() => $"CullingGroup · 밴드 {_distances[0]}m / {_distances[1]}m";

#if UNITY_EDITOR
    // 밴드 경계 = ⑤의 켜기 · 끄기 원. 실제 켜짐은 1프레임 전 기준점으로 한 컬링 결과다.
    public override void DrawGizmos(Vector3 playerPos) =>
        ActivationGizmos.DrawRange(playerPos, _distances[0], _distances[1]);
#endif

    public override void Clear()
    {
        if (_group != null)
        {
            _group.onStateChanged = null;
            _group.Dispose();
            _group = null;
        }

        _on.Clear();
        _firstFrame = -1;
        _synced = false;
    }
}
