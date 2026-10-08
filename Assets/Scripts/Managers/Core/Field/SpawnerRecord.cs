using System.Collections.Generic;
using UnityEngine;

public class SpawnerRecord
{
    public readonly int _spawnerId;
    public readonly Vector3 _center;
    public bool IsActive = false;
    public readonly SpawnSlot[] _slots;

    public SpawnerRecord(int spawnerId, Vector3 center, SpawnSlot[] slots)
    {
        _spawnerId = spawnerId;
        _center = center;
        _slots = slots;
    }


}
