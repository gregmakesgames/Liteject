using UnityEngine;

namespace Liteject.Tests.Bindings.FromSubContainerPrefab
{
    public class CircBar : MonoBehaviour
    {
        [Inject]
        public CircFoo Foo;
    }
}
