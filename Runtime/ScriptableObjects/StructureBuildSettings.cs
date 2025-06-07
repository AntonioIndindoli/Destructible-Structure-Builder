using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mayuns.DSB
{
    public enum DisableDirection
    {
        None,
        Diagonal,
        Orthogonal
    }
    [CreateAssetMenu(menuName = "StructuralMember Build Settings")]
    public class StructureBuildSettings : ScriptableObject
    {
        public float memberLength = 5f;
        public float memberThickness = 0.25f;
        public float memberMass = 10f;
        public float memberSupportCapacity = 100f;
        public Material memberMaterial;
        public DisableDirection disableDirection;
        public int strengthModifier = 100;
        public float minPropagationTime = 1f;
        public float maxPropagationTime = 5f;
        public float connectionSize = 0.25f;
        public Material connectionMaterial;
        public Material wallMaterial;
        public Material glassMaterial;
        public WallDesign defaultWallDesign;
        public float wallHeight = 5f;
        public float wallWidth = 5f;
        public bool matchMemberLength = true; //Overrides custom wall width
        public bool isWallCentered = true;
        public bool allowWallOverlap = false;
        public float wallThickness = .2f;
        public int wallColumnCellCount = 5;
        public int wallRowCellCount = 5;

        // ────────────────────────  mesh‑cache  ──────────────────────────────
        [System.Serializable]
        public class WallMeshCacheEntry
        {
            public int fingerprint;               // layout hash
            public List<Mesh> pieceMeshes = new();
        }

        [SerializeField]
        private List<WallMeshCacheEntry> wallMeshCache
            = new();                              // one asset keeps *all* entries

        // ------------------------------------------------------------------  helpers
        public bool TryGetWallMesh(int fp, out WallMeshCacheEntry entry)
        {
            entry = wallMeshCache.Find(e => e.fingerprint == fp);
            return entry != null;
        }

#if UNITY_EDITOR
    public void SaveWallMeshes(int fp, Mesh[] singlePieces)
    {
        if (wallMeshCache.Exists(e => e.fingerprint == fp))
            return;                     // already cached – nothing to do

        var entry = new WallMeshCacheEntry { fingerprint = fp };

        // ---- Begin one big batch ---------------------------------
        AssetDatabase.StartAssetEditing();           // <‑‑ pause auto‑imports
        try
        {
            for (int i = 0; i < singlePieces.Length; ++i)
            {
                Mesh src = singlePieces[i];
                if (!src) { entry.pieceMeshes.Add(null); continue; }

                Mesh clone = Object.Instantiate(src);
                clone.name = $"fp{fp}_cell{i}";
                clone.hideFlags = HideFlags.HideInHierarchy;

                AssetDatabase.AddObjectToAsset(clone, this);
                entry.pieceMeshes.Add(clone);
            }

            wallMeshCache.Add(entry);
            EditorUtility.SetDirty(this);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }
#endif

    }
}