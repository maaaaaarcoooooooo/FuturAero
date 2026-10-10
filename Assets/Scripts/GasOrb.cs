using System;
using UnityEngine;

namespace GasHunter
{
    public class GasOrb : MonoBehaviour
    {
        [SerializeField] float attractRadius = 6f;
        [SerializeField] float attractSpeed = 9f;
        [SerializeField] float lifeTime = 0f;
        [SerializeField] float absorbRadius = 0.8f;
        [SerializeField] float bobAmplitude = 0.16f;
        [SerializeField] float bobSpeed = 3.4f;
        [SerializeField] bool stationUntilNear = true;
        [SerializeField] Color orbColor = new Color(0.45f, 0.98f, 0.65f, 1f);
        [SerializeField] SpriteRenderer spriteRenderer;

        static readonly System.Collections.Generic.List<GasOrb> Pool = new System.Collections.Generic.List<GasOrb>();

        Transform player;
        float life = -1f;
        float bobPhase;
        Vector2 drift;
        Vector3 basePosition;
        bool active;
        bool attracted;
        bool drifting;

        public float Value = 16f;

        void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            bobPhase = UnityEngine.Random.Range(0f, 6.28f);
            basePosition = transform.position;
            active = true;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = orbColor;
            }
        }

        public void Launch(Transform playerTransform, Vector2 initialDrift, float value = 0f)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            player = playerTransform;
            drift = initialDrift;
            drifting = drift.sqrMagnitude > 0.0001f;
            basePosition = transform.position;
            bobPhase = UnityEngine.Random.Range(0f, 6.28f);
            life = lifeTime > 0f ? lifeTime : -1f;
            active = true;
            attracted = false;

            gameObject.SetActive(true);
            gameObject.layer = LayerMask.NameToLayer(GameLayers.Orb);

            if (value > 0f)
            {
                Value = value;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = orbColor;
            }
        }

        public static GasOrb Spawn(GasOrb prefab, Vector3 position, Transform player, Vector2 drift, float value = 0f)
        {
            GasOrb instance = null;

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

            instance.transform.localScale = Vector3.one;
            instance.transform.localRotation = Quaternion.identity;
            instance.Launch(player, drift, value);
            return instance;
        }

        void Update()
        {
            if (!active)
            {
                return;
            }

            if (life > 0f)
            {
                life -= Time.deltaTime;
                if (life <= 0f)
                {
                    Recycle();
                    return;
                }
            }

            bobPhase += bobSpeed * Time.deltaTime;
            float bob = Mathf.Sin(bobPhase) * bobAmplitude;

            if (player == null)
            {
                player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            }

            if (player == null)
            {
                if (!drifting)
                {
                    transform.position = basePosition + new Vector3(0f, bob, 0f);
                }

                return;
            }

            Vector3 pos = transform.position;
            Vector3 toPlayer = player.position + Vector3.up * 0.2f - pos;
            float dist = toPlayer.magnitude;

            if (!attracted)
            {
                if (stationUntilNear && dist > attractRadius)
                {
                    transform.position = basePosition + new Vector3(0f, bob, 0f);
                    Animate(0.75f, 1f);
                    return;
                }

                attracted = true;
                basePosition = pos;
            }

            if (dist < absorbRadius)
            {
                Absorb();
                return;
            }

            Vector3 dir = toPlayer / Mathf.Max(dist, 0.001f);
            Vector3 velocity = dir * attractSpeed + new Vector3(drift.x, drift.y, 0f);
            transform.position = pos + velocity * Time.deltaTime + new Vector3(0f, bob * Time.deltaTime, 0f);

            if (drifting)
            {
                drift = Vector2.Lerp(drift, Vector2.zero, 3f * Time.deltaTime);
            }

            float targetScale = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(1f - dist / 12f));
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, 6f * Time.deltaTime);

            Animate(0.8f, 1.25f);
        }

        void Animate(float minScale, float maxScale)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            float pulse = Mathf.Lerp(minScale, maxScale, Mathf.Sin(bobPhase) * 0.5f + 0.5f);
            spriteRenderer.transform.localScale = new Vector3(pulse, pulse, 1f);

            Color c = orbColor;
            c.a *= 0.85f + 0.15f * (Mathf.Sin(Time.time * 7f + bobPhase) * 0.5f + 0.5f);
            spriteRenderer.color = c;
        }

        void Absorb()
        {
            if (!active)
            {
                return;
            }

            GasTank tank = PlayerController.Instance != null
                ? PlayerController.Instance.GetComponent<GasTank>()
                : FindAnyObjectByType<GasTank>();

            if (tank != null)
            {
                tank.Absorb(Value);
            }

            GameManager.Instance?.RegisterOrbAbsorbed();
            Recycle();
        }

        public void Recycle()
        {
            if (!active)
            {
                return;
            }

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