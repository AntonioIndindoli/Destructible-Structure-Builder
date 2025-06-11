using UnityEditor;
using UnityEngine;

namespace Mayuns.DSB
{
    [CustomEditor(typeof(MemberPiece))]
    public class MemberPieceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var piece = (MemberPiece)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Info", EditorStyles.boldLabel);

            if (piece.member != null)
            {
                EditorGUILayout.FloatField("Max Health (From Member)", piece.member.memberPieceHealth);
            }

            EditorGUI.BeginChangeCheck();
            float newDamage = EditorGUILayout.FloatField("Accumulated Damage", piece.accumulatedDamage);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(piece, "Change Accumulated Damage");
                piece.accumulatedDamage = newDamage;
                EditorUtility.SetDirty(piece);
            }
        }
    }
}
