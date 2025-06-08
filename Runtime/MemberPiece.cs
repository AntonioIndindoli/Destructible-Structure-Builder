using UnityEngine;

namespace Mayuns.DSB
{
    public class MemberPiece : Destructible, IDamageable
    {
        [field: SerializeField, HideInInspector] public bool isDestroyed = false;
        [field: SerializeField, HideInInspector] public StructuralMember member;
        [field: SerializeField] public float accumulatedDamage = 0;
        public void DestroyMemberPiece()
        {
            TakeDamage(member.memberPieceHealth);
        }

        public void TakeDamage(float damage)
        {
            if (isDestroyed) return;

            accumulatedDamage += damage;

            if (accumulatedDamage >= member.memberPieceHealth)
            {
                isDestroyed = true;

                if (member != null)
                {
                    member.PieceDestroyed();
                    int idx = System.Array.IndexOf(member.memberPieces, gameObject);

                    if (idx >= 0)      // found it
                        member.memberPieces[idx] = null;
                    member.SelfDestructCheck();
                }

                Crumble();
            }
        }

    }
}