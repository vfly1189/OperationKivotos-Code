using System;
using System.Collections.Generic;

using UnityEngine;

public interface IDataCacheable
{
    void CacheData(Dictionary<Type, object> dataDicts);
}
