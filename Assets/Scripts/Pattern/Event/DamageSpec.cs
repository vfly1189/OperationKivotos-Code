using System;
using UnityEngine;

[Serializable]
public class DamageSpec
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
   
    public DamageInfo Pack(in PatternContext ctx)
    {
        return ctx.stat.BuildOutgoingDamage(ctx.owner.gameObject);
    }
}
