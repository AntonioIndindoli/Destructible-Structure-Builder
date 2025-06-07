using UnityEngine;
using System.Collections.Generic;

namespace Mayuns.DSB
{
	public static class GibBuildingUtility
	{
		private static bool edgeSet = false;
		private static Vector3 edgeVertex = Vector3.zero;
		private static Vector2 edgeUV = Vector2.zero;
		private static Plane edgePlane = new Plane();
		[System.Serializable]
		public struct DebrisData
		{
			public Mesh sharedMesh;
			public Material[] materials;

			public Vector3 localScale;
		}

		public static List<DebrisData> CreateDebris(GameObject source, int cutCount, bool isWindow)
		{
			var meshFilter = source.GetComponent<MeshFilter>();
			if (meshFilter == null) return null;

			var originalMesh = meshFilter.sharedMesh;
			var parts = new List<MeshSlicer>();
			var subParts = new List<MeshSlicer>();

			var mainPart = new MeshSlicer
			{
				UV = originalMesh.uv,
				Vertices = originalMesh.vertices,
				Normals = originalMesh.normals,
				Triangles = new int[originalMesh.subMeshCount][],
				Bounds = originalMesh.bounds
			};

			for (int i = 0; i < originalMesh.subMeshCount; i++)
				mainPart.Triangles[i] = originalMesh.GetTriangles(i);

			parts.Add(mainPart);

			for (int c = 0; c < cutCount; c++)
			{
				for (int i = 0; i < parts.Count; i++)
				{
					var bounds = parts[i].Bounds;
					bounds.Expand(0.5f);

					// Choose a completely random point within the expanded bounds
					Vector3 randomInBounds = new Vector3(
						Random.Range(bounds.min.x, bounds.max.x),
						Random.Range(bounds.min.y, bounds.max.y),
						Random.Range(bounds.min.z, bounds.max.z)
					);

					// Fully random slicing direction
					Vector3 randomNormal = Random.onUnitSphere;

					// Construct slicing plane
					var plane = new Plane(randomNormal, randomInBounds);

					// Apply slicing
					subParts.Add(GenerateMesh(parts[i], plane, true));
					subParts.Add(GenerateMesh(parts[i], plane, false));
				}


				parts = new List<MeshSlicer>(subParts);
				subParts.Clear();
			}

			List<DebrisData> debrisList = new List<DebrisData>(parts.Count);
			for (int i = 0; i < parts.Count; i++)
			{
				var data = parts[i].MakeDebrisData(source, isWindow);
				debrisList.Add(data);
			}

			return debrisList;
		}

		private static MeshSlicer GenerateMesh(MeshSlicer original, Plane plane, bool left)
		{
			var splitMesh = new MeshSlicer() { };
			var ray1 = new Ray();
			var ray2 = new Ray();


			for (var i = 0; i < original.Triangles.Length; i++)
			{
				var triangles = original.Triangles[i];
				edgeSet = false;

				for (var j = 0; j < triangles.Length; j = j + 3)
				{
					var sideA = plane.GetSide(original.Vertices[triangles[j]]) == left;
					var sideB = plane.GetSide(original.Vertices[triangles[j + 1]]) == left;
					var sideC = plane.GetSide(original.Vertices[triangles[j + 2]]) == left;

					var sideCount = (sideA ? 1 : 0) +
													(sideB ? 1 : 0) +
													(sideC ? 1 : 0);
					if (sideCount == 0)
					{
						continue;
					}
					if (sideCount == 3)
					{
						splitMesh.AddTriangle(i,
						original.Vertices[triangles[j]], original.Vertices[triangles[j + 1]], original.Vertices[triangles[j + 2]],
						original.Normals[triangles[j]], original.Normals[triangles[j + 1]], original.Normals[triangles[j + 2]],
						original.UV[triangles[j]], original.UV[triangles[j + 1]], original.UV[triangles[j + 2]]);
						continue;
					}

					var singleIndex = sideB == sideC ? 0 : sideA == sideC ? 1 : 2;

					ray1.origin = original.Vertices[triangles[j + singleIndex]];
					var dir1 = original.Vertices[triangles[j + ((singleIndex + 1) % 3)]] - original.Vertices[triangles[j + singleIndex]];
					ray1.direction = dir1;
					plane.Raycast(ray1, out var enter1);
					var lerp1 = enter1 / dir1.magnitude;

					ray2.origin = original.Vertices[triangles[j + singleIndex]];
					var dir2 = original.Vertices[triangles[j + ((singleIndex + 2) % 3)]] - original.Vertices[triangles[j + singleIndex]];
					ray2.direction = dir2;
					plane.Raycast(ray2, out var enter2);
					var lerp2 = enter2 / dir2.magnitude;

					AddEdge(i,
									splitMesh,
									left ? plane.normal * -1f : plane.normal,
									ray1.origin + ray1.direction.normalized * enter1,
									ray2.origin + ray2.direction.normalized * enter2,
									Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
									Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));

					if (sideCount == 1)
					{
						splitMesh.AddTriangle(i,
						original.Vertices[triangles[j + singleIndex]],
						//Vector3.Lerp(originalMesh.vertices[triangles[j + singleIndex]], originalMesh.vertices[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
						//Vector3.Lerp(originalMesh.vertices[triangles[j + singleIndex]], originalMesh.vertices[triangles[j + ((singleIndex + 2) % 3)]], lerp2),
						ray1.origin + ray1.direction.normalized * enter1,
						ray2.origin + ray2.direction.normalized * enter2,
						original.Normals[triangles[j + singleIndex]],
						Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
						Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2),
						original.UV[triangles[j + singleIndex]],
						Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
						Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));

						continue;
					}

