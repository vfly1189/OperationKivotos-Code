using System;
using UnityEngine;

// 씬 오브젝트(몬스터/캐릭터 프리팹)가 자기 VFX 부착 위치들을 '이름표(id)'로 보관한다.
// SO(EffectData)는 씬 오브젝트를 참조할 수 없으므로, Effect는 런타임에
// ctx.CasterGO 에서 이 컴포넌트를 찾아 id로 Transform 을 조회한다.
//   → 위치는 인스펙터로 연결(예전 방식 유지), SO에는 '이름'만 둔다.
// 부착점이 여러 개(총구·탄피 배출·보조포신 등)로 늘어나도 id로 구분되므로
// GetComponentInChildren 식의 애매함이 없다.
public class VfxAnchorSet : MonoBehaviour
{
    [Serializable]
    public struct Anchor
    {
        public string Id;       // 예: "Muzzle", "ShellEject"
        public Transform Point; // 인스펙터로 연결하는 실제 위치
    }

    [SerializeField] private Anchor[] _anchors;

    // id에 해당하는 부착 위치. 없으면 null.
    public Transform Get(string id)
    {
        if (_anchors == null) return null;
        for (int i = 0; i < _anchors.Length; i++)
            if (_anchors[i].Id == id) return _anchors[i].Point;
        return null;
    }
}
