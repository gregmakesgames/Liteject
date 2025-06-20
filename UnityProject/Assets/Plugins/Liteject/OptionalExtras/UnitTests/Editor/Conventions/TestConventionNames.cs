
#if !(UNITY_WSA && ENABLE_DOTNET)

using NUnit.Framework;
using Assert = Liteject.Internal.Assert;

namespace Liteject.Tests.Convention.Names
{
    [TestFixture]
    public class TestConventionNames : ZenjectUnitTestFixture
    {
        [Test]
        public void TestWithSuffix()
        {
            Container.Bind<IController>()
                .To(x => x.AllNonAbstractClasses().InNamespace("Liteject.Tests.Convention.Names").WithSuffix("Controller")).AsTransient();

            Assert.That(Container.Resolve<IController>() is FooController);
        }

        [Test]
        public void TestWithPrefix()
        {
            Container.Bind<IController>()
                .To(x => x.AllTypes().InNamespace("Liteject.Tests.Convention.Names").WithPrefix("Controller")).AsTransient();

            Assert.That(Container.Resolve<IController>() is ControllerBar);
        }

        [Test]
        public void TestMatchingRegex()
        {
            Container.Bind<IController>()
                .To(x => x.AllNonAbstractClasses().InNamespace("Liteject.Tests.Convention.Names").MatchingRegex("Controller$")).AsTransient();

            Assert.That(Container.Resolve<IController>() is FooController);
        }

        interface IController
        {
        }

        class FooController : IController
        {
        }

        class ControllerBar : IController
        {
        }

        class QuxControllerAsdf : IController
        {
        }

        class IgnoredFooController
        {
        }

        class ControllerBarIgnored
        {
        }
    }
}

#endif
