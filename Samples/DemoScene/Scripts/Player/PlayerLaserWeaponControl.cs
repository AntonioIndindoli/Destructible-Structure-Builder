using Mayuns.DSB;
using UnityEngine;

public class PlayerLaserWeaponControl : MonoBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private Transform muzzleTransform;       // Where the raycast originates
    [SerializeField] private Transform laserVisualStartPoint; // Where the line renderer starts
    [SerializeField] private LineRenderer laserRenderer;

    public float laserDamagePerSecond = 10000f;
    public float explosionRadius = 3f;
    private float laserRange = 1000f;
    private bool isFiringLaser = false;

    private Vector3 currentLaserStart;
    private Vector3 currentLaserEnd;
    private float laserStartSmoothSpeed = 10f;  // Fast to stay connected to weapon
    private float laserEndSmoothSpeed = 500f;   // Slower for a trailing effect

    void Update()
    {
        // Legacy input: holding down left mouse button (or fire1)
        isFiringLaser = Input.GetButton("Fire1"); // Default maps to left mouse button

        if (isFiringLaser)
        {
            FireLaser();
        }
        else
        {
            laserRenderer.enabled = false;
        }
    }

    private void FireLaser()
    {
        Vector3 targetStart = laserVisualStartPoint.position;
        Vector3 targetEnd;

        if (Physics.Raycast(muzzleTransform.position, muzzleTransform.forward, out RaycastHit hit, laserRange))
        {
            targetEnd = hit.point;

            // Apply continuous damage
            float damageThisFrame = laserDamagePerSecond * Time.deltaTime;
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damageThisFrame);
            }
        }
        else
        {
            targetEnd = targetStart + laserVisualStartPoint.forward * laserRange;
        }

        if (!laserRenderer.enabled)
        {
            laserRenderer.enabled = true;
            currentLaserStart = targetStart;
            currentLaserEnd = targetEnd;
        }

        currentLaserStart = Vector3.Lerp(currentLaserStart, targetStart, Time.deltaTime * laserStartSmoothSpeed);
        currentLaserEnd = Vector3.Lerp(currentLaserEnd, targetEnd, Time.deltaTime * laserEndSmoothSpeed);

        laserRenderer.positionCount = 2;
        laserRenderer.SetPosition(0, currentLaserStart);
        laserRenderer.SetPosition(1, currentLaserEnd);

        // Visual pulsing
        float pulseSpeed = 5f;
        float minWidth = 0.005f;
        float maxWidth = 0.01f;
        float pulsedWidth = Mathf.Lerp(minWidth, maxWidth, Mathf.PingPong(Time.time * pulseSpeed, 1f));
        laserRenderer.startWidth = pulsedWidth;
        laserRenderer.endWidth = pulsedWidth;

        Color baseColor = Color.cyan;
        Color pulseColor = Color.Lerp(baseColor, Color.white, Mathf.PingPong(Time.time * 4f, 1f));
        laserRenderer.material.SetColor("_Color", pulseColor);
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
