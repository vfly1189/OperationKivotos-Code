using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static BaseCharacter;

public class MonsterStat : BaseStat
{
    [Header("Data")]
    [SerializeField] private MonsterDataSO _data; // 초기 데이터
    // 몬스터 전용: 처치 시 주는 경험치, 드랍 아이템 확률 등
    public float DropExpAmount = 300f;

    public void Init(MonsterDataSO data, int stageLevel)
    {
        base.Init();
        // 스테이지 레벨에 따른 스탯 뻥튀기 적용
        float multiplier = 1 + (stageLevel * 0.1f);

        MaxHp.SetBaseValue(data.MaxHp * multiplier);
        Attack.SetBaseValue(data.Attack * multiplier);
        Defense.SetBaseValue(data.Defense * multiplier);

        CurrentHp = MaxHp.Value;
    }

    public override void Init()
    {
        base.Init();
        if (_data != null)
        {
            MaxHp.SetBaseValue(_data.MaxHp);
            Attack.SetBaseValue(_data.Attack);
            Defense.SetBaseValue(_data.Defense);
            CurrentHp = MaxHp.Value;
        }
    }

    public override void TakeDamage(float damage, GameObject shooter)
    {
        // 1. 이미 죽었으면 무시
        if (CurrentHp <= 0) return;

        // 2. 데미지 계산 (최소 1 데미지 보장)
        // 방어력이 높으면 데미지가 0이 될 수 있으므로 Mathf.Max(..., 1) 사용
        float finalDamage = Mathf.Max(damage - Defense.Value, 1f);

        // 3. HP 차감
        CurrentHp -= finalDamage;

        // 4. HP 범위 제한 (0 ~ MaxHp)
        CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp.Value);

        Debug.Log($"몬스터 피격! 남은 체력: {CurrentHp}");

        // 5. HP 변경 이벤트 발생 (UI 갱신용)
        CallOnHpChanged(CurrentHp, MaxHp.Value);

        // 6. 사망 처리
        if (CurrentHp <= 0)
        {
            HandleDeath(shooter);
        }
    }

    protected override void HandleDeath(GameObject shooter)
    {
        // 1. 공격자가 있고, 플레이어라면 경험치 지급
        if (shooter != null && shooter.CompareTag("Player"))
        {
            // 플레이어 스탯 컴포넌트 가져오기 (예: PlayerStat)
            CharacterStat playerStat = shooter.GetComponent<CharacterStat>();
            if (playerStat != null)
            {
                playerStat.AddExp(DropExpAmount); // 경험치 추가 함수 호출
                Debug.Log($"플레이어에게 경험치 {DropExpAmount} 지급!");
            }
        }

        // 2. 몬스터 소멸 처리
        Debug.Log("몬스터 사망!");
        //Managers.Resource.Destroy(gameObject); // 혹은 풀링 반납
        //몬스터 사망은 MonsterController에서 처리함
    }
}
