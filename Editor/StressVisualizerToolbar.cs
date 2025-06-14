using UnityEditor;
using UnityEngine;

namespace Mayuns.DSB.Editor
{
    [InitializeOnLoad]
    internal static class StressVisualizerToolbar
    {
        const string kMenuPath = "Tools/DSB/Visualize Stress Gizmos";
        static bool _enabled;

        static StressVisualizerToolbar()
        {
            _enabled = StressVisualizerState.IsEnabled;
            Menu.SetChecked(kMenuPath, _enabled);
        }

        [MenuItem(kMenuPath)]
        static void Toggle()
        {
            _enabled = !_enabled;
            StressVisualizerState.IsEnabled = _enabled;   // ‹— keep single source of truth
            Menu.SetChecked(kMenuPath, _enabled);

            if (_enabled && !EditorApplication.isPlaying)
            {
                foreach (var mgr in Object.FindObjectsOfType<StructuralGroupManager>())
                    mgr.CalculateLoadsForEditor();
            }

            SceneView.RepaintAll();                      // ‹— instant feedback
        }

        [MenuItem(kMenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(kMenuPath, _enabled);
            return true;
        }
    }
}