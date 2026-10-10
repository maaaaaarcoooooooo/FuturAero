using System.Collections.Generic;
using UnityEngine;

namespace GasHunter
{
    public class BossProjectile : MonoBehaviour
    {
        [SerializeField] float speed = 8f;
        [SerializeField] float lifeTime = 2.6f;
        [SerializeField] SpriteRenderer spriteRenderer;

        static readonly List<BossProjectile> Pool = new List<BossProjectile>();

        Rigidbody2D body;
        Collider2D projCollider;
        float life;
        bool active;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            projCollider = GetComponent<Collider2D>();
        }

        public void Fire(Vector2 direction)
        {
            gameObject.SetActive(true);

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (projCollider == null)
            {
                projCollider = GetComponent<Collider2D>();
            }

            body.linearVelocity = direction.normalized * speed;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            life = lifeTime;
            active = true;
            gameObject.layer = LayerMask.NameToLayer(GameLayers.Projectile);
        }

        public static BossProjectile Spawn(BossProjectile prefab, Vector3 position, Vector2 direction, Collider2D ignoredCollider)
        {
            BossProjectile instance = null;

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

            instance.Fire(direction);

            if (ignoredCollider != null && instance.projCollider != null)
            {
                Physics2D.IgnoreCollision(ignoredCollider, instance.projCollider, true);
            }

            return instance;
        }

        void Update()
        {
            if (!active)
            {
                return;
            }

            life -= Time.deltaTime;

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

            PlayerHealth player = other.GetComponent<PlayerHealth>();
            if (player != null)
            {
                if (!player.IsDead && !player.IsInvulnerable)
                {
                    player.TakeDamage();
                }

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