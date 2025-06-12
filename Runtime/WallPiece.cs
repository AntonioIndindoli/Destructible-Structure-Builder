using UnityEngine;
using UnityEngine.Events;

namespace Mayuns.DSB
{
    /// <summary>
    /// Single cell of a wall grid that can be damaged or destroyed.
    /// </summary>
    public class WallPiece : Destructible, IDamageable
    {
        [HideInInspector] public bool isDestroyed = false;
         public WallManager manager;
        [HideInInspector] public StructuralMember attachedMember;
        [HideInInspector] public MemberPiece closestMemberPiece;
        [HideInInspector] public Vector2Int gridPosition;
        [HideInInspector] public Chunk chunk;
        [HideInInspector] public bool isEdge = false;
        [HideInInspector] public bool isProxy = false;
        [HideInInspector] public float accumulatedDamage = 0;
        public UnityEvent onDestroyed;
        public UnityEvent onWindowShatter;
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
            // Prepare debris data so runtime destruction has no delay
            CreateAndStoreDebrisData(1, isWindow);
        }

        /// <summary>
        /// Convenience helper to destroy the piece instantly.
        /// </summary>
        public void DestroyWallPiece()
        {
            TakeDamage(manager.voxelHealth);
        }

        /// <summary>
        /// Apply damage and check for destruction thresholds.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (isDestroyed || isProxy) return;

            accumulatedDamage += damage;

            if (accumulatedDamage >= manager.voxelHealth)
            {
                HandleDestruction();
            }

        }

        /// <summary>
        /// Called when the damage threshold has been exceeded.
        /// Spawns debris and notifies the owning wall.
        /// </summary>
        public void HandleDestruction()
        {
            isDestroyed = true;

            if (isWindow)
            {
                onWindowShatter?.Invoke();
                if (manager != null && manager.structuralGroup != null)
                {
                    manager.structuralGroup.PlayWindowShatterAt(transform.position);
                }
            }
            else
            {
                onDestroyed?.Invoke();
                if (manager != null && manager.structuralGroup != null)
                {
                    manager.structuralGroup.PlayCrumbleAt(transform.position);
                }
            }

            if (manager != null)
            {
                manager.WallPieceDestroyed(gridPosition.x, gridPosition.y);
                manager.SelfDestructCheck();
            }

            Crumble();

        }

    }
}