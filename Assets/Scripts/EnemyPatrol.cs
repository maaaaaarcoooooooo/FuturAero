using UnityEngine;

namespace GasHunter
{
    public class EnemyPatrol : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 2.2f;
        [SerializeField] float hoverHeight = 0f;
        [SerializeField] float bobAmplitude = 0.22f;
        [SerializeField] float bobSpeed = 1.9f;
        [SerializeField] float edgeProbeDistance = 0.55f;
        [SerializeField] float wallProbeDistance = 0.45f;
        [SerializeField] float turnPause = 0.15f;
        [SerializeField] bool floatsInPlace = true;
        [SerializeField] LayerMask groundMask;
        [SerializeField] SpriteRenderer spriteRenderer;

        static readonly RaycastHit2D[] Hits = new RaycastHit2D[4];

        Rigidbody2D body;
        BoxCollider2D box;
        int direction = 1;
        float turnTimer;
        float bobPhase;
        float startY;
        bool initialized;
        bool dead;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            startY = transform.position.y;
            bobPhase = Random.Range(0f, 6.28f);
        }

        public void InitializeDirection(int dir)
        {
            direction = dir >= 0 ? 1 : -1;
            initialized = true;
        }

        public void SetDead()
        {
            dead = true;
        }

        void Update()
        {
            if (dead || body == null)
            {
                return;
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
                box = GetComponent<BoxCollider2D>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            float dt = Time.deltaTime;
            bobPhase += bobSpeed * dt;

            float y = body.linearVelocity.y;
            if (floatsInPlace)
            {
                y = Mathf.Cos(bobPhase) * bobAmplitude * 2f;
            }

            if (turnTimer > 0f)
            {
                turnTimer -= dt;
                body.linearVelocity = new Vector2(0f, y);
                ApplyFlip();
                return;
            }

            if (!initialized)
            {
                direction = Random.value < 0.5f ? -1 : 1;
                initialized = true;
            }

            if (ShouldTurn())
            {
                direction = -direction;
                turnTimer = turnPause;
                body.linearVelocity = new Vector2(direction * moveSpeed * 0.4f, y);
                ApplyFlip();
                return;
            }

            body.linearVelocity = new Vector2(direction * moveSpeed, y);
            ApplyFlip();
        }

        void ApplyFlip()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.transform.localScale = new Vector3(direction < 0 ? -1f : 1f, 1f, 1f);
        }

        bool ShouldTurn()
        {
            Bounds b = box != null
                ? box.bounds
                : new Bounds(transform.position, new Vector3(1f, 1f, 0.1f));

            float bottom = b.min.y + 0.08f;
            float side = b.size.x * 0.5f;
            Vector3 forward = Vector3.right * direction;

            Vector3 edgeOrigin = new Vector3(transform.position.x + side * direction, bottom, 0f);
            if (Blocked(edgeOrigin, forward, edgeProbeDistance))
            {
                return true;
            }

            Vector3 wallOrigin = new Vector3(transform.position.x + side * direction, transform.position.y, 0f);
            return Blocked(wallOrigin, forward, wallProbeDistance);
        }

        bool Blocked(Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics2D.RaycastNonAlloc(origin, direction, Hits, distance, groundMask);

            for (int i = 0; i < count; i++)
            {
                if (Hits[i].collider != null && !Hits[i].collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }
    }
}