using System.Collections;
using UnityEngine;

namespace GasHunter
{
    public class BossController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 1.7f;
        [SerializeField] float turnPause = 0.3f;
        [SerializeField] float leftLimit = 138f;
        [SerializeField] float rightLimit = 157f;
        [SerializeField] float detectionDistance = 20f;
        [SerializeField] float attackRange = 14f;
        [SerializeField] int burstCount = 3;
        [SerializeField] float burstInterval = 0.22f;
        [SerializeField] float burstCooldown = 1.7f;
        [SerializeField] float projectileVerticalBlend = 0.5f;
        [SerializeField] Transform firePoint;
        [SerializeField] Animator animator;
        [SerializeField] BossProjectile projectilePrefab;

        const float FeetOffset = 0.83f;

        static readonly int WalkingParam = Animator.StringToHash("Walking");
        static readonly int ShootingParam = Animator.StringToHash("Shooting");

        Rigidbody2D body;
        Collider2D bodyCollider;
        int direction = -1;
        float turnTimer;
        float cooldownTimer;
        bool dead;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
        }

        void Start()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (firePoint == null)
            {
                firePoint = transform;
            }

            GroundToFloor();
        }

        void GroundToFloor()
        {
            LayerMask groundMask = LayerMask.GetMask(GameLayers.Ground);
            Vector2 origin = new Vector2(transform.position.x, transform.position.y + 3f);
            RaycastHit2D ground = Physics2D.Raycast(origin, Vector2.down, 8f, groundMask);
            if (ground.collider != null)
            {
                transform.position = new Vector3(transform.position.x, ground.point.y + FeetOffset, transform.position.z);
            }
        }

        PlayerController FindPlayer()
        {
            PlayerController pc = PlayerController.Instance;
            if (pc == null)
            {
                pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
            }

            return pc;
        }

        public void SetDead()
        {
            dead = true;
        }

        public bool IsDead
        {
            get { return dead; }
        }

        void Update()
        {
            if (dead || body == null)
            {
                return;
            }

            float dt = Time.deltaTime;

            if (turnTimer > 0f)
            {
                turnTimer -= dt;
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            Transform player = FindPlayer() != null ? FindPlayer().transform : null;
            bool hasPlayer = player != null;
            float distX = hasPlayer ? Mathf.Abs(player.position.x - transform.position.x) : float.MaxValue;
            float signToPlayer = hasPlayer ? Mathf.Sign(player.position.x - transform.position.x) : 0f;

            cooldownTimer -= dt;

            if (hasPlayer && distX <= attackRange && cooldownTimer <= 0f)
            {
                StartCoroutine(ShootBurstRoutine(player.position));
                cooldownTimer = burstCooldown;
                return;
            }

            if (hasPlayer && distX <= detectionDistance && signToPlayer != 0f && Mathf.Sign(direction) != signToPlayer)
            {
                direction = (int)signToPlayer;
                turnTimer = turnPause;
                ApplyFacing();
                return;
            }

            if (transform.position.x <= leftLimit && direction < 0)
            {
                direction = 1;
            }
            else if (transform.position.x >= rightLimit && direction > 0)
            {
                direction = -1;
            }

            ApplyFacing();

            if (animator != null)
            {
                animator.SetBool(WalkingParam, true);
            }

            body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);
        }

        void ApplyFacing()
        {
            Transform visual = animator != null ? animator.transform : transform;
            visual.localScale = new Vector3(direction < 0 ? -1f : 1f, 1f, 1f);

            if (firePoint != null)
            {
                Vector3 lp = firePoint.localPosition;
                firePoint.localPosition = new Vector3(Mathf.Abs(lp.x) * direction, lp.y, lp.z);
            }
        }

        IEnumerator ShootBurstRoutine(Vector3 target)
        {
            if (animator != null)
            {
                animator.SetBool(WalkingParam, false);
                animator.SetBool(ShootingParam, true);
            }

            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            for (int i = 0; i < burstCount; i++)
            {
                if (dead)
                {
                    yield break;
                }

                Vector2 aim = Vector2.right * direction;
                PlayerController pc = FindPlayer();
                if (pc != null)
                {
                    Vector2 toTarget = (target - (Vector3)firePoint.position).normalized;
                    aim = Vector2.Lerp(toTarget, Vector2.right * direction, projectileVerticalBlend).normalized;
                }

                BossProjectile.Spawn(projectilePrefab, firePoint.position, aim, bodyCollider);
                yield return new WaitForSeconds(burstInterval);
            }

            if (animator != null)
            {
                animator.SetBool(ShootingParam, false);
            }
        }
    }
}