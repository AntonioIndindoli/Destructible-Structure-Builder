using System.Collections.Generic;
using UnityEngine;
using static Mayuns.DSB.GibBuildingUtility;
using UnityEngine.Events;

namespace Mayuns.DSB
{
    public abstract class Destructible : MonoBehaviour
    {
        [HideInInspector] public DebrisData[] gibs;
        private GibManager gibManager;
        [Header("Destruction Events")]
        public UnityEvent onCrumble;

        void Awake()
        {
            // GibManager must be present in scene for gibs to spawn
            if (gibManager == null && GibManager.Instance != null)
            {
                gibManager = GibManager.Instance;
            }
        }

        public virtual void CreateAndStoreDebrisData(int cutCascades, bool isWindow)
        {
            var debrisDataList = GibBuildingUtility.CreateDebris(gameObject, cutCascades, isWindow);
            if (debrisDataList == null)
            {
                gibs = new DebrisData[0];
                return;
            }

            // Filter out null or invalid mesh data (e.g., mesh is empty)
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
                // Decide whether to spawn gibs or not
                if (data.sharedMesh == null || data.sharedMesh.vertexCount == 0 || Random.value > spawnChance || gibManager == null)
                    continue;

                // Spawn gibs using pool
                GameObject gib = GibManager.Instance.GetReusableGibShell(data, transform.position, transform.rotation);
                if (gib != null)
                {
                    GibManager.Instance.RegisterTimedGib(gib, GibManager.Instance.smallGibLifetime);
                    gib.transform.SetParent(null);
                }
            }

            onCrumble?.Invoke();

            Destroy(gameObject);
        }
    }
}
