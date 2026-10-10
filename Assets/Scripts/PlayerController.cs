using UnityEngine;
using UnityEngine.InputSystem;

namespace GasHunter
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] float moveSpeed = 7.5f;
        [SerializeField] float groundAcceleration = 70f;
        [SerializeField] float airAcceleration = 40f;
        [SerializeField] float groundFriction = 80f;
        [SerializeField] float jumpForce = 17f;
        [SerializeField] float jumpCutMultiplier = 0.45f;
        [SerializeField] float maxFallSpeed = 26f;
        [SerializeField] float coyoteTime = 0.12f;
        [SerializeField] float jumpBufferTime = 0.12f;
        [SerializeField] int maxJumps = 2;

        [Header("Ground Check")]
        [SerializeField] float groundCheckDistance = 0.12f;
        [SerializeField] LayerMask groundMask;

        [Header("Combat")]
        [SerializeField] GasProjectile projectilePrefab;
        [SerializeField] Transform firePoint;
        [SerializeField] float fireCooldown = 0.18f;
        [SerializeField] float shootUpAngle = 55f;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] ParticleSystem shootFlash;
        [SerializeField] Animator animator;

        [Header("Feel")]
        [SerializeField] float coyoteTimer;

        Rigidbody2D body;
        BoxCollider2D box;
        GasTank tank;
        PlayerHealth health;

        int jumpsLeft;
        float bufferTimer;
        float fireTimer;
        float currentFireAngle;
        int facing = 1;
        bool wasGrounded;
        bool jumpCutApplied;

        static readonly RaycastHit2D[] GroundHits = new RaycastHit2D[6];
        static readonly Collider2D[] OverlapHits = new Collider2D[8];

        public bool IsGrounded { get; private set; }
        public Vector2 AimDirection { get; private set; }
        public Rigidbody2D Body => body;

        void Awake()
        {
            Instance = this;
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            tank = GetComponent<GasTank>();
            health = GetComponent<PlayerHealth>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            shootFlash = GetComponentInChildren<ParticleSystem>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            jumpsLeft = maxJumps;
            AimDirection = Vector2.right;

            if (shootFlash != null)
            {
                ParticleSystem.MainModule main = shootFlash.main;
                main.stopAction = ParticleSystemStopAction.None;
                shootFlash.Stop();
            }
        }

        void Update()
        {
            if (health != null && health.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            float dt = Time.deltaTime;
            bool grounded = CheckGrounded();
            IsGrounded = grounded;

            if (grounded)
            {
                coyoteTimer = coyoteTime;
                if (!wasGrounded)
                {
                    jumpsLeft = maxJumps - 1;
                    jumpCutApplied = false;
                    GameManager.Instance?.RegisterLanding();
                }
            }
            else
            {
                coyoteTimer -= dt;
            }

            wasGrounded = grounded;

            float horizontal = ReadHorizontal();
            Vector2 velocity = body.linearVelocity;

            float accel = grounded ? groundAcceleration : airAcceleration;
            float targetX = horizontal * moveSpeed;

            if (Mathf.Abs(horizontal) > 0.01f)
            {
                velocity.x = Mathf.MoveTowards(velocity.x, targetX, accel * dt);
                facing = horizontal > 0f ? 1 : -1;
            }
            else if (grounded)
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, groundFriction * dt);
            }

            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
            body.linearVelocity = velocity;

            if (ReadJumpPressed())
            {
                bufferTimer = jumpBufferTime;
            }
            else
            {
                bufferTimer -= dt;
            }

            if (bufferTimer > 0f)
            {
                if (grounded || coyoteTimer > 0f)
                {
                    Jump(1f);
                    bufferTimer = 0f;
                }
                else if (jumpsLeft > 0)
                {
                    Jump(0.92f);
                    jumpsLeft--;
                    bufferTimer = 0f;
                }
            }

            if (!ReadJumpHeld() && body.linearVelocity.y > 0f && !jumpCutApplied)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * jumpCutMultiplier);
                jumpCutApplied = true;
            }

            fireTimer -= dt;
            UpdateAim(horizontal);
            UpdateFacing();

            if (ReadFirePressed())
            {
                TryFire();
            }

            CheckOutOfBounds();
            UpdateAnimator();
        }

        void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool("IsGrounded", IsGrounded);
            animator.SetFloat("Speed", Mathf.Abs(body.linearVelocity.x));
        }

        void Jump(float powerScale)
        {
            jumpCutApplied = false;
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce * powerScale);
            GameManager.Instance?.RegisterJump();
        }

        void UpdateAim(float horizontal)
        {
            float vertical = ReadVertical();

            if (Mathf.Abs(vertical) > 0.5f)
            {
                float dirX = Mathf.Abs(horizontal) < 0.1f ? facing * 0.45f : horizontal;
                Vector2 dir = new Vector2(dirX, vertical).normalized;
                currentFireAngle = Mathf.Atan2(dir.y, dirX) * Mathf.Rad2Deg;
                AimDirection = dir;
            }
            else
            {
                currentFireAngle = 0f;
                AimDirection = new Vector2(facing, 0f);
            }

            currentFireAngle = Mathf.Clamp(currentFireAngle, -shootUpAngle, shootUpAngle);
        }

        void UpdateFacing()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.transform.localScale = new Vector3(facing < 0 ? -1f : 1f, 1f, 1f);

            if (firePoint != null)
            {
                Vector3 lp = firePoint.localPosition;
                firePoint.localPosition = new Vector3(Mathf.Abs(lp.x) * facing, lp.y, lp.z);
            }
        }

        void TryFire()
        {
            if (tank == null || !tank.HasEnoughForShot)
            {
                GameManager.Instance?.RegisterEmptyShot();
                fireTimer = 0.3f;
                return;
            }

            tank.TryConsumeShot();
            fireTimer = fireCooldown;

            if (animator != null)
            {
                animator.SetTrigger("Shoot");
            }

            Transform origin = firePoint != null ? firePoint : transform;
            float angle = currentFireAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle));

            float power = tank.Normalized;
            GasProjectile.Spawn(projectilePrefab, origin.position, dir, power);

            body.linearVelocity = new Vector2(body.linearVelocity.x - dir.x * tank.ConsumeRecoil(), body.linearVelocity.y);

            if (shootFlash != null)
            {
                shootFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                shootFlash.Emit(6);
            }

            GameManager.Instance?.RegisterShot();
        }

        bool CheckGrounded()
        {
            Bounds b = box != null ? box.bounds : new Bounds(transform.position, new Vector3(0.8f, 1.6f, 0.1f));
            Vector2 center = new Vector2(b.center.x, b.min.y + groundCheckDistance * 0.5f);
            Vector2 size = new Vector2(b.size.x * 0.9f, groundCheckDistance);

            int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, OverlapHits, groundMask);
            for (int i = 0; i < count; i++)
            {
                Collider2D c = OverlapHits[i];
                if (c != null && !c.isTrigger && !c.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        void CheckOutOfBounds()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            if (transform.position.y < GameManager.Instance.KillPlaneY)
            {
                GameManager.Instance?.PlayFallSound();
                health.RegisterFall();
            }
        }

        public void ApplyKnockback(Vector2 force)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x + force.x, body.linearVelocity.y + force.y);
        }

        static bool KeyHeld(params Key[] keys)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (keyboard[keys[i]].isPressed)
                {
                    return true;
                }
            }

            return false;
        }

        static bool KeyPressedThisFrame(params Key[] keys)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (keyboard[keys[i]].wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }

        float ReadHorizontal()
        {
            float left = 0f;
            float right = 0f;

            if (KeyHeld(Key.A, Key.LeftArrow)) left = -1f;
            if (KeyHeld(Key.D, Key.RightArrow)) right = 1f;

            return Mathf.Clamp(right + left, -1f, 1f);
        }

        float ReadVertical()
        {
            float up = 0f;
            float down = 0f;

            if (KeyHeld(Key.W, Key.UpArrow)) up = 1f;
            if (KeyHeld(Key.S, Key.DownArrow)) down = -1f;

            return Mathf.Clamp(up - down, -1f, 1f);
        }

        bool ReadJumpPressed()
        {
            return KeyPressedThisFrame(Key.Space, Key.K, Key.Z);
        }

        bool ReadJumpHeld()
        {
            return KeyHeld(Key.Space, Key.K, Key.Z);
        }

        bool ReadFirePressed()
        {
            bool key = KeyPressedThisFrame(Key.X, Key.J);
            bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            return key || mouse;
        }
    }
}