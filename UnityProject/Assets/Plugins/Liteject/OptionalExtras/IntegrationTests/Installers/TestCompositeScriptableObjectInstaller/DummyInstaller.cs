using UnityEngine;
using Liteject;

namespace Liteject.Tests.Installers.CompositeScriptableObjectInstallers
{
    // [CreateAssetMenu(fileName = "DummyInstaller", menuName = "Installers/DummyInstaller")]
    public class DummyInstaller : ScriptableObjectInstaller<DummyInstaller>
    {
        public override void InstallBindings()
        {
        }
    }
}