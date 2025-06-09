using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace Mayuns.DSB
{
    public static class VoxelBuildingUtility
    {
        static public GameObject CreateWindow(
                Vector2 size,
                Vector3 position,
                int x, int y,
                Material glassMaterial,
                Vector2 tileSize,
                Vector3[,,] vertexOffsets,
                Vector3 worldWallSize,
                int numRows,
                int numColumns,
                Vector2 textureScale,
                string objectName)
        {
            GameObject quad = new GameObject(objectName);
            quad.transform.position = position + Vector3.forward * (0.5f);

            MeshFilter mf = quad.AddComponent<MeshFilter>();
            MeshRenderer mr = quad.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glassMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            Mesh mesh = new Mesh { name = "WindowQuad" };
            mf.sharedMesh = mesh;

            Vector3[] vertices =
            {
        new(-0.5f + vertexOffsets[x,   y,   0].x,
            -0.5f + vertexOffsets[x,   y,   0].y,
             0.0f + vertexOffsets[x,   y,   0].z),

        new( 0.5f + vertexOffsets[x+1, y,   0].x,
            -0.5f + vertexOffsets[x+1, y,   0].y,
             0.0f + vertexOffsets[x+1, y,   0].z),

        new( 0.5f + vertexOffsets[x+1, y+1, 0].x,
             0.5f + vertexOffsets[x+1, y+1, 0].y,
             0.0f + vertexOffsets[x+1, y+1, 0].z),

        new(-0.5f + vertexOffsets[x,   y+1, 0].x,
             0.5f + vertexOffsets[x,   y+1, 0].y,
             0.0f + vertexOffsets[x,   y+1, 0].z)
    };

            int[] triangles = { 0, 2, 1, 0, 3, 2 };

            Vector2[] uvs =
            {
        // bottom-left
        new(
            1f - ((x * tileSize.x) / worldWallSize.x +
                 (vertexOffsets[x, y, 0].x * tileSize.x) / worldWallSize.x),
            1f - ((y * tileSize.y) / worldWallSize.y +
                 (vertexOffsets[x, y, 0].y * tileSize.y) / worldWallSize.y)),

        // bottom-right
        new(
            1f - (((x + 1) * tileSize.x) / worldWallSize.x +
                 (vertexOffsets[x+1, y, 0].x * tileSize.x) / worldWallSize.x),
            1f - ((y * tileSize.y) / worldWallSize.y +
                 (vertexOffsets[x+1, y, 0].y * tileSize.y) / worldWallSize.y)),

        // top-right
        new(
            1f - (((x + 1) * tileSize.x) / worldWallSize.x +
                 (vertexOffsets[x+1, y+1, 0].x * tileSize.x) / worldWallSize.x),
            1f - (((y + 1) * tileSize.y) / worldWallSize.y +
                 (vertexOffsets[x+1, y+1, 0].y * tileSize.y) / worldWallSize.y)),

        // top-left
        new(
            1f - ((x * tileSize.x) / worldWallSize.x +
                 (vertexOffsets[x, y+1, 0].x * tileSize.x) / worldWallSize.x),
            1f - (((y + 1) * tileSize.y) / worldWallSize.y +
                 (vertexOffsets[x, y+1, 0].y * tileSize.y) / worldWallSize.y))
    };

            Vector3[] normals = { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            quad.transform.localScale = new Vector3(1.0f / (float)numColumns, 1.0f / (float)numRows, 1f);
            return quad;
        }

        static public GameObject CreateTriangle(
                Vector3 size,
                Vector3 position, int x, int y, int z,
                Material originalMaterial,
                Vector3 cubeSize,
                WallPiece.TriangularCornerDesignation corner,
                Vector3 worldWallSize,
                int numRows, int numColumns,
                Vector2 textureScale,
                string objectName)
        {
            GameObject triangle = new GameObject(objectName);
            triangle.transform.position = position;
            triangle.transform.localScale = size;

            MeshFilter meshFilter = triangle.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = triangle.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = originalMaterial;

            Mesh mesh = new Mesh();
            meshFilter.sharedMesh = mesh;

            // Compute the four base vertices for the cell.
            // Label them: a = bottom–left, b = bottom–right, c = top–right, d = top–left.
            Vector3 a = new Vector3(-0.5f, -0.5f, -0.5f);
            Vector3 b = new Vector3(0.5f, -0.5f, -0.5f);
            Vector3 c = new Vector3(0.5f, 0.5f, -0.5f);
            Vector3 d = new Vector3(-0.5f, 0.5f, -0.5f);

            // For the back face (z = +0.5), use the same X and Y positions.
            Vector3 aBack = new Vector3(-0.5f, -0.5f, 0.5f);
            Vector3 bBack = new Vector3(0.5f, -0.5f, 0.5f);
            Vector3 cBack = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 dBack = new Vector3(-0.5f, 0.5f, 0.5f);

            // UV coordinates corresponding to the square corners.
            Vector2 uvA = new Vector2(0f, 0f);
            Vector2 uvB = new Vector2(1f, 0f);
            Vector2 uvC = new Vector2(1f, 1f);
            Vector2 uvD = new Vector2(0f, 1f);

            Vector3[] frontVertsCase;
            Vector3[] backVertsOriginal;

            switch (corner)
            {
                case WallPiece.TriangularCornerDesignation.BottomLeft:
                    frontVertsCase = new Vector3[] { a, d, c };
                    backVertsOriginal = new Vector3[] { aBack, dBack, cBack };
                    break;
                case WallPiece.TriangularCornerDesignation.TopRight:
                    frontVertsCase = new Vector3[] { a, c, b };
                    backVertsOriginal = new Vector3[] { aBack, cBack, bBack };
                    break;
                case WallPiece.TriangularCornerDesignation.TopLeft:
                    frontVertsCase = new Vector3[] { b, d, c };
                    backVertsOriginal = new Vector3[] { bBack, dBack, cBack };
                    break;
                case WallPiece.TriangularCornerDesignation.BottomRight:
                    frontVertsCase = new Vector3[] { a, d, b };
                    backVertsOriginal = new Vector3[] { aBack, dBack, bBack };
                    break;
                default:
                    frontVertsCase = new Vector3[] { a, b, c };
                    backVertsOriginal = new Vector3[] { aBack, bBack, cBack };
                    break;
            }

            Vector2 ComputeFrontUVFromGrid(Vector2 gridPos)
            {
                return new Vector2(
                      ((gridPos.x * cubeSize.x) / worldWallSize.x),
                      ((gridPos.y * cubeSize.y) / worldWallSize.y)
                );
            }

            Vector2 ComputeBackUVFromGrid(Vector2 gridPos)
            {
                return new Vector2(
                      ((gridPos.x * cubeSize.x) / worldWallSize.x),
                          ((gridPos.y * cubeSize.y) / worldWallSize.y)
                );
            }
            Vector2 frontCornerA = ComputeFrontUVFromGrid(new Vector2(x, y));
            Vector2 frontCornerB = ComputeFrontUVFromGrid(new Vector2(x + 1, y));
            Vector2 frontCornerC = ComputeFrontUVFromGrid(new Vector2(x + 1, y + 1));
            Vector2 frontCornerD = ComputeFrontUVFromGrid(new Vector2(x, y + 1));

            Vector2 backCornerA = ComputeBackUVFromGrid(new Vector2(x, y));
            Vector2 backCornerB = ComputeBackUVFromGrid(new Vector2(x + 1, y));
            Vector2 backCornerC = ComputeBackUVFromGrid(new Vector2(x + 1, y + 1));
            Vector2 backCornerD = ComputeBackUVFromGrid(new Vector2(x, y + 1));

            Vector2 frontUV0, frontUV1, frontUV2;
            Vector2 backUV0, backUV1, backUV2;
            switch (corner)
            {
                case WallPiece.TriangularCornerDesignation.BottomLeft:
                    // Front:  A, D, C  (clockwise)
                    frontUV0 = frontCornerA;
                    frontUV1 = frontCornerD;
                    frontUV2 = frontCornerC;

                    // Back:   A, C, D  (counter-clockwise)
                    backUV0 = backCornerA;
                    backUV1 = backCornerD;
                    backUV2 = backCornerC;
                    break;

                case WallPiece.TriangularCornerDesignation.TopRight:
                    // Front:  A, C, B
                    frontUV0 = frontCornerA;
                    frontUV1 = frontCornerC;
                    frontUV2 = frontCornerB;

                    // Back:   A, B, C
                    backUV0 = backCornerA;
                    backUV1 = backCornerC;
                    backUV2 = backCornerB;
                    break;

                case WallPiece.TriangularCornerDesignation.TopLeft:
                    // Front:  B, D, C
                    frontUV0 = frontCornerB;
                    frontUV1 = frontCornerD;
                    frontUV2 = frontCornerC;

                    // Back:   B, C, D
                    backUV0 = backCornerB;
                    backUV1 = backCornerD;
                    backUV2 = backCornerC;
                    break;

                case WallPiece.TriangularCornerDesignation.BottomRight:
                    // Front:  A, D, B
                    frontUV0 = frontCornerA;
                    frontUV1 = frontCornerD;
                    frontUV2 = frontCornerB;

                    // Back:   A, B, D
                    backUV0 = backCornerA;
                    backUV1 = backCornerD;
                    backUV2 = backCornerB;
                    break;

                default:
                    // Front:  A, B, C
                    frontUV0 = frontCornerA;
                    frontUV1 = frontCornerB;
                    frontUV2 = frontCornerC;

                    // Back:   A, C, B
                    backUV0 = backCornerA;
                    backUV1 = backCornerC;
                    backUV2 = backCornerB;
                    break;
            }

            List<Vector3> verticesList = new List<Vector3>();
            List<int> trianglesList = new List<int>();
            List<Vector3> normalsList = new List<Vector3>();
            List<Vector2> uvsList = new List<Vector2>();

            // --- Front face (triangle) ---
            int frontStart = verticesList.Count;
            verticesList.Add(frontVertsCase[0]);
            verticesList.Add(frontVertsCase[1]);
            verticesList.Add(frontVertsCase[2]);
            normalsList.Add(Vector3.back);
            normalsList.Add(Vector3.back);
            normalsList.Add(Vector3.back);
            uvsList.Add(frontUV0);
            uvsList.Add(frontUV1);
            uvsList.Add(frontUV2);
            // Ensure clockwise winding:
            trianglesList.Add(frontStart);
            trianglesList.Add(frontStart + 1);
            trianglesList.Add(frontStart + 2);

            // --- Back face (triangle) ---
            int backStart = verticesList.Count;

            // add the ORIGINAL vertices (no re-ordering)
            verticesList.Add(backVertsOriginal[0]);
            verticesList.Add(backVertsOriginal[1]);
            verticesList.Add(backVertsOriginal[2]);

            normalsList.Add(Vector3.forward);
            normalsList.Add(Vector3.forward);
            normalsList.Add(Vector3.forward);

            // add the matching UVs – same order as the vertices
            uvsList.Add(backUV0);
            uvsList.Add(backUV1);
            uvsList.Add(backUV2);

            // reverse the *index order* so the normal points +Z
            trianglesList.Add(backStart);   // 0
            trianglesList.Add(backStart + 2);   // 2
            trianglesList.Add(backStart + 1);   // 1

            // --- Side faces (3 quads) ---
            // For each edge of the triangle, create a quad between front and back.
            // For the side UVs, we use fixed coordinates to "push" them into the right half of the atlas.
            for (int i = 0; i < 3; i++)
            {
                int next = (i + 1) % 3;
                // Define quad vertices: front[i], front[next], corresponding back (from original order) 
                Vector3 v0 = frontVertsCase[i];
                Vector3 v1 = frontVertsCase[next];
                // For the back, use the non-reversed order from backVertsOriginal so that pairing is consistent.
                Vector3 v2 = backVertsOriginal[next];
                Vector3 v3 = backVertsOriginal[i];

                // Compute a flat normal for the side.
                Vector3 sideNormal = Vector3.Cross(v1 - v0, v3 - v0).normalized;

                int sideStart = verticesList.Count;
                verticesList.Add(v0);
                verticesList.Add(v1);
                verticesList.Add(v2);
                verticesList.Add(v3);
                normalsList.Add(sideNormal);
                normalsList.Add(sideNormal);
                normalsList.Add(sideNormal);
                normalsList.Add(sideNormal);

                // Assign static UVs for the side (right half of the atlas):
                uvsList.Add(new Vector2(0.5f, 0.0f));
                uvsList.Add(new Vector2(1.0f, 0.0f));
                uvsList.Add(new Vector2(1.0f, 1.0f));
                uvsList.Add(new Vector2(0.5f, 1.0f));

                trianglesList.Add(sideStart);
                trianglesList.Add(sideStart + 2);
                trianglesList.Add(sideStart + 1);

                trianglesList.Add(sideStart);
                trianglesList.Add(sideStart + 3);
                trianglesList.Add(sideStart + 2);
            }

            for (int i = 0; i < uvsList.Count; i++)
                uvsList[i] = Vector2.Scale(uvsList[i], textureScale);

            mesh.SetVertices(verticesList);
            mesh.SetTriangles(trianglesList, 0);
            mesh.SetNormals(normalsList);
            mesh.SetUVs(0, uvsList);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.UploadMeshData(false);
            triangle.transform.localScale = new Vector3(1.0f / numColumns, 1.0f / numRows, 1.0f);
            return triangle;
        }
        static public GameObject CreateIrregularCube(
                Vector3 size,
                Vector3 position,
                int x, int y, int z,
                Material originalMaterial,
                Vector3 cubeSize,
                Vector3[,,] vertexOffsets,
                Vector3 worldWallSize,
                int numRows, int numColumns,
                Vector2 textureScale,
                int numZDepth,
                string objectName)
        {
            GameObject cube = new GameObject(objectName);
            cube.transform.position = position;
            cube.transform.localScale = size;

            MeshFilter meshFilter = cube.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = cube.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = originalMaterial;

            Mesh mesh = new Mesh();
            meshFilter.sharedMesh = mesh;

            // Define vertices for flat shading (24 vertices, 4 for each face)
            Vector3[] vertices = new Vector3[24]
            {
            // Front face
            new Vector3(-0.5f + vertexOffsets[x, y, z].x, -0.5f + vertexOffsets[x, y, z].y, -0.5f + vertexOffsets[x, y, z].z), // 0
            new Vector3( 0.5f + vertexOffsets[x + 1, y, z].x, -0.5f + vertexOffsets[x + 1, y, z].y, -0.5f + vertexOffsets[x + 1, y, z].z), // 1
            new Vector3( 0.5f + vertexOffsets[x + 1, y + 1, z].x,  0.5f + vertexOffsets[x + 1, y + 1, z].y, -0.5f + vertexOffsets[x + 1, y + 1, z].z), // 2
            new Vector3(-0.5f + vertexOffsets[x, y + 1, z].x,  0.5f + vertexOffsets[x, y + 1, z].y, -0.5f + vertexOffsets[x, y + 1, z].z), // 3

            // Back face
            new Vector3( 0.5f + vertexOffsets[x + 1, y, z + 1].x, -0.5f + vertexOffsets[x + 1, y, z + 1].y,  0.5f + vertexOffsets[x + 1, y, z + 1].z), // 4
            new Vector3(-0.5f + vertexOffsets[x, y, z + 1].x, -0.5f + vertexOffsets[x, y, z + 1].y,  0.5f + vertexOffsets[x, y, z + 1].z), // 5
            new Vector3(-0.5f + vertexOffsets[x, y + 1, z + 1].x,  0.5f + vertexOffsets[x, y + 1, z + 1].y,  0.5f + vertexOffsets[x, y + 1, z + 1].z), // 6
            new Vector3( 0.5f + vertexOffsets[x + 1, y + 1, z + 1].x,  0.5f + vertexOffsets[x + 1, y + 1, z + 1].y,  0.5f + vertexOffsets[x + 1, y + 1, z + 1].z), // 7

            // Top face
            new Vector3(-0.5f + vertexOffsets[x, y + 1, z].x,  0.5f + vertexOffsets[x, y + 1, z].y, -0.5f + vertexOffsets[x, y + 1, z].z), // 8
            new Vector3( 0.5f + vertexOffsets[x + 1, y + 1, z].x,  0.5f + vertexOffsets[x + 1, y + 1, z].y, -0.5f + vertexOffsets[x + 1, y + 1, z].z), // 9
            new Vector3( 0.5f + vertexOffsets[x + 1, y + 1, z + 1].x,  0.5f + vertexOffsets[x + 1, y + 1, z + 1].y,  0.5f + vertexOffsets[x + 1, y + 1, z + 1].z), // 10
            new Vector3(-0.5f + vertexOffsets[x, y + 1, z + 1].x,  0.5f + vertexOffsets[x, y + 1, z + 1].y,  0.5f + vertexOffsets[x, y + 1, z + 1].z), // 11

            // Bottom face
            new Vector3(-0.5f + vertexOffsets[x, y, z].x, -0.5f + vertexOffsets[x, y, z].y, -0.5f + vertexOffsets[x, y, z].z), // 12
            new Vector3( 0.5f + vertexOffsets[x + 1, y, z].x, -0.5f + vertexOffsets[x + 1, y, z].y, -0.5f + vertexOffsets[x + 1, y, z].z), // 13
            new Vector3( 0.5f + vertexOffsets[x + 1, y, z + 1].x, -0.5f + vertexOffsets[x + 1, y, z + 1].y,  0.5f + vertexOffsets[x + 1, y, z + 1].z), // 14
            new Vector3(-0.5f + vertexOffsets[x, y, z + 1].x, -0.5f + vertexOffsets[x, y, z + 1].y,  0.5f + vertexOffsets[x, y, z + 1].z), // 15

            // Left face
            new Vector3(-0.5f + vertexOffsets[x, y, z].x, -0.5f + vertexOffsets[x, y, z].y, -0.5f + vertexOffsets[x, y, z].z), // 16
            new Vector3(-0.5f + vertexOffsets[x, y + 1, z].x,  0.5f + vertexOffsets[x, y + 1, z].y, -0.5f + vertexOffsets[x, y + 1, z].z), // 17
            new Vector3(-0.5f + vertexOffsets[x, y + 1, z + 1].x,  0.5f + vertexOffsets[x, y + 1, z + 1].y,  0.5f + vertexOffsets[x, y + 1, z + 1].z), // 18
            new Vector3(-0.5f + vertexOffsets[x, y, z + 1].x, -0.5f + vertexOffsets[x, y, z + 1].y,  0.5f + vertexOffsets[x, y, z + 1].z), // 19

            // Right face
            new Vector3( 0.5f + vertexOffsets[x + 1, y, z].x, -0.5f + vertexOffsets[x + 1, y, z].y, -0.5f + vertexOffsets[x + 1, y, z].z), // 20
            new Vector3( 0.5f + vertexOffsets[x + 1, y + 1, z].x,  0.5f + vertexOffsets[x + 1, y + 1, z].y, -0.5f + vertexOffsets[x + 1, y + 1, z].z), // 21
            new Vector3( 0.5f + vertexOffsets[x + 1, y + 1, z + 1].x,  0.5f + vertexOffsets[x + 1, y + 1, z + 1].y,  0.5f + vertexOffsets[x + 1, y + 1, z + 1].z), // 22
            new Vector3( 0.5f + vertexOffsets[x + 1, y, z + 1].x, -0.5f + vertexOffsets[x + 1, y, z + 1].y,  0.5f + vertexOffsets[x + 1, y, z + 1].z) // 23
                    };

            // Define triangles
            int[] triangles = new int[]
            {
			
			// Front face
			0, 2, 1,
            0, 3, 2,

			// Back face
			4, 6, 5,
            4, 7, 6,

			// Top face
			8, 10, 9,
            8, 11, 10,

			// Bottom face
			12, 13, 14,
            12, 14, 15,

			// Left face
			16, 18, 17,
            16, 19, 18,

			// Right face
			20, 21, 22,
            20, 22, 23
            };

            Vector2[] uvs = new Vector2[24];

            // Front face
            uvs[0] = UvFront(x, y, z);
            uvs[1] = UvFront(x + 1, y, z);
            uvs[2] = UvFront(x + 1, y + 1, z);
            uvs[3] = UvFront(x, y + 1, z);

            // Back face
            uvs[4] = UvBack(x + 1, y, z + 1);
            uvs[5] = UvBack(x, y, z + 1);
            uvs[6] = UvBack(x, y + 1, z + 1);
            uvs[7] = UvBack(x + 1, y + 1, z + 1);

            // TOP face 
            uvs[8] = UvTop(x, y + 1, z);
            uvs[9] = UvTop(x + 1, y + 1, z);
            uvs[10] = UvTop(x + 1, y + 1, z + 1);
            uvs[11] = UvTop(x, y + 1, z + 1);

            // BOTTOM face 
            uvs[12] = UvBottom(x, y, z);
            uvs[13] = UvBottom(x + 1, y, z);
            uvs[14] = UvBottom(x + 1, y, z + 1);
            uvs[15] = UvBottom(x, y, z + 1);

            // LEFT face
            uvs[16] = UvLeft(x, y, z);
            uvs[17] = UvLeft(x, y + 1, z);
            uvs[18] = UvLeft(x, y + 1, z + 1);
            uvs[19] = UvLeft(x, y, z + 1);

            // RIGHT face
            uvs[20] = UvRight(x + 1, y, z);
            uvs[21] = UvRight(x + 1, y + 1, z);
            uvs[22] = UvRight(x + 1, y + 1, z + 1);
            uvs[23] = UvRight(x + 1, y, z + 1);

            for (int i = 0; i < uvs.Length; i++)
                uvs[i] = Vector2.Scale(uvs[i], textureScale);


            Vector2 UvFront(int vx, int vy, int vz)
            {
                float u = (vx * cubeSize.x + vertexOffsets[vx, vy, vz].x * cubeSize.x) / worldWallSize.x;
                float v = (vy * cubeSize.y + vertexOffsets[vx, vy, vz].y * cubeSize.y) / worldWallSize.y;
                return new Vector2(u, v);
            }

            Vector2 UvBack(int vx, int vy, int vz)
            {
                float u = (vx * cubeSize.x + vertexOffsets[vx, vy, vz].x * cubeSize.x) / worldWallSize.x;
                float v = (vy * cubeSize.y + vertexOffsets[vx, vy, vz].y * cubeSize.y) / worldWallSize.y;
                return new Vector2(u, v);
            }

            Vector2 UvTop(int vx, int vy, int vz)
            {
                float u = (vx * cubeSize.x + vertexOffsets[vx, vy, vz].x * cubeSize.x) / worldWallSize.x; // X-span
                float v = (vz * cubeSize.z + vertexOffsets[vx, vy, vz].z * cubeSize.z) / worldWallSize.x; // Z-span
                return new Vector2(u, v);
            }

            Vector2 UvBottom(int vx, int vy, int vz)
            {
                float u = (vx * cubeSize.x + vertexOffsets[vx, vy, vz].x * cubeSize.x) / worldWallSize.x; // X-span
                float v = (vz * cubeSize.z + vertexOffsets[vx, vy, vz].z * cubeSize.z) / worldWallSize.x; // Z-span
                return new Vector2(u, v);
            }
            Vector2 UvLeft(int vx, int vy, int vz)
            {
                // u runs along Z‑span, v runs along Y‑span
                float u = (vz * cubeSize.z + vertexOffsets[vx, vy, vz].z * cubeSize.z) / worldWallSize.x;
                float v = (vy * cubeSize.y + vertexOffsets[vx, vy, vz].y * cubeSize.y) / worldWallSize.y;
                return new Vector2(u, v);
            }

            Vector2 UvRight(int vx, int vy, int vz)
            {
                // Same mapping as left; flip u if you need the texture mirrored
                float u = (vz * cubeSize.z + vertexOffsets[vx, vy, vz].z * cubeSize.z) / worldWallSize.x;
                float v = (vy * cubeSize.y + vertexOffsets[vx, vy, vz].y * cubeSize.y) / worldWallSize.y;
                return new Vector2(u, v);
            }

            // Define normals for flat shading
            Vector3[] normals = new Vector3[24]
            {
            // Front
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,

            // Back
            Vector3.back, Vector3.back, Vector3.back, Vector3.back,

            // Top
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,

            // Bottom
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,

            // Left
            Vector3.left, Vector3.left, Vector3.left, Vector3.left,

            // Right
            Vector3.right, Vector3.right, Vector3.right, Vector3.right
            };

            // Assign vertices, triangles, uvs, and normals to the mesh
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;

            // Recalculate bounds
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.UploadMeshData(false);
            cube.transform.localScale = new Vector3(1.0f / (float)numColumns, 1.0f / (float)numRows, 1.0f / (float)numZDepth);
            return cube;
        }
    }
}