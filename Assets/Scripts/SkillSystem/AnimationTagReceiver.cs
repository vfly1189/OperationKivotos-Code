using UnityEngine;

// 애니메이션 이벤트 → SS 신호. TimelineTagReceiver(TagMarker)와 같은 자리의 애니 클립 버전.
// 클립 이벤트 함수: OnAnimTag(태그 에셋) · PlaySfx(오디오 클립) — Animator와 같은 오브젝트에 붙인다.
// 함수 이름은 오브젝트 안에서 유일해야 한다 — 애니 이벤트는 같은 이름의 함수를 가진 컴포넌트 전부에 간다.
//  (SendTag로 두면 SkillSystem.SendTag도 같이 불려 태그가 두 번 들어간다)
public class AnimationTagReceiver : MonoBehaviour
{
    private SkillSystem _ss;

    private void Awake() => _ss = GetComponent<SkillSystem>();

    public void OnAnimTag(GameplayTagSO tag)
    {
        if (tag != null) _ss.SendTag(tag);
    }

    // 소리는 신호가 아니라 그 프레임의 연출 — 패턴이 끊겨도 클립이 돌면 난다
    public void PlaySfx(AudioClip clip)
    {
        if (clip != null) Managers.Sound.Play(clip, Define.Sound.Effect);
    }
}
