using UnityEditor;
using UnityEngine;

namespace Mayuns.DSB.Editor
{
    [CustomEditor(typeof(StructuralGroupManager))]
    public class StructuralGroupManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            StructuralGroupManager manager = (StructuralGroupManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Structural Group Overview", EditorStyles.boldLabel);

            // Strength Modifier with Tooltip
            GUIContent strengthLabel = new GUIContent(
                "Strength Modifier",
                "This value increases the support capacity of all structural members during the initial load propagation phase. Higher values make the structure more stable."
            );

            EditorGUI.BeginChangeCheck(); // Track changes
            int newStrength = EditorGUILayout.IntField(strengthLabel, manager.strengthModifier);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(manager, "Change Strength Modifier");
                manager.strengthModifier = newStrength;
                EditorUtility.SetDirty(manager);
            }

            EditorGUILayout.Space();

            // Total active pieces (child objects)
            int totalActivePieces = 0;
            foreach (Transform child in manager.transform)
            {
                if (child.gameObject.activeInHierarchy)
                    totalActivePieces++;
            }

            EditorGUILayout.LabelField("Total Active Pieces", totalActivePieces.ToString());

            EditorGUILayout.Space();

            if (GUILayout.Button("Rebuild Voxels"))
            {
                manager.RebuildVoxels();
                EditorUtility.SetDirty(manager);
            }
        }
    }
}
