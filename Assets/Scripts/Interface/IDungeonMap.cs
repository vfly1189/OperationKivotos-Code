using UnityEngine;

public interface IDungeonMap
{
    Transform GetEndingCameraPoint();
    Transform[] GetEndingTransforms(); // 엔딩 포지션 배열
}
