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
            manager.strengthModifier = EditorGUILayout.IntField(strengthLabel, manager.strengthModifier);

            EditorGUILayout.Space();

            // Total pieces (active child GameObjects)
            int totalActivePieces = 0;
            foreach (Transform child in manager.transform)
            {
                if (child.gameObject.activeInHierarchy)
                    totalActivePieces++;
            }

            EditorGUILayout.LabelField("Total Active Pieces", totalActivePieces.ToString());

            if (GUILayout.Button("Rebuild Voxels"))
            {
                manager.RebuildVoxels();
                EditorUtility.SetDirty(manager);
            }
        }
    }
}