using UnityEditor;
using Liteject;

namespace Liteject
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CompositeScriptableObjectInstaller))]
    [NoReflectionBaking]
    public class CompositeScriptableObjectInstallerEditor : BaseCompositetInstallerEditor<CompositeScriptableObjectInstaller, ScriptableObjectInstallerBase>
    {
    }
}