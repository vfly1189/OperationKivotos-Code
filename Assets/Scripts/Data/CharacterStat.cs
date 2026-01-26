using System;
using UnityEngine;

public class CharacterStat : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] 
    private CharacterDataSO _data; // 초기 데이터

    // 계산된 스탯들
    public Stat MaxHp;
    public Stat Attack;
    public Stat Defense;
    public Stat MoveSpeed;
    public Stat AttackSpeed;
    public Stat MaxEnergy;

    // 실시간 변동 스탯들
    public float CurrentHp { get; private set; }
    public float CurrentEnergy { get; private set; }

    public event Action<float, float> OnHpChanged; // cur , max
    public event Action<float ,float> OnEnergyChanged; // cur , max


    public void Init()
    {
        // 초기화
        MaxHp = new Stat();
        Attack = new Stat();
        Defense = new Stat();
        MoveSpeed = new Stat();
        AttackSpeed = new Stat();
        MaxEnergy = new Stat();

        if (_data != null) SetCharacterData(_data);
    }


    void Awake()
    {
          
    }

    public void SetCharacterData(CharacterDataSO data)
    {
        _data = data;

        // 기본값 세팅
        MaxHp.SetBaseValue(data.MaxHp);
        Attack.SetBaseValue(data.Attack);
        Defense.SetBaseValue(data.Defense);
        MoveSpeed.SetBaseValue(data.MoveSpeed);
        AttackSpeed.SetBaseValue(data.AttackSpeed);
        MaxEnergy.SetBaseValue(data.MaxEnergy);

        CurrentHp = MaxHp.Value; // 체력 풀로 채우기
        CurrentEnergy = 0;
    }

    // 데미지 받는 함수 예시
    public void TakeDamage(float damage)
    {
        // 방어력 적용 공식 (예: 데미지 감소)
        float finalDamage = Mathf.Max(damage - Defense.Value, 1);

        CurrentHp -= finalDamage;
        CurrentHp = Mathf.Clamp(CurrentHp, 0, MaxHp.Value); // 0~Max 사이 유지

        Debug.Log($"{name} took {finalDamage} damage. Current HP: {CurrentHp}");

        if (CurrentHp <= 0)
        {
            // 사망 처리 (BaseCharacter에게 알리거나 직접 처리)
            //GetComponent<BaseCharacter>().OnDead();


            //OnDead();
        }
    }

    private void OnDead()
    {
        Debug.Log($"{name} Died.");
        // 사망 처리
    }


    //테스트용
    private void Update()
    {
        //CurrentEnergy += 5.0f * Time.deltaTime;

        //if (CurrentEnergy > MaxEnergy.Value)
        //{
        //    CurrentEnergy = MaxEnergy.Value;
        //}

        //OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy.Value);
    }
}
