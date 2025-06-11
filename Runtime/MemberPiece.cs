using UnityEngine;
using UnityEngine.Events;

namespace Mayuns.DSB
{
    /// <summary>
    /// Individual voxel that belongs to a <see cref="StructuralMember"/>.
    /// </summary>
    public class MemberPiece : Destructible, IDamageable
    {
        [HideInInspector] public bool isDestroyed = false;
        [HideInInspector] public StructuralMember member;
        public float accumulatedDamage = 0;
        [Header("Destruction Events")]
        public UnityEvent onDestroyed;

        /// <summary>
        /// Apply enough damage to immediately destroy this piece.
        /// </summary>
        public void DestroyMemberPiece()
        {
            TakeDamage(member.memberPieceHealth);
        }

        void Start()
        {
            // Pre‑generate debris so destruction is instant at runtime
            CreateAndStoreDebrisData(1, false);
        }

        /// <summary>
        /// Deal damage to this voxel. When enough damage accumulates it will
        /// notify its parent member and spawn debris.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (isDestroyed) return;

            accumulatedDamage += damage;

            if (accumulatedDamage >= member.memberPieceHealth)
            {
                isDestroyed = true;

                onDestroyed?.Invoke();
                if (member != null && member.structuralGroup != null)
                {
                    member.structuralGroup.PlayCrumbleAt(transform.position);
                }

                if (member != null)
                {
                    member.PieceDestroyed();
                    int idx = System.Array.IndexOf(member.memberPieces, gameObject);
                    member.memberPieces[idx] = null;
                    member.AdjustNeighboursAfterDestruction(idx);
                    member.SelfDestructCheck();
                }

                Crumble();
            }
        }

    }
}