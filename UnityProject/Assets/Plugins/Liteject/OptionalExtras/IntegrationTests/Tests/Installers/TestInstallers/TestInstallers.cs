
using System.Collections;
using Liteject;
using UnityEngine.TestTools;
using Liteject.Tests.Installers.Installers;
using Liteject.Internal;

namespace Liteject.Tests.Installers
{
    public class TestInstallers : ZenjectIntegrationTestFixture
    {
        [UnityTest]
        public IEnumerator TestZeroArgs()
        {
            PreInstall();
            FooInstaller.Install(Container);

            PostInstall();

            FixtureUtil.AssertResolveCount<Foo>(Container, 1);
            yield break;
        }

        [UnityTest]
        public IEnumerator TestOneArg()
        {
            PreInstall();
            BarInstaller.Install(Container, "blurg");

            PostInstall();

            Assert.IsEqual(Container.Resolve<string>(), "blurg");
            yield break;
        }

        [UnityTest]
        public IEnumerator TestThreeArgs()
        {
            PreInstall();
            QuxInstaller.Install(Container, "blurg", 2.0f, 1);

            PostInstall();

            Assert.IsEqual(Container.Resolve<string>(), "blurg");
            yield break;
        }
    }
}

