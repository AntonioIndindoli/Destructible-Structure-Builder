using UnityEngine;

namespace Mayuns.DSB
{
    public enum DisableDirection
    {
        None,
        Diagonal,
        Orthogonal
    }
    [CreateAssetMenu(menuName = "StructuralMember Build Settings")]
    public class StructureBuildSettings : ScriptableObject
    {
        public float memberLength = 5f;
        public float memberThickness = 0.25f;
        public float memberMass = 10f;
        public float memberPieceHealth = 100f;
        public float memberSupportCapacity = 100f;
        public Material memberMaterial;
        public DisableDirection disableDirection;
        public int strengthModifier = 100;
        public float minPropagationTime = 1f;
        public float maxPropagationTime = 5f;
        public float connectionSize = 0.25f;
        public Material connectionMaterial;
        public Material wallMaterial;
        public Material glassMaterial;
        public WallDesign defaultWallDesign;
        public float wallHeight = 5f;
        public float wallWidth = 5f;
        public bool matchMemberLength = true; //Overrides custom wall width
        public bool isWallCentered = true;
        public bool allowWallOverlap = false;
        public float wallThickness = .2f;
        public int wallColumnCellCount = 5;
        public int wallRowCellCount = 5;
        public float wallPieceMass = 50f;
        public float wallPieceHealth = 100f;
        public float wallPieceWindowHealth = 1f;
    }
}