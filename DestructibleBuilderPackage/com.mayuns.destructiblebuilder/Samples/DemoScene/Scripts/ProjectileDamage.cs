using UnityEngine;

namespace Mayuns.DSB
{
    public class  ProjectileDamage : MonoBehaviour
{
    private float damage;
    private float radius;

    public void Initialize(float damage, float radius)
    {
        this.damage = damage;
        this.radius = radius;
    }

    private void OnCollisionEnter(Collision collision)
    {
        ApplyExplosionDamage(transform.position, radius, damage);
        Destroy(gameObject); // Destroy projectile on impact
    }

    private void ApplyExplosionDamage(Vector3 center, float radius, float maxDamage)
    {
        Collider[] hitColliders = Physics.OverlapSphere(center, radius);
        foreach (Collider collider in hitColliders)
        {
            float distance = Vector3.Distance(center, collider.transform.position);
            float damageMultiplier = Mathf.Clamp01(1f - (distance / radius));
            float damageToApply = maxDamage * damageMultiplier;

            IDamageable damageable = collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damageToApply);
            }
        }
    }
}
