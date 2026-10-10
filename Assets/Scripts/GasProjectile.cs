using System.Collections.Generic;
using UnityEngine;

namespace GasHunter
{
    public class GasProjectile : MonoBehaviour
    {
        [SerializeField] float speed = 15f;
        [SerializeField] float lifeTime = 2.2f;
        [SerializeField] float radius = 0.22f;
        [SerializeField] Color gasColor = new Color(0.40f, 0.85f, 1f, 0.95f);
        [SerializeField] TrailRenderer trail;
        [SerializeField] SpriteRenderer spriteRenderer;

        static readonly List<GasProjectile> Pool = new List<GasProjectile>();

        Rigidbody2D body;
        float life;
        bool active;

        public void Fire(Vector2 direction, float power = 1f)
        {
            gameObject.SetActive(true);
            transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.15f, Mathf.Clamp01(power));

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            body.linearVelocity = direction.normalized * speed;
            life = lifeTime;
            active = true;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = gasColor;
            }

            if (trail != null)
            {
                trail.emitting = true;
                trail.Clear();
                trail.startColor = gasColor;
            }

            gameObject.layer = LayerMask.NameToLayer(GameLayers.Projectile);
        }

        public static GasProjectile Spawn(GasProjectile prefab, Vector3 position, Vector2 direction, float power = 1f)
        {
            GasProjectile instance = null;

            for (int i = Pool.Count - 1; i >= 0; i--)
            {
                if (Pool[i] != null)
                {
                    instance = Pool[i];
                    Pool.RemoveAt(i);
                    break;
                }
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, position, Quaternion.identity);
            }
            else
            {
                instance.transform.position = position;
            }

            instance.transform.rotation = Quaternion.identity;
            instance.Fire(direction, power);
            return instance;
        }

        void Update()
        {
            if (!active)
            {
                return;
            }

            life -= Time.deltaTime;
            float pulse = 1f + Mathf.Sin(Time.time * 14f) * 0.12f;
            transform.localScale = Vector3.one * pulse;

            if (life <= 0f)
            {
                Recycle();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!active)
            {
                return;
            }

            EnemyHealth enemy = other.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                Vector2 knockback = body != null ? body.linearVelocity.normalized : Vector2.right;
                enemy.TakeDamage(knockback);
                GameManager.Instance?.RegisterShotHit();
                Recycle();
                return;
            }

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                Vector2 knockback = body != null ? body.linearVelocity.normalized : Vector2.right;
                damageable.TakeDamage(knockback);
                GameManager.Instance?.RegisterShotHit();
                Recycle();
                return;
            }

            if (other.isTrigger)
            {
                return;
            }

            Recycle();
        }

        public void Recycle()
        {
            if (!active)
            {
                return;
            }

            active = false;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }

            gameObject.SetActive(false);
            if (!Pool.Contains(this))
            {
                Pool.Add(this);
            }
        }

        void OnDisable()
        {
            active = false;
        }
    }
}