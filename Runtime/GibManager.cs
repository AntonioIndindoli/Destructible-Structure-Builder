using System.Collections.Generic;
using UnityEngine;
using static Mayuns.DSB.GibBuildingUtility;

namespace Mayuns.DSB
{
    public class GibManager : MonoBehaviour
    {
        public static GibManager Instance { get; private set; }

        [Header("Pool Settings")]
        [Tooltip("Maximum number of gibs allowed to be active at once. Older gibs will be culled beyond this limit.")]
        public int maxActiveGibs = 500;

        [Tooltip("Current number of gibs active in the scene.")]
        [SerializeField]
        public int currentActiveGibs = 0;

        [Tooltip("Maximum number of pooled gib shells that can be stored for reuse.")]
        public int maxPoolSize = 500;

        [Header("Small Debris Mass")]
        [Tooltip("Global Mass that is applied to all active small gibs (DebrisChunks)")]
        public float smallGibMass = 10f;

        [Header("Gib Lifetimes (seconds)")]
        [Tooltip("Lifetime of small gibs (DebrisChunks) before they are returned to the pool.")]
        public float smallGibLifetime = 5f;

        [Tooltip("Lifetime of medium gibs (Detached chunks, DetachedWallGroups, DetachedStructuralMembers) before they are returned to the pool.")]
        public float mediumGibLifetime = 10f;

        [Tooltip("Lifetime of large gibs (DetachedStructuralGroups) before they are deleted. Note: DetachedStructuralGroups are not pooled.")]
        public float largeGibLifetime = 200f;

        [Header("Chunk Uncombine Throttle")]
        [Tooltip("Maximum number of chunks that can be uncombined (fractured) within a given window of time.")]
        public int maxUncombinesPerWindow = 5;

        [Tooltip("Time window (in seconds) in which uncombines are limited to prevent performance spikes.")]
        public float uncombineWindowSeconds = 1f;

        private readonly Queue<float> uncombineTimestamps = new Queue<float>();

        [Header("Editor Settings")]
        [Tooltip("If enabled, pooled gib objects will be hidden from the hierarchy view in the editor.")]
        public bool hidePooledGibsInHierarchy = true;

        [Header("Explosion Impulse")]

        [Tooltip("Linear scale applied to force per kilogram of the gib. "
                   + "FinalForce = forcePerKg * mass * randomJitter.")]
        public float explosionForcePerKg = 25f;

        [Tooltip("Linear scale applied to torque per kilogram of the gib.")]
        public float explosionTorquePerKg = 5f;

        [Tooltip("Random variation applied to the base force (±percentage).")]
        [Range(0f, 1f)]
        public float explosionForceJitter = 0.35f;

        [Tooltip("Radius of the virtual explosion used in AddExplosionForce().")]
        public float explosionRadius = 2f;

        [Tooltip("Extra upward bias added by AddExplosionForce().")]
        public float explosionUpwardModifier = 0.5f;

        private readonly Queue<GameObject> gibShellPool = new Queue<GameObject>();
        private readonly HashSet<GameObject> activeGibs = new HashSet<GameObject>();
        private readonly List<TimedGib> timedGibs = new List<TimedGib>(1024);
        private struct TimedGib
        {
            public GameObject gib;
            public float expireTime;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            float time = Time.time;
            for (int i = timedGibs.Count - 1; i >= 0; i--)
            {
                if (timedGibs[i].gib == null || time >= timedGibs[i].expireTime)
                {
                    ReturnGibToPool(timedGibs[i].gib);
                    timedGibs.RemoveAt(i);
                }
            }
        }
        public bool CanUncombineNow()
        {
            float currentTime = Time.time;

            // Clean up old timestamps outside the window
            while (uncombineTimestamps.Count > 0 && currentTime - uncombineTimestamps.Peek() > uncombineWindowSeconds)
            {
                uncombineTimestamps.Dequeue();
            }

            if (uncombineTimestamps.Count < maxUncombinesPerWindow)
            {
                uncombineTimestamps.Enqueue(currentTime);
                return true;
            }

            return false;
        }
        public GameObject GetReusableGibShell(DebrisData data, Vector3 position, Quaternion rotation)
        {
            GameObject gib = gibShellPool.Count > 0 ? gibShellPool.Dequeue() : CreateGibShell();

            if (gib == null)
            {
                return null;
            }
            gib.transform.SetPositionAndRotation(position, rotation);
            gib.transform.localScale = Vector3.one;
            gib.SetActive(true);

            var mf = gib.GetComponent<MeshFilter>();
            mf.sharedMesh = data.sharedMesh;

            var mr = gib.GetComponent<MeshRenderer>();
            mr.sharedMaterials = data.materials;

            var bc = gib.GetComponent<BoxCollider>();
            if (bc != null)
            {
                Bounds meshBounds = data.sharedMesh.bounds;
                bc.center = meshBounds.center;
                bc.size = meshBounds.size*.3f;
            }

            var rb = gib.GetComponent<Rigidbody>();
            rb.mass = smallGibMass;

            ApplyHideFlag(gib);
            RegisterGib(gib);

            return gib;
        }


