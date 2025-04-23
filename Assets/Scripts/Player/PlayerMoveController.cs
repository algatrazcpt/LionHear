using System.Collections;
using UnityEngine;

public class PlayerMoveController : MonoBehaviour
{


    public PlayerData playerData;
    public enum PlayerState
    {
        Idle,
        Run,
        Attacking,
        SpecialAttack,
        Dashing,
        Stunned,
        Grapping,
        Jumping,
        Landing,
        Sliding,
        Falling
    }
    private Rigidbody2D rb;

    [Header("Movement Settings")]
    public float runMaxSpeed;
    public float runAcceleration;
    public float runDecceleration;
    public float accelInAir;
    public float deccelInAir;
    public bool doConserveMomentum = true;

    [Header("Jump Settings")]
    public float jumpHeight;
    public float jumpTimeToApex;
    public float fallGravityMult;
    public float fastFallGravityMult;
    public float maxFallSpeed;
    public float maxFastFallSpeed;
    public float jumpCutGravityMult;
    public float jumpHangGravityMult;
    public float jumpHangTimeThreshold;

    [Header("Dash Settings")]
    public float dashSpeed;
    public float dashDuration;

    [Header("Ground Check Settings")]
    public Transform groundCheckPoint;
    public Vector2 groundCheckSize;
    public LayerMask groundLayer;

    public float moveInput;
    public bool isGrounded;
    public bool isJumping;
    public bool isJumpCut;
    public bool isDashing;
    public bool isFastFalling;
    public float gravityStrength;
    public float gravityScale;
    public float jumpForce;
    public float dashTimeCounter;

    public PlayerState currentState;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();







        CalculateGravityAndJump();
    }

    private void Update()
    {
        GroundCheck();
        HandleInput();

        switch (currentState)
        {
            case PlayerState.Idle:
                if (Mathf.Abs(moveInput) > 0.01f)
                    ChangeState(PlayerState.Run);
                if (Input.GetButtonDown("Jump") && isGrounded)
                    ChangeState(PlayerState.Jumping);
                break;

            case PlayerState.Run:
                if (Mathf.Abs(moveInput) < 0.01f)
                    ChangeState(PlayerState.Idle);
                if (Input.GetButtonDown("Jump") && isGrounded)
                    ChangeState(PlayerState.Jumping);
                break;

            case PlayerState.Jumping:
                if (rb.linearVelocity.y <= 0)
                    ChangeState(PlayerState.Landing);
                break;

            case PlayerState.Landing:
                if (isGrounded)
                    ChangeState(PlayerState.Idle);
                break;
        }
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            Dash();
        }
        else
        {
            Run();
            ApplyGravity();
        }
    }

    private void HandleInput()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        if (!isDashing)
        {
            if (Input.GetButtonUp("Jump"))
                isJumpCut = true;

            if (Input.GetKeyDown(KeyCode.LeftShift))
                StartDash();
        }
    }

    private void ChangeState(PlayerState newState)
    {
        if (newState == PlayerState.Jumping)
        {
            Jump();
        }

        currentState = newState;
    }

    private void CalculateGravityAndJump()
    {
        gravityStrength = -(2 * jumpHeight) / (jumpTimeToApex * jumpTimeToApex);
        gravityScale = gravityStrength / Physics2D.gravity.y;
        rb.gravityScale = gravityScale;
        jumpForce = Mathf.Abs(gravityStrength) * jumpTimeToApex;
    }

    private void Run()
    {
        float targetSpeed = moveInput * runMaxSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? runAcceleration : runDecceleration;
        accelRate *= (isGrounded) ? 1f : (targetSpeed != 0 ? accelInAir : deccelInAir);

        if (doConserveMomentum && Mathf.Abs(rb.linearVelocity.x) > Mathf.Abs(targetSpeed) && Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(targetSpeed))
        {
            accelRate = 0;
        }

        float movement = Mathf.Pow(Mathf.Abs(speedDiff) * accelRate, 0.9f) * Mathf.Sign(speedDiff);

        rb.AddForce(movement * Vector2.right);

        if (Mathf.Abs(rb.linearVelocity.x) > runMaxSpeed)
        {
            rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * runMaxSpeed, rb.linearVelocity.y);
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        isJumping = true;
        isJumpCut = false;
    }

    private void ApplyGravity()
    {
        bool isFalling = rb.linearVelocity.y < -0.01f;
        bool isJumpingUp = rb.linearVelocity.y > 0.01f;

        float gravityMult = 1;

        if (isFastFalling)
        {
            gravityMult = fastFallGravityMult;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFastFallSpeed));
        }
        else if (isFalling)
        {
            gravityMult = fallGravityMult;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFallSpeed));
        }
        else if (isJumpCut)
        {
            gravityMult = jumpCutGravityMult;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFallSpeed));
        }
        else if (isJumpingUp && Mathf.Abs(rb.linearVelocity.y) < jumpHangTimeThreshold)
        {
            gravityMult = jumpHangGravityMult;
        }

        rb.gravityScale = gravityScale * gravityMult;

        if (Input.GetAxisRaw("Vertical") < -0.5f && isFalling)
        {
            isFastFalling = true;
        }
        else
        {
            isFastFalling = false;
        }
    }

    private void GroundCheck()
    {
        isGrounded = Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0, groundLayer);
        if (isGrounded)
        {
            isJumping = false;
            isJumpCut = false;
        }
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimeCounter = dashDuration;
        rb.linearVelocity = new Vector2(moveInput * dashSpeed, 0f);
    }

    private void Dash()
    {
        dashTimeCounter -= Time.fixedDeltaTime;

        rb.linearVelocity = new Vector2(moveInput * dashSpeed, 0f);

        if (dashTimeCounter <= 0)
        {
            isDashing = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
        }
    }




}
