using UnityEngine;
using UnityEngine.Playables;

public class TimelineTagReceiver : MonoBehaviour, INotificationReceiver
{
    private SkillSystem _ss;

    private void Awake() => _ss = GetComponent<SkillSystem>();

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is TagMarker marker && marker.Tag != null)
            _ss.SendTag(marker.Tag);
    }
}