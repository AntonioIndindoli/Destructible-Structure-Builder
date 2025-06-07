using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mayuns.DSB;

namespace Mayuns.DSB.Editor
{
    [ExecuteAlways]                                    // draw in Edit mode too
    [RequireComponent(typeof(StructuralMember))]
    public class StructuralStressVisualizer : MonoBehaviour
    {
        /*──────────────────── STATIC TOGGLE ────────────────────*/
        private static bool visualizeGizmos;

        /// <summary>Turn the overlay on/off and add/remove components automatically.</summary>
        public static void SetVisualizeGizmos(bool enable)
        {
            visualizeGizmos = enable;

            StructuralMember[] members =
                Object.FindObjectsByType<StructuralMember>
                       (FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (StructuralMember m in members)
            {
                StructuralStressVisualizer vis = m.GetComponent<StructuralStressVisualizer>();

                if (enable)
                {
                    if (vis == null) vis = m.gameObject.AddComponent<StructuralStressVisualizer>();
                }
                else
                {
                    if (!Application.isPlaying && vis != null) DestroyImmediate(vis);
                    else if (vis != null) Destroy(vis);
                }
            }
        }

        /*──────────────────── INSTANCE DATA ────────────────────*/
        private StructuralMember member;
        private void Awake() => member = GetComponent<StructuralMember>();

        /*──────────────────── G I Z M O  ───────────────────────*/
        private void OnDrawGizmos()
        {
            if (!visualizeGizmos) return;
            if (member == null || member.isDestroyed || member.isGrouped) return;

            // 1. Try to draw combinedObject's collider if present
            if (!member.isSplit && member.combinedObject != null)
            {
                Collider col = member.combinedObject.GetComponent<Collider>();
                if (col is BoxCollider box)
                {
                    Color c = CalculateStressColor();
                    DrawBoxColliderGizmo(box, new Color(c.r, c.g, c.b, 0.25f), true);
                    DrawBoxColliderGizmo(box, c, false);
                    return;
                }
                // Add other collider types here as needed
            }

            // 2. Otherwise, draw each memberPiece's collider
            if (member.memberPieces != null)
            {
                foreach (var piece in member.memberPieces)
                {
                    if (piece == null) continue;
                    foreach (var col in piece.GetComponents<Collider>())
                    {
                        Color c = CalculateStressColor();
                        if (col is BoxCollider box)
                        {
                            DrawBoxColliderGizmo(box, new Color(c.r, c.g, c.b, 0.25f), true);
                            DrawBoxColliderGizmo(box, c, false);
                        }
                        // Optionally: handle other collider types!
                    }
                }
            }
        }
        private void DrawBoxColliderGizmo(BoxCollider box, Color color, bool filled)
        {
            Gizmos.color = color;
            var mat = Matrix4x4.TRS(
                box.transform.TransformPoint(box.center),
                box.transform.rotation,
                box.transform.lossyScale
            );
            Gizmos.matrix = mat;
            if (filled)
                Gizmos.DrawCube(Vector3.zero, box.size);
            else
                Gizmos.DrawWireCube(Vector3.zero, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        /*─────────────────── BOUNDS HELPERS ────────────────────*/
        private bool TryGetMemberBounds(out Bounds result)
        {
            /* 1️⃣ combinedObject still alive? */
            if (!member.isSplit && member.combinedObject != null)
            {
                Collider col = member.combinedObject.GetComponent<Collider>();
                if (col != null)
                {
                    result = col.bounds;
                    return true;
                }
            }

            /* 2️⃣ build hull from memberPiece colliders */
            if (member.memberPieces == null)
            {
                result = default;
                return false;
            }

            List<Collider> cols = member.memberPieces
                                        .Where(p => p != null)
                                        .SelectMany(p => p.GetComponents<Collider>())
                                        .Where(c => c != null)
                                        .ToList();
            if (cols.Count == 0)
            {
                result = default;
                return false;
            }

            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Count; ++i) b.Encapsulate(cols[i].bounds);

            result = b;
            return true;
        }

        /*─────────────────── COLOUR LOGIC ──────────────────────*/
        private Color CalculateStressColor()
        {
            float ratio = 0f;
            if (member.supportCapacity > 0f)
                ratio = Mathf.Clamp01(member.accumulatedLoad / member.supportCapacity);

            if (member.wasDamaged) ratio = Mathf.Max(ratio, 0.85f);

            return Color.Lerp(Color.green, Color.red, ratio);
        }
    }
}