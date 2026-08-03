using UnityEngine;

// 현재 시각(now) 제공자. AbilityRunner의 쿨다운 판단은 이 시계 '하나만' 읽는다.
// 기본 UnityClock은 Time.time을 그대로 돌려주므로 동작은 Time.time 직접 호출과 100% 동일하다.
// 테스트(가짜 시계 주입)나 선택적 시간 제어(몹만 슬로우 등)가 필요해지면 이 구현만 교체하면 되고,
// 쿨다운 '상태'는 여전히 러너가 per-caster로 소유한다. (전역 TimeManager를 두지 않는 이유)
public interface IClock
{
    float Now { get; }
}

// 프로젝트 기본 시계. 상태가 없어 공유 단일 인스턴스로 충분하다.
public sealed class UnityClock : IClock
{
    public static readonly UnityClock Instance = new UnityClock();
    public float Now => Time.time;
}
