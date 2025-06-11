#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Mayuns.DSB.Editor
{
    [InitializeOnLoad]
    internal static class StructuralMemberStressGizmo
    {
        static StructuralMemberStressGizmo() => SceneView.RepaintAll();

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected |
                   GizmoType.Pickable | GizmoType.Active)]
        private static void DrawStress(StructuralMember member, GizmoType gizmoType)
        {
            if (!StressVisualizerState.IsEnabled ||
                member == null || member.isDestroyed || member.isSplit || member.isGrouped)
                return;

            if (!Application.isPlaying)
            {
                var mgr = member.structuralGroup ? member.structuralGroup : member.GetComponentInParent<StructuralGroupManager>();
                if (mgr)
                    mgr.CalculateLoadsForEditor();
            }

            /*────────── Colour calculation (same as before) ──────────*/
            float stress =
                Mathf.Clamp01(member.accumulatedLoad / Mathf.Max(0.0001f, member.supportCapacity));

            Color wire = Color.Lerp(Color.green, Color.red, stress);
            Color fill = new Color(wire.r, wire.g, wire.b, 0.25f);

            /*────────── Draw the relevant colliders ──────────*/
            bool any = DrawCollider(member.combinedObject ? member.combinedObject.GetComponent<Collider>() : null, wire, fill);

            if (!member.isSplit && any) return;   // combined object done

            if (member.memberPieces == null) return;
            foreach (var piece in member.memberPieces)
                if (piece) foreach (var col in piece.GetComponents<Collider>())
                        DrawCollider(col, wire, fill);
        }

        private static bool DrawCollider(Collider col, Color wire, Color fill)
        {
            if (!(col is BoxCollider box)) return false;

            var mtx = Matrix4x4.TRS(box.transform.TransformPoint(box.center),
                                    box.transform.rotation,
                                    box.transform.lossyScale);

            using (new Handles.DrawingScope(mtx))
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                foreach (var quad in LocalBoxQuads(box.size))            // six faces
                    Handles.DrawSolidRectangleWithOutline(quad, fill, Color.clear);

                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                Handles.color = wire;
                Handles.DrawWireCube(Vector3.zero, box.size);
            }
            return true;
        }

        private static Vector3[][] LocalBoxQuads(Vector3 size)
        {
            Vector3 e = size * 0.5f;
            Vector3[] v =
            {
                new(-e.x,-e.y,-e.z), new(e.x,-e.y,-e.z), new(e.x,e.y,-e.z), new(-e.x,e.y,-e.z),
                new(-e.x,-e.y, e.z), new(e.x,-e.y, e.z), new(e.x,e.y, e.z), new(-e.x,e.y, e.z)
            };
            return new[]
            {
                new[]{v[0],v[1],v[2],v[3]}, new[]{v[5],v[4],v[7],v[6]},
                new[]{v[4],v[0],v[3],v[7]}, new[]{v[1],v[5],v[6],v[2]},
                new[]{v[4],v[5],v[1],v[0]}, new[]{v[3],v[2],v[6],v[7]}
            };
        }
    }
}
#endif