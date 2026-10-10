using UnityEngine;

namespace GasHunter
{
    public class HazardArea : MonoBehaviour
    {
        [SerializeField] float damageCooldown = 0.9f;
        [SerializeField] float knockbackX = 4f;
        [SerializeField] float knockbackY = 11f;
        [SerializeField] bool killOnTouch;

        float cooldown;
        PlayerController player;

        void Reset()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.isTrigger = true;
            }
        }

        void OnTriggerStay2D(Collider2D other)
        {
            TryHit(other);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            TryHit(other);
        }

        void TryHit(Collider2D other)
        {
            if (cooldown > 0f || other == null)
            {
                return;
            }

            if (player == null)
            {
                player = other.GetComponent<PlayerController>();
            }

            if (player == null)
            {
                return;
            }

            var health = player.GetComponent<PlayerHealth>();
            if (health == null || health.IsDead || health.IsInvulnerable)
            {
                return;
            }

            float dir = Mathf.Sign(player.transform.position.x - transform.position.x);
            if (Mathf.Approximately(dir, 0f))
            {
                dir = 1f;
            }

            player.ApplyKnockback(new Vector2(dir * knockbackX, knockbackY));
            cooldown = damageCooldown;
            health.TakeDamage();
        }
    }
}