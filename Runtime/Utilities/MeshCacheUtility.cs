#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Mayuns.DSB
{
    /// <summary>
    /// Editor‑only helpers for fingerprint‑based mesh caching.
    /// Used by WallManager and StructuralMember.
    /// </summary>
    public static class MeshCacheUtility
    {
        /*──────── project‑wide prefs (shared with earlier code) ───────*/
        const string kEnableKey = "DSB.MeshCacheEnabled";
        const string kPathKey = "DSB.MeshCachePath";
        const string kDefault = "Assets/_DSBGenerated";

        public static bool Enabled => EditorPrefs.GetBool(kEnableKey, false);
        public static string CachePath => EditorPrefs.GetString(kPathKey, kDefault);

        public static void SetEnabled(bool on)
        {
            EditorPrefs.SetBool(kEnableKey, on);
            if (on && !AssetDatabase.IsValidFolder(CachePath))
                AssetDatabase.CreateFolder("Assets", "_DSBGenerated");
        }

        public static void PickFolder()
        {
            string abs = EditorUtility.OpenFolderPanel(
                "Choose DSB mesh‑cache folder", "Assets", "_DSBGenerated");
            if (string.IsNullOrEmpty(abs)) return;

            string proj = Application.dataPath[..^6];
            string rel = abs.Replace(proj + Path.DirectorySeparatorChar, "");
            EditorPrefs.SetString(kPathKey, rel);
            AssetDatabase.Refresh();
        }

        /*──────── shared low‑level APIs ───────────────────────────────*/

        /// <summary>Return cached mesh if present, otherwise null.</summary>
        public static Mesh TryLoad(int fp, int idx)
        {
            if (!Enabled) return null;
            string p = $"{CachePath}/fp{fp}_cell{idx}.asset";
            return AssetDatabase.LoadAssetAtPath<Mesh>(p);
        }

        /// <summary>
        /// Persist <paramref name="src"/> under fp/idx and return the asset reference.
        /// If it already exists the old asset is reused.
        /// </summary>
        public static Mesh Persist(Mesh src, int fp, int idx)
        {
            if (!Enabled || src == null) return src;

            string root = CachePath;
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder("Assets", "_DSBGenerated");

            string path = $"{root}/fp{fp}_cell{idx}.asset";
            Mesh dst = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (dst == null)
            {
                dst = Object.Instantiate(src);
                dst.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(dst, path);
            }
            else
            {
                EditorUtility.CopySerialized(src, dst);        // keep GUID, overwrite data
            }
            return dst;
        }
        /// <summary>
        /// Try to load a *chunk* mesh. Returns null if not found.
        /// </summary>
        public static Mesh TryLoadChunk(int fp, int chunkIdx)
        {
            if (!Enabled) return null;
            string path = $"{CachePath}/fp{fp}_chunk{chunkIdx}.asset";
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>
        /// Persist a *chunk* mesh under the chunk namespace.
        /// </summary>
        public static Mesh PersistChunk(Mesh src, int fp, int chunkIdx)
        {
            if (!Enabled || src == null) return src;
            if (!AssetDatabase.IsValidFolder(CachePath))
                AssetDatabase.CreateFolder("Assets", "_DSBGenerated");

            string path = $"{CachePath}/fp{fp}_chunk{chunkIdx}.asset";
            Mesh   dst  = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (dst == null)
            {
                dst      = Object.Instantiate(src);
                dst.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(dst, path);
            }
            else
            {
                EditorUtility.CopySerialized(src, dst);
            }
            return dst;
        }

        /// <summary>
        /// Remove cached meshes that are not referenced by any wall or structural
        /// member in the currently open scenes.
        /// </summary>
        public static void CleanUnusedCache()
        {
            if (!AssetDatabase.IsValidFolder(CachePath))
                return;

            // Gather all mesh asset paths used by objects in loaded scenes
            var usedPaths = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; ++i)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh m = mf.sharedMesh;
                        string p = AssetDatabase.GetAssetPath(m);
                        if (!string.IsNullOrEmpty(p) && p.StartsWith(CachePath))
                            usedPaths.Add(p);
                    }

                    foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                    {
                        Mesh m = mc.sharedMesh;
                        string p = AssetDatabase.GetAssetPath(m);
                        if (!string.IsNullOrEmpty(p) && p.StartsWith(CachePath))
                            usedPaths.Add(p);
                    }
                }
            }

            // Delete any cached mesh that isn't referenced
            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { CachePath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!usedPaths.Contains(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.Refresh();
        }
    }
}
#endif
