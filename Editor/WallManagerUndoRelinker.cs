using UnityEngine;
using UnityEditor;

namespace Mayuns.DSB.Editor
{
    [InitializeOnLoad]
    public static class WallManagerUndoRelinker
    {
        static WallManagerUndoRelinker()
        {
            Undo.undoRedoPerformed -= RelinkAllWallManagers;
            Undo.undoRedoPerformed += RelinkAllWallManagers;
        }

        static void RelinkAllWallManagers()
        {
            foreach (var wall in Object.FindObjectsByType<WallManager>(FindObjectsSortMode.None))
            {
                wall.RelinkWallGridReferences();
            }
        }
    }
}