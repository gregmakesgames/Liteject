using UnityEngine;

namespace Liteject.Tests.Bindings.FromSubContainerPrefab
{
    public class CircFoo : MonoBehaviour
    {
        [Inject]
        public CircBar Bar;
    }
}
