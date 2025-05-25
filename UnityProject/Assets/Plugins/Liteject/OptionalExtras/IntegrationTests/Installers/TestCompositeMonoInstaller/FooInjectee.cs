using UnityEngine;
using Liteject;

namespace Liteject.Tests.Installers.CompositeMonoInstallers
{
    public class FooInjectee
    {
        public FooInjectee(Foo foo)
        {
            Foo = foo;
        }

        public Foo Foo { get; }
    }
}