					if (sideCount == 2)
					{
						splitMesh.AddTriangle(i,
						ray1.origin + ray1.direction.normalized * enter1,
						original.Vertices[triangles[j + ((singleIndex + 1) % 3)]],
						original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
						Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
						original.Normals[triangles[j + ((singleIndex + 1) % 3)]],
						original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
						Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
						original.UV[triangles[j + ((singleIndex + 1) % 3)]],
						original.UV[triangles[j + ((singleIndex + 2) % 3)]]);
						splitMesh.AddTriangle(i,
						ray1.origin + ray1.direction.normalized * enter1,
						original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
						ray2.origin + ray2.direction.normalized * enter2,
						Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
						original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
						Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2),
						Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
						original.UV[triangles[j + ((singleIndex + 2) % 3)]],
						Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));
						continue;
					}


				}
			}

			splitMesh.FillArrays();

			return splitMesh;
		}
		private static void AddEdge(int subMesh, MeshSlicer splitMesh, Vector3 normal, Vector3 vertex1, Vector3 vertex2, Vector2 uv1, Vector2 uv2)
		{
			if (!edgeSet)
			{
				edgeSet = true;
				edgeVertex = vertex1;
				edgeUV = uv1;
			}
			else
			{
				edgePlane.Set3Points(edgeVertex, vertex1, vertex2);

				splitMesh.AddInteriorTriangle(
				edgeVertex,
				edgePlane.GetSide(edgeVertex + normal) ? vertex1 : vertex2,
				edgePlane.GetSide(edgeVertex + normal) ? vertex2 : vertex1,
				normal, normal, normal,
				edgeUV, uv1, uv2
			);
			}
		}

		public class MeshSlicer
		{
			private List<Vector3> _Verticies = new List<Vector3>();
			private List<Vector3> _Normals = new List<Vector3>();
			private List<List<int>> _Triangles = new List<List<int>>();
			private List<int> _InteriorTriangles = new List<int>();
			private List<Vector2> _UVs = new List<Vector2>();
			public Vector3[] Vertices;
			public Vector3[] Normals;
			public int[][] Triangles;
			public Vector2[] UV;
			public GameObject GameObject;
			public Bounds Bounds = new Bounds();

			public MeshSlicer()
			{

			}

			public void AddInteriorTriangle(
				Vector3 vert1, Vector3 vert2, Vector3 vert3,
				Vector3 normal1, Vector3 normal2, Vector3 normal3,
				Vector2 uv1, Vector2 uv2, Vector2 uv3
			)
			{
				int i0 = _Verticies.Count; _Verticies.Add(vert1);
				int i1 = _Verticies.Count; _Verticies.Add(vert2);
				int i2 = _Verticies.Count; _Verticies.Add(vert3);

				_Normals.Add(normal1);
				_Normals.Add(normal2);
				_Normals.Add(normal3);

				_UVs.Add(uv1);
				_UVs.Add(uv2);
				_UVs.Add(uv3);

				_InteriorTriangles.Add(i0);
				_InteriorTriangles.Add(i1);
				_InteriorTriangles.Add(i2);

				Bounds.min = Vector3.Min(Bounds.min, vert1);
				Bounds.min = Vector3.Min(Bounds.min, vert2);
				Bounds.min = Vector3.Min(Bounds.min, vert3);
				Bounds.max = Vector3.Min(Bounds.max, vert1);
				Bounds.max = Vector3.Min(Bounds.max, vert2);
				Bounds.max = Vector3.Min(Bounds.max, vert3);
			}
			public void AddTriangle(int submesh, Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 normal1, Vector3 normal2, Vector3 normal3, Vector2 uv1, Vector2 uv2, Vector2 uv3)
			{
				if (_Triangles.Count - 1 < submesh)
					_Triangles.Add(new List<int>());

				_Triangles[submesh].Add(_Verticies.Count);
				_Verticies.Add(vert1);
				_Triangles[submesh].Add(_Verticies.Count);
				_Verticies.Add(vert2);
				_Triangles[submesh].Add(_Verticies.Count);
				_Verticies.Add(vert3);
				_Normals.Add(normal1);
				_Normals.Add(normal2);
				_Normals.Add(normal3);
				_UVs.Add(uv1);
				_UVs.Add(uv2);
				_UVs.Add(uv3);

				Bounds.min = Vector3.Min(Bounds.min, vert1);
				Bounds.min = Vector3.Min(Bounds.min, vert2);
				Bounds.min = Vector3.Min(Bounds.min, vert3);
				Bounds.max = Vector3.Min(Bounds.max, vert1);
				Bounds.max = Vector3.Min(Bounds.max, vert2);
				Bounds.max = Vector3.Min(Bounds.max, vert3);
			}

			public void FillArrays()
			{
				Vertices = _Verticies.ToArray();
				Normals = _Normals.ToArray();
				UV = _UVs.ToArray();

				// We'll have exactly 2 submeshes now:
				// 0 for exterior, 1 for interior
				Triangles = new int[2][];

				// Merge all exterior submeshes into 1 big list:
				var exteriorAll = new List<int>();
				for (int s = 0; s < _Triangles.Count; s++)
				{
					exteriorAll.AddRange(_Triangles[s]);
				}
				Triangles[0] = exteriorAll.ToArray();

				// The interior submesh is our new cut polygons
				Triangles[1] = _InteriorTriangles.ToArray();
			}

			public DebrisData MakeDebrisData(GameObject original, bool isWindow)
			{
				Vector3[] scaledVertices = new Vector3[Vertices.Length];
				Vector3 lossyScale = original.transform.lossyScale;

				for (int i = 0; i < Vertices.Length; i++)
				{
					// Apply scale manually — not TransformPoint (which also applies rotation and position)
					Vector3 v = Vertices[i];
					scaledVertices[i] = new Vector3(v.x * lossyScale.x, v.y * lossyScale.y, v.z * lossyScale.z);
				}

				Mesh mesh = new Mesh();
				mesh.MarkDynamic();
				mesh.name = original.GetComponent<MeshFilter>().sharedMesh.name;
				mesh.vertices = scaledVertices;
				mesh.normals = Normals; // assuming already correct
				mesh.uv = UV;
				mesh.subMeshCount = Triangles.Length;

				for (int i = 0; i < Triangles.Length; i++)
					mesh.SetTriangles(Triangles[i], i);

				mesh.RecalculateBounds();

				Material baseMat = original.GetComponent<MeshRenderer>().sharedMaterial;

				return new DebrisData
				{
					sharedMesh = mesh,
					materials = new[] { baseMat, baseMat },
					localScale = Vector3.one, // we baked scale in manually
				};
			}


		}
	}
}

