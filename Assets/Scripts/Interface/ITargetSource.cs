using UnityEngine;

public interface ITargetSource
{
    Transform CurrentTarget { get; }
}