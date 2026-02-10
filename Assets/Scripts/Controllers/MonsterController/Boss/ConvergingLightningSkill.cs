using System.Collections;
using UnityEngine;

public class ConvergingLightningSkill : BossSkillBase
{
    [Header("설정")]
    [SerializeField] private GameObject _lightningSubPrefab;
    [SerializeField] private float _startDistance = 8f; // 시작 거리
    [SerializeField] private int _stepCount = 5;        // 몇 번에 걸쳐 다가갈지 (예: 5번 꽝꽝꽝꽝꽝)
    [SerializeField] private float _interval = 0.2f;    // 한 번 치고 대기하는 시간
    [SerializeField] private AudioClip _sfx;

    public override void Cast(BossSkillContext context)
    {
        // 프리팹이 활성화될 때 코루틴 시작
        StartCoroutine(ProcessSkillRoutine(context._targetPosition));
    }

    private IEnumerator ProcessSkillRoutine(Vector3 centerPos)
    {       
        // 1. 방향 설정 (동서남북)
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        // 인스턴스를 관리할 배열
        GameObject[] instances = new GameObject[4];
        ParticleSystem[] particles = new ParticleSystem[4];

        // 2. 일단 4개의 번개 객체를 미리 생성 (위치는 나중에 잡음)
        // (Tip: 번개 프리팹은 Loop가 꺼져있고 PlayOnAwake가 꺼져있어야 제어하기 편합니다)
        for (int i = 0; i < 4; i++)
        {
            instances[i] = Instantiate(_lightningSubPrefab, centerPos, Quaternion.identity);
            particles[i] = instances[i].GetComponent<ParticleSystem>();
            //instances[i].SetActive(false); // 일단 꺼둠
        }

        // 3. 단계별로 조여오며 실행 (Step Loop)
        for (int step = 0; step < _stepCount; step++)
        {
            // 현재 단계에서의 거리 계산 (0 ~ 1 사이 비율)
            // step이 0이면 시작거리, step이 마지막이면 0(중앙)에 가까워짐
            float t = (float)step / (_stepCount - 1);
            float currentDist = Mathf.Lerp(_startDistance, 0f, t);

            // 4방향 모두 처리
            for (int i = 0; i < 4; i++)
            {
                if (instances[i] != null)
                {
                    // 위치 이동
                    Vector3 newPos = centerPos + (directions[i] * currentDist);
                    instances[i].transform.position = newPos;

                    particles[i].Clear();
                    // 이펙트 초기화 후 재생 (중요: 잔상이 남지 않게 Clear)
                    
                    particles[i].time = 0;
                    particles[i].Play();
                }
            }
            Managers.Sound.Play(_sfx, Define.Sound.Effect);
            // 다음 쾅! 까지 대기
            yield return new WaitForSeconds(_interval);
        }

        // 4. 모든 단계 종료 후 조금 기다렸다가 삭제 (이펙트 잔여물 보여주기 위해)
        yield return new WaitForSeconds(1.0f);

        for (int i = 0; i < 4; i++)
        {
            if (instances[i] != null) Destroy(instances[i]);
        }
    }
}
