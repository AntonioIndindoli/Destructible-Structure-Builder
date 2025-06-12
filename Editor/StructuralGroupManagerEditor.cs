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
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Structural Group Overview", EditorStyles.boldLabel);

            // Strength Modifier with Tooltip
            GUIContent strengthLabel = new GUIContent(
                "Global Member Support Capacity Adjustment",
                "This value modifies the support capacity of all structural members in the structure during the initial load propagation phase. Higher values make the structure more stable."
            );

            EditorGUI.BeginChangeCheck(); // Track changes
            int newStrength = EditorGUILayout.IntField(strengthLabel, manager.strengthModifier);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(manager, "Change Strength Modifier");
                manager.strengthModifier = newStrength;
                EditorUtility.SetDirty(manager);
            }

            EditorGUI.BeginChangeCheck();
            float newMemberMass = EditorGUILayout.FloatField("Voxel Mass", manager.voxelMass);
            if (EditorGUI.EndChangeCheck())
            {
                manager.ApplyvoxelMass(newMemberMass);
            }

            EditorGUI.BeginChangeCheck();
            float newMemberHealth = EditorGUILayout.FloatField("Voxel Health", manager.voxelHealth);
            if (EditorGUI.EndChangeCheck())
            {
                manager.ApplyvoxelHealth(newMemberHealth);
            }

            EditorGUI.BeginChangeCheck();
            SerializedProperty effectsProp = serializedObject.FindProperty("effects");
            EditorGUILayout.PropertyField(effectsProp, new GUIContent("Sound Settings"), true);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(manager, "Modify Sound Settings");
                serializedObject.ApplyModifiedProperties();
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

            serializedObject.ApplyModifiedProperties();
        }
    }
}
