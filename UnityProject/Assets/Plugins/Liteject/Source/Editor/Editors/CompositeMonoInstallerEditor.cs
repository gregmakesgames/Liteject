using UnityEditor;
using Liteject;

namespace Liteject
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CompositeMonoInstaller))]
    [NoReflectionBaking]
    public class CompositeMonoInstallerEditor : BaseCompositetInstallerEditor<CompositeMonoInstaller, MonoInstallerBase>
    {
    }
}