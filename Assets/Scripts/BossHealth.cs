using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GasHunter
{
    public class BossHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] int maxHealth = 30;
        [SerializeField] int orbsOnDeath = 10;
        [SerializeField] float hurtFlashTime = 0.12f;
        [SerializeField] float dissolveTime = 0.5f;
        [SerializeField] Color hurtColor = new Color(1f, 0.4f, 0.4f, 1f);
        [SerializeField] Color healthyColor = new Color(0.3f, 0.95f, 0.45f, 1f);
        [SerializeField] Color midColor = new Color(0.95f, 0.75f, 0.3f, 1f);
        [SerializeField] Color lowColor = new Color(0.95f, 0.3f, 0.3f, 1f);
        [SerializeField] float lowThreshold = 0.34f;
        [SerializeField] SpriteRenderer visual;
        [SerializeField] SpriteRenderer healthBg;
        [SerializeField] SpriteRenderer healthFill;
        [SerializeField] float barTotalWidth = 1.9f;
        [SerializeField] float barHeight = 0.14f;
        [SerializeField] GasOrb orbPrefab;

        int current;
        bool active = true;
        Color baseColor = Color.white;

        void Awake()
        {
            current = maxHealth;

            if (visual == null)
            {
                visual = GetComponentInChildren<SpriteRenderer>();
            }

            if (visual != null)
            {
                baseColor = visual.color;
            }

            RefreshBar();
        }

        public void TakeDamage(Vector2 knockbackDirection)
        {
            if (!active)
            {
                return;
            }

            current = Mathf.Max(0, current - 1);
            RefreshBar();
            StartCoroutine(FlashRoutine());

            if (current <= 0)
            {
                StartCoroutine(DieRoutine());
            }
        }

        void RefreshBar()
        {
            if (healthFill == null)
            {
                return;
            }

            float frac = maxHealth > 0 ? (float)current / maxHealth : 0f;
            healthFill.transform.localScale = new Vector3(barTotalWidth * frac, barHeight, 1f);
            healthFill.transform.localPosition = new Vector3(-barTotalWidth * 0.5f, 0f, 0f);
            healthFill.color = frac <= lowThreshold ? lowColor : (frac <= 0.66f ? midColor : healthyColor);
        }

        IEnumerator FlashRoutine()
        {
            if (visual != null)
            {
                visual.color = hurtColor;
            }

            yield return new WaitForSeconds(hurtFlashTime);

            if (visual != null)
            {
                visual.color = baseColor;
            }
        }

        IEnumerator DieRoutine()
        {
            active = false;

            var controller = GetComponent<BossController>();
            if (controller != null)
            {
                controller.SetDead();
            }

            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                gm = UnityEngine.Object.FindObjectOfType<GameManager>();
            }

            if (gm != null)
            {
                gm.RegisterBossDefeated();
            }

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.simulated = false;
            }

            Collider2D collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Transform player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            for (int i = 0; i < orbsOnDeath; i++)
            {
                Vector2 drift = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f);
                Vector3 scatter = Random.insideUnitCircle * 0.3f;
                GasOrb.Spawn(orbPrefab, transform.position + scatter, player, drift);
            }

            Vector3 startScale = transform.localScale;
            float t = 0f;

            while (t < dissolveTime)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / dissolveTime);
                transform.localScale = new Vector3(startScale.x * k, startScale.y * k, startScale.z);

                if (visual != null)
                {
                    Color c = visual.color;
                    c.a = k;
                    visual.color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}