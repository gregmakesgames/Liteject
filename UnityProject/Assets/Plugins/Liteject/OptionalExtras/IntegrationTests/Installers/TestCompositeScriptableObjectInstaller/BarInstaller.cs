using UnityEngine;
using Liteject;

namespace Liteject.Tests.Installers.CompositeScriptableObjectInstallers
{
    // [CreateAssetMenu(fileName = "BarInstaller", menuName = "Installers/BarInstaller")]
    public class BarInstaller : ScriptableObjectInstaller<BarInstaller>
    {
        [SerializeField] string _value;

        public override void InstallBindings()
        {
            Container.BindInstance(_value);
        }
    }
}
