using System;
using UnityEngine;

public readonly struct ResourceKey : IEquatable<ResourceKey>
{
    public readonly string Key;
    public readonly Type type;

    public ResourceKey(string key, Type type) { Key = key; this.type = type; }

    public bool Equals(ResourceKey other) => Key == other.Key && type == other.type;

    public override int GetHashCode() => (Key, type).GetHashCode();
}
