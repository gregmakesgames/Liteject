using UnityEngine;
using Liteject;

namespace Liteject.Tests.Installers.CompositeMonoInstallers
{
    public class FooInjecteeInstaller : MonoInstaller<FooInjecteeInstaller>
    {
        public override void InstallBindings()
        {
            Container
                .Bind<FooInjectee>()
                .AsSingle()
                .NonLazy();
        }
    }
}