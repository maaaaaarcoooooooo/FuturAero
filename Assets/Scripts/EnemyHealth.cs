using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GasHunter
{
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] int hitPoints = 1;
        [SerializeField] int orbsOnDeath = 3;
        [SerializeField] float hurtFlashTime = 0.12f;
        [SerializeField] float knockbackResistance = 0.35f;
        [SerializeField] float dissolveTime = 0.22f;
        [SerializeField] Color healthyColor = Color.white;
        [SerializeField] Color hurtColor = new Color(1f, 0.55f, 0.65f, 1f);
        [SerializeField] GasOrb orbPrefab;

        static readonly List<EnemyHealth> Pool = new List<EnemyHealth>();

        SpriteRenderer spriteRenderer;
        Color baseColor = Color.white;
        int currentHp;
        bool active;
        bool flashing;
        Rigidbody2D body;
        BoxCollider2D hitbox;

        void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }

            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            currentHp = hitPoints;
            active = true;
        }

        public void Spawn(Vector3 position)
        {
            gameObject.SetActive(true);
            transform.position = position;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            currentHp = hitPoints;
            active = true;
            flashing = false;
            gameObject.layer = LayerMask.NameToLayer(GameLayers.Enemy);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = true;
            }

            if (hitbox != null)
            {
                hitbox.enabled = true;
            }
        }

        public static EnemyHealth Spawn(EnemyHealth prefab, Vector3 position)
        {
            EnemyHealth instance = null;

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

            instance.Spawn(position);
            return instance;
        }

        public void TakeDamage(Vector2 knockbackDirection)
        {
            if (!active)
            {
                return;
            }

            currentHp -= 1;

            if (currentHp > 0)
            {
                flashing = true;
                StartCoroutine(FlashRoutine());
                if (body != null)
                {
                    body.AddForce(knockbackDirection.normalized * knockbackResistance, ForceMode2D.Impulse);
                }
                return;
            }

            StartCoroutine(DieRoutine(knockbackDirection));
        }

        IEnumerator FlashRoutine()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = hurtColor;
            }

            yield return new WaitForSeconds(hurtFlashTime);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }

            flashing = false;
        }

        IEnumerator DieRoutine(Vector2 knockbackDirection)
        {
            active = false;

            var patrol = GetComponent<EnemyPatrol>();
            if (patrol != null)
            {
                patrol.SetDead();
            }

            if (body != null)
            {
                body.simulated = false;
            }

            if (hitbox != null)
            {
                hitbox.enabled = false;
            }

            GameManager.Instance?.RegisterEnemyKilled();

            Transform player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            for (int i = 0; i < orbsOnDeath; i++)
            {
                Vector2 drift = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.5f, 3.5f);
                Vector3 scatter = UnityEngine.Random.insideUnitCircle * 0.25f;
                GasOrb.Spawn(orbPrefab, transform.position + scatter, player, drift);
            }

            Vector3 startScale = transform.localScale;
            float t = 0f;

            while (t < dissolveTime)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / dissolveTime);
                transform.localScale = new Vector3(startScale.x * k, startScale.y * k * (1f + (1f - k) * 0.6f), startScale.z);
                if (spriteRenderer != null)
                {
                    Color c = baseColor;
                    c.a = k;
                    spriteRenderer.color = c;
                }
                yield return null;
            }

            Recycle();
        }

        public void Recycle()
        {
            active = false;
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