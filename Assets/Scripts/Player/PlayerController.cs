using System;
using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{

    public PlayerData Data;
    private Animator animator;
    public enum PlayerState
    {
        Idle,
        Run,
        Jumping,
        JumpFalling,
        WallJumping,
        Rising,
        Falling,
        Landing,
        Dash,
        Attack
    }


    public Rigidbody2D rb;
    public Vector2 moveInput;
    public PlayerState currentState;

    //Ground
    public bool isGrounded = false;
    [SerializeField] private Transform groundCheckPoint;
    //Size of groundCheck depends on the size of your character generally you want them slightly small than width (for ground) and height (for the wall check)
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.49f, 0.03f);
    public LayerMask groundLayer;
    //Jump
    public bool _isJumpCut;
    public bool _isJumpFalling;

    //Wall Jump
    public float _wallJumpStartTime;
    public int _lastWallJumpDir;

    //Attack
    public bool isAttacking = false;
    public int attackIndex = 0;
    public float attackTimer = 0f;
    public float attackDelay = 0.4f; // Her saldýrýdan sonra bekleme süresi
    public bool attackQueued = false;
    private int maxComboCount = 2;
    public bool basicAttackAviable = false;
    //FastRun
    public bool fastRunMode = false;
    public float fastRunTimer = 0f;
    public float fastRunClampTime = 1.5f;
    public float fastRunSliding = 0.5f;
    private float tempRunDeccel;
    private float fastRunExitTimer = 0f; // Ekstra deðiþken: koþmayý býrakýnca sayaç baþlasýn
    private float fastRunExitDelay = 0.2f; // 0.2 saniye bekleyecek
    //SlowEffect
    public bool isChangingDirection = false;
    private float slowDownDuration = 0.5f;
    private float slowDownFactor = 0.01f; // Ne kadar yavaþlasýn (1 = normal hýz, 0.3 = %30 hýz)
    public bool applySlowDown = false;
    public float dynamicRun = 1f;
    public float currentDeccelAmount = 0;
    private Vector2 lastMoveDirection;

    //Timers (also all fields, could be private and a method returning a bool could be used)
    public float LastOnGroundTime { get; private set; }
    public float LastOnWallTime { get; private set; }
    public float LastOnWallRightTime { get; private set; }
    public float LastOnWallLeftTime { get; private set; }
    public float LastPressedJumpTime { get; private set; }
    public float LastPressedDashTime { get; private set; }
    public bool IsFacingRight { get; private set; }



    void Start()
    {
        currentState = PlayerState.Idle;
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        IsFacingRight = true;
        tempRunDeccel = Data.runDeccelAmount;
        currentDeccelAmount = Data.runDeccelAmount;
    }
    private void Update()
    {
        TimeHandler();//

        HandleFastRunTimer();
        CheckDirectionChange();

        
        GroundCheck();//
        LandingHandler();//
        InputHandler();


        
        

        JumpHandler();
        FallLandRiseHandler();
        IdleRunHandler();
        AttackHandler();
        Gravity();//
    }
    private void FixedUpdate()
    {
        if (!StateControl(PlayerState.Dash) && currentState != PlayerState.Attack && !isAttacking)
        {
            if (StateControl(PlayerState.WallJumping))
                Run(Data.wallJumpRunLerp);
            else
                Run(dynamicRun);
        }
        /*else if (_isDashAttacking)
        {
            Run(Data.dashEndRunLerp);
        }
        */
    }

    void InputHandler()
    {

        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        if (moveInput.x != 0)
        {
           // _lastMoveInput = moveInput;
            CheckDirectionToFace(moveInput.x > 0);
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.J))
        {
            Debug.Log("JumpKey");
            OnJumpInput();
        }

        if (Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.C) || Input.GetKeyUp(KeyCode.J))
        {
            OnJumpUpInput();
        }
        /*
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
           // ChangeState(PlayerState.Dash);
        */
        
        if ((Input.GetButtonDown("Fire1") || (Input.GetButton("Fire1")) && attackTimer <= 0))
        {
            
            if (CanBasicAttack())
            {
                Debug.Log(CanBasicAttack());
                currentState =PlayerState.Attack;
            }
        }
    }
    void IdleRunHandler()
    {
        if (currentState == PlayerState.Attack||isAttacking)
            return;


        if (Mathf.Abs(moveInput.x)==0&&rb.linearVelocity.y==0&&rb.linearVelocity.y==0&&!isAttacking)
        {
            currentState= PlayerState.Idle;
            Idle();
        }
        else if(Mathf.Abs(moveInput.x)>0&&StateControl(PlayerState.Idle)&&!isAttacking)
        {
            currentState = PlayerState.Run;
        }
    }
    void FallLandRiseHandler()
    {
        if (StateControl(PlayerState.Falling) && isGrounded)
        {
            animator.SetBool("isFalling", false);
            animator.SetTrigger("LandingT");
            currentState = PlayerState.Idle;
        }
        if (rb.linearVelocity.y < 0)
        {
            currentState = PlayerState.Falling;
            animator.SetBool("isFalling", true);
            animator.SetBool("isRising", false);

        }
        if (StateControl(PlayerState.Jumping) && rb.linearVelocity.y > 0)
        {
            animator.SetBool("isFalling", false);
            animator.SetBool("isRising", true);
            currentState = PlayerState.Rising;
        }
    }
    void TimeHandler()
    {
        #region TIMERS
        LastOnGroundTime -= Time.deltaTime;
        LastOnWallTime -= Time.deltaTime;
        LastOnWallRightTime -= Time.deltaTime;
        LastOnWallLeftTime -= Time.deltaTime;

        LastPressedJumpTime -= Time.deltaTime;
        LastPressedDashTime -= Time.deltaTime;
        attackTimer -= Time.deltaTime;
        #endregion
    }
    private void ResetTriggers()
    {
        animator.ResetTrigger("LandingT");
        animator.ResetTrigger("AttackT");
        animator.ResetTrigger("JumpingT");
        // Ýstersen baþka triggerlarý da ekleyebilirsin
    }
    void LandingHandler()
    {
        if (isGrounded)
        {
            if ((currentState == PlayerState.Attack || currentState == PlayerState.Jumping || currentState == PlayerState.Falling || currentState == PlayerState.WallJumping) && !StateControl(PlayerState.Landing))
            {
                currentState = PlayerState.Landing;
                Landing();
            }
            else if (currentState == PlayerState.Landing)
            {
                // Ýniþten sonra Idle veya Run'a geç
                if (Mathf.Abs(moveInput.x) == 0)
                {
                    currentState = PlayerState.Idle;
                    Idle();
                }
                else
                {
                    currentState = PlayerState.Run;
                }
            }
        }
    }
    private void Landing()
    {
        animator.SetBool("isFalling", false);
        animator.SetTrigger("LandingT");
        //currentState = PlayerState.Idle;
    }
    void Gravity()
    {

            if (rb.linearVelocity.y < 0 && moveInput.y < 0)
            {
                //Much higher gravity if holding down
                SetGravityScale(Data.gravityScale * Data.fastFallGravityMult);
                //Caps maximum fall speed, so when falling over large distances we don't accelerate to insanely high speeds
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -Data.maxFastFallSpeed));
            }
            else if (_isJumpCut)
            {
                //Higher gravity if jump button released
                SetGravityScale(Data.gravityScale * Data.jumpCutGravityMult);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -Data.maxFallSpeed));
            }
            else if ((currentState == PlayerState.Jumping || currentState == PlayerState.WallJumping || currentState == PlayerState.JumpFalling) && Mathf.Abs(rb.linearVelocity.y) < Data.jumpHangTimeThreshold)
            {
                SetGravityScale(Data.gravityScale * Data.jumpHangGravityMult);
            }
            else if (rb.linearVelocity.y < 0)
            {
                //Higher gravity if falling
                SetGravityScale(Data.gravityScale * Data.fallGravityMult);
            //Caps maximum fall speed, so when falling over large distances we don't accelerate to insanely high speeds
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -Data.maxFallSpeed));
            }
            else
            {
                //Default gravity if standing on a platform or moving upwards
                SetGravityScale(Data.gravityScale);
            }
    }
    void Idle()
    {
        if(StateControl(PlayerState.Idle))
        {
            ResetTriggers();
            animator.SetInteger("moving", -1);
        }
    }
    void JumpHandler()
    {
        if(currentState!=PlayerState.Dash)
        { 
            if (currentState==PlayerState.Jumping && rb.linearVelocity.y < 0)
            {
               // IsJumping = false;
               currentState = PlayerState.Falling;
                _isJumpFalling = true;
            }

            if (currentState == PlayerState.WallJumping && Time.time - _wallJumpStartTime > Data.wallJumpTime)
            {
                currentState = PlayerState.Falling;
               // IsWallJumping = false;
            }

            if (LastOnGroundTime > 0 && currentState != PlayerState.Jumping && currentState != PlayerState.WallJumping)
            {
                _isJumpCut = false;

                _isJumpFalling = false;
            }
            //Jump
            if (CanJump() && LastPressedJumpTime > 0)
            {
                //jump change  state
                //IsJumping = true;
                currentState =PlayerState.Jumping;
                _isJumpCut = false;
                _isJumpFalling = false;
                Jump();

                //AnimHandler.startedJumping = true;
            }
            //WALL JUMP
            else if (CanWallJump() && LastPressedJumpTime > 0)
            {
                //IsWallJumping = true;
                // IsJumping = false;
                currentState = PlayerState.WallJumping;
                _isJumpCut = false;
                _isJumpFalling = false;

                _wallJumpStartTime = Time.time;
                _lastWallJumpDir = (LastOnWallRightTime > 0) ? -1 : 1;

                //WallJump(_lastWallJumpDir);
            }
        }
    }
    void AttackHandler()
    {
        if (StateControl(PlayerState.Attack)&&CanBasicAttack()&&!isAttacking)
        {
            ResetTriggers();//animator clean

            isAttacking = true;
            rb.linearVelocity = Vector2.zero;
            attackTimer = attackDelay;
            attackIndex++;

            animator.SetInteger("AttackComboCount", attackIndex);
            animator.SetTrigger("AttackT");

            // Ýleri itme kuvveti combo adýmýna göre deðiþiyor
            float attackPushForce = GetAttackPushForce(attackIndex);
            Vector2 pushDirection = new Vector2(transform.localScale.x * attackPushForce, 0);
            rb.AddForce(pushDirection, ForceMode2D.Impulse);

            if (attackIndex >= maxComboCount)
                attackIndex = 0;

            StartCoroutine("BasicAttackAnimWait");
        }
    }

    IEnumerator BasicAttackAnimWait()
    {
        Debug.Log(isAttacking);
        yield return new WaitForSeconds(attackDelay);
        rb.linearVelocity = Vector2.zero;
        currentState = PlayerState.Idle;
        isAttacking = false;
    }
    bool CanBasicAttack()
    {
        if(isGrounded&&attackTimer<=0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private float GetAttackPushForce(int comboStep)
    {
        switch (comboStep)
        {
            case 1:
                return 10f; // 1. saldýrý adýmý
            case 2:
                return 20f; // 2. saldýrý adýmý daha güçlü
            case 3:
                return 30f; // 3. saldýrý adýmý en güçlü
            default:
                return 40f; // Hata olursa varsayýlan
        }
    }
    public void BasicAttackTargeted()
    {
        Debug.Log("AttackTargeted");
    }


    public void OnJumpInput()
    {
        LastPressedJumpTime = Data.jumpInputBufferTime;
    }

    public void OnJumpUpInput()
    {
        if (CanJumpCut() || CanWallJumpCut())
            _isJumpCut = true;
    }


    private void SetGravityScale(float v)
    {
        rb.gravityScale = v;
    }


    private bool CanJump()
    {
        return LastOnGroundTime > 0 && currentState != PlayerState.Jumping;
    }

    private bool CanWallJump()
    {
        return LastPressedJumpTime > 0 && LastOnWallTime > 0 && LastOnGroundTime <= 0 && (currentState != PlayerState.WallJumping ||
             (LastOnWallRightTime > 0 && _lastWallJumpDir == 1) || (LastOnWallLeftTime > 0 && _lastWallJumpDir == -1));
    }

    private bool CanJumpCut()
    {
        return currentState==PlayerState.Jumping && rb.linearVelocity.y > 0;
    }
    private bool CanWallJumpCut()
    {
        return currentState==PlayerState.WallJumping && rb.linearVelocity.y > 0;
    }
    private void Jump()
    {
        animator.SetTrigger("JumpingT");
        //Ensures we can't call Jump multiple times from one press
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;
        float force = Data.jumpForce;
        if (rb.linearVelocity.y < 0)
            force -= rb.linearVelocity.y;

        rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);
    }
    void GroundCheck()
    {
        //Ground Check
        isGrounded = Physics2D.OverlapBox(groundCheckPoint.position, groundCheckSize, 0, groundLayer);
        if (isGrounded) //checks if set box overlaps with ground
        {
            if (LastOnGroundTime < -0.1f)
            {
                //AnimHandler.justLanded = true;
            }

            LastOnGroundTime = Data.coyoteTime; //if so sets the lastGrounded to coyoteTime
        }
        animator.SetBool("isGround", isGrounded);
    }


    public void CheckDirectionToFace(bool isMovingRight)
    {
        if (isMovingRight != IsFacingRight)
            Turn();
    }
    private void Turn()
    {
        //stores scale and flips the player along the x axis, 
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;

        IsFacingRight = !IsFacingRight;
    }


    private void Run(float lerpAmount)
    {
        if (StateControl(PlayerState.Run)&&!isAttacking)
        {
            animator.SetInteger("moving", 1);
            //Calculate the direction we want to move in and our desired velocity
            float targetSpeed = moveInput.x * Data.runMaxSpeed;
            //We can reduce are control using Lerp() this smooths changes to are direction and speed
            targetSpeed = Mathf.Lerp(rb.linearVelocity.x, targetSpeed, lerpAmount);

            #region Calculate AccelRate
            float accelRate;

            //Gets an acceleration value based on if we are accelerating (includes turning) 
            //or trying to decelerate (stop). As well as applying a multiplier if we're air borne.
            if (LastOnGroundTime > 0)
                accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount : Data.runDeccelAmount;
            else
                accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount * Data.accelInAir : Data.runDeccelAmount * Data.deccelInAir;
            #endregion

            #region Add Bonus Jump Apex Acceleration
            //Increase are acceleration and maxSpeed when at the apex of their jump, makes the jump feel a bit more bouncy, responsive and natural
            if ((StateControl(PlayerState.Jumping) || StateControl(PlayerState.WallJumping) || _isJumpFalling) && Mathf.Abs(rb.linearVelocity.y) < Data.jumpHangTimeThreshold)
            {
                accelRate *= Data.jumpHangAccelerationMult;
                targetSpeed *= Data.jumpHangMaxSpeedMult;
            }
            #endregion

            #region Conserve Momentum
            //We won't slow the player down if they are moving in their desired direction but at a greater speed than their maxSpeed
            if (Data.doConserveMomentum && Mathf.Abs(rb.linearVelocity.x) > Mathf.Abs(targetSpeed) && Mathf.Sign(rb.linearVelocity.x) == Mathf.Sign(targetSpeed) && Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
            {
                //Prevent any deceleration from happening, or in other words conserve are current momentum
                //You could experiment with allowing for the player to slightly increae their speed whilst in this "state"
                accelRate = 0;
            }
            #endregion

            //Calculate difference between current velocity and desired velocity
            float speedDif = targetSpeed - rb.linearVelocity.x;
            //Calculate force along x-axis to apply to thr player

            float movement = speedDif * accelRate;

            //Convert this to a vector and apply to rigidbody
            rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
        }
    }
    private void HandleFastRunTimer()
    {
        // Eðer karakter koþuyorsa (moveInput != 0) ve yerdeyse
        if (Mathf.Abs(moveInput.x) > 0.01f && isGrounded && rb.linearVelocityX != 0)
        {
            if (fastRunTimer >= fastRunClampTime)
            {
                currentDeccelAmount = fastRunSliding;
                fastRunExitTimer = 0f;
                Data.runDeccelAmount = fastRunSliding;
                fastRunMode = true; // 1.5 saniyeyi geçtiyse hýzlý koþmaya gir
            }
            else
            {
                fastRunTimer += Time.deltaTime; // Zamaný artýr
            }
        }
        else
        {
            if (fastRunMode) // Eðer hýzlý koþma modundaysa
            {
                fastRunExitTimer += Time.deltaTime;
                if (fastRunExitTimer >= fastRunExitDelay)
                {
                    fastRunMode = false;
                    fastRunTimer = 0f;
                    fastRunExitTimer = 0f;
                }
            }
            else
            {
                Data.runDeccelAmount = tempRunDeccel;
                currentDeccelAmount = tempRunDeccel;
                fastRunTimer = 0f;
                fastRunMode = false;
            }


        }
    }
    private void CheckDirectionChange()
    {
        if (Mathf.Abs(moveInput.x) > 0.01f&&Mathf.Abs(rb.linearVelocity.x)>0) // Hareket ediyor muyuz
        {
            if (Mathf.Sign(moveInput.x) != Mathf.Sign(lastMoveDirection.x) && lastMoveDirection.x != 0 && fastRunMode)
            {
                if (applySlowDown == false)
                {
                    StartCoroutine("SlowDownEffectCoroutine");
                }
            }

            lastMoveDirection = moveInput;
        }
    }

    private IEnumerator SlowDownEffectCoroutine()
    {
        applySlowDown = true;

        float elapsedTime = 0f;
        dynamicRun = 1f;
        while (elapsedTime < slowDownDuration)
        {
            float t = elapsedTime / slowDownDuration;
            float lerpSpeed = Mathf.Lerp(slowDownFactor, 1f, t); // yavaþ -> normal hýz
            dynamicRun = lerpSpeed;

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        applySlowDown = false;
    }
    bool StateControl(PlayerState value)
    {
        return currentState == value;
    }
}
