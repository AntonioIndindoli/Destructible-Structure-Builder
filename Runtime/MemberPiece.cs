using UnityEngine;
using UnityEngine.Events;

namespace Mayuns.DSB
{
    public class MemberPiece : Destructible, IDamageable
    {
        [HideInInspector] public bool isDestroyed = false;
        [HideInInspector] public StructuralMember member;
        public float accumulatedDamage = 0;
        [Header("Destruction Events")]
        public UnityEvent onDestroyed;

        public void DestroyMemberPiece()
        {
            TakeDamage(member.memberPieceHealth);
        }

        void Start()
        {
            CreateAndStoreDebrisData(1, false);
        }

        public void TakeDamage(float damage)
        {
            if (isDestroyed) return;

            accumulatedDamage += damage;

            if (accumulatedDamage >= member.memberPieceHealth)
            {
                isDestroyed = true;

                onDestroyed?.Invoke();

                if (member != null)
                {
                    member.PieceDestroyed();
                    // tell the member a voxel is gone
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