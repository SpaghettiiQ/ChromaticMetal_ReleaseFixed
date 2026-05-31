#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using _Project.Core.Networking;

namespace _Project.Core.EditorTools
{
    /// <summary>
    /// One-shot conversion utility. Adds PhasedNetworkObject to every prefab whose
    /// root has a NetworkObject and matches a known phased category (player, enemy,
    /// projectile, chest, extraction). Idempotent — re-running is safe.
    ///
    /// Menu: Tools → ChromaticMetal → Add PhasedNetworkObject To Networked Prefabs
    /// </summary>
    public static class AddPhasedNetworkObjectToPrefabs
    {
        private static readonly string[] _searchFolders = new[]
        {
            "Assets/_Project/Features/Player/Prefabs",
            "Assets/_Project/Features/Enemies/Prefabs",
            "Assets/_Project/Features/Interactables/Prefabs",
            "Assets/_Project/Features/StageSystem/Prefabs",
            "Assets/_Project/Features/Weapons/Prefabs",
            "Assets/_Project/Features/Items/Prefabs",
            "Assets/_Project/Features/ProxyAbilities/Prefabs"
        };

        [MenuItem("Tools/ChromaticMetal/Add PhasedNetworkObject To Networked Prefabs")]
        public static void Run()
        {
            var existingFolders = new List<string>();
            foreach (var f in _searchFolders)
            {
                if (AssetDatabase.IsValidFolder(f)) existingFolders.Add(f);
            }
            if (existingFolders.Count == 0)
            {
                Debug.LogWarning("[PhasedPrefabTool] No known prefab folders exist. Aborting.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", existingFolders.ToArray());
            int added = 0, skippedHasIt = 0, skippedNoNetObj = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var go = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (go.GetComponent<NetworkObject>() == null)
                        {
                            skippedNoNetObj++;
                            continue;
                        }

                        if (go.GetComponent<PhasedNetworkObject>() != null)
                        {
                            skippedHasIt++;
                            continue;
                        }

                        go.AddComponent<PhasedNetworkObject>();
                        PrefabUtility.SaveAsPrefabAsset(go, path);
                        added++;
                        Debug.Log($"[PhasedPrefabTool] + {path}");
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(go);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[PhasedPrefabTool] Done. Added: {added}. Already had it: {skippedHasIt}. No NetworkObject (skipped): {skippedNoNetObj}.");
        }
    }
}
#endif
