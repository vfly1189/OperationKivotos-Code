using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TagMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField] private GameplayTagSO _tag;
    [SerializeField] private bool _retroactive;   // 마커를 지난 시점부터 재생해도 발사
    [SerializeField] private bool _emitOnce;      // 루프에서 한 번만 발사

    public GameplayTagSO Tag => _tag;
    public PropertyName id => new PropertyName(nameof(TagMarker));

    NotificationFlags INotificationOptionProvider.flags =>
        (_retroactive ? NotificationFlags.Retroactive : 0) |
        (_emitOnce ? NotificationFlags.TriggerOnce : 0);
}