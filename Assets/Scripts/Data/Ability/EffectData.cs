using UnityEngine;

public abstract class EffectData : ScriptableObject
{
    public abstract IEffect CreateRuntime();
}