        public void RegisterTimedGib(GameObject gib, float lifetime)
        {
            if (gib == null) return;

            if (activeGibs.Count < maxActiveGibs)
            {
                // If already registered, ignore
                if (!activeGibs.Contains(gib))
                {
                    activeGibs.Add(gib);
                    currentActiveGibs = activeGibs.Count;
                }


                timedGibs.Add(new TimedGib
                {
                    gib = gib,
                    expireTime = Time.time + lifetime
                });

                ApplyHideFlag(gib);
            }
            else
            {
                Destroy(gib);
            }
        }

        public void RegisterGib(GameObject gib)
        {
            if (gib == null) return;

            if (activeGibs.Count < maxActiveGibs)
            {
                activeGibs.Add(gib);
                currentActiveGibs = activeGibs.Count;
                //ApplyRandomExplosion(gib);
            }
            else
            {
                Destroy(gib);
            }
        }

        public void ReturnGibToPool(GameObject gib)
        {
            if (gib == null) return;
            bool invalid = false;

            gib.SetActive(false);
            activeGibs.Remove(gib);
            currentActiveGibs = activeGibs.Count;
            ApplyHideFlag(gib);

            var mf = gib.GetComponent<MeshFilter>();
            var mr = gib.GetComponent<MeshRenderer>();
            var bc = gib.GetComponent<BoxCollider>();
            var rb = gib.GetComponent<Rigidbody>();

            if (mf != null && mr != null && bc != null && rb != null)
            {
                mf.sharedMesh = null;
            }
            else
            {
                invalid = true;
            }

            if (gibShellPool.Count < maxPoolSize && !invalid)
            {
                //ApplyRandomExplosion(gib);
                gibShellPool.Enqueue(gib);
            }
            else
            {
                Destroy(gib);
            }
        }

        private void ApplyHideFlag(GameObject obj)
        {
            obj.hideFlags = hidePooledGibsInHierarchy ? HideFlags.HideInHierarchy : HideFlags.None;
        }

        private GameObject CreateGibShell()
        {
            var go = new GameObject("GibShell");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();
            go.AddComponent<Rigidbody>();
            ApplyHideFlag(go);
            return go;
        }

        public void ApplyRandomExplosion(GameObject gib)
        {
            if (gib == null) return;

            Rigidbody rb = gib.GetComponent<Rigidbody>();
            if (rb == null) return;

            // --- pick a slightly random origin ------------------------------------
            Vector3 origin = rb.worldCenterOfMass + Random.insideUnitSphere * 0.3f;

            // --- scale force & torque by mass -------------------------------------
            float mass = Mathf.Max(rb.mass, 0.0001f); // avoid div‑by‑zero corner cases

            float force =
                explosionForcePerKg * mass *
                Random.Range(1f - explosionForceJitter, 1f + explosionForceJitter);

            float torque =
                explosionTorquePerKg * mass *
                Random.Range(1f - explosionForceJitter, 1f + explosionForceJitter);

            // --- apply ------------------------------------------------------------
            rb.AddExplosionForce(
                force,
                origin,
                explosionRadius,
                explosionUpwardModifier,
                ForceMode.Impulse);

            if (torque > 0f)
            {
                Vector3 randomTorque = Random.insideUnitSphere.normalized * torque;
                rb.AddTorque(randomTorque, ForceMode.Impulse);
            }
        }

    }
}