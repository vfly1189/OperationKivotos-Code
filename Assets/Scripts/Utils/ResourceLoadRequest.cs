using UnityEngine;

public class ResourceLoadRequest
{
    public string[] resourePaths;

    public System.Action<UnityEngine.Object> onComplete;

    public ResourceLoadRequest(string[] paths , System.Action<UnityEngine.Object> callback = null)
    {
        resourePaths = paths;
        onComplete = callback;
    }
}
