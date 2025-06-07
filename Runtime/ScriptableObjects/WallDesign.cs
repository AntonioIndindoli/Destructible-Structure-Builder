using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mayuns.DSB
{
    [CreateAssetMenu(menuName = "Structures/Wall Design", fileName = "NewWallDesign")]
    public class WallDesign : ScriptableObject
    {
        [Serializable] public enum CellType { Empty, Cube, Window, TriangleBL, TriangleTL, TriangleBR, TriangleTR }

        [Serializable]
        public struct Cell
        {
            public CellType type;
        }

        public int columns;
        public int rows;
        public List<Cell> cells;       // flat list: (row * columns) + col
        public Material material;
        public Material glassMaterial;
    }
}