﻿
using System.Collections;
using Liteject;
using Liteject.Internal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Liteject.Tests.Bindings.FromGameObjectInstaller
{
    public class TestFromGameObjectInstaller : ZenjectIntegrationTestFixture
    {
        [UnityTest]
        public IEnumerator TestInstaller()
        {
            PreInstall();

            Container.Bind<Qux>().FromSubContainerResolve()
                .ByNewGameObjectInstaller<FooInstaller>().AsCached();

            PostInstall();

            Assert.IsEqual(Container.Resolve<Qux>().Data, "asdf");
            yield break;
        }

        [UnityTest]
        public IEnumerator TestMethod()
        {
            PreInstall();

            Container.Bind<Qux>().FromSubContainerResolve()
                .ByNewGameObjectMethod(InstallFoo).AsCached();

            PostInstall();

            Assert.IsEqual(Container.Resolve<Qux>().Data, "asdf");
            yield break;
        }

        void InstallFoo(DiContainer subContainer)
        {
            subContainer.Bind<Qux>().AsSingle().WithArguments("asdf");
        }

        public class Qux
        {
            [Inject]
            public string Data;
        }

        public class FooInstaller : Installer<FooInstaller>
        {
            public override void InstallBindings()
            {
                Container.Bind<Qux>().AsSingle().WithArguments("asdf");
            }
        }
    }
}

