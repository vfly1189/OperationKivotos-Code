// ApplyStatModifier의 적용 대상 범위.
public enum ETargetScope
{
    Self,    // 시전자 (가드 등)
    Party,   // 파티 전체 (버프 등)
    Target,  // 지정 대상 (표식 등 — ctx.Target)
}
