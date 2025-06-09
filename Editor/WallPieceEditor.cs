using UnityEngine;
using UnityEditor;

namespace Mayuns.DSB
{
    [CustomEditor(typeof(WallPiece))]
    public class WallPieceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI(); // Draw default fields (if any)

            WallPiece piece = (WallPiece)target;

            if (piece.manager != null)
            {
                EditorGUILayout.FloatField("Wall Piece Health", piece.manager.wallPieceHealth);
                if (piece.isWindow)
                {
                    EditorGUILayout.FloatField("Window Health", piece.manager.wallPieceWindowHealth);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Manager is not assigned.", MessageType.Warning);
            }

            EditorGUILayout.FloatField("Accumulated Damage", piece.accumulatedDamage);
        }
    }
}
