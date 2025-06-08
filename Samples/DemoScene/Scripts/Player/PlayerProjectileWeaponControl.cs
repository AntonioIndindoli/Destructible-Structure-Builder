using UnityEngine;

public class PlayerProjectileWeaponControl : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileForce = 1000f;
    [SerializeField] private float damage = 500f;
    [SerializeField] private float explosionRadius = 3f;

    void Update()
    {
        // Check for Fire1 input (default: left mouse button)
        if (Input.GetButtonDown("Fire1"))
        {
            FireProjectile();
        }
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || muzzleTransform == null)
        {
            return;
        }

        GameObject projectileInstance = Instantiate(projectilePrefab, muzzleTransform.position, muzzleTransform.rotation);
        Rigidbody rb = projectileInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(muzzleTransform.forward * projectileForce, ForceMode.Impulse);
        }

        ProjectileDamage projectileDamage = projectileInstance.GetComponent<ProjectileDamage>();
        if (projectileDamage != null)
        {
            projectileDamage.Initialize(damage, explosionRadius);
        }
    }
}
