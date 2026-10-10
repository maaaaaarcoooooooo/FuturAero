using UnityEngine;

namespace GasHunter
{
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField] float contactCooldown = 0.8f;
        [SerializeField] float knockbackX = 6f;
        [SerializeField] float knockbackY = 9f;
        [SerializeField] float hitFlashTime = 0.1f;

        float cooldown;
        SpriteRenderer spriteRenderer;
        Color baseColor = Color.white;

        void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }
        }

        void Update()
        {
            if (cooldown > 0f)
            {
                cooldown -= Time.deltaTime;

                if (spriteRenderer != null)
                {
                    float k = Mathf.Clamp01(cooldown / hitFlashTime);
                    spriteRenderer.color = Color.Lerp(baseColor, Color.red, k);
                }
            }
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            TryHit(collision.collider);
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            TryHit(collision.collider);
        }

        void TryHit(Collider2D other)
        {
            if (cooldown > 0f || other == null)
            {
                return;
            }

            PlayerController player = other.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null || health.IsDead)
            {
                return;
            }

            if (health.IsInvulnerable)
            {
                return;
            }

            Vector2 dir = (player.transform.position - transform.position).normalized;
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = Vector2.up;
            }

            cooldown = contactCooldown;

            player.ApplyKnockback(new Vector2(dir.x * knockbackX, knockbackY));
            health.TakeDamage();
        }
    }
}