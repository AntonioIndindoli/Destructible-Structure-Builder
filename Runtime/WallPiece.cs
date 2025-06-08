using UnityEngine;

namespace Mayuns.DSB
{
    [System.Serializable]
    public class WallPiece : Destructible, IDamageable
    {
        [field: SerializeField, HideInInspector] public bool isDestroyed = false;
        [field: SerializeField, HideInInspector] public WallManager manager;
        [field: SerializeField, HideInInspector] public StructuralMember attachedMember;
        [field: SerializeField, HideInInspector] public MemberPiece closestMemberPiece;
        [field: SerializeField, HideInInspector] public Vector2Int gridPosition;
        [field: SerializeField, HideInInspector] public Chunk chunk;
        [field: SerializeField, HideInInspector] public bool isEdge = false;
        [field: SerializeField, HideInInspector] public bool isProxy = false;
        [field: SerializeField, HideInInspector] public float accumulatedDamage = 0;
        public enum TriangularCornerDesignation
        {
            None,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
        [field: SerializeField, HideInInspector] public TriangularCornerDesignation cornerDesignation = TriangularCornerDesignation.None;
        [field: SerializeField, HideInInspector] public bool isWindow;
        [field: SerializeField, HideInInspector] public bool isEmpty;
        
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