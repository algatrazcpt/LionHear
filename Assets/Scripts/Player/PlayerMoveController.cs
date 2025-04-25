using System.Collections;
using UnityEngine;

public class PlayerMoveController : MonoBehaviour
{
        public Animator animator;
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
            Rising,
            Falling
        }
        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
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
        public float dashTimeCounter;

        public PlayerState currentState;


        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer= GetComponent<SpriteRenderer>();
            CalculateGravityAndJump();
        }

    private void Update()
    {
        GroundCheck();
        HandleInput();
        Turn(moveInput);
        if (!isGrounded && rb.linearVelocity.y < -0.02f &&
            currentState != PlayerState.Falling &&
            currentState != PlayerState.Jumping &&
            currentState != PlayerState.Rising &&
            currentState != PlayerState.Landing)
        {
            ChangeState(PlayerState.Falling);
            return; // diðer durumlara geçmesin artýk
        }

        switch (currentState)
        {
            case PlayerState.Idle:
                if (Mathf.Abs(moveInput) > 0.01f)
                    ChangeState(PlayerState.Run);
                if (Input.GetButtonDown("Jump") && isGrounded)
                    ChangeState(PlayerState.Jumping);
                break;

            case PlayerState.Run:
                if (Input.GetButtonDown("Jump") && isGrounded)
                    ChangeState(PlayerState.Jumping);
                else if (Mathf.Abs(moveInput) <= 0.01f)
                    ChangeState(PlayerState.Idle);
                break;

            case PlayerState.Jumping:
                if (rb.linearVelocity.y > 0.01f)
                    ChangeState(PlayerState.Rising);
                break;

            case PlayerState.Rising:
                if (rb.linearVelocity.y <= 0)
                    ChangeState(PlayerState.Falling);
                break;

            case PlayerState.Falling:
                if (isGrounded)
                    ChangeState(PlayerState.Landing);
                break;

            case PlayerState.Landing:
                if (isGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.01f)
                    ChangeState(Mathf.Abs(moveInput) > 0.01f ? PlayerState.Run : PlayerState.Idle);
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
                Run(1);
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
        switch (newState)
        {
            case PlayerState.Idle:
                animator.SetInteger("moving", -1);
                break;

            case PlayerState.Run:
                animator.SetInteger("moving", Mathf.Abs((int)moveInput));
                break;

            case PlayerState.Jumping:
                Jump();
                animator.SetTrigger("JumpingT"); // animator'da varsa
                break;

            case PlayerState.Rising:
                animator.SetBool("isRising", true);
                animator.SetBool("isFalling", false);
                break;

            case PlayerState.Falling:
                animator.SetBool("isFalling", true);
                animator.SetBool("isRising", false);
                break;

            case PlayerState.Landing:
                animator.SetTrigger("LandingT");
                animator.SetBool("isFalling", false);
                animator.SetBool("isRising", false);
                break;
        }

        currentState = newState;
    }

        private void CalculateGravityAndJump()
        {
        playerData.gravityStrength = -(2 * playerData.jumpHeight) / (playerData.jumpTimeToApex * playerData.jumpTimeToApex);
        playerData.gravityScale = playerData.gravityStrength / Physics2D.gravity.y;
        rb.gravityScale = playerData.gravityScale;
        playerData.jumpForce = Mathf.Abs(playerData.gravityStrength) * playerData.jumpTimeToApex;
        }
        private void Run(float lerpAmount)
        {
            //Calculate the direction we want to move in and our desired velocity
            float targetSpeed = moveInput * playerData.runMaxSpeed;
            //We can reduce are control using Lerp() this smooths changes to are direction and speed
            targetSpeed = Mathf.Lerp(rb.linearVelocity.x, targetSpeed, lerpAmount);

            #region Calculate AccelRate
            float accelRate;


            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? playerData.runAcceleration : playerData.runDecceleration;

            #endregion

            #region Add Bonus Jump Apex Acceleration
            //Increase are acceleration and maxSpeed when at the apex of their jump, makes the jump feel a bit more bouncy, responsive and natural
            if ((currentState == PlayerState.Jumping || currentState == PlayerState.Landing) && Mathf.Abs(rb.linearVelocity.y) < playerData.jumpHangTimeThreshold)
            {
                accelRate *= playerData.jumpHangGravityMult;
                targetSpeed *= playerData.jumpHangGravityMult;
            }
            #endregion

            #region Conserve Momentum
            //We won't slow the player down if they are moving in their desired direction but at a greater speed than their maxSpeed
            if (playerData.doConserveMomentum && Mathf.Abs(rb.linearVelocity.x) > Mathf.Abs(targetSpeed) && Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(targetSpeed) && Mathf.Abs(targetSpeed) > 0.01f)
            {
                accelRate = 0;
            }
            #endregion

            //Calculate difference between current velocity and desired velocity
            float speedDif = targetSpeed - rb.linearVelocity.x;
            //Calculate force along x-axis to apply to thr player

            float movement = speedDif * accelRate;

            //Convert this to a vector and apply to rigidbody
            rb.AddForce(movement * Vector2.right, ForceMode2D.Force);

            /*
             * For those interested here is what AddForce() will do
             * RB.velocity = new Vector2(RB.velocity.x + (Time.fixedDeltaTime  * speedDif * accelRate) / RB.mass, RB.velocity.y);
             * Time.fixedDeltaTime is by default in Unity 0.02 seconds equal to 50 FixedUpdate() calls per second
            */
        }

        private void Turn(float movePos)
        {
            if(movePos != 0)
            {
                spriteRenderer.flipX = movePos<1?true:false;
            }
        }
        private void Jump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, playerData.jumpForce);
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
                gravityMult = playerData.fastFallGravityMult;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -playerData.maxFastFallSpeed));
            }
            else if (isFalling)
            {
                gravityMult = playerData.fallGravityMult;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -playerData.maxFallSpeed));
            }
            else if (isJumpCut)
            {
                gravityMult = playerData.jumpCutGravityMult;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -playerData.maxFallSpeed));
            }
            else if (isJumpingUp && Mathf.Abs(rb.linearVelocity.y) < playerData.jumpHangTimeThreshold)
            {
                gravityMult = playerData.jumpHangGravityMult;
            }

            rb.gravityScale = playerData.gravityScale * gravityMult;

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
            dashTimeCounter = playerData.dashEndTime;
            rb.linearVelocity = new Vector2(moveInput * playerData.dashSpeed, 0f);
        }

        private void Dash()
        {
            dashTimeCounter -= Time.fixedDeltaTime;

            rb.linearVelocity = new Vector2(moveInput * playerData.dashSpeed, 0f);

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






    /*
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

    public PlayerState currentState;
    private float moveInput;
    private bool isGrounded;
    private bool isDashing;
    private float dashTimeCounter;
    private bool isJumping;
    private bool isJumpCut;
    private bool isFastFalling;
    public Transform groundCheckPoint;
    public Vector2 groundCheckSize;
    public LayerMask groundLayer;

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
        playerData.gravityStrength = -(2 * playerData.jumpHeight) / (playerData.jumpTimeToApex * playerData.jumpTimeToApex);
        playerData.gravityScale = playerData.gravityStrength / Physics2D.gravity.y;
        rb.gravityScale = playerData.gravityScale;
        playerData.jumpForce = Mathf.Abs(playerData.gravityStrength) * playerData.jumpTimeToApex;
    }

    private void Run()
    {
        float targetSpeed = moveInput * playerData.runMaxSpeed;
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? playerData.runAcceleration : playerData.runDecceleration;
        accelRate *= (isGrounded) ? 1f : (targetSpeed != 0 ? playerData.accelInAir : playerData.deccelInAir);

        if (playerData.doConserveMomentum && Mathf.Abs(rb.linearVelocity.x) > Mathf.Abs(targetSpeed) && Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(targetSpeed))
        {
            accelRate = 0;
        }

        float movement = Mathf.Pow(Mathf.Abs(speedDiff) * accelRate, 0.9f) * Mathf.Sign(speedDiff);

        rb.AddForce(movement * Vector2.right);

        if (Mathf.Abs(rb.linearVelocity.x) > playerData.runMaxSpeed)
        {
            rb.linearVelocity = new Vector2(Mathf.Sign(rb.linearVelocity.x) * playerData.runMaxSpeed, rb.linearVelocity.y);
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, playerData.jumpForce);
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
            gravityMult = playerData.fastFallGravityMult;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -playerData.maxFastFallSpeed));
        }
        else if (isFalling)
        {
            gravityMult = playerData.fallGravityMult;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -playerData.maxFallSpeed));
        }
        else if (isJumpCut)
        {
            gravityMult = playerData.jumpCutGravityMult;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -playerData.maxFallSpeed));
        }
        else if (isJumpingUp && Mathf.Abs(rb.linearVelocity.y) < playerData.jumpHangTimeThreshold)
        {
            gravityMult = playerData.jumpHangGravityMult;
        }

        rb.gravityScale = playerData.gravityScale * gravityMult;

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
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimeCounter = playerData.dashEndTime;
        rb.linearVelocity = new Vector2(moveInput * playerData.dashSpeed, 0f);
    }

    private void Dash()
    {
        dashTimeCounter -= Time.fixedDeltaTime;

        rb.linearVelocity = new Vector2(moveInput * playerData.dashSpeed, 0f);

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


    */

