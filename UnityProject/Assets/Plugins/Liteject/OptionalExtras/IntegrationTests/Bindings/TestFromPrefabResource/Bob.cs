using System;
using UnityEngine;

namespace Liteject.Tests.Bindings.FromPrefabResource
{
    public class Bob : MonoBehaviour
    {
        [NonSerialized]
        [Inject]
        public Jim Jim;
    }
}
