#if !NOT_UNITY3D

using System.IO;
using UnityEditor;
using UnityEngine;
using Liteject.Internal;

namespace Liteject.ReflectionBaking
{
    public static class ReflectionBakingMenuItems
    {
        [MenuItem("Assets/Create/Zenject/Reflection Baking Settings", false, 100)]
        public static void CreateReflectionBakingSettings()
        {
            var folderPath = ZenUnityEditorUtil.GetCurrentDirectoryAssetPathFromSelection();

            var config = ScriptableObject.CreateInstance<ZenjectReflectionBakingSettings>();

            ZenUnityEditorUtil.SaveScriptableObjectAsset(
                Path.Combine(folderPath, "ZenjectReflectionBakingSettings.asset"), config);
        }
    }
}
#endif
