using Cysharp.Threading.Tasks;
using SixLabors.Fonts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MonsterTankController : RangedMonsterController
{
    [Header("Artillery Settings")]
    [SerializeField] private TankBombController[] _bombPoints;

    [SerializeField] private float _delayTime = 1.0f;   // 경고 후 폭발까지 시간
    [SerializeField] private float _randomSpread = 1.5f; // 탄착군 범위
    [SerializeField] private float _explosionRadius = 1.0f; //폭발 범위

    [Header("Debug Settings")]
    [SerializeField] private bool _showExplosionGizmo = true; // 기즈모 켜고 끄기

    int _flag = 0;   
    private readonly float _decalYOffset = 0.05f;// 데칼이 바닥에 묻히지 않도록 띄울 높이

    // 디버그용: 마지막으로 폭발한 위치와 유지 시간을 기억하는 클래스
    private class GizmoInfo
    {
        public Vector3 Position;
        public float ExpireTime;
    }
    private List<GizmoInfo> _explosionGizmos = new List<GizmoInfo>();
    private float _gizmoDuration = 2.0f;


    // 공격할 때 호출됨 (애니메이션 이벤트에서 연결)
    protected override void PerformAttackAction()
    {
        if (_bombPoints == null || _bombPoints.Length == 0 || _monsterCts == null) return;

        //// 2발을 동시에 쏘는 로직 시작
        //StartCoroutine(CoArtilleryAttackSequence(_flag));

        // [핵심 1] 코루틴 대신 UniTask 실행, 부모의 전역 토큰 전달
        ArtilleryAttackSequenceAsync(_flag, _monsterCts.Token).Forget();

        // 0 -> 1 -> 0 -> 1 순환
        _flag = (_flag + 1) % _bombPoints.Length;
    }

    //private IEnumerator CoArtilleryAttackSequence(int index)
    //{
    //    // 기준점 설정
    //    Vector3 centerPoint = _target.position;

    //    // 랜덤 위치 계산
    //    Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * _randomSpread;
    //    Vector3 targetPos = centerPoint + new Vector3(randomCircle.x, 0f, randomCircle.y);
    //    targetPos = GetGroundPosition(targetPos);

    //    //Z-Fighting 방지를 위해 Y축을 살짝 올림
    //    targetPos.y += _decalYOffset;

    //    _bombPoints[index].ShowWarning(targetPos);

    //    // 경고 시간 대기
    //    yield return new WaitForSeconds(_delayTime);

    //    // 폭발 및 데미지
    //    _bombPoints[index].Explode();
    //    ApplyAreaDamage(_bombPoints[index].transform.position);

    //    // 이펙트 재생 시간만큼 대기 후 복귀)
    //    yield return new WaitForSeconds(1.0f);

    //    // 다시 탱크 자식으로 복귀 및 비활성화
    //    _bombPoints[index].ResetState();
    //}

    // [핵심 2] UniTask로 변환된 폭격 시퀀스
    private async UniTaskVoid ArtilleryAttackSequenceAsync(int index, CancellationToken token)
    {
        if (_target == null) return;

        // 기준점 설정 및 랜덤 위치 계산
        Vector3 centerPoint = _target.position;
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * _randomSpread;
        Vector3 targetPos = centerPoint + new Vector3(randomCircle.x, 0f, randomCircle.y);

        targetPos = GetGroundPosition(targetPos);
        targetPos.y += _decalYOffset; // Z-Fighting 방지

        // 폭격 지점 경고 표시
        _bombPoints[index].ShowWarning(targetPos);

        // 경고 시간 대기 (취소 시 에러 없이 종료)
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_delayTime), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled)
        {
            _bombPoints[index].ResetState(); // 취소되면 탱크 자식으로 강제 복귀
            return;
        }

        // 폭발 및 데미지 적용
        _bombPoints[index].Explode();
        ApplyAreaDamage(_bombPoints[index].transform.position);

        // 이펙트 재생 시간 대기
        isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled) return;

        // 대기가 끝난 후 다시 탱크 자식으로 복귀 및 비활성화
        _bombPoints[index].ResetState();
    }

    // 지형의 정확한 Y값을 찾기 위한 Raycast
    private Vector3 GetGroundPosition(Vector3 targetPos)
    {
        // 하늘 높은 곳에서 아래로 레이를 쏨
        RaycastHit hit;

        int layerMask = LayerMask.GetMask("MapGround");
        // 지형 레이어 마스크가 있다면 1 << LayerMask.NameToLayer("Ground") 등을 사용
        if (Physics.Raycast(targetPos + Vector3.up * 10f, Vector3.down, out hit, 20f, layerMask))
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
           
            _explosionGizmos.Add(new GizmoInfo { Position = pos, ExpireTime = Time.time + _gizmoDuration });
        }

        // 반경 내의 모든 콜라이더 검출
        int layerMask = 1 << LayerMask.NameToLayer("Player");
        Collider[] colliders = Physics.OverlapSphere(pos, _explosionRadius, layerMask);

        foreach (Collider col in colliders)
        {
            if(col.TryGetComponent<IDamageable>(out IDamageable target))
            {
                DamageInfo damageInfo = new DamageInfo();
                damageInfo.Amount = Stat.Attack.Value;
                damageInfo.Attacker = this.gameObject;
                damageInfo.IsCritical = false;

                // 정확한 타격 위치 계산 (이펙트 용)
                Vector3 hitPoint = col.ClosestPoint(transform.position);
                damageInfo.HitPoint = hitPoint;
                // 인터페이스 메서드 호출 (상대가 Player든 Monster든 상관 안 함)
                target.TakeDamage(damageInfo);
            }


            //GameLog.Log($"Hit Player! at {pos}");
        }
    }


    ////에디터 Scene 뷰에서 그림을 그려주는 함수
    //private void OnDrawGizmos()
    //{
    //    if (_showExplosionGizmo == false) return;

    //    // 1. 탄착군 범위 (하늘색 원)
    //    Gizmos.color = Color.cyan;
    //    if (_target != null) // 타겟이 있을 때만
    //    {
    //        Vector3 center = _target.position; // 혹은 transform.position + forward * 4
    //        Gizmos.DrawWireSphere(center, _randomSpread);
    //    }

    //    // 2. 실제 폭발 반경 (빨간색 구체)
    //    Gizmos.color = new Color(1, 0, 0, 0.5f); // 반투명 빨강
    //    foreach (var pos in _lastExplosionPositions)
    //    {
    //        Gizmos.DrawSphere(pos, _explosionRadius); // explosionRadius와 같게 설정
    //    }
    //}

    // 에디터 Scene 뷰에서 그림을 그려주는 함수 (유니티 생명주기)
    private void OnDrawGizmos()
    {
        if (!_showExplosionGizmo) return;

        // 1. 탄착군 범위 (하늘색 원)
        Gizmos.color = Color.cyan;
        if (_target != null)
        {
            Gizmos.DrawWireSphere(_target.position, _randomSpread);
        }

        // 2. 실제 폭발 반경 (빨간색 구체)
        Gizmos.color = new Color(1, 0, 0, 0.5f);

        // 시간이 지난 기즈모 데이터는 여기서 그려주면서 동시에 지워줌
        for (int i = _explosionGizmos.Count - 1; i >= 0; i--)
        {
            if (Time.time > _explosionGizmos[i].ExpireTime)
            {
                _explosionGizmos.RemoveAt(i); // 시간 만료 시 제거
            }
            else
            {
                Gizmos.DrawSphere(_explosionGizmos[i].Position, _explosionRadius);
            }
        }
    }

}
