
using UnityEditor;

namespace Mayuns.DSB
{
    [CustomEditor(typeof(MemberPiece))]
    public class MemberPieceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI(); // Draw default inspector

            var piece = (MemberPiece)target;

            if (piece.member != null)
            {
                EditorGUILayout.FloatField("Max Health (From Member)", piece.member.memberPieceHealth);
            }

            EditorGUILayout.FloatField("Accumulated Damage", piece.accumulatedDamage);
        }
    }
}
