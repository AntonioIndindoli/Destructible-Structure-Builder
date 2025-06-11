using UnityEngine;
using UnityEditor;

namespace Mayuns.DSB
{
    [CustomEditor(typeof(WallPiece))]
    public class WallPieceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            WallPiece piece = (WallPiece)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wall Piece Debug Info", EditorStyles.boldLabel);

            if (piece.manager == null)
                piece.manager = piece.GetComponentInParent<WallManager>();

            if (piece.manager != null)
            {
                EditorGUILayout.FloatField("Wall Piece Health", piece.manager.wallPieceHealth);
            }
            else
            {
                EditorGUILayout.HelpBox("Manager is not assigned.", MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            float newDamage = EditorGUILayout.FloatField("Accumulated Damage", piece.accumulatedDamage);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(piece, "Edit Accumulated Damage");
                piece.accumulatedDamage = newDamage;
                EditorUtility.SetDirty(piece);
            }
        }
    }
}
