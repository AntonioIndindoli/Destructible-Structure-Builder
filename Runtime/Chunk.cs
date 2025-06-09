using System.Collections.Generic;
using UnityEngine;

namespace Mayuns.DSB
{
	public class Chunk : MonoBehaviour, IDamageable
	{
		[HideInInspector] public WallManager wallManager;
		[HideInInspector] public StructuralMember structuralMember;
		[HideInInspector] public StructuralGroupManager structuralGroup;
		[HideInInspector] public List<GameObject> wallPieces;
		[HideInInspector] public bool IsBroken;
		[SerializeField] public float accumulatedDamage = 0;

		void Start()
		{
			if (structuralGroup == null)
			{
				structuralGroup = GetComponentInParent<StructuralGroupManager>();
			}
			if (structuralGroup == null)
			{
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