#if !AUTH_PACKAGE_PRESENT
using Unity.Services.Vivox.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace ChatChannelSample.Editor
{
    /// <summary>
    /// Used to force add any package dependencies our ChatChannelSample requires.
    /// </summary>
    [InitializeOnLoad]
    class PackageImporter
    {
        const string k_authPackageDependency = "com.unity.services.authentication@2.0.0";

        /// <summary>
        /// Adds required packages to the project that are not defined/found during any domain reload.
        /// </summary>
        static PackageImporter()
        {
            /// Locates a specific version of the com.unity.services.authentication package and adds it to the project.
            Debug.Log($"[Vivox] Because the Chat Channel Sample requires {k_authPackageDependency}, it has been added to your project.");
            Client.Add(k_authPackageDependency);
        }
    }
}
#endif
