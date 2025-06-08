using UnityEngine;

namespace Mayuns.DSB
{
    [System.Serializable]
    public class WallPiece : Destructible, IDamageable
    {
        public bool isDestroyed = false;
        [SerializeField] public WallManager manager;
        public StructuralMember attachedMember;
        public MemberPiece closestMemberPiece;
        [SerializeField] public Vector2Int gridPosition;
        public Chunk chunk;
        public bool isEdge = false;
        public bool isProxy = false;
        public float accumulatedDamage = 0;
        public enum TriangularCornerDesignation
        {
            None,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
        [SerializeField] public TriangularCornerDesignation cornerDesignation = TriangularCornerDesignation.None;
        [SerializeField] public bool isWindow;
        [SerializeField] public bool isEmpty;
        public void DestroyWallPiece()
        {
            TakeDamage(manager.wallPieceHealth);
        }

        public void TakeDamage(float damage)
        {
            if (isDestroyed || isProxy) return;

            accumulatedDamage += damage;

            if (accumulatedDamage >= manager.wallPieceHealth && !isWindow)
            {
                HandleDestruction();
            }
            else if (accumulatedDamage >= manager.wallPieceWindowHealth && isWindow)
            {
                HandleDestruction();
            }
        }

        private void HandleDestruction()
        {
            isDestroyed = true;

            if (manager != null)
            {
                manager.WallPieceDestroyed(gridPosition.x, gridPosition.y);
                manager.SelfDestructCheck();
            }

            Crumble();
        }

    }
}