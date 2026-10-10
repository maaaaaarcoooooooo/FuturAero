using System;
using UnityEngine;

namespace GasHunter
{
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] int maxLives = 3;
        [SerializeField] float invulnerabilityTime = 1.6f;
        [SerializeField] float respawnDelay = 0.35f;
        [SerializeField] Transform spawnPoint;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Color flashColor = Color.white;

        public event Action<int> LivesChanged;
        public event Action Damaged;
        public event Action Died;

        public int Lives { get; private set; }
        public bool IsInvulnerable { get; private set; }
        public bool IsDead { get; private set; }

        float invulnTimer;
        float respawnTimer = -1f;
        Color baseColor = Color.white;
        float flickerTimer;

        void Awake()
        {
            Lives = maxLives;
            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }
        }

        public void SetSpawnPoint(Transform point)
        {
            spawnPoint = point;
        }

        public void TakeDamage()
        {
            if (IsDead || IsInvulnerable)
            {
                return;
            }

            Lives = Mathf.Max(0, Lives - 1);
            LivesChanged?.Invoke(Lives);
            Damaged?.Invoke();

            if (Lives <= 0)
            {
                IsDead = true;
                respawnTimer = respawnDelay;
                Died?.Invoke();
                return;
            }

            IsInvulnerable = true;
            invulnTimer = invulnerabilityTime;
        }

        void Update()
        {
            if (IsInvulnerable)
            {
                invulnTimer -= Time.deltaTime;
                flickerTimer += Time.deltaTime;

                if (spriteRenderer != null)
                {
                    float alpha = 0.35f + 0.65f * (Mathf.Sin(flickerTimer * 32f) * 0.5f + 0.5f);
                    Color c = Color.Lerp(baseColor, flashColor, 0.6f);
                    c.a = alpha;
                    spriteRenderer.color = c;
                }

                if (invulnTimer <= 0f)
                {
                    IsInvulnerable = false;
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = baseColor;
                    }
                }
            }
        }

        public void Respawn()
        {
            IsDead = false;
            IsInvulnerable = true;
            invulnTimer = invulnerabilityTime;

            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.localScale = spawnPoint.localScale;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            GameManager.Instance?.NotifyPlayerRespawn();
        }

        public void RegisterFall()
        {
            TakeDamage();
            if (!IsDead)
            {
                Respawn();
            }
        }
    }
}