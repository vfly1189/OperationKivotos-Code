using UnityEngine;

public class PartyWipeCondition : BaseCondition
{
    public override void SetUp()
    {
        // 하위 시스템에 직접 접근하지 않고 Manager의 이벤트를 구독
        Managers.Party.OnPartyWiped += OnPartyWiped;
    }

    private void OnPartyWiped()
    {
        InvokeConditionMet();
    }

    private void OnDestroy()
    {
        if (Managers.Party != null)
            Managers.Party.OnPartyWiped -= OnPartyWiped;
    }
}
