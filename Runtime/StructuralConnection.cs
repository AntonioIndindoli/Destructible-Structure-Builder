using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Mayuns.DSB
{
    [System.Serializable]
    public enum Slot
    {
        Top, Bottom,
        Left, Right,
        Front, Back,
        TopRight, TopLeft,
        BottomRight, BottomLeft,
        FrontRight, FrontLeft,
        BackRight, BackLeft,
        TopFront, TopBack,
        BottomFront, BottomBack
    }

    public static class SlotLookup
    {
        // Bidirectional maps between enum and the original lowercase/dash strings.
        public static readonly IReadOnlyDictionary<Slot, string> SlotToStringMap = new Dictionary<Slot, string>
    {
        {Slot.Top, "top"},   {Slot.Bottom, "bottom"}, {Slot.Left, "left"}, {Slot.Right, "right"},
        {Slot.Front, "front"},{Slot.Back, "back"},
        {Slot.TopRight, "top-right"},   {Slot.TopLeft, "top-left"},
        {Slot.BottomRight,"bottom-right"}, {Slot.BottomLeft, "bottom-left"},
        {Slot.FrontRight, "front-right"}, {Slot.FrontLeft, "front-left"},
        {Slot.BackRight,  "back-right"},  {Slot.BackLeft, "back-left"},
        {Slot.TopFront,   "top-front"},   {Slot.TopBack,  "top-back"},
        {Slot.BottomFront,"bottom-front"}, {Slot.BottomBack,"bottom-back"}
    };
        public static readonly IReadOnlyDictionary<string, Slot> FromString = SlotToStringMap.ToDictionary(kv => kv.Value, kv => kv.Key);

        public static readonly IReadOnlyDictionary<Slot, Vector3> DirLocal = new Dictionary<Slot, Vector3>
    {
        {Slot.Top, Vector3.up}, {Slot.Bottom, Vector3.down}, {Slot.Left, Vector3.left}, {Slot.Right, Vector3.right},
        {Slot.Front, Vector3.forward}, {Slot.Back, Vector3.back},
        {Slot.TopRight,   (Vector3.up + Vector3.right).normalized},
        {Slot.TopLeft,    (Vector3.up + Vector3.left).normalized},
        {Slot.BottomRight,(Vector3.down + Vector3.right).normalized},
        {Slot.BottomLeft, (Vector3.down + Vector3.left).normalized},
        {Slot.FrontRight, (Vector3.forward + Vector3.right).normalized},
        {Slot.FrontLeft,  (Vector3.forward + Vector3.left ).normalized},
        {Slot.BackRight,  (Vector3.back + Vector3.right).normalized},
        {Slot.BackLeft,   (Vector3.back + Vector3.left ).normalized},
        {Slot.TopFront,   (Vector3.up + Vector3.forward).normalized},
        {Slot.TopBack,    (Vector3.up + Vector3.back   ).normalized},
        {Slot.BottomFront,(Vector3.down + Vector3.forward).normalized},
        {Slot.BottomBack, (Vector3.down + Vector3.back   ).normalized},
    };

        public static readonly IReadOnlyDictionary<Slot, Quaternion> Rotation = new Dictionary<Slot, Quaternion>
    {
        {Slot.Top, Quaternion.Euler(-90,0,0)},      {Slot.Bottom, Quaternion.Euler(90,0,0)},
        {Slot.Left, Quaternion.Euler(0,-90,0)},     {Slot.Right, Quaternion.Euler(0,90,0)},
        {Slot.Front, Quaternion.identity},          {Slot.Back, Quaternion.Euler(0,180,0)},
        {Slot.TopRight, Quaternion.Euler(-45,90,0)},    {Slot.TopLeft, Quaternion.Euler(-45,-90,0)},
        {Slot.BottomRight, Quaternion.Euler(45,90,0)},  {Slot.BottomLeft, Quaternion.Euler(45,-90,0)},
        {Slot.FrontRight, Quaternion.Euler(0,45,0)},    {Slot.FrontLeft, Quaternion.Euler(0,-45,0)},
        {Slot.BackRight, Quaternion.Euler(0,135,0)},    {Slot.BackLeft, Quaternion.Euler(0,-135,0)},
        {Slot.TopFront, Quaternion.Euler(-45,0,0)},     {Slot.TopBack, Quaternion.Euler(-45,180,0)},
        {Slot.BottomFront, Quaternion.Euler(45,0,0)},   {Slot.BottomBack, Quaternion.Euler(45,180,0)},
    };

        public static bool IsDiagonal(Slot s) => (int)s >= (int)Slot.TopRight; // first 6 are cardinal

        public static Slot Opposite(this Slot s) => s switch
        {
            Slot.Top => Slot.Bottom,
            Slot.Bottom => Slot.Top,
            Slot.Left => Slot.Right,
            Slot.Right => Slot.Left,
            Slot.Front => Slot.Back,
            Slot.Back => Slot.Front,
            Slot.TopRight => Slot.BottomLeft,
            Slot.TopLeft => Slot.BottomRight,
            Slot.BottomRight => Slot.TopLeft,
            Slot.BottomLeft => Slot.TopRight,
            Slot.FrontRight => Slot.BackLeft,
            Slot.FrontLeft => Slot.BackRight,
            Slot.BackRight => Slot.FrontLeft,
            Slot.BackLeft => Slot.FrontRight,
            Slot.TopFront => Slot.BottomBack,
            Slot.TopBack => Slot.BottomFront,
            Slot.BottomFront => Slot.TopBack,
            Slot.BottomBack => Slot.TopFront,
            _ => s
        };
    }

    [System.Serializable]
    sealed class MemberMap : ISerializationCallbackReceiver
    {
        [SerializeField] List<Slot> keys = new();
        [SerializeField] List<StructuralMember> values = new();
        readonly Dictionary<Slot, StructuralMember> dict = new();

        public StructuralMember this[Slot s]
        {
            get => dict.TryGetValue(s, out var m) ? m : null;
            set
            {
                if (value == null) dict.Remove(s);
                else dict[s] = value;
            }
        }

        public IEnumerable<KeyValuePair<Slot, StructuralMember>> Pairs => dict;

        // ——— serialization plumbing ———
        public void OnBeforeSerialize()
        {
            keys.Clear(); values.Clear();
            foreach (var kv in dict)
            {
                keys.Add(kv.Key); values.Add(kv.Value);
            }
        }
        public void OnAfterDeserialize()
        {
            dict.Clear();
            int n = Mathf.Min(keys.Count, values.Count);
            for (int i = 0; i < n; ++i)
                dict[keys[i]] = values[i];
        }
    }


    public class StructuralConnection : Destructible, IDamageable
    {
        [SerializeField] MemberMap _members = new();

        [field: SerializeField, HideInInspector]
        public StructuralGroupManager structuralGroup;
        public float health = 100f;
        [field: SerializeField, HideInInspector]
        public bool isDestroyed;

        void Start() => CreateAndStoreDebrisData(1, false);

        public void SelfDestructCheck()
        {
            if (GetMembers().Count == 0) Destroy(gameObject);
            else if (GetMembers().Count == 1)
            {
                DestroyConnection();
            }
        }

        public void TakeDamage(float damage)
        {
            if (isDestroyed) return;
            health -= damage;
            if (health > 0f) return;

            isDestroyed = true;
            var connected = GetMembers();
            connected.RemoveAll(s => s == null);
            foreach (var m in connected)
                m.cachedAdjacentMembers.RemoveAll(adj => connected.Contains(adj));
            if (structuralGroup != null)
            {
                structuralGroup.ValidateGroupIntegrity();
            }
            Crumble();
        }

        public void DestroyConnection() => TakeDamage(health);

        public List<StructuralMember> GetMembers() =>
            _members.Pairs.Select(kv => kv.Value).Where(m => m != null).ToList();

        public StructuralMember Get(Slot slot) => _members[slot];

        public void ReplaceMember(StructuralMember oldMember, StructuralMember newMember)
        {
            foreach (var slot in _members.Pairs.Where(kv => kv.Value == oldMember).Select(kv => kv.Key).ToList())
                _members[slot] = newMember;
        }

        public Quaternion SlotRotation(string slot)
        {
            return SlotLookup.FromString.TryGetValue(slot, out var s)
                ? SlotLookup.Rotation[s]
                : Quaternion.identity;
        }

        public string DirectionToSlot(Vector3 dirWorld)
        {
            Vector3 dirLocal = transform.InverseTransformDirection(dirWorld.normalized);
            float bestDot = float.NegativeInfinity; Slot best = Slot.Top;
            foreach (var kv in SlotLookup.DirLocal)
            {
                float d = Vector3.Dot(dirLocal, kv.Value);
                if (d > bestDot) { bestDot = d; best = kv.Key; }
            }
            return SlotLookup.SlotToStringMap[best];
        }

        public void AddMembersFromConnection(StructuralConnection c, List<StructuralMember> list)
        {
            if (c == null) return;
            foreach (var m in c.GetMembers()) list.Add(m);
        }

        public void RemoveInvalidMembers(StructuralGroupManager expected)
        {
            foreach (var m in GetMembers())
                if (m != null && m.structuralGroup != expected)
                    ReplaceMember(m, null);
            SelfDestructCheck();
        }

        public void AssignMemberRef(Slot slot, StructuralMember newMem, StructuralConnection endConn)
        {
            _members[slot] = newMem;
            endConn._members[slot.Opposite()] = newMem;
        }
    }
}