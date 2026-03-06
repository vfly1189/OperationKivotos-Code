using System;
using System.Collections.Generic;
using UnityEditor.VisionOS;
using UnityEngine;

public interface IDataCacheable
{
    void CacheData(Dictionary<Type, object> dataDicts);
}
