using UnityEditor;

namespace Mayuns.DSB.Editor
{
    [InitializeOnLoad]
    static class StressVisualizerToolbar
    {
        const string kMenuPath = "Tools/DSB/Visualize Stress Gizmos";
        const string kSessionKey = "DSB_VISUALIZE_STRESS";
        static bool _enabled;

        static StressVisualizerToolbar()
        {
            _enabled = SessionState.GetBool(kSessionKey, false);
            StructuralStressVisualizer.SetVisualizeGizmos(_enabled);
            Menu.SetChecked(kMenuPath, _enabled);
        }

        [MenuItem(kMenuPath)]
        static void Toggle()
        {
            _enabled = !_enabled;
            SessionState.SetBool(kSessionKey, _enabled);
            StructuralStressVisualizer.SetVisualizeGizmos(_enabled);
            Menu.SetChecked(kMenuPath, _enabled);
        }

        [MenuItem(kMenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(kMenuPath, _enabled);
            return true;
        }
    }
}