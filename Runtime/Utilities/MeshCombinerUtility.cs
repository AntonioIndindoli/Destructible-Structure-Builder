using UnityEngine;
using System.Collections.Generic;

namespace Mayuns.DSB
{
    public static class MeshCombinerUtility
    {
        public static GameObject CombineMeshes(GameObject parent, GameObject[] pieces, string combinedName = "CombinedMesh")
        {
            Dictionary<Material, List<CombineInstance>> materialGroups = new();

            foreach (var piece in pieces)
            {
                if (piece == null) continue;

                MeshFilter mf = piece.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                Material mat = piece.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (mat == null) continue;

                if (!materialGroups.ContainsKey(mat))
                    materialGroups[mat] = new List<CombineInstance>();

                CombineInstance ci = new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    transform = parent.transform.worldToLocalMatrix * piece.transform.localToWorldMatrix
                };
                materialGroups[mat].Add(ci);

                PreparePieceForCombination(piece);
            }

            List<CombineInstance> finalCombines = new();
            List<Material> materials = new();

            foreach (var kvp in materialGroups)
            {
                Mesh submesh = new Mesh();
                submesh.CombineMeshes(kvp.Value.ToArray(), true, true);

                CombineInstance ci = new CombineInstance
                {
                    mesh = submesh,
                    transform = Matrix4x4.identity
                };

                finalCombines.Add(ci);
                materials.Add(kvp.Key);
            }

            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(finalCombines.ToArray(), false, false);

            GameObject combinedObject = new GameObject(combinedName, typeof(MeshFilter), typeof(MeshRenderer));
            combinedObject.transform.SetParent(parent.transform, false);
            combinedObject.GetComponent<MeshFilter>().sharedMesh = combinedMesh;
            combinedObject.GetComponent<MeshRenderer>().materials = materials.ToArray();

            return combinedObject;
        }
        public static void PreparePieceForCombination(GameObject piece)
        {
            if (piece == null) return;

            piece.SetActive(false);
        }

    }
}