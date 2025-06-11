using UnityEngine;
using UnityEngine.Events;

namespace Mayuns.DSB
{
    public class WallPiece : Destructible, IDamageable
    {
        [HideInInspector] public bool isDestroyed = false;
        [HideInInspector] public WallManager manager;
        [HideInInspector] public StructuralMember attachedMember;
        [HideInInspector] public MemberPiece closestMemberPiece;
        [HideInInspector] public Vector2Int gridPosition;
        [HideInInspector] public Chunk chunk;
        [HideInInspector] public bool isEdge = false;
        [HideInInspector] public bool isProxy = false;
        [HideInInspector] public float accumulatedDamage = 0;
        [Header("Destruction Events")]
        public UnityEvent onDestroyed;
        public enum TriangularCornerDesignation
        {
            None,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
        [HideInInspector] public TriangularCornerDesignation cornerDesignation = TriangularCornerDesignation.None;
        [HideInInspector] public bool isWindow;
        [HideInInspector] public bool isEmpty;

        void Start()
        {
            CreateAndStoreDebrisData(1, isWindow);
        }
        
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

            onDestroyed?.Invoke();

            if (manager != null)
            {
                manager.WallPieceDestroyed(gridPosition.x, gridPosition.y);
                manager.SelfDestructCheck();
            }

            Crumble();
        }

    }
}