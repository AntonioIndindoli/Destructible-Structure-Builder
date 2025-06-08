using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace Mayuns.DSB
{
	public class WallManager : MonoBehaviour
	{
		[HideInInspector]
		[SerializeField] public float wallPieceHealth = 100f;
		[HideInInspector]
		[SerializeField] public float wallPieceWindowHealth = 1f;
		[HideInInspector]
		[SerializeField] public HashSet<StructuralMember> edgeMembers = new HashSet<StructuralMember>();
		[HideInInspector]
		[SerializeField] public int numRows = 8;
		[HideInInspector]
		[SerializeField] public int numColumns = 8;
		[SerializeField] public List<Chunk> _chunks = new List<Chunk>();         // all created chunks
		[HideInInspector]
		[SerializeField] public float WallPieceMass = 50.0f;
		[HideInInspector]
		[SerializeField] public bool isGrouped = false;
		[SerializeField] public List<WallPiece> wallGrid;
		[SerializeField] public StructuralGroupManager structuralGroup;
		[SerializeField] public bool isDamaged = false;
		private float variationAmount = .25f;
		private Vector3[,,] vertexOffsets;
		private Vector3 worldWallSize;
		[HideInInspector]
		public float validationInterval = .1f;
		public bool validateAgain = false;
		public bool isValidating = false;
		public bool isRebuildingGrid = false;
		private int _lastWallFingerprint;

		[HideInInspector]
		[SerializeField] public Material glassMaterial;
		[HideInInspector]
		[SerializeField] public Material wallMaterial;
		IEnumerator Start()
		{
			if (isGrouped)
				yield break;

			if (structuralGroup == null)
			{
				structuralGroup = GetComponentInParent<StructuralGroupManager>();
			}
			if (structuralGroup == null)
			{
				Debug.Log("Destructable piece spawned without StructuralGroupManager in parent. Deleting immediately.");
				Destroy(gameObject);
			}

			yield return null; // Wait for one frame (after all Start() methods are done)


			UpdateEdgeStatusForGrid();
			RegisterWithAttachedMembers();
		}

		public void ValidateWallIntegrity()
		{
			if (isValidating || isRebuildingGrid)
			{
				validateAgain = true;
				return;
			}
			isValidating = true;
			StartCoroutine(IntegrityRoutine());
		}

		IEnumerator IntegrityRoutine()
		{
			try
			{
				DetachDisconnectedPieces();
				SelfDestructCheck();

				yield return new WaitForSeconds(validationInterval);
				if (validateAgain)
				{
					validateAgain = false;
					StartCoroutine(IntegrityRoutine());
				}

				isValidating = false;
			}
			finally
			{
				isValidating = false;
			}
		}

		public void WallPieceDestroyed(int x, int y)
		{
			if (x >= 0 && x < numColumns && y >= 0 && y < numRows)
			{
				if (wallGrid[x + y * numColumns] != null)
				{
					wallGrid[x + y * numColumns] = null;
				}
			}

			ValidateWallIntegrity();
		}

		public void DetachChunk(Chunk chunk)
		{
			if (chunk == null || chunk.IsBroken || chunk.gameObject == null) return;

			chunk.IsBroken = true;
			isDamaged = true;
			SwitchToConvexCollider(chunk);

			GameObject combinedGO = chunk.gameObject;

			var piece = combinedGO.GetComponent<WallPiece>();

			// If its a window chunk, shatter into pieces instead of detaching
			if (piece != null && piece.isWindow)
			{
				StartCoroutine(ApplyUncombinedDamageOverTime(chunk, int.MaxValue));
			}
			else
			{
				// Detach from wall hierarchy
				combinedGO.transform.SetParent(null, true);

				// Add rigidbody if not already present
				Rigidbody rb = combinedGO.GetComponent<Rigidbody>();
				if (rb == null)
				{
					rb = combinedGO.AddComponent<Rigidbody>();
				}

				rb.mass = WallPieceMass * chunk.wallPieces.Count;
				rb.interpolation = RigidbodyInterpolation.None;
				rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

				Rigidbody sourceRb = this.GetComponent<Rigidbody>();
				if (sourceRb != null)
				{
					rb.velocity = sourceRb.velocity;
					rb.angularVelocity = sourceRb.angularVelocity;
				}

				// Disable proxy logic
				var proxy = combinedGO.GetComponent<Chunk>();
				if (proxy != null)
				{
					Destroy(proxy); // prevents accidental re-entry into damage logic
				}

				if (piece != null)
				{
					Destroy(piece);
				}
				foreach (GameObject wallPiece in chunk.wallPieces)
				{
					var pos = wallPiece.GetComponent<WallPiece>().gridPosition;
					int idx = pos.x + pos.y * numColumns;
					wallGrid[idx] = null;
				}

				if (structuralGroup != null && structuralGroup.gibManager != null)
				{
					structuralGroup.gibManager.RegisterTimedGib(combinedGO, structuralGroup.gibManager.mediumGibLifetime);
				}
			}
			ValidateWallIntegrity();
			SelfDestructCheck();
		}

		public void SwitchToConvexCollider(Chunk chunk)
		{
			if (chunk != null)
			{
				MeshCollider collider = chunk.gameObject.GetComponent<MeshCollider>();
				if (collider != null)
				{
					collider.enabled = false;
					Destroy(collider);
				}
				var box = chunk.gameObject.GetComponent<BoxCollider>();
				if (box != null)
				{
					box.isTrigger = false;
				}

			}
		}

		public void SelfDestructCheck()
		{
			int count = 0;
			foreach (var p in wallGrid)
			{
				if (p != null)
				{
					count++;
					if (count > 1)
						return;
				}
			}

			if (gameObject.transform.parent != null)
			{
				Destroy(gameObject.transform.parent.gameObject);
			}
			else
			{
				Destroy(gameObject);
			}
		}

		public void UncombineChunk(Chunk chunk, float accumulatedDamage)
		{
			if (chunk.IsBroken) return;
			chunk.IsBroken = true;
			isDamaged = true;
			SwitchToConvexCollider(chunk);

			StartCoroutine(ApplyUncombinedDamageOverTime(chunk, accumulatedDamage));
		}

		private IEnumerator ApplyUncombinedDamageOverTime(Chunk chunk, float totalDamage)
		{
			isRebuildingGrid = true;
			int pieceCount = chunk.wallPieces.Count;
			if (pieceCount == 0)
				yield break;

			float damagePerPiece = totalDamage / pieceCount;

			for (int i = 0; i < pieceCount; i++)
			{
				GameObject go = chunk.wallPieces[i];
				if (go)
				{
					go.SetActive(true);
					go.hideFlags = HideFlags.None;
					var wp = go.GetComponent<WallPiece>();
					if (wp)
					{
						var pos = wp.gridPosition;
						int idx = pos.x + pos.y * numColumns;
						wallGrid[idx] = wp;

						wp.TakeDamage(damagePerPiece);
					}
				}

				// Yield every few pieces or each piece to spread the load
				if (i % 2 == 0) // Adjust this batch size as needed
					yield return null;
			}

			Destroy(chunk.gameObject);
			isRebuildingGrid = false;
			ValidateWallIntegrity();
		}

		public void InstantUncombine()
		{
			foreach (Chunk chunk in _chunks)
			{
				if (chunk != null)
				{
					for (int i = 0; i < chunk.wallPieces.Count; i++)
					{
						GameObject go = chunk.wallPieces[i];
						if (go)
						{
							go.SetActive(true);
							var wp = go.GetComponent<WallPiece>();
							if (wp)
							{
								var pos = wp.gridPosition;
								int idx = pos.x + pos.y * numColumns;
								wallGrid[idx] = wp;
							}
						}
					}
#if UNITY_EDITOR
					DestroyImmediate(chunk.gameObject);
#else
					Destroy(chunk.gameObject);
#endif
				}
			}
			_chunks.Clear();
		}

		public void DetachDisconnectedPieces()
		{
			int[,] distances = new int[numColumns, numRows];

			// 1. Prepare a 2D array to hold each cell’s distance from a valid edge.
			for (int x = 0; x < numColumns; x++)
			{
				for (int y = 0; y < numRows; y++)
				{
					distances[x, y] = int.MaxValue;
				}
			}
			bool foundAnyEdge = false;
			if (!isGrouped)
			{
				// ------------------------------------------------------------------
				// 2. Seed a multi-source BFS with every *edge* WallPiece
				// ------------------------------------------------------------------
				Queue<(int x, int y)> queue = new();


				for (int x = 0; x < numColumns; ++x)
				{
					for (int y = 0; y < numRows; ++y)
					{
						if (wallGrid[x + y * numColumns] == null) continue;

						WallPiece piece = wallGrid[x + y * numColumns];
						if (piece == null || piece.isDestroyed) continue;

						bool isEdgeOfThisStructure =
							piece.isEdge &&
							piece.attachedMember != null &&
							!piece.attachedMember.isDestroyed &&
							piece.closestMemberPiece != null &&
							!piece.closestMemberPiece.isDestroyed &&
							piece.closestMemberPiece.GetComponentInParent<StructuralGroupManager>() == structuralGroup;


						if (isEdgeOfThisStructure)
						{
							distances[x, y] = 0;
							queue.Enqueue((x, y));
							foundAnyEdge = true;
						}

					}
				}

				// ------------------------------------------------------------------
				// 3. Flood-fill outwards from every edge piece
				// ------------------------------------------------------------------
				System.ReadOnlySpan<Vector2Int> dirs = stackalloc[]
				{
			new Vector2Int( 0,  1),
			new Vector2Int( 0, -1),
			new Vector2Int( 1,  0),
			new Vector2Int(-1,  0)
			};

				while (queue.Count > 0)
				{
					var (cx, cy) = queue.Dequeue();
					int nextD = distances[cx, cy] + 1;

					for (int i = 0; i < dirs.Length; i++)
					{
						var d = dirs[i];
						int nx = cx + d.x;
						int ny = cy + d.y;

						if (nx < 0 || nx >= numColumns || ny < 0 || ny >= numRows) continue;
						int nIdx = nx + ny * numColumns;
						if (wallGrid[nIdx] == null) continue;
						WallPiece neighbor = wallGrid[nIdx];
						if (neighbor == null || neighbor.isDestroyed) continue;

						bool isWindow = neighbor.isWindow;
						bool alreadyVisited = distances[nx, ny] != int.MaxValue;

						if (isWindow)
						{
							if (!alreadyVisited)
							{
								// Mark as visited with a special sentinel value
								distances[nx, ny] = -1; // use -1 to mark visited window
								queue.Enqueue((nx, ny));
							}
							continue; // Do not propagate distances through windows
						}

						// Normal non-window cell
						if (distances[nx, ny] > nextD)
						{
							distances[nx, ny] = nextD;
							queue.Enqueue((nx, ny));
						}
					}

				}

			}
			List<Chunk> chunksToUncombine = new();
			HashSet<WallPiece> piecesToGroup = new();

			// Decide what needs uncombining
			for (int x = 0; x < numColumns; ++x)
			{
				for (int y = 0; y < numRows; ++y)
				{
					if (wallGrid[x + y * numColumns] == null) continue;

					WallPiece piece = wallGrid[x + y * numColumns];
					if (piece == null || piece.isDestroyed) continue;

					bool detached = distances[x, y] > (numColumns + numRows);
					bool unsupported = !foundAnyEdge || detached;

					if (piece.isProxy)
					{
						Chunk chunk = piece.chunk;
						if (unsupported && piece.chunk != null && !piece.chunk.IsBroken)
						{
							piece.isEdge = false;
							DetachChunk(chunk);
							wallGrid[x + y * numColumns] = null;
						}

						continue;   // proxies never go into piecesToGroup
					}

					if (unsupported)
					{
						piece.isEdge = false;
						piecesToGroup.Add(piece);
					}
				}
			}

			List<List<WallPiece>> connectedGroups = GetConnectedGroups(piecesToGroup);
			foreach (var group in connectedGroups)
			{
				CreateChunkGroup(group);
			}
		}

		private List<List<WallPiece>> GetConnectedGroups(HashSet<WallPiece> allPieces)
		{
			List<List<WallPiece>> groups = new List<List<WallPiece>>();
			HashSet<WallPiece> visited = new HashSet<WallPiece>();

			Dictionary<Vector2Int, WallPiece> positionMap = new Dictionary<Vector2Int, WallPiece>();
			foreach (var piece in allPieces)
			{
				positionMap[piece.gridPosition] = piece;
			}

			Vector2Int[] directions = new Vector2Int[]
			{
		new Vector2Int(0, 1),
		new Vector2Int(0, -1),
		new Vector2Int(1, 0),
		new Vector2Int(-1, 0)
			};

			foreach (var piece in allPieces)
			{
				if (visited.Contains(piece)) continue;

				List<WallPiece> group = new List<WallPiece>();
				Queue<WallPiece> queue = new Queue<WallPiece>();
				queue.Enqueue(piece);
				visited.Add(piece);

				while (queue.Count > 0)
				{
					var current = queue.Dequeue();
					group.Add(current);

					foreach (var dir in directions)
					{
						Vector2Int neighborPos = current.gridPosition + dir;
						if (positionMap.TryGetValue(neighborPos, out WallPiece neighbor) && !visited.Contains(neighbor))
						{
							queue.Enqueue(neighbor);
							visited.Add(neighbor);
						}
					}
				}

				groups.Add(group);
			}

			return groups;
		}

		private void CreateChunkGroup(List<WallPiece> pieces)
		{
			if (pieces == null || pieces.Count == 0) return;

			GameObject groupParent = new GameObject("DetachedWallGroup");
			groupParent.transform.SetPositionAndRotation(transform.position, Quaternion.identity);

			Rigidbody rb = groupParent.AddComponent<Rigidbody>();
			WallManager newWallManager = groupParent.AddComponent<WallManager>();

			foreach (WallPiece wallPiece in pieces)
			{
				if (wallPiece == null)
				{
					continue;
				}

				Transform pieceTransform = wallPiece.transform;

				Vector3 worldPos = pieceTransform.position;
				Quaternion worldRot = pieceTransform.rotation;
				Vector3 worldScale = pieceTransform.lossyScale;

				pieceTransform.SetParent(groupParent.transform, false);
				pieceTransform.position = worldPos;
				pieceTransform.rotation = worldRot;
				pieceTransform.localScale = worldScale;

				rb.mass += WallPieceMass;

				Vector2Int pos = wallPiece.gridPosition;
				if (pos.x >= 0 && pos.x < numColumns && pos.y >= 0 && pos.y < numRows)
				{
					wallGrid[pos.x + pos.y * numColumns] = null;
				}

			}

			SetupNewWallManager(newWallManager, pieces);

			Rigidbody originalRb = transform.GetComponent<Rigidbody>();
			if (originalRb != null)
			{
				rb.velocity = originalRb.velocity;
				rb.angularVelocity = originalRb.angularVelocity;
			}
			Destroy(groupParent, 10f);
		}

		public void RegisterWithAttachedMembers()
		{
			foreach (StructuralMember member in edgeMembers)
			{
				// HashSet guarantees uniqueness, null-check is just defensive
				if (member != null)
					member.AddWallManager(this);
			}
		}

		private void SetupNewWallManager(WallManager newWallManager, List<WallPiece> group)
		{
			// 1. Calculate bounding box
			int minX = int.MaxValue, minY = int.MaxValue;
			int maxX = int.MinValue, maxY = int.MinValue;

			foreach (var piece in group)
			{
				var pos = piece.gridPosition;
				minX = Mathf.Min(minX, pos.x);
				maxX = Mathf.Max(maxX, pos.x);
				minY = Mathf.Min(minY, pos.y);
				maxY = Mathf.Max(maxY, pos.y);
			}

			int width = maxX - minX + 1;
			int height = maxY - minY + 1;

			// 2. Set new dimensions
			newWallManager.numColumns = width;
			newWallManager.numRows = height;

			// 3. Initialize wall grid (flat array)
			newWallManager.numColumns = width;
			newWallManager.numRows = height;
			newWallManager.wallGrid = new List<WallPiece>(new WallPiece[width * height]);

			// 4. Assign pieces into grid and update gridPosition
			foreach (var piece in group)
			{
				Vector2Int globalPos = piece.gridPosition;
				Vector2Int localPos = new Vector2Int(globalPos.x - minX, globalPos.y - minY);

				// Compute flat index
				int idx = localPos.x + localPos.y * width;
				newWallManager.wallGrid[idx] = piece;

				piece.gridPosition = localPos; // update to new local grid
				piece.manager = newWallManager;
			}

			// 5. Copy other properties
			newWallManager.isGrouped = true;
		}

		private StructuralMember GetAdjacentMember(WallPiece piece)
		{
			Collider wallCol = piece.GetComponent<Collider>();
			if (wallCol == null)
				return null;

			// ─── Prepare the two OverlapBox passes ─────────────────────
			Bounds b = wallCol.bounds;
			float skin = 0.01f;                         // 1 cm “skin” for contact
			float longestSide = Mathf.Max(b.size.x, b.size.y, b.size.z);
			float pad = longestSide * 0.05f;           // extra reach for adjacency

			Vector3 halfExtTight = b.extents + Vector3.one * skin;
			Vector3 halfExtWide = halfExtTight + Vector3.one * pad;

			Collider[] contacts = Physics.OverlapBox(
				b.center, halfExtWide, Quaternion.identity,
				~0, QueryTriggerInteraction.Ignore);

			StructuralMember found = FirstStructuralMember(contacts, b.center);
			if (found != null)
				return found;

			return null;
		}

		private static StructuralMember FirstStructuralMember(Collider[] cols, Vector3 center)
		{
			StructuralMember closestMember = null;
			float minDistance = float.MaxValue;

			foreach (var c in cols)
			{
				StructuralMember m = null;
				Chunk proxy = c.GetComponent<Chunk>();
				if (proxy != null && proxy.structuralMember != null)
				{
					m = proxy.GetComponentInParent<StructuralMember>();
				}

				if (m != null)
				{
					float dist = Vector3.Distance(center, c.transform.position);
					if (dist < minDistance)
					{
						minDistance = dist;
						closestMember = m;
					}
				}
			}

			return closestMember;
		}

		public void UpdateEdgeStatusForGrid()
		{
			edgeMembers.Clear();
			foreach (Chunk chunk in _chunks)
			{
				if (chunk != null)
				{
					for (int i = 0; i < chunk.wallPieces.Count; i++)
					{
						GameObject go = chunk.wallPieces[i];
						if (go)
						{
							go.SetActive(true);
							var piece = go.GetComponent<WallPiece>();
							if (piece == null) continue;
							StructuralMember member = GetAdjacentMember(piece);

							piece.isEdge = member != null;
							piece.attachedMember = member;
							WallPiece proxyWallPiece = chunk.gameObject.GetComponent<WallPiece>();
							MemberPiece closestPiece = null;
							if (member != null)
							{
								float minDistance = float.MaxValue;

								for (int pieceIndex = 0; pieceIndex < member.memberPieces.Length; ++pieceIndex)
								{
									float dist = Vector3.Distance(piece.transform.position, member.memberPieces[pieceIndex].transform.position);
									if (dist < minDistance)
									{
										minDistance = dist;
										closestPiece = member.memberPieces[pieceIndex].GetComponent<MemberPiece>();
									}

								}
								piece.closestMemberPiece = closestPiece;
								edgeMembers.Add(member);
							}
							if (proxyWallPiece != null)
							{
								if (proxyWallPiece.attachedMember == null) proxyWallPiece.attachedMember = piece.attachedMember;
								if (proxyWallPiece.closestMemberPiece == null) proxyWallPiece.closestMemberPiece = piece.closestMemberPiece;
								if (proxyWallPiece.isEdge == false) proxyWallPiece.isEdge = piece.isEdge;
								if (proxyWallPiece.manager == null) proxyWallPiece.manager = this;
							}
							go.SetActive(false);
						}

					}
				}
			}
		}

#if UNITY_EDITOR
		private bool IsEdge(GameObject obj)
		{
			if (obj == null)
				return true;

			WallPiece wp = obj.GetComponent<WallPiece>();
			return wp != null && (wp.cornerDesignation != WallPiece.TriangularCornerDesignation.None || wp.isWindow);
		}

		private bool IsWindow(GameObject obj)
		{
			if (obj == null) return false;

			WallPiece wp = obj.GetComponent<WallPiece>();
			return wp != null && wp.isWindow;
		}

		public int ComputeFingerprint()
		{
			unchecked
			{
				int hash = 17;
				hash = hash * 31 + numRows;
				hash = hash * 31 + numColumns;

				for (int i = 0; i < wallGrid.Count; ++i)
				{
					WallPiece p = wallGrid[i];

					// treat “hole” and “proxy” the same
					if (p == null || p.isProxy) { hash = hash * 31; continue; }

					int code = p.isWindow ? 2 :
							   (p.cornerDesignation != WallPiece.TriangularCornerDesignation.None ? 3 : 1);

					hash = hash * 31 + code;
				}
				return hash;
			}
		}

		/*–– helper to build a voxel from an already‑cached mesh –––––––––––––*/
		GameObject CreateVoxelFromCachedMesh(int gx, int gy,
											 Vector3 worldPos, Vector3 cubeSize,
											 bool isWindow, bool isTriangle,
											 Mesh cachedMesh, Material defaultMat)
		{
			string n = isWindow ? $"WallWindowVoxel_{gx}_{gy}" :
					   isTriangle ? $"WallTriangleVoxel_{gx}_{gy}" :
									 $"WallVoxel_{gx}_{gy}";

			var go = new GameObject(n);
			go.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
			go.transform.localScale = new(1f / numColumns, 1f / numRows, 1f);

			var mf = go.AddComponent<MeshFilter>();
			mf.sharedMesh = cachedMesh;

			var mr = go.AddComponent<MeshRenderer>();
			mr.sharedMaterial = isWindow ? glassMaterial : defaultMat;

			return go;
		}

		/*───────────────────────────────────────────────────────────────────*\
         *  BUILD‑WALL  (editor‑only version)                                *
        \*───────────────────────────────────────────────────────────────────*/
		public void BuildWall(List<WallPiece> wallGrid, bool rebuilding, StructureBuildSettings _)
		{
			int fp = ComputeFingerprint();   // walls that “look” the same share fp
			_lastWallFingerprint = fp;

			/*–––– basic data from the source mesh –––––*/
			MeshFilter mfRoot = GetComponent<MeshFilter>();
			if (!mfRoot) { Debug.LogError("Wall needs a MeshFilter"); return; }

			Mesh srcMesh = mfRoot.sharedMesh;
			Material defaultMat = wallMaterial;
			if (glassMaterial == null) glassMaterial = defaultMat;

			GetComponent<MeshRenderer>().enabled = false;

			Bounds b = srcMesh.bounds;
			Vector3 scale = transform.localScale;
			worldWallSize = Vector3.Scale(b.size, scale);
			Vector3 cubeSize = new(worldWallSize.x / numColumns,
								   worldWallSize.y / numRows,
								   worldWallSize.z);

			/*–––– vertexOffsets code (identical to your original) –––––*/
			vertexOffsets = new Vector3[numColumns + 1, numRows + 1, 2];
			for (int x = 0; x <= numColumns; ++x)
				for (int y = 0; y <= numRows; ++y)
					for (int z = 0; z <= 1; ++z)
					{
						bool edge = (x == 0 || x == numColumns || y == 0 || y == numRows);
						bool adjNull = false, allWin = false;

						if (!edge && rebuilding)
						{
							WallPiece p00 = wallGrid[(x - 1) + (y - 1) * numColumns];
							WallPiece p10 = wallGrid[x + (y - 1) * numColumns];
							WallPiece p01 = wallGrid[(x - 1) + y * numColumns];
							WallPiece p11 = wallGrid[x + y * numColumns];

							allWin = IsWindow(p00?.gameObject) && IsWindow(p10?.gameObject) &&
									  IsWindow(p01?.gameObject) && IsWindow(p11?.gameObject);

							bool anyEdgeOrMissing =
									  IsEdge(p00?.gameObject) || IsEdge(p10?.gameObject) ||
									  IsEdge(p01?.gameObject) || IsEdge(p11?.gameObject);

							if (anyEdgeOrMissing && !allWin) adjNull = true;
						}

						if (edge || adjNull) vertexOffsets[x, y, z] = Vector3.zero;
						else if (allWin) vertexOffsets[x, y, z] = new(Random.Range(0, variationAmount),
																					   Random.Range(0, variationAmount), 0);
						else vertexOffsets[x, y, z] = new(Random.Range(-variationAmount * 2, variationAmount * 2),
																					   Random.Range(-variationAmount * 2, variationAmount * 2), 0);
					}

			/*–––– main grid loop –––––*/
			for (int gy = 0; gy < numRows; ++gy)
				for (int gx = 0; gx < numColumns; ++gx)
				{
					bool cellExists = wallGrid[gx + gy * numColumns] != null;

					int idx = gx + gy * numColumns;

					Vector3 localPos = new(
						(gx * cubeSize.x - worldWallSize.x * .5f + cubeSize.x * .5f) / scale.x,
						(gy * cubeSize.y - worldWallSize.y * .5f + cubeSize.y * .5f) / scale.y,
						(-worldWallSize.z * .5f + cubeSize.z * .5f) / scale.z);

					Vector3 worldPos = transform.TransformPoint(localPos);

					/*–‑ gather old info (window / triangle) –‑*/
					WallPiece old = cellExists ? wallGrid[idx] : null;
					bool isWindow = old?.isWindow ?? false;
					bool isTri = old && old.cornerDesignation != WallPiece.TriangularCornerDesignation.None;
					if (old) Undo.DestroyObjectImmediate(old.gameObject);

					/*–‑ see if mesh is already cached –‑*/
					Mesh cachedMesh = null;
					if (MeshCacheUtility.Enabled)
					{
						cachedMesh = MeshCacheUtility.TryLoad(fp, idx);
					}

					GameObject voxelGO;
					WallPiece voxelComp;

					if (cachedMesh)   /*───────── FAST PATH ─────────*/
					{
						voxelGO = CreateVoxelFromCachedMesh(gx, gy, worldPos, cubeSize,
															isWindow, isTri,
															cachedMesh, defaultMat);
						Undo.RegisterCreatedObjectUndo(voxelGO, "Create Cached Voxel");
						voxelComp = Undo.AddComponent<WallPiece>(voxelGO);
						voxelComp.isWindow = isWindow;
						voxelComp.cornerDesignation = isTri ? old?.cornerDesignation ?? 0 : 0;
					}
					else              /*───────── SLOW / PROC PATH ──*/
					{
						if (isTri)
							voxelGO = VoxelBuildingUtility.CreateTriangle(
										 cubeSize, worldPos, gx, gy, 0,
										 defaultMat, cubeSize, old?.cornerDesignation ?? 0,
										 worldWallSize, numRows, numColumns,
										 "WallTriangleVoxel");
						else if (isWindow)
							voxelGO = VoxelBuildingUtility.CreateWindow(
										 cubeSize, worldPos, gx, gy, glassMaterial,
										 cubeSize, vertexOffsets, worldWallSize,
										 numRows, numColumns, "WallWindowVoxel");
						else
							voxelGO = VoxelBuildingUtility.CreateIrregularCube(
										 cubeSize, worldPos, gx, gy, 0,
										 defaultMat, cubeSize, vertexOffsets,
										 worldWallSize, numRows, numColumns,
										 1, "WallVoxel");

						Undo.RegisterCreatedObjectUndo(voxelGO, "Create Wall Voxel");
						voxelComp = Undo.AddComponent<WallPiece>(voxelGO);
						voxelComp.isWindow = isWindow;
						voxelComp.cornerDesignation = isTri ? old?.cornerDesignation ?? 0 : 0;

						MeshFilter mf = voxelGO.GetComponent<MeshFilter>();
						if (mf && mf.sharedMesh)
							mf.sharedMesh = MeshCacheUtility.Persist(mf.sharedMesh, fp, idx);
					}

					/*‑‑ common finalisation –‑*/
					SetupWallComponent(voxelComp, localPos, gx, gy, isWindow);
				}

			if (numColumns > 2 && numRows > 2)
				CombineWall();
		}

		void OnEnable()
		{
			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
		}

		void OnDisable()
		{
			Undo.undoRedoPerformed -= OnUndoRedo;
		}

		private void OnUndoRedo()
		{
			// pick your default here:
			RelinkWallGridReferences();
		}

		private void SetupWallComponent(WallPiece wallComponent, Vector3 localCubePosition, int x, int y, bool isWindow)
		{
			// Record wall manager for wallGrid change
			Undo.RecordObject(this, "Assign Wall Piece in Grid");
			Mesh mesh = wallComponent.GetComponent<MeshFilter>()?.sharedMesh;

			if (isWindow || mesh.bounds.size.z < 0.01f)
			{
				BoxCollider boxCollider = Undo.AddComponent<BoxCollider>(wallComponent.gameObject);
			}
			else
			{
				// Add MeshCollider with Undo so it can be removed on undo
				MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(wallComponent.gameObject);
				meshCollider.convex = true;
			}


			// Cache original transform values
			Vector3 localScale = wallComponent.transform.localScale;
			Quaternion localRotation = wallComponent.transform.localRotation;

			// Record transform changes for undo
			Undo.RecordObject(wallComponent.transform, "Move Wall Component");

			wallComponent.transform.SetParent(this.transform, true);
			wallComponent.transform.localPosition = localCubePosition;
			wallComponent.transform.localScale = localScale;
			wallComponent.transform.localRotation = localRotation;

			// Record WallPiece for undo before modifying its fields
			Undo.RecordObject(wallComponent, "Update Wall Piece Data");
			wallComponent.manager = this;
			wallComponent.gridPosition = new Vector2Int(x, y);

			// Remove old wall piece at this grid position (with Undo)
			if (wallGrid[x + y * numColumns] != null && wallGrid[x + y * numColumns] != wallComponent)
			{
				Undo.DestroyObjectImmediate(wallGrid[x + y * numColumns].gameObject);
			}

			wallGrid[x + y * numColumns] = wallComponent;
		}

		public void CombineWall()
		{
			_chunks.RemoveAll(chunk => chunk == null);

			for (int gx = 0; gx < numColumns; gx += numColumns / 2)
			{
				for (int gy = 0; gy < numRows; gy += numRows / 2)
				{
					BuildChunk(gx, gy);
				}
			}
		}
		// Called after undo/redo to re-link grid references
		public void RelinkWallGridReferences()
		{
			// 1. Clear the grid
			for (int i = 0; i < wallGrid.Count; i++)
				wallGrid[i] = null;

			// 2. Place chunk proxies first.
			foreach (var chunk in _chunks)
			{
				if (chunk == null) continue;
				WallPiece proxy = chunk.gameObject.GetComponent<WallPiece>();
				if (proxy == null) continue;

				foreach (var original in chunk.wallPieces)
				{
					if (original == null) continue;
					var wp = original.GetComponent<WallPiece>();
					if (wp == null) continue;
					var pos = wp.gridPosition;
					int idx = pos.x + pos.y * numColumns;
					if (pos.x >= 0 && pos.x < numColumns && pos.y >= 0 && pos.y < numRows)
						wallGrid[idx] = proxy;
				}
			}

			// 3. Place loose/real wallPieces in slots not already filled.
			foreach (var wp in GetComponentsInChildren<WallPiece>(true))
			{
				if (wp.isProxy) continue; // skip chunk proxies

				var pos = wp.gridPosition;
				int idx = pos.x + pos.y * numColumns;
				if (pos.x >= 0 && pos.x < numColumns && pos.y >= 0 && pos.y < numRows)
				{
					// Only set if nothing (i.e., not a proxy) is already in this grid slot.
					if (wallGrid[idx] == null)
						wallGrid[idx] = wp;
				}
			}
		}

		void BuildChunk(int startX, int startY)
		{
			var windowSet = new HashSet<WallPiece>();
			var nonWindowSet = new HashSet<WallPiece>();

			// Separate wall pieces into two categories
			for (int x = startX; x < Mathf.Min(startX + numColumns / 2, numColumns); x++)
			{
				for (int y = startY; y < Mathf.Min(startY + numRows / 2, numRows); y++)
				{
					var piece = wallGrid[x + y * numColumns];
					if (piece == null) continue;

					// Undo: Record state before hiding
					Undo.RecordObject(piece.gameObject, "Hide WallPiece in Hierarchy");
					piece.gameObject.hideFlags = HideFlags.HideInHierarchy;
					EditorUtility.SetDirty(piece.gameObject);

					if (piece.isWindow)
					{
						windowSet.Add(piece);
					}
					else
					{
						nonWindowSet.Add(piece);
					}
				}
			}

			// Process window groups
			foreach (var group in GetConnectedGroups(windowSet))
				CreateChunkFromGroup(group, $"window_chunk_{startX}_{startY}", true);

			// Process non-window groups
			foreach (var group in GetConnectedGroups(nonWindowSet))
				CreateChunkFromGroup(group, $"chunk_{startX}_{startY}", false);
		}

		void CreateChunkFromGroup(List<WallPiece> group, string namePrefix, bool isWindow)
		{
			if (group.Count == 0) return;

			int chunkIdx = _chunks.Count;
			int fp = _lastWallFingerprint;
			List<GameObject> pieces = group.Select(p => p.gameObject).ToList();

			GameObject combinedGO;

			// FAST PATH — load pre-cached chunk mesh
			Mesh cachedMesh = MeshCacheUtility.TryLoadChunk(fp, chunkIdx);
			if (cachedMesh != null)
			{
				combinedGO = new GameObject($"{namePrefix}_{chunkIdx}");
				Undo.RegisterCreatedObjectUndo(combinedGO, "Create Cached Wall Chunk");

				combinedGO.transform.SetParent(this.transform, false);
				combinedGO.transform.localPosition = Vector3.zero;
				combinedGO.transform.localRotation = Quaternion.identity;

				var mf = Undo.AddComponent<MeshFilter>(combinedGO);
				mf.sharedMesh = cachedMesh;

				var mr = Undo.AddComponent<MeshRenderer>(combinedGO);
				mr.sharedMaterial = isWindow ? glassMaterial : wallMaterial;

				foreach (var piece in pieces)
					MeshCombinerUtility.PreparePieceForCombination(piece);
			}
			else
			{
				// SLOW PATH — combine, then cache
				combinedGO = MeshCombinerUtility.CombineMeshes(
					this.gameObject, pieces.ToArray(), $"{namePrefix}_{chunkIdx}");

				Undo.RegisterCreatedObjectUndo(combinedGO, "Create Combined Wall Chunk");

				var mfNew = combinedGO.GetComponent<MeshFilter>();
				if (mfNew && mfNew.sharedMesh)
				{
					mfNew.sharedMesh = MeshCacheUtility.PersistChunk(mfNew.sharedMesh, fp, chunkIdx);
				}
			}


			// ─────────────────────────────────────────────────────────────
			// COLLIDERS
			if (isWindow)
			{
				BoxCollider windowCollider = Undo.AddComponent<BoxCollider>(combinedGO);
				Vector3 original = windowCollider.size;
				windowCollider.size = new Vector3(original.x, original.y, worldWallSize.z * 2);
			}
			else
			{
				Undo.AddComponent<MeshCollider>(combinedGO);
			}

			BoxCollider trigger = Undo.AddComponent<BoxCollider>(combinedGO);
			Vector3 boxSize = trigger.size;
			trigger.size = new Vector3(boxSize.x * 0.7f, boxSize.y * 0.7f, boxSize.z);
			trigger.isTrigger = true;

			// ─────────────────────────────────────────────────────────────
			// CHUNK COMPONENT
			var chunk = Undo.AddComponent<Chunk>(combinedGO);
			chunk.wallPieces = pieces;
			chunk.wallManager = this;
			chunk.structuralGroup = structuralGroup;
			_chunks.Add(chunk);

			// PROXY WALLPIECE (marker for chunk)
			WallPiece proxy = Undo.AddComponent<WallPiece>(combinedGO);
			proxy.isProxy = true;
			proxy.chunk = chunk;
			proxy.isWindow = isWindow;

			// ─────────────────────────────────────────────────────────────
			// Assign proxy to all grid cells originally covered
			foreach (var original in group)
			{
				Vector2Int pos = original.gridPosition;
				if (pos.x >= 0 && pos.x < numColumns && pos.y >= 0 && pos.y < numRows)
				{
					int idx = pos.x + pos.y * numColumns;
					Undo.RecordObject(this, "Assign Wall Proxy Piece");
					wallGrid[idx] = proxy;
				}
			}
		}

#endif
	}
}