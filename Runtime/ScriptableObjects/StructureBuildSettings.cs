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
        public float memberThickness = 0.5f;
        public float memberTextureScaleX = .33f;
        public float memberTextureScaleY = .33f;
        public float memberSupportCapacity = 500f;
        public Material memberMaterial;
        public DisableDirection disableDirection;
        public int strengthModifier = 100;
        public float minPropagationTime = 1f;
        public float maxPropagationTime = 10f;
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
        public float wallTextureScaleX = 1f;
        public float wallTextureScaleY = 1f;
        public float voxelMass = 100f;
        public float voxelHealth = 100f;
    }
}