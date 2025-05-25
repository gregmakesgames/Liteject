#if !NOT_UNITY3D

namespace Liteject
{
    public interface IPrefabProvider
    {
        UnityEngine.Object GetPrefab(InjectContext context);
    }
}

#endif

