using System.Collections.Generic;
using UnityEngine;

namespace Mayuns.DSB
{
	public class Chunk : MonoBehaviour, IDamageable
	{
		[SerializeField] public WallManager wallManager;
		[SerializeField] public StructuralMember structuralMember;
		[SerializeField] public StructuralGroupManager structuralGroup;
		[SerializeField] public float accumulatedDamage = 0;
		[SerializeField] public List<GameObject> wallPieces;
		[SerializeField] public bool IsBroken;
		void Start()
		{
			if (structuralGroup == null)
			{
				structuralGroup = GetComponentInParent<StructuralGroupManager>();
			}
			if (structuralGroup == null)
			{
				Debug.LogError("Chunk needs StructuralGroupManager.", this);
				Destroy(gameObject);
			}
		}

		public void TakeDamage(float damage)
		{
			accumulatedDamage += damage;
			if (wallManager != null && damage > 0)
			{
				if (structuralGroup == null)
				{
					return;
				}
				if (structuralGroup.gibManager == null)
				{
					return;
				}
				if (structuralGroup.gibManager.CanUncombineNow())
				{
					wallManager.UncombineChunk(this, accumulatedDamage);
				}
				else if (accumulatedDamage >= wallManager.wallPieceHealth)
				{
					if (this == null || IsBroken) return;

					wallManager.DetachChunk(this);
					return;

				}
			}
			else if (structuralMember != null && damage > 0)
			{
				if (accumulatedDamage > structuralMember.memberPieceHealth)
				{
					structuralMember.DestroyRandomMemberPiece();
				}
				else
				{
					structuralMember.UncombineMember();
				}
			}
		}
	}
}