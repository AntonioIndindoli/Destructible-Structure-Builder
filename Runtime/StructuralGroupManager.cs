using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Mayuns.DSB
{
    public class StructuralGroupManager : MonoBehaviour
    {
        public int strengthModifier;
        public float minPropagationTime = 0f;
        public float maxPropagationTime = 3f;
        public float voxelMass = 10f;
        public float voxelHealth = 100f;

        [System.Serializable]
        public class EffectInfo
        {
            public EffectType type;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;

            // Per-type cooldown (seconds).  Default is 0.5 s.
            [Min(0f)] public float cooldown = .5f;

            public GameObject[] particlePrefabs;
        }

        private readonly Dictionary<EffectType, float> _nextPlayTime = new Dictionary<EffectType, float>();

        public enum EffectType
        {
            Crumble,
            MemberStress,
            LargeCollapse,
            WindowShatter
        }

        public EffectInfo[] effects;
        [HideInInspector] public AudioSource audioSource;
        [HideInInspector] public StructureBuildSettings buildSettings;
        [HideInInspector] public bool isDetached = false;
        [HideInInspector] public List<StructuralMember> structuralMembers = new List<StructuralMember>();
        [HideInInspector] public List<StructuralConnection> memberConnections = new List<StructuralConnection>();
        [HideInInspector] public List<WallManager> walls = new List<WallManager>();
        [HideInInspector] public HashSet<StructuralMember> structuralMembersHash;
        [HideInInspector] public HashSet<StructuralConnection> memberConnectionsHash;
        [HideInInspector] public HashSet<WallManager> wallsHash;
        [HideInInspector] public float collisionCooldown = 0.2f; // seconds; adjust as needed
        [HideInInspector] public GibManager gibManager;
        [HideInInspector] public bool hasGibManager = false;
        [HideInInspector] public float validationDuration = 0f;
        [HideInInspector] public float validationInterval = .1f;
        private float validationCooldown = 0f;
        private float cleanupTimer = 0f;
        private const float cleanupInterval = 5f;

        void Awake()
        {
            foreach (EffectType t in System.Enum.GetValues(typeof(EffectType)))
                _nextPlayTime[t] = 0f;
        }

        void Update()
        {
            if (isDetached)
            {
                cleanupTimer += Time.deltaTime;
                if (cleanupTimer >= cleanupInterval)
                {
                    cleanupTimer = 0f;

                    bool membersEmpty = structuralMembersHash == null || !structuralMembersHash.Any(x => x != null);
                    bool wallsEmpty = wallsHash == null || !wallsHash.Any(x => x != null);

                    if (membersEmpty && wallsEmpty && memberConnectionsHash.Count < 2 || transform.childCount < 2)
                    {
                        if (hasGibManager)
                        {
                            Destroy(gameObject, gibManager.smallGibLifetime);
                        }
                        else
                        {
                            Destroy(gameObject);
                            enabled = false;
                        }

                    }
                }
            }

            if (validationDuration > 0f)
            {
                validationDuration -= Time.deltaTime;
                validationCooldown -= Time.deltaTime;

                foreach (var conn in memberConnectionsHash)
                {
                    if (conn)
                        conn.SelfDestructCheck();
                }

                if (validationCooldown <= 0f)
                {
                    ValidateGroup();
                    validationCooldown = validationInterval;
                }
            }
        }

        void Start()
        {
            if (!isDetached)
            {
                // GibManager must be present in scene for gibs to spawn
                if (gibManager == null && GibManager.Instance != null)
                {
                    gibManager = GibManager.Instance;
                    hasGibManager = true;
                }

                RefreshHash();
                UpdateCurrentMinDistancesToGround(true);
                PropagateStructuralLoads(true);

                // Ensure members and connections do not generate self-collisions
                IgnoreInternalCollisions();
            }
        }

        void RefreshHash()
        {
            structuralMembers.RemoveAll(m => m == null);
            memberConnections.RemoveAll(m => m == null);
            walls.RemoveAll(m => m == null);
            structuralMembersHash = new HashSet<StructuralMember>(structuralMembers);
            memberConnectionsHash = new HashSet<StructuralConnection>(memberConnections);
            WallManager[] wallsArray = GetComponentsInChildren<WallManager>();
            wallsHash = new HashSet<WallManager>(wallsArray);
        }

        void IgnoreInternalCollisions()
        {
            var colliders = new List<Collider>();

            if (structuralMembersHash != null)
                foreach (var m in structuralMembersHash)
                    if (m != null)
                        colliders.AddRange(m.GetComponentsInChildren<Collider>(true));

            if (memberConnectionsHash != null)
                foreach (var c in memberConnectionsHash)
                    if (c != null)
                        colliders.AddRange(c.GetComponentsInChildren<Collider>(true));

            for (int i = 0; i < colliders.Count; ++i)
                for (int j = i + 1; j < colliders.Count; ++j)
                    if (colliders[i] != null && colliders[j] != null)
                        Physics.IgnoreCollision(colliders[i], colliders[j]);
        }

        public void ValidateGroupIntegrity()
        {
            // Restart validation timer
            validationDuration = validationInterval;
        }

        public void ValidateGroup()
        {
            if (!isDetached)
            {
                UpdateCurrentMinDistancesToGround(false);
                PropagateStructuralLoads(false);
            }
            else
            {
                ComputeConnectedComponents();
            }
        }

        private void ComputeConnectedComponents()
        {
            if (structuralMembersHash == null || structuralMembersHash.Count == 0)
            {
                Destroy(gameObject);
                return;
            }

            foreach (var member in structuralMembersHash)
            {
                member.cachedAdjacentMembers.RemoveAll(adj =>
                    adj == null ||
                    adj.isDestroyed ||
                    adj.GetComponentInParent<StructuralGroupManager>() != this
                );
            }

            var remaining = new HashSet<StructuralMember>(structuralMembersHash);
            var components = new List<HashSet<StructuralMember>>();
            HashSet<StructuralMember> mainComponent = null;

            while (remaining.Count > 0)
            {
                var seed = remaining.First();
                var component = new HashSet<StructuralMember>();
                var queue = new Queue<StructuralMember>();

                queue.Enqueue(seed);
                component.Add(seed);
                remaining.Remove(seed);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var adj in current.cachedAdjacentMembers)
                    {
                        if (remaining.Contains(adj))
                        {
                            queue.Enqueue(adj);
                            component.Add(adj);
                            remaining.Remove(adj);
                        }
                    }
                }
                components.Add(component);

                if (mainComponent == null || component.Count > mainComponent.Count)
                    mainComponent = component;
            }

            if (components.Count == 1)
                return;

            structuralMembersHash.Clear();
            structuralMembersHash.UnionWith(mainComponent);

            foreach (var comp in components)
                if (comp != mainComponent)
                    CreateMemberGroup(comp);
        }

        private void UpdateCurrentMinDistancesToGround(bool isInitial)
        {
            // Identify all ground Members in structuralMembersHash
            var groundMembers = structuralMembersHash.Where(Member => Member.isGrounded);

            // Create a set of all Members we need to process
            HashSet<StructuralMember> unvisitedMembers = new HashSet<StructuralMember>(structuralMembersHash);

            // BFS starting from ground Members
            Queue<(StructuralMember Member, int distance)> queue = new Queue<(StructuralMember Member, int distance)>();
            HashSet<StructuralMember> visited = new HashSet<StructuralMember>();

            // Enqueue all ground Members with distance 0
            foreach (var groundMember in groundMembers)
            {
                queue.Enqueue((groundMember, 0));
                groundMember.currentMinDistanceToGround = 0; // Update distance for ground Members
                visited.Add(groundMember);
                unvisitedMembers.Remove(groundMember);
            }

            while (queue.Count > 0)
            {
                var (currentMember, currentDistance) = queue.Dequeue();

                foreach (var adjacentMember in currentMember.cachedAdjacentMembers)
                {
                    if (adjacentMember != null && !adjacentMember.isDestroyed && !adjacentMember.isGrouped && !visited.Contains(adjacentMember))
                    {
                        adjacentMember.currentMinDistanceToGround = currentDistance + 1;
                        queue.Enqueue((adjacentMember, currentDistance + 1));
                        visited.Add(adjacentMember);
                        unvisitedMembers.Remove(adjacentMember);
                    }
                }
            }

            if (!isInitial)
            {
                // Members not visited during BFS are not connected to ground Members
                foreach (var Member in unvisitedMembers)
                {
                    Member.currentMinDistanceToGround = int.MaxValue;
                }

                // Collect Members where currentMinDistanceToGround > maxDistance
                HashSet<StructuralMember> disconnectedMembers = new HashSet<StructuralMember>();

                foreach (var Member in unvisitedMembers)
                {
                    if (Member != null && !Member.isDestroyed)
                    {
                        structuralMembersHash.Remove(Member);
                        disconnectedMembers.Add(Member);
                    }
                }

                HashSet<StructuralMember> unprocessed = new HashSet<StructuralMember>(disconnectedMembers);
                while (unprocessed.Count > 0)
                {
                    StructuralMember seed = unprocessed.First();
                    // Create a new component set and a queue for BFS.
                    HashSet<StructuralMember> component = new HashSet<StructuralMember>();
                    Queue<StructuralMember> componentQueue = new Queue<StructuralMember>();

                    componentQueue.Enqueue(seed);
                    component.Add(seed);
                    unprocessed.Remove(seed);

                    while (componentQueue.Count > 0)
                    {
                        StructuralMember current = componentQueue.Dequeue();
                        foreach (var adjacent in current.cachedAdjacentMembers)
                        {
                            if (adjacent != null &&
                                disconnectedMembers.Contains(adjacent) &&
                                unprocessed.Contains(adjacent))
                            {
                                componentQueue.Enqueue(adjacent);
                                component.Add(adjacent);
                                unprocessed.Remove(adjacent);
                            }
                        }
                    }

                    // Create a new group for this connected component of disconnectedMembers.
                    CreateMemberGroup(component);
                }
            }
        }

        private void PropagateStructuralLoads(bool isInitial)
        {
            // Clear previous load data
            foreach (var member in structuralMembersHash)
            {
                member.accumulatedLoad = member.mass;
            }

            // Sort by distance to ground descending (top-down)
            var membersSorted = structuralMembersHash
                .Where(m => !m.isDestroyed && !m.isNewSplitMember && m.currentMinDistanceToGround < int.MaxValue)
                .OrderByDescending(m => m.currentMinDistanceToGround)
                .ToList();

            foreach (var member in membersSorted)
            {
                // Find all directly "lower" neighbors (closer to ground)
                var lowerSupports = member.cachedAdjacentMembers
                    .Where(n => n != null &&
                                !n.isDestroyed &&
                                n.currentMinDistanceToGround < member.currentMinDistanceToGround)
                    .ToList();

                if (lowerSupports.Count == 0)
                {
                    // No support below = member is unsupported
                    continue;
                }

                // Distribute this member's load equally to all lower supports
                float sharedLoad = member.accumulatedLoad / lowerSupports.Count;

                foreach (var lower in lowerSupports)
                {
                    if (!isInitial)
                        lower.accumulatedLoad += sharedLoad;
                    else
                        lower.supportCapacity += sharedLoad;
                }
            }
            if (!isInitial)
            {
                var membersToDamage = new List<StructuralMember>();
                // Evaluate overloads
                foreach (var member in structuralMembersHash)
                {
                    if (!member.isDestroyed && member.accumulatedLoad > member.supportCapacity)
                    {
                        membersToDamage.Add(member);
                    }
                }
                StartCoroutine(DamageMembersOverTime(membersToDamage, Random.Range(minPropagationTime, maxPropagationTime)));
            }
            else
            {
                foreach (var member in structuralMembersHash)
                {
                    member.supportCapacity += strengthModifier;
                }
            }

        }

        private IEnumerator DamageMembersOverTime(List<StructuralMember> members, float delayBetween)
        {
            foreach (var member in members)
            {
                if (member != null && !member.isDestroyed && !member.isGrouped && !member.isSplit && !member.isGrounded)
                {
                    PlayMemberStressAt(member.transform.position);
                    member.DestroyRandomMemberPiece();
                    yield return new WaitForSeconds(delayBetween);
                }
            }
        }

        private void MoveConnectionToGroup(StructuralConnection connection, Transform groupParent)
        {
            if (connection != null && connection.transform != null)
            {
                connection.transform.SetParent(groupParent, true);
            }

        }

        void CreateMemberGroup(HashSet<StructuralMember> members)
        {
            if (members == null || members.Count < 1) return;

            GameObject groupGO = new GameObject("DetachedStructuralGroup");
            Vector3 center = CalculateGroupCenter(members);

            groupGO.transform.SetPositionAndRotation(center, Quaternion.identity);
            groupGO.transform.localScale = Vector3.one;

            // Add components
            var rb = groupGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var structuralGroup = groupGO.AddComponent<StructuralGroupManager>();
            structuralGroup.isDetached = true;

            // Copy runtime data so the new group behaves like the source
            structuralGroup.strengthModifier = strengthModifier;
            structuralGroup.minPropagationTime = minPropagationTime;
            structuralGroup.maxPropagationTime = maxPropagationTime;
            structuralGroup.voxelMass = voxelMass;
            structuralGroup.voxelHealth = voxelHealth;
            structuralGroup.voxelMass = voxelMass;
            structuralGroup.voxelHealth = voxelHealth;
            structuralGroup.collisionCooldown = collisionCooldown;
            structuralGroup.buildSettings = buildSettings;
            structuralGroup.validationInterval = validationInterval;

            structuralGroup.effects = effects;
            structuralGroup.audioSource = groupGO.GetComponent<AudioSource>();
            if (structuralGroup.audioSource == null)
                structuralGroup.audioSource = groupGO.AddComponent<AudioSource>();
            structuralGroup.EnsureDefaultEffects();

            if (gibManager != null)
            {
                structuralGroup.gibManager = gibManager;
                structuralGroup.hasGibManager = hasGibManager;
            }

            if (structuralGroup.structuralMembersHash == null)
                structuralGroup.structuralMembersHash = new HashSet<StructuralMember>();

            if (structuralGroup.memberConnectionsHash == null)
                structuralGroup.memberConnectionsHash = new HashSet<StructuralConnection>();

            if (structuralGroup.wallsHash == null)
                structuralGroup.wallsHash = new HashSet<WallManager>();

            var membersToRemove = new List<StructuralMember>(members.Count);
            var wallsToRemove = new List<WallManager>();
            var connectionsToDamage = new List<StructuralConnection>();
            var connectionsToRemove = new List<StructuralConnection>();
            var disabled = new List<Collider>();
            int count = 0;

            // Move Members to new group
            foreach (var member in members)
            {
                if (member == null || member.isDestroyed || member.transform == null)
                    continue;

                Transform t = member.transform;
                Transform parent = groupGO.transform;

                Vector3 worldPos = t.position;
                Quaternion worldRot = t.rotation;
                Vector3 worldScale = t.lossyScale;

                Vector3 parentScale = parent.lossyScale;
                Vector3 localPos = parent.InverseTransformPoint(worldPos);
                Quaternion localRot = Quaternion.Inverse(parent.rotation) * worldRot;

                Vector3 localScale = new Vector3(
                    worldScale.x / parentScale.x,
                    worldScale.y / parentScale.y,
                    worldScale.z / parentScale.z);

                t.SetParent(parent, false);


                t.localPosition = localPos;
                t.localRotation = localRot;
                t.localScale = localScale;

                member.isGrouped = true;
                member.structuralGroup = structuralGroup;
                structuralGroup.structuralMembersHash.Add(member);
                rb.mass += member.mass;

                if (member.startConnection) structuralGroup.memberConnectionsHash.Add(member.startConnection);
                if (member.endConnection) structuralGroup.memberConnectionsHash.Add(member.endConnection);

                count++;
            }

            // Move walls
            foreach (WallManager wallmanager in wallsHash)
            {
                if (wallmanager != null)
                {
                    int inNewGroupCount = 0;
                    int inOldGroupCount = 0;

                    foreach (var m in wallmanager.edgeMembers)
                    {
                        if (structuralGroup.structuralMembersHash.Contains(m))
                        {
                            inNewGroupCount++;
                        }
                        else if (structuralMembersHash.Contains(m))
                        {
                            inOldGroupCount++;
                        }
                    }

                    if (inNewGroupCount > 0 && inOldGroupCount == 0)
                    {
                        foreach (Chunk chunk in wallmanager._chunks)
                        {
                            wallmanager.SwitchToConvexCollider(chunk);
                        }

                        if (wallmanager.structuralGroup != structuralGroup)
                            wallmanager.transform.parent.SetParent(groupGO.transform, true);

                        wallsToRemove.Add(wallmanager);
                        structuralGroup.wallsHash.Add(wallmanager);
                        wallmanager.structuralGroup = structuralGroup;
                    }
                }
                else
                {
                    wallsToRemove.Add(wallmanager);
                }

            }

            if (isDetached)
            {
                foreach (var wallManager in wallsHash)
                {
                    if (wallManager != null)
                    {
                        wallManager.ValidateWallIntegrity();
                    }

                }
            }
            else
            {
                foreach (var wallManager in walls)
                {
                    if (wallManager != null)
                    {
                        wallManager.ValidateWallIntegrity();
                    }
                }
                foreach (var wallManager in wallsHash)
                {
                    if (wallManager != null)
                    {
                        wallManager.ValidateWallIntegrity();
                    }
                }
            }

            // Cleanup wall references list
            foreach (var wall in wallsToRemove)
            {
                wallsHash.Remove(wall);
            }

            foreach (var member in structuralGroup.structuralMembersHash)
            {
                if (member == null) continue;

                // Ensure only correct walls are in the manager list
                member.managerList.RemoveAll(wall => wall == null || !wallsHash.Contains(wall));
            }

            foreach (var conn in structuralGroup.memberConnectionsHash)
            {
                var connectedMembers = conn.GetMembers();
                bool shouldMove = true;
                foreach (var m in connectedMembers)
                {
                    if (!structuralGroup.structuralMembersHash.Contains(m))
                    {
                        shouldMove = false;
                        break;
                    }
                }

                if (shouldMove)
                {
                    MoveConnectionToGroup(conn, groupGO.transform);
                    conn.structuralGroup = structuralGroup;
                    conn.RemoveInvalidMembers(structuralGroup);
                    memberConnectionsHash.Remove(conn);
                }
                else
                {
                    conn.DestroyConnection();

                }
            }

            void CleanAdjacency(HashSet<StructuralMember> members, StructuralGroupManager intendedManager)
            {
                foreach (var member in members)
                {
                    if (member == null) continue;

                    member.cachedAdjacentMembers.RemoveAll(adjacent =>
                        adjacent == null ||
                        adjacent.structuralGroup != intendedManager
                    );
                }
            }
            CleanAdjacency(structuralMembersHash, this);
            CleanAdjacency(structuralGroup.structuralMembersHash, structuralGroup);


            rb.isKinematic = false;
            Rigidbody sourceRb = this.GetComponent<Rigidbody>();
            if (sourceRb != null)
            {
                rb.velocity = sourceRb.velocity;
                rb.angularVelocity = sourceRb.angularVelocity;
            }

            if (hasGibManager)
            {
                if (structuralGroup.structuralMembersHash.Count > 4)
                    Destroy(groupGO, gibManager.largeGibLifetime);
                else if (structuralGroup.structuralMembersHash.Count > 1)
                {
                    Destroy(groupGO, gibManager.mediumGibLifetime);
                }
                else
                {
                    Destroy(groupGO, gibManager.smallGibLifetime);
                }
            }

            float tVal = Mathf.InverseLerp(1f, 100f, structuralGroup.structuralMembersHash.Count);
            float collapseVolume = Mathf.Lerp(0.1f, .8f, tVal);

            PlayLargeCollapseAt(groupGO.transform.position, collapseVolume);
        }

        public void PlayCrumbleAt(Vector3 position)
        {
            PlayEffects(EffectType.Crumble, position);
        }

        public void PlayMemberStressAt(Vector3 position)
        {
            PlayEffects(EffectType.MemberStress, position);
        }

        public void PlayLargeCollapseAt(Vector3 position, float volumeScale)
        {
            PlayEffects(EffectType.LargeCollapse, position, volumeScale);
        }

        public void PlayWindowShatterAt(Vector3 position)
        {
            PlayEffects(EffectType.WindowShatter, position);
        }

        private void PlayEffects(EffectType type, Vector3 position)
        {
            PlayEffects(type, position, .5f);
        }

        private void PlayEffects(EffectType type, Vector3 position, float volumeScale)
        {
            if (effects == null) return;

            // --- COOLDOWN CHECK --------------------------------------------------------
            if (Time.time < _nextPlayTime[type]) return;      // still cooling down

            foreach (var effect in effects)
            {
                if (effect.type != type) continue;

                // >>> play audio -------------------------------------------------------
                if (audioSource != null && effect.clips != null && effect.clips.Length > 0)
                {
                    var clip = effect.clips[Random.Range(0, effect.clips.Length)];
                    if (clip) audioSource.PlayOneShot(clip, effect.volume * volumeScale);
                }

                // >>> spawn particles --------------------------------------------------
                if (effect.particlePrefabs != null)
                    foreach (var prefab in effect.particlePrefabs)
                        if (prefab)
                        {
                            var obj = Instantiate(prefab, position, Quaternion.identity);
                            if (type == EffectType.LargeCollapse)
                                obj.transform.localScale *= (1f + volumeScale);
                        }

                // --- SET NEXT ALLOWED PLAY TIME --------------------------------------
                _nextPlayTime[type] = Time.time + effect.cooldown;
                break;
            }
        }

        public void EnsureDefaultEffects()
        {
            var effectList = effects != null ? new List<EffectInfo>(effects) : new List<EffectInfo>();

            void AddIfMissing(EffectType type, string[] clipNames)
            {
                if (effectList.Any(e => e.type == type)) return;

                var info = new EffectInfo();
                info.type = type;
                var clips = new List<AudioClip>();
                foreach (var name in clipNames)
                {
                    var clip = Resources.Load<AudioClip>("SoundEffects/" + name);
                    if (clip != null)
                        clips.Add(clip);
                }
                info.clips = clips.ToArray();
                info.particlePrefabs = null;

                if (type == EffectType.LargeCollapse)
                {
                    var dust = Resources.Load<GameObject>("ParticleEffects/Dust_1");
                    if (dust != null)
                        info.particlePrefabs = new[] { dust };
                }

                if (type == EffectType.Crumble)
                {
                    info.volume = .2f;
                    info.cooldown = .5f;
                }
                if (type == EffectType.LargeCollapse)
                {
                    info.volume = .5f;
                    info.cooldown = 5f;
                }
                if (type == EffectType.MemberStress)
                {
                    info.volume = .2f;
                    info.cooldown = 3f;
                }
                if (type == EffectType.WindowShatter)
                {
                    info.volume = .2f;
                    info.cooldown = .5f;
                }
                effectList.Add(info);
            }

            AddIfMissing(EffectType.MemberStress, new[]
            {
                "Member_Stress_Default1",
                "Member_Stress_Default2",
                "Member_Stress_Default3"
            });

            AddIfMissing(EffectType.LargeCollapse, new[]
            {
                "Large_Collapse_Default1",
                "Large_Collapse_Default2",
                "Large_Collapse_Default3",
                "Large_Collapse_Default4",
                "Large_Collapse_Default5"
            });

            AddIfMissing(EffectType.Crumble, new[]
            {
                "Crumble_Default1",
                "Crumble_Default2",
                "Crumble_Default3",
                "Crumble_Default4",
                "Crumble_Default5",
                "Crumble_Default6",
                "Crumble_Default7",
            });

            AddIfMissing(EffectType.WindowShatter, new[]
            {
                "Window_Shatter_Default1",
                "Window_Shatter_Default2",
                "Window_Shatter_Default3",
                "Window_Shatter_Default4"
            });

            // Ensure large collapses use dust particles by default
            foreach (var info in effectList)
            {
                if (info.type == EffectType.LargeCollapse &&
                    (info.particlePrefabs == null || info.particlePrefabs.Length == 0))
                {
                    var dust = Resources.Load<GameObject>("ParticleEffects/Dust_1");
                    if (dust != null)
                        info.particlePrefabs = new[] { dust };
                }
            }

            effects = effectList.ToArray();
        }

        private Vector3 CalculateGroupCenter(HashSet<StructuralMember> Members)
        {
            Vector3 sumPositions = Vector3.zero;
            int count = 0;
            foreach (StructuralMember Member in Members)
            {
                if (Member != null && !Member.isDestroyed)
                {
                    sumPositions += Member.transform.position;
                    count++;
                }
            }
            return count > 0 ? sumPositions / count : this.transform.position;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Calculate structural loads without running the full runtime logic.
        /// Used by the stress visualizer when toggled in the editor.
        /// </summary>
        public void CalculateLoadsForEditor()
        {
            if (structuralMembers == null || structuralMembers.Count == 0)
                return;

            RefreshHash();
            UpdateCurrentMinDistancesToGround(true);

            foreach (var member in structuralMembers)
                member.accumulatedLoad = member.mass;

            var membersSorted = structuralMembers
                .Where(m => !m.isDestroyed &&
                            !m.isNewSplitMember &&
                            m.currentMinDistanceToGround < int.MaxValue)
                .OrderByDescending(m => m.currentMinDistanceToGround)
                .ToList();

            foreach (var member in membersSorted)
            {
                var lowerSupports = member.cachedAdjacentMembers
                    .Where(n => n != null &&
                                !n.isDestroyed &&
                                n.currentMinDistanceToGround < member.currentMinDistanceToGround)
                    .ToList();

                if (lowerSupports.Count == 0)
                    continue;

                float sharedLoad = member.accumulatedLoad / lowerSupports.Count;
                foreach (var lower in lowerSupports)
                    lower.accumulatedLoad += sharedLoad;
            }
        }

        public void ApplyvoxelMass(float newMass)
        {
            if (Mathf.Approximately(voxelMass, 0f))
                voxelMass = 1f;

            float ratio = newMass / voxelMass;

            foreach (var member in structuralMembers)
            {
                if (member == null) continue;
                Undo.RecordObject(member, "Change Member Mass");
                member.mass *= ratio;
                EditorUtility.SetDirty(member);
            }

            foreach (var wall in walls)
            {
                if (wall == null) continue;
                Undo.RecordObject(wall, "Change Wall Piece Mass");
                wall.voxelMass *= ratio;
                EditorUtility.SetDirty(wall);
            }

            Undo.RecordObject(this, "Change Member Piece Mass");
            voxelMass = newMass;
            EditorUtility.SetDirty(this);
        }

        public void ApplyvoxelHealth(float newHealth)
        {
            foreach (var member in structuralMembers)
            {
                if (member == null) continue;
                Undo.RecordObject(member, "Change Member Piece Health");
                member.voxelHealth = newHealth;
                EditorUtility.SetDirty(member);
            }

            foreach (var wall in walls)
            {
                if (wall == null) continue;
                Undo.RecordObject(wall, "Change Wall Piece Health");
                wall.voxelHealth = newHealth;
                EditorUtility.SetDirty(wall);
            }

            Undo.RecordObject(this, "Change Structure Piece Health");
            voxelHealth = newHealth;
            EditorUtility.SetDirty(this);
        }

        public void RebuildVoxels()
        {
            // Rebuild all walls
            walls.RemoveAll(m => m == null);
            foreach (var wall in walls)
            {
                if (wall.wallGrid != null)
                {
                    wall.InstantUncombine();
                    wall.RelinkWallGridReferences();
                    wall.BuildWall(wall.wallGrid, true, buildSettings);
                    UnityEditor.EditorUtility.SetDirty(wall);
                }
            }

            // Rebuild all structural members
            foreach (var member in structuralMembers)
            {
                if (member != null)
                {
                    member.BuildMember();
                    UnityEditor.EditorUtility.SetDirty(member);

                }
            }
        }

        void OnDrawGizmosSelected()
        {
            // Draw for all currently validating walls
            if (wallsHash != null)
            {
                foreach (var wall in wallsHash)
                {
                    if (wall == null) continue;

                    if (wall.isValidating)
                    {
                        Gizmos.color = Color.yellow;
                        UnityEditor.Handles.Label(wall.transform.position + Vector3.up * 2f, "VALIDATING");
                    }
                }
            }
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            EnsureDefaultEffects();
        }
#endif
    }
}