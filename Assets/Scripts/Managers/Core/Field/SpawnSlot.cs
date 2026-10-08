using UnityEngine;


public enum SlotState { Waiting, Spawning, Alive, Dying }

public class SpawnSlot
{
    public readonly int _spawnerId;
    public readonly int _monsterId;
    public readonly float _respawnTime;
    public readonly Vector3 _pos;
    public readonly Quaternion _rot;

    public float _readyAt;
    public int _gen;

    public SlotState _state;

    // 지금 기다리는 게 사망 뒤 리스폰인가 — 계측(0-4)에서 정책 때문에 생긴 스폰과 구분한다.
    public bool _afterDeath;

    public BaseMonsterController _instance;

    public SpawnSlot(int spawnerId, int monsterId, float respawnTime,  Vector3 pos, Quaternion rot)
    {
        _spawnerId = spawnerId;
        _monsterId = monsterId;
        _respawnTime = respawnTime;
        _pos = pos;
        _rot = rot;

        _state = SlotState.Waiting;
    }
}
