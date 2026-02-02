using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterTankController : MonsterController
{
    [Header("Artillery Settings")]
    [SerializeField] private TankBombController[] _bombPoints;

    [SerializeField] private float _delayTime = 1.0f;   // 경고 후 폭발까지 시간
    [SerializeField] private float _randomSpread = 1.5f; // 탄착군 범위
    [SerializeField] private float _explosionRadius = 1.0f; //폭발 범위

    int _flag = 0;   
    private readonly float _decalYOffset = 0.05f;// 데칼이 바닥에 묻히지 않도록 띄울 높이



    [Header("Debug Settings")]
    [SerializeField] private bool _showExplosionGizmo = true; // 기즈모 켜고 끄기

    // 디버그용: 마지막으로 폭발한 위치를 기억해둠 (기즈모 그리기 위해)
    private List<Vector3> _lastExplosionPositions = new List<Vector3>();
    private float _gizmoDuration = 2.0f; // 기즈모가 화면에 남을 시간



    // 공격할 때 호출됨 (애니메이션 이벤트에서 연결)
    protected override void PerformAttackAction()
    {
        // 2발을 동시에 쏘는 로직 시작
        StartCoroutine(CoArtilleryAttackSequence(_flag));

        // 0 -> 1 -> 0 -> 1 순환
        _flag = (_flag + 1) % _bombPoints.Length;
    }

    private IEnumerator CoArtilleryAttackSequence(int index)
    {
        // 기준점 설정
        Vector3 centerPoint = _target.position;


        // 랜덤 위치 계산
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * _randomSpread;
        Vector3 targetPos = centerPoint + new Vector3(randomCircle.x, 0f, randomCircle.y);
        targetPos = GetGroundPosition(targetPos);

        //Z-Fighting 방지를 위해 Y축을 살짝 올림
        targetPos.y += _decalYOffset;

        _bombPoints[index].ShowWarning(targetPos);

        // 경고 시간 대기
        yield return new WaitForSeconds(_delayTime);

        // 폭발 및 데미지
        _bombPoints[index].Explode();
        ApplyAreaDamage(_bombPoints[index].transform.position);

        // 이펙트 재생 시간만큼 대기 후 복귀)
        yield return new WaitForSeconds(1.0f);

        // 다시 탱크 자식으로 복귀 및 비활성화
        _bombPoints[index].ResetState();
    }

    // 지형의 정확한 Y값을 찾기 위한 Raycast
    private Vector3 GetGroundPosition(Vector3 targetPos)
    {
        // 하늘 높은 곳에서 아래로 레이를 쏨
        RaycastHit hit;
        // 지형 레이어 마스크가 있다면 1 << LayerMask.NameToLayer("Ground") 등을 사용
        if (Physics.Raycast(targetPos + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            return hit.point;
        }
        return targetPos; // 바닥을 못 찾으면 원래 좌표 반환
    }

    private void ApplyAreaDamage(Vector3 pos)
    {
        //float explosionRadius = 1.0f; // 폭발 반경

        if (_showExplosionGizmo)
        {
            StartCoroutine(DrawExplosionGizmo(pos, _explosionRadius));
        }

        // 반경 내의 모든 콜라이더 검출
        int layerMask = 1 << LayerMask.NameToLayer("Unit");
        Collider[] colliders = Physics.OverlapSphere(pos, _explosionRadius, layerMask);

        foreach (Collider col in colliders)
        {
            Debug.Log($"Hit Player! at {pos}");
        }
    }

    //폭발 위치에 잠시동안 구체를 그려주는 코루틴 (Scene 뷰용)
    private IEnumerator DrawExplosionGizmo(Vector3 pos, float radius)
    {
        // 리스트에 추가해서 OnDrawGizmos에서 그리게 함
        _lastExplosionPositions.Add(pos);

        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(_gizmoDuration);

        // 시간이 지나면 리스트에서 제거 (기즈모 사라짐)
        _lastExplosionPositions.Remove(pos);
    }

    //에디터 Scene 뷰에서 그림을 그려주는 함수
    private void OnDrawGizmos()
    {
        if (_showExplosionGizmo == false) return;

        // 1. 탄착군 범위 (하늘색 원)
        Gizmos.color = Color.cyan;
        if (_target != null) // 타겟이 있을 때만
        {
            Vector3 center = _target.position; // 혹은 transform.position + forward * 4
            Gizmos.DrawWireSphere(center, _randomSpread);
        }

        // 2. 실제 폭발 반경 (빨간색 구체)
        Gizmos.color = new Color(1, 0, 0, 0.5f); // 반투명 빨강
        foreach (var pos in _lastExplosionPositions)
        {
            Gizmos.DrawSphere(pos, _explosionRadius); // explosionRadius와 같게 설정
        }
    }

}
