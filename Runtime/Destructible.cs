using System.Collections.Generic;
using UnityEngine;
using static Mayuns.DSB.GibBuildingUtility;
using UnityEngine.Events;

namespace Mayuns.DSB
{
    /// <summary>
    /// Base behaviour for all objects that can break apart into debris.
    /// </summary>
    public abstract class Destructible : MonoBehaviour
    {
        // Cached debris information generated at build time.
        [HideInInspector] public DebrisData[] gibs;
        private GibManager gibManager;
        [Header("Destruction Events")]
        public UnityEvent onCrumble;

        /// <summary>
        /// Locate the global <see cref="GibManager"/> so debris can be spawned
        /// when this object breaks.
        /// </summary>
        void Awake()
        {
            if (gibManager == null && GibManager.Instance != null)
            {
                gibManager = GibManager.Instance;
            }
        }

        /// <summary>
        /// Pre-compute meshes that will be used when the object crumbles.
        /// </summary>
        public virtual void CreateAndStoreDebrisData(int cutCascades, bool isWindow)
        {
            var debrisDataList = GibBuildingUtility.CreateDebris(gameObject, cutCascades, isWindow);
            if (debrisDataList == null)
            {
                gibs = new DebrisData[0];
                return;
            }

            // Discard any mesh with no vertices to avoid spawning empty debris
            var validData = new List<DebrisData>();
            foreach (var data in debrisDataList)
            {
                if (data.sharedMesh != null && data.sharedMesh.vertexCount > 0)
                {
                    validData.Add(data);
                }
            }

            gibs = validData.ToArray();
        }

        /// <summary>
        /// Spawn precomputed debris pieces and destroy this object.
        /// </summary>
        public virtual void Crumble()
        {
            if (gibs == null || gibs.Length == 0)
                return;

            if (gibManager == null && GibManager.Instance != null)
            {
                gibManager = GibManager.Instance;
            }

            float spawnChance = 1f;
            if (gibManager != null && gibManager.maxActiveGibs > 0)
            {
                float loadRatio = gibManager.currentActiveGibs / (float)gibManager.maxActiveGibs;
                spawnChance = Mathf.Clamp01(1f - loadRatio);
            }

            foreach (DebrisData data in gibs)
            {
                // Skip empty meshes and throttle spawn rate based on the current load
                if (data.sharedMesh == null || data.sharedMesh.vertexCount == 0 || Random.value > spawnChance || gibManager == null)
                    continue;

                GameObject gib = GibManager.Instance.GetReusableGibShell(data, transform.position, transform.rotation);
                if (gib != null)
                {
                    GibManager.Instance.RegisterTimedGib(gib, GibManager.Instance.smallGibLifetime);
                    gib.transform.SetParent(null);
                }
            }

            // Notify listeners and remove this object from the scene
            onCrumble?.Invoke();
            Destroy(gameObject);
        }
    }
}
