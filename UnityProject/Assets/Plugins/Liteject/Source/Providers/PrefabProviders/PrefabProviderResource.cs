#if !NOT_UNITY3D

using Liteject;
using Liteject.Internal;
using UnityEngine;

namespace Liteject
{
    [NoReflectionBaking]
    public class PrefabProviderResource : IPrefabProvider
    {
        readonly string _resourcePath;

        public PrefabProviderResource(string resourcePath)
        {
            _resourcePath = resourcePath;
        }

        public UnityEngine.Object GetPrefab(InjectContext context)
        {
            var prefab = (GameObject)Resources.Load(_resourcePath);

            Assert.That(prefab != null,
                "Expected to find prefab at resource path '{0}'", _resourcePath);

            return prefab;
        }
    }
}

#endif

