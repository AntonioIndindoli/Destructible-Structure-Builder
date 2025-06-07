using UnityEditor;
using UnityEngine;
using Mayuns.DSB;

namespace Mayuns.DSB.Editor
{
    [CustomEditor(typeof(StructuralGroupManager))]
    public class StructuralGroupManagerEditor : UnityEditor.Editor 
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            StructuralGroupManager manager = (StructuralGroupManager)target;

            if (GUILayout.Button("Rebuild Voxels"))
            {
                manager.RebuildVoxels();
                EditorUtility.SetDirty(manager);
            }
        }
    }
}