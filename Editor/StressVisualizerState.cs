using UnityEditor;

namespace Mayuns.DSB.Editor
{
    /// <summary>Public helper so other editor scripts can check the menu state.</summary>
    internal static class StressVisualizerState
    {
        internal const string SessionKey = "DSB_VISUALIZE_STRESS";

        internal static bool IsEnabled
        {
            get => SessionState.GetBool(SessionKey, false);
            set => SessionState.SetBool(SessionKey, value);
        }
    }
}
