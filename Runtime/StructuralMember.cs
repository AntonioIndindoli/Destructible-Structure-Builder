using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace Mayuns.DSB
{
	public class StructuralMember : MonoBehaviour
	{
		[HideInInspector] public GameObject[] memberPieces;
		[HideInInspector] public bool isDestroyed = false;
		[HideInInspector] public bool isGrouped = false;
		[HideInInspector] public bool isGrounded = false;
		[HideInInspector] public int initialDistanceToGround = int.MaxValue;
		[HideInInspector] public int currentMinDistanceToGround = int.MaxValue;
		[HideInInspector] public List<StructuralMember> cachedAdjacentMembers = new List<StructuralMember>();
		[HideInInspector] public List<WallManager> managerList = new List<WallManager>();
		[HideInInspector] public bool isNewSplitMember = false;
		[HideInInspector] public StructuralGroupManager structuralGroup;
		[HideInInspector] public StructuralConnection startConnection;
		[HideInInspector] public StructuralConnection endConnection;
		[HideInInspector] public bool isSplit = false;
		[ReadOnly] public float thickness;
		[ReadOnly] public float length;
		[HideInInspector] public GameObject combinedObject;
		[HideInInspector] public bool wasDamaged = false;
		private int memberDivisionsCount = 5;
		private float variationAmount = 0.25f;
		private Vector3[,,] vertexOffsets;
		private Vector3 worldMemberSize;
		public float mass = 10f;
		public float supportCapacity = 100f;
		public float accumulatedLoad = 0f;
		public float voxelHealth = 100f;
		[HideInInspector] public float textureScaleX = 1f;
		[HideInInspector] public float textureScaleY = 1f;

		int ComputeFingerprint()
		{
			unchecked
			{
				int hash = 17;
				hash = hash * 31 + memberDivisionsCount;
				hash = hash * 31 + textureScaleX.GetHashCode();
				hash = hash * 31 + textureScaleY.GetHashCode();
				return hash;
			}
		}

#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			if (cachedAdjacentMembers == null)
				return;

			Gizmos.color = Color.yellow;

			foreach (var adjacent in cachedAdjacentMembers)
			{
				if (adjacent == null) continue;

				// Draw a line from this member to the adjacent member
				Gizmos.DrawLine(transform.position, adjacent.transform.position);

				// Draw a sphere at the adjacent member's position
				Gizmos.DrawSphere(adjacent.transform.position, 0.1f);
			}

			// Optionally, draw a small sphere at the center of this member
			Gizmos.color = Color.cyan;
			Gizmos.DrawSphere(transform.position, 0.1f);
		}
#endif

		void Start()
		{
			if (combinedObject == null && !isSplit)
			{
				CombineMember();
				if (structuralGroup == null)
				{
					structuralGroup = GetComponentInParent<StructuralGroupManager>();
				}
				if (structuralGroup == null)
				{
					Destroy(gameObject);
				}
			}

		}
		public void AddWallManager(WallManager manager)
		{
			managerList.Add(manager);
		}

		public void PieceDestroyed()
		{
			if (!isSplit)
			{
				isSplit = true;
				SplitMember();
				foreach (WallManager wall in managerList)
				{
					if (wall != null)
						wall.ValidateWallIntegrity();
				}
			}
			else
			{
				DetachUnconnectedPieces();
				foreach (WallManager wall in managerList)
				{
					if (wall != null)
						wall.ValidateWallIntegrity();
				}
			}

			if (structuralGroup != null)
				structuralGroup.ValidateGroupIntegrity();
			SelfDestructCheck();
		}

		public void SelfDestructCheck()
		{
			bool allNull = memberPieces.All(obj => obj == null);
			if (memberPieces == null || allNull)
			{
				isDestroyed = true;
				Destroy(gameObject);
			}
		}

		/// <summary>
		/// Shrinks the Z–size of the collider on the piece to the
		/// left or right of <paramref name="destroyedIndex"/> if that piece
		/// is still active. 
		/// </summary>
		public void AdjustNeighboursAfterDestruction(int destroyedIndex)
		{
			// Early‑out: works only after we have split at least once.
			if (!isSplit || memberPieces == null) return;

			void ShrinkIfValid(int neighbourIndex)
			{
				// ensure index is in bounds
				if (neighbourIndex < 0 || neighbourIndex >= memberPieces.Length) return;

				GameObject neighGO = memberPieces[neighbourIndex];
				if (neighGO == null) return;

				// shrink only once
				BoxCollider col = neighGO.GetComponent<BoxCollider>();
				if (!col || col.size.z < 0.75f) return;  // already shrunk? skip

				Vector3 size = col.size; size.z *= 0.3f;
				Vector3 centre = col.center; centre.z = 0f;

				col.size = size;
				col.center = centre;
			}

			// try both sides
			ShrinkIfValid(destroyedIndex - 1);
			ShrinkIfValid(destroyedIndex + 1);
		}

		public void DestroyRandomMemberPiece()
		{
			if (isDestroyed || memberPieces == null || memberPieces.Length == 0)
				return;

			UncombineMember();

			StartCoroutine(DelayedDestroy());
		}

		private IEnumerator DelayedDestroy()
		{
			// Wait one frame to allow Start() methods to run
			yield return null;

			// Get all non-null and not-yet-destroyed pieces
			List<GameObject> validPieces = memberPieces
				.Where(p => p != null && !p.GetComponent<MemberPiece>().isDestroyed)
				.ToList();

			if (validPieces.Count == 0)
				yield break;

			// Pick a random piece to destroy
			GameObject randomPiece = validPieces[Random.Range(0, validPieces.Count)];
			MemberPiece memberPiece = randomPiece.GetComponent<MemberPiece>();

			if (memberPiece != null && !memberPiece.isDestroyed)
			{
				memberPiece.TakeDamage(voxelHealth * 999f);
			}
		}

		void CombineMember()
		{
			combinedObject = MeshCombinerUtility.CombineMeshes(this.gameObject, memberPieces, "CombinedMember");

			var proxy = combinedObject.AddComponent<Chunk>();
			proxy.structuralGroup = structuralGroup;
			proxy.structuralMember = this;
			proxy.gameObject.AddComponent<BoxCollider>();
		}

		public void UncombineMember()
		{
			if (wasDamaged || !combinedObject) return;
			wasDamaged = true;

			if (combinedObject)
				Destroy(combinedObject);

			foreach (var piece in memberPieces)
			{
				if (piece != null)
				{
					piece.SetActive(true);
				}
			}
		}

		public void DetachUnconnectedPieces()
		{
			int splitIndex = -1;

			// Find the index of the destroyed piece.
			if (isNewSplitMember)
			{
				// Reverse iteration: take the first destroyed piece from the end.
				for (int i = memberPieces.Length - 1; i >= 0; i--)
				{
					if (memberPieces[i].GetComponent<MemberPiece>().isDestroyed)
					{
						splitIndex = i;
						break;
					}
				}
			}
			else
			{
				// Normal iteration: take the first destroyed piece from the start.
				for (int i = 0; i < memberPieces.Length; i++)
				{
					if (memberPieces[i] != null && memberPieces[i].GetComponent<MemberPiece>().isDestroyed)
					{
						splitIndex = i;
						break;
					}
				}
			}

			// If no destroyed piece is found, there's nothing to detach.
			if (splitIndex == -1)
			{
				return;
			}

			// Create a new GameObject for the detached member.
			GameObject newMemberObject = new GameObject("DetachedStructuralMember");
			StructuralMember newMember = newMemberObject.AddComponent<StructuralMember>();

			// Prepare a list to store transferred pieces.
			List<GameObject> transferredPieces = new List<GameObject>();

			if (isNewSplitMember)
			{
				// Detach pieces BEFORE the destroyed piece.
				// (e.g. from index 0 up to splitIndex - 1)
				for (int i = 0; i < splitIndex; i++)
				{
					if (memberPieces[i] != null)
					{
						transferredPieces.Add(memberPieces[i]);
						// Update the piece's reference to its new StructuralMember.
						MemberPiece wp = memberPieces[i].GetComponent<MemberPiece>();
						if (wp != null)
						{
							wp.member = newMember;
						}
						// Reparent the piece to the new member.
						memberPieces[i].transform.SetParent(newMember.transform);
						// Remove the piece from the current member.
						memberPieces[i] = null;
					}
				}
			}
			else
			{
				// Detach pieces AFTER the destroyed piece.
				for (int i = splitIndex + 1; i < memberPieces.Length; i++)
				{
					if (memberPieces[i] != null)
					{
						transferredPieces.Add(memberPieces[i]);
						MemberPiece wp = memberPieces[i].GetComponent<MemberPiece>();
						if (wp != null)
						{
							wp.member = newMember;
						}
						memberPieces[i].transform.SetParent(newMember.transform);
						memberPieces[i] = null;
					}
				}
			}

			// Assign the transferred pieces to the new StructuralMember.
			newMember.memberPieces = transferredPieces.ToArray();

			// Detach the new member physically by adding a Rigidbody.
			Rigidbody rb = newMemberObject.AddComponent<Rigidbody>();
			rb.isKinematic = false;

			// Mark both members as having been split (and no longer grounded).
			newMember.isSplit = true;
			isGrounded = false;

			bool allNull = newMember.memberPieces.All(obj => obj == null);
			if (newMember.memberPieces == null || allNull)
			{
				Destroy(newMemberObject);
				return;
			}
			if (structuralGroup != null && structuralGroup.gibManager != null)
			{
				structuralGroup.gibManager.RegisterTimedGib(newMemberObject, structuralGroup.gibManager.mediumGibLifetime);
			}
		}

		public void SplitMember()
		{
			// Find the index of the first null piece in the memberPieces array.
			int splitIndex = -1;
			int splitIndexReference = -1;
			for (int i = 0; i < memberPieces.Length; i++)
			{
				if (memberPieces[i].GetComponent<MemberPiece>().isDestroyed)
				{
					splitIndexReference = i;
					splitIndex = i;
					break;
				}
			}

			if (splitIndex != -1 && splitIndex < memberPieces.Length - 1)
			{
				// Create a new GameObject for the new StructuralMember.
				GameObject newMemberObject = new GameObject("SplitStructuralMember");
				StructuralMember newMember = newMemberObject.AddComponent<StructuralMember>();
				newMember.isNewSplitMember = true;

				newMemberObject.transform.SetParent(transform.parent, false);
				newMemberObject.transform.position = transform.position;
				structuralGroup.structuralMembersHash.Add(newMember);
				newMember.structuralGroup = structuralGroup;
				newMember.managerList = managerList;
				newMember.initialDistanceToGround = initialDistanceToGround;
				newMember.currentMinDistanceToGround = currentMinDistanceToGround;

				if (splitIndex == 0 && startConnection != null)
				{
					startConnection.ReplaceMember(this, null);
					startConnection.SelfDestructCheck();
					List<StructuralMember> startConnectionMembers = startConnection.GetMembers();
					foreach (StructuralMember member in startConnectionMembers)
					{
						if (member != null)
						{
							cachedAdjacentMembers.Remove(member);
							member.cachedAdjacentMembers.Remove(this);
						}
					}
				}

				// Transfer all pieces after the split index.
				List<GameObject> transferredPieces = new List<GameObject>();
				for (int i = splitIndex + 1; i < memberPieces.Length; i++)
				{
					if (memberPieces[i] != null)
					{
						transferredPieces.Add(memberPieces[i]);
						// Update the piece's rseference to its StructuralMember.
						MemberPiece wp = memberPieces[i].GetComponent<MemberPiece>();
						if (wp != null)
						{
							wp.member = newMember;
						}
						// Reparent the piece under the new member.
						memberPieces[i].transform.SetParent(newMember.transform);
						// Remove the piece from the current member.
						memberPieces[i] = null;
					}
				}
				// Assign the transferred pieces to the new StructuralMember.
				newMember.memberPieces = transferredPieces.ToArray();

				if (endConnection != null)
				{
					List<StructuralMember> endConnectionMembers = endConnection.GetMembers();
					foreach (StructuralMember member in endConnectionMembers)
					{
						if (member != null)
						{
							cachedAdjacentMembers.Remove(member);
							member.cachedAdjacentMembers.Remove(this);
							if (!newMember.cachedAdjacentMembers.Contains(member) && member != this && member.structuralGroup == newMember.structuralGroup)
							{
								newMember.cachedAdjacentMembers.Add(member);
							}

							if (!member.cachedAdjacentMembers.Contains(newMember) && member != this && member.structuralGroup == newMember.structuralGroup)
							{
								member.cachedAdjacentMembers.Add(newMember);
							}

						}
					}
					endConnection.ReplaceMember(this, newMember);
					newMember.endConnection = endConnection;
				}


				newMember.isSplit = true;
			}
			else if (splitIndexReference == memberPieces.Length - 1 && endConnection != null)
			{
				endConnection.ReplaceMember(this, null);
				endConnection.SelfDestructCheck();
				List<StructuralMember> endConnectionMembers = endConnection.GetMembers();
				foreach (StructuralMember member in endConnectionMembers)
				{
					if (member != null)
					{
						cachedAdjacentMembers.Remove(member);
						member.cachedAdjacentMembers.Remove(this);
					}
				}
			}
			if (endConnection != null)
				endConnection = null;
			isGrounded = false;
		}

#if UNITY_EDITOR
                public void BuildMember()
                {
			// ────────────────────────────────────────────────────────────────────────────
			// 1) Wrap everything in ONE undo group
			// ────────────────────────────────────────────────────────────────────────────
			int undoGroup = -1;
			if (!Application.isPlaying)
			{
				Undo.IncrementCurrentGroup();
				Undo.SetCurrentGroupName("Rebuild Structural Member Voxels");
				undoGroup = Undo.GetCurrentGroup();
				Undo.RecordObject(this, "Rebuild Structural Member");   // length etc. may change
			}


			//──────────────────────────────────────────────────────────────────────────────
			// 2) Destroy existing voxels via the Undo system
			//──────────────────────────────────────────────────────────────────────────────
                        if (memberPieces != null)
                        {
                                for (int i = 0; i < memberPieces.Length; i++)
                                {
                                        if (!Application.isPlaying && memberPieces[i])
                                                Undo.DestroyObjectImmediate(memberPieces[i]);
                                        else

                                                if (memberPieces[i])
                                                DestroyImmediate(memberPieces[i]);

                                        memberPieces[i] = null;
                                }
                        }

                        RemoveOrphanMemberPieces();

			//──────────────────────────────────────────────────────────────────────────────
			// 3) Regular mesh/size maths (unchanged) …                                     
			//──────────────────────────────────────────────────────────────────────────────
			MeshFilter meshFilter = GetComponent<MeshFilter>();
			if (!meshFilter)
			{
				return;
			}

			memberPieces = new GameObject[memberDivisionsCount];

			Mesh originalMesh = meshFilter.sharedMesh;
			Material originalMaterial = GetComponent<MeshRenderer>().sharedMaterial;

			Bounds bounds = originalMesh.bounds;
			Vector3 wallSize = bounds.size;
			Vector3 wallScale = transform.localScale;
			worldMemberSize = Vector3.Scale(wallSize, wallScale);

			Vector3 cubeSize = new Vector3(
				worldMemberSize.x / 1,
				worldMemberSize.y / 1,
				worldMemberSize.z / memberDivisionsCount);

			vertexOffsets = new Vector3[2, 2, memberDivisionsCount + 1];
			for (int x = 0; x <= 1; x++)
				for (int y = 0; y <= 1; y++)
					for (int z = 0; z <= memberDivisionsCount; z++)
					{
						Vector3 offset = Vector3.zero;
						bool isEnd = (z == 0 || z == memberDivisionsCount);
						if (!isEnd)
							offset.z = Random.Range(-variationAmount, variationAmount);

						vertexOffsets[x, y, z] = offset;
					}

			//──────────────────────────────────────────────────────────────────────────────
			// 4) Create voxels – every new object/component registered with Undo
			//──────────────────────────────────────────────────────────────────────────────
			int fp = ComputeFingerprint();
			for (int z = 0; z < memberDivisionsCount; z++)
			{
				Vector3 localCubePosition = new(
					0,
					0,
				   (z * cubeSize.z - worldMemberSize.z * 0.5f + cubeSize.z * 0.5f) / wallScale.z);

				Vector3 worldCubePosition = transform.TransformPoint(localCubePosition);

				int idx = z;   // 1‑D index for member cells

				/*──── FAST PATH : load cached mesh ────*/
				Mesh cached = MeshCacheUtility.TryLoadPiece(fp, idx);
				GameObject cube;

				if (cached)
				{
					cube = new GameObject($"StructuralMemberVoxel_{z}");
					cube.transform.SetPositionAndRotation(worldCubePosition, Quaternion.identity);
					cube.transform.localScale = new(1f, 1f, 1f / memberDivisionsCount);

					var mf = cube.AddComponent<MeshFilter>();
					mf.sharedMesh = cached;

					var mr2 = cube.AddComponent<MeshRenderer>();
					mr2.sharedMaterial = originalMaterial;
				}
				else
				{
					/*──── SLOW PATH : procedural build ────*/
					cube = VoxelBuildingUtility.CreateIrregularCube(
							 cubeSize, worldCubePosition, 0, 0, z,
							 originalMaterial, cubeSize, vertexOffsets,
							 worldMemberSize, 1, 1,
							 new Vector2(textureScaleX, textureScaleY),
							 memberDivisionsCount,
							 "StructuralMemberVoxel");

					MeshFilter mf = cube.GetComponent<MeshFilter>();
					if (mf && mf.sharedMesh)
						mf.sharedMesh = MeshCacheUtility.PersistPiece(mf.sharedMesh, fp, idx);
				}

				if (!Application.isPlaying)
				{
					// tell Undo about the fresh GameObject
					Undo.RegisterCreatedObjectUndo(cube, "Create StructuralMemberVoxel");
					Undo.RecordObject(cube.transform, "Position StructuralMemberVoxel");
				}


				// add collider via Undo
				BoxCollider boxCol = !Application.isPlaying
					? Undo.AddComponent<BoxCollider>(cube)
					: cube.AddComponent<BoxCollider>();


				// Ignore collision with start/end connections if present
				if (startConnection && startConnection.TryGetComponent<Collider>(out var startCol))
				{
					Physics.IgnoreCollision(boxCol, startCol);
				}
				if (endConnection && endConnection.TryGetComponent<Collider>(out var endCol))
				{
					Physics.IgnoreCollision(boxCol, endCol);
				}

				Vector3 localScale = cube.transform.localScale;
				Quaternion localRotation = cube.transform.localRotation;
				cube.transform.SetParent(this.transform, true);
				cube.transform.localPosition = localCubePosition;
				cube.transform.localScale = localScale;
				cube.transform.localRotation = localRotation;


				// MemberPiece component
				MemberPiece piece = !Application.isPlaying
					? Undo.AddComponent<MemberPiece>(cube)
					: cube.AddComponent<MemberPiece>();


				piece.member = this;

				memberPieces[z] = cube;
			}

			//──────────────────────────────────────────────────────────────────────────────
			// 5) Hide the placeholder mesh renderer; record the change in editor
			//──────────────────────────────────────────────────────────────────────────────
			MeshRenderer mr = GetComponent<MeshRenderer>();
			if (mr)
			{
				if (!Application.isPlaying) Undo.RecordObject(mr, "Hide member shell");

				mr.enabled = false;
			}

			MeshCacheUtility.CleanUnusedCache(); // Auto clean cache to prevent leaks

			//──────────────────────────────────────────────────────────────────────────────
			// 6) Dirty the object & scene, then collapse/group the whole operation
			//──────────────────────────────────────────────────────────────────────────────
                        if (!Application.isPlaying)
                        {
                                EditorUtility.SetDirty(this);
                                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                                Undo.CollapseUndoOperations(undoGroup);
                        }

                }

                void RemoveOrphanMemberPieces()
                {
                        var pieces = GetComponentsInChildren<MemberPiece>(true);
                        var referenced = new HashSet<GameObject>();
                        if (memberPieces != null)
                        {
                                foreach (var go in memberPieces)
                                        if (go != null)
                                                referenced.Add(go);
                        }

                        foreach (var piece in pieces)
                        {
                                bool keep = referenced.Contains(piece.gameObject) || piece.GetComponentInParent<Chunk>() != null;
                                if (!keep)
                                {
                                        Undo.DestroyObjectImmediate(piece.gameObject);
                                }
                        }
                }

#if UNITY_EDITOR
                public void EnsureMeshesPersisted()
                {
                        int fp = ComputeFingerprint();
                        if (memberPieces != null)
                        {
                                for (int i = 0; i < memberPieces.Length; i++)
                                {
                                        GameObject go = memberPieces[i];
                                        if (!go) continue;
                                        var mf = go.GetComponent<MeshFilter>();
                                        if (mf && mf.sharedMesh && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mf.sharedMesh)))
                                        {
                                                mf.sharedMesh = MeshCacheUtility.PersistPiece(mf.sharedMesh, fp, i);
                                        }
                                }
                        }
                }
#endif
#endif
	}

}