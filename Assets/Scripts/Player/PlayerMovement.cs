/*
	Created by @DawnosaurDev at youtube.com/c/DawnosaurStudios
	Thanks so much for checking this out and I hope you find it helpful! 
	If you have any further queries, questions or feedback feel free to reach out on my twitter or leave a comment on youtube :D

	Feel free to use this in your own games, and I'd love to see anything you make!
 */

using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;



public enum PlayerState
{
    Idle,
    Run,
    Jumping,
    Rising,
    Falling,
    Landing,
	Dash,
    Attack
}


public class PlayerMovement : MonoBehaviour
{
	//Scriptable object which holds all the player's movement parameters. If you don't want to use it
	//just paste in all the parameters, though you will need to manuly change all references in this script
	public PlayerData Data;
    private Animator animator;

    public PlayerState currentState;
    private PlayerState lastState;
    #region COMPONENTS
    public Rigidbody2D RB { get; private set; }
    //Script to handle all player animations, all references can be safely removed if you're importing into your own project.
    //public PlayerAnimator AnimHandler { get; private set; }
    #endregion

    #region STATE PARAMETERS
    //Variables control the various actions the player can perform at any time.
    //These are fields which can are public allowing for other sctipts to read them
    public bool canDash = true;
   // private bool isDashing = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;

    //but can only be privately written to.
    public bool IsFacingRight { get; private set; }
	public bool IsJumping { get; private set; }
	public bool IsWallJumping { get; private set; }
	public bool IsDashing { get; private set; }
	public bool IsSliding { get; private set; }

	//Timers (also all fields, could be private and a method returning a bool could be used)
	public float LastOnGroundTime { get; private set; }
	public float LastOnWallTime { get; private set; }
	public float LastOnWallRightTime { get; private set; }
	public float LastOnWallLeftTime { get; private set; }

	//Jump
	private bool _isJumpCut;
	private bool _isJumpFalling;

	//Wall Jump
	private float _wallJumpStartTime;
	private int _lastWallJumpDir;

	//Dash
	private int _dashesLeft;
	private bool _dashRefilling;
	private Vector2 _lastDashDir;
	private bool _isDashAttacking;

	#endregion

	#region INPUT PARAMETERS
    public bool fastRunMode=false;
    public float fastRunTimer = 0f;
    public float fastRunClampTime = 1.5f;
    public float fastRunSliding = 0.5f;
    private float tempRunDeccel;
    private float fastRunExitTimer = 0f; // Ekstra değişken: koşmayı bırakınca sayaç başlasın
    private float fastRunExitDelay = 0.2f; // 0.2 saniye bekleyecek

    //
    public bool isChangingDirection = false;
    private float slowDownDuration =0.2f;
    private float slowDownFactor = 0.01f; // Ne kadar yavaşlasın (1 = normal hız, 0.3 = %30 hız)
    public bool applySlowDown = false;
    public float dynamicRun = 1f;
    //Attack
    public bool isAttacking = false;
    public int attackIndex = 0;
    public float attackTimer = 0f;
    public float attackDelay = 0.4f; // Her saldırıdan sonra bekleme süresi
    public bool attackQueued = false;
    private int maxComboCount = 2;
    public bool basicAttackAviable=false;



    private Vector2 _moveInput;
    private Vector2 _lastMoveInput;
    private bool isGrounded;
    public float LastPressedJumpTime { get; private set; }
	public float LastPressedDashTime { get; private set; }
	#endregion

	#region CHECK PARAMETERS
	//Set all of these up in the inspector
	[Header("Checks")] 
	[SerializeField] private Transform _groundCheckPoint;
	//Size of groundCheck depends on the size of your character generally you want them slightly small than width (for ground) and height (for the wall check)
	[SerializeField] private Vector2 _groundCheckSize = new Vector2(0.49f, 0.03f);
	[Space(5)]
	[SerializeField] private Transform _frontWallCheckPoint;
	[SerializeField] private Transform _backWallCheckPoint;
	[SerializeField] private Vector2 _wallCheckSize = new Vector2(0.5f, 1f);
    #endregion

    #region LAYERS & TAGS
    [Header("Layers & Tags")]
	[SerializeField] private LayerMask _groundLayer;
    private Vector2 lastMoveDirection;
    
    #endregion

    private void Awake()
	{
		RB = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        currentState = PlayerState.Idle;
        tempRunDeccel = Data.runDecceleration;
        //AnimHandler = GetComponent<PlayerAnimator>();
    }

	private void Start()
	{
		SetGravityScale(Data.gravityScale);
		IsFacingRight = true;
	}

	private void Update()
	{
		#region TIMERS
		TimeHandler();
		#endregion
		#region INPUT HANDLER
		InputHandler();
        #endregion
        AttackHandlerBasic();
        CheckDirectionChange();
        #region FastRunMode
        HandleFastRunTimer();
        #endregion
        #region COLLISION CHECKS
        CollisionHandler();
		#endregion
		#region JUMP CHECKS
		JumpHandler();
        #endregion
        #region DASH CHECKS
		//DashHandler();
		#endregion
		#region SLIDE CHECKS
		SlideHandler();
        #endregion
        #region GRAVITY
        GravityHandler();

        UpdateStateMachine();
        #endregion
    }

	private void UpdateStateMachine()
    {
        GroundCheck();
        BasicAttackCheck();
        //Turn(moveInput);
        if (!isGrounded && RB.linearVelocity.y < -0.02f &&
            currentState != PlayerState.Falling &&
            currentState != PlayerState.Jumping &&
            currentState != PlayerState.Rising &&
            currentState != PlayerState.Landing)
        {
            ChangeState(PlayerState.Falling);
            return; // diğer durumlara geçmesin artık
        }

        switch (currentState)
        {
            case PlayerState.Idle:
                if (Mathf.Abs(_moveInput.x) > 0.01f)
                    ChangeState(PlayerState.Run);

                if (Input.GetButtonDown("Jump") && isGrounded)
                    ChangeState(PlayerState.Jumping);
                break;

            case PlayerState.Run:
               if (Input.GetButtonDown("Fire1") && isGrounded)
                    ChangeState(PlayerState.Attack);

                if (Mathf.Abs(_moveInput.x) <= 0.01f)
                    ChangeState(PlayerState.Idle);
                break;

            case PlayerState.Jumping:
                if (RB.linearVelocity.y > 0.01f)
                    ChangeState(PlayerState.Rising);
                break;

            case PlayerState.Rising:
                if (RB.linearVelocity.y <= 0)
                    ChangeState(PlayerState.Falling);
                break;

            case PlayerState.Falling:
                if (isGrounded)
                    ChangeState(PlayerState.Landing);
                break;
            case PlayerState.Attack:
                if(!isAttacking&&basicAttackAviable)
                {
                    if (_moveInput.x != 0)
                    {
                        ChangeState(PlayerState.Run);
                    }
                    else
                    {
                        ChangeState(PlayerState.Idle);
                    }
                }
                else
                {
                    ChangeState(lastState);
                }

            break;

            case PlayerState.Landing:
                if (isGrounded && Mathf.Abs(RB.linearVelocity.y) < 0.01f)
                    ChangeState(Mathf.Abs(_moveInput.x) > 0.01f ? PlayerState.Run : PlayerState.Idle);
                break;
            case PlayerState.Dash:
                if (!IsDashing)
                {
                    Dash();
                }
                break;
        }
    }

    private void ChangeState(PlayerState newState)
    {
        lastState = currentState;
        currentState = newState;

        switch (newState)
        {
            case PlayerState.Idle:
                animator.SetInteger("moving", -1);
                break;
            case PlayerState.Run:
                animator.SetInteger("moving", 1);
                break;
            case PlayerState.Jumping:
                animator.SetTrigger("JumpingT");
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
            case PlayerState.Dash:
                animator.SetTrigger("DashT"); // varsa dash animasyonu
                break;



        }
    }

    void GroundCheck()
	{
		//Ground Check
		isGrounded = Physics2D.OverlapBox(_groundCheckPoint.position, _groundCheckSize, 0, _groundLayer);
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
    void BasicAttackCheck()
    {
        basicAttackAviable = isGrounded;
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
        #endregion
    }
	void SlideHandler()
	{
        if (CanSlide() && ((LastOnWallLeftTime > 0 && _moveInput.x < 0) || (LastOnWallRightTime > 0 && _moveInput.x > 0)))
            IsSliding = true;
        else
            IsSliding = false;
    }
    /*
	void DashHandler()
	{
        if (CanDash() && LastPressedDashTime > 0)
        {
            //Freeze game for split second. Adds juiciness and a bit of forgiveness over directional input
            Sleep(Data.dashSleepTime);
			
            //If not direction pressed, dash forward
            if (_moveInput != Vector2.zero)
                _lastDashDir = _moveInput;
            else
                _lastDashDir = IsFacingRight ? Vector2.right : Vector2.left;



            IsDashing = true;
            IsJumping = false;
            IsWallJumping = false;
            _isJumpCut = false;

            //StartCoroutine(nameof(StartDash), _lastDashDir);
        }
    }
    */

    private void HandleFastRunTimer()
    {
        // Eğer karakter koşuyorsa (moveInput != 0) ve yerdeyse
        if (Mathf.Abs(_moveInput.x) > 0.01f && isGrounded&&RB.linearVelocityX!=0)
        {
            if (fastRunTimer >= fastRunClampTime)
            {
                fastRunExitTimer = 0f;
                Data.runDeccelAmount = fastRunSliding;
                fastRunMode = true; // 1.5 saniyeyi geçtiyse hızlı koşmaya gir
            }
            else
            {
                fastRunTimer += Time.deltaTime; // Zamanı artır
            }
        }
        else
        {
            if (fastRunMode) // Eğer hızlı koşma modundaysa
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
                fastRunTimer = 0f;
                fastRunMode = false;
            }


        }
    }

    private void CheckDirectionChange()
    {
        if (Mathf.Abs(_moveInput.x) > 0.01f) // Hareket ediyor muyuz
        {
            if (Mathf.Sign(_moveInput.x) != Mathf.Sign(lastMoveDirection.x) && lastMoveDirection.x != 0&&fastRunMode)
            {
                if (applySlowDown == false)
                {
                    StartCoroutine("SlowDownEffectCoroutine");
                }
            }

            lastMoveDirection = _moveInput;
        }
    }

    private IEnumerator SlowDownEffectCoroutine()
    {
        applySlowDown = true;

        float elapsedTime = 0f;

        while (elapsedTime < slowDownDuration)
        {
            float t = elapsedTime / slowDownDuration;
            float lerpSpeed = Mathf.Lerp(slowDownFactor, 1f, t); // yavaş -> normal hız
            dynamicRun = lerpSpeed;

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        dynamicRun = 1f;
        applySlowDown = false;
    }

    void JumpHandler()
	{
        if (IsJumping && RB.linearVelocity.y < 0)
        {
            IsJumping = false;

            _isJumpFalling = true;
        }

        if (IsWallJumping && Time.time - _wallJumpStartTime > Data.wallJumpTime)
        {
            IsWallJumping = false;
        }

        if (LastOnGroundTime > 0 && !IsJumping && !IsWallJumping)
        {
            _isJumpCut = false;

            _isJumpFalling = false;
        }
        if (!IsDashing)
        {
            //Jump
            if (CanJump() && LastPressedJumpTime > 0)
            {
				//jump change  state
                ChangeState(PlayerState.Jumping);
                IsJumping = true;
                IsWallJumping = false;
                _isJumpCut = false;
                _isJumpFalling = false;
                Jump();

                //AnimHandler.startedJumping = true;
            }
            //WALL JUMP
            else if (CanWallJump() && LastPressedJumpTime > 0)
            {
                IsWallJumping = true;
                IsJumping = false;
                _isJumpCut = false;
                _isJumpFalling = false;

                _wallJumpStartTime = Time.time;
                _lastWallJumpDir = (LastOnWallRightTime > 0) ? -1 : 1;

                WallJump(_lastWallJumpDir);
            }
        }
    }
	void CollisionHandler()
	{
        if (!IsDashing && !IsJumping)
        {



            //Right Wall Check
            if (((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && IsFacingRight)
                    || (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !IsFacingRight)) && !IsWallJumping)
                LastOnWallRightTime = Data.coyoteTime;

            //Right Wall Check
            if (((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !IsFacingRight)
                || (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && IsFacingRight)) && !IsWallJumping)
                LastOnWallLeftTime = Data.coyoteTime;

            //Two checks needed for both left and right walls since whenever the play turns the wall checkPoints swap sides
            LastOnWallTime = Mathf.Max(LastOnWallLeftTime, LastOnWallRightTime);
        }
    }
	void InputHandler()
	{
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");

        if (_moveInput.x != 0)
        {
            _lastMoveInput = _moveInput;
            CheckDirectionToFace(_moveInput.x > 0);
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

        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
            ChangeState(PlayerState.Dash);

        if ((Input.GetButtonDown("Fire1") || (Input.GetButton("Fire1") && attackTimer <= 0)))
        {
            if (basicAttackAviable)
            {
                ChangeState(PlayerState.Attack);
                StartAttack();
                
            }
        }
    }
    void GravityHandler()
	{
        #region GRAVITY
        if (!_isDashAttacking)
        {
            //Higher gravity if we've released the jump input or are falling
            if (IsSliding)
            {
                SetGravityScale(0);
            }
            else if (RB.linearVelocity.y < 0 && _moveInput.y < 0)
            {
                //Much higher gravity if holding down
                SetGravityScale(Data.gravityScale * Data.fastFallGravityMult);
                //Caps maximum fall speed, so when falling over large distances we don't accelerate to insanely high speeds
                RB.linearVelocity = new Vector2(RB.linearVelocity.x, Mathf.Max(RB.linearVelocity.y, -Data.maxFastFallSpeed));
            }
            else if (_isJumpCut)
            {
                //Higher gravity if jump button released
                SetGravityScale(Data.gravityScale * Data.jumpCutGravityMult);
                RB.linearVelocity = new Vector2(RB.linearVelocity.x, Mathf.Max(RB.linearVelocity.y, -Data.maxFallSpeed));
            }
            else if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(RB.linearVelocity.y) < Data.jumpHangTimeThreshold)
            {
                SetGravityScale(Data.gravityScale * Data.jumpHangGravityMult);
            }
            else if (RB.linearVelocity.y < 0)
            {
                //Higher gravity if falling
                SetGravityScale(Data.gravityScale * Data.fallGravityMult);
                //Caps maximum fall speed, so when falling over large distances we don't accelerate to insanely high speeds
                RB.linearVelocity = new Vector2(RB.linearVelocity.x, Mathf.Max(RB.linearVelocity.y, -Data.maxFallSpeed));
            }
            else
            {
                //Default gravity if standing on a platform or moving upwards
                SetGravityScale(Data.gravityScale);
            }
        }
        else
        {
            //No gravity when dashing (returns to normal once initial dashAttack phase over)
            SetGravityScale(0);
        }
        #endregion
    }
    private void FixedUpdate()
	{
		//Handle Run
		if (!IsDashing&&currentState!=PlayerState.Attack&&!isAttacking)
		{
			if (IsWallJumping)
				Run(Data.wallJumpRunLerp);
			else
				Run(dynamicRun);
		}
		else if (_isDashAttacking)
		{
			Run(Data.dashEndRunLerp);
		}

		//Handle Slide
		if (IsSliding)
			Slide();
    }





    #region INPUT CALLBACKS
	//Methods which whandle input detected in Update()
    public void OnJumpInput()
	{
		LastPressedJumpTime = Data.jumpInputBufferTime;
	}

	public void OnJumpUpInput()
	{
		if (CanJumpCut() || CanWallJumpCut())
			_isJumpCut = true;
	}

	public void OnDashInput()
	{
		LastPressedDashTime = Data.dashInputBufferTime;
	}
    #endregion

    #region GENERAL METHODS
    public void SetGravityScale(float scale)
	{
		RB.gravityScale = scale;
	}

	private void Sleep(float duration)
    {
		//Method used so we don't need to call StartCoroutine everywhere
		//nameof() notation means we don't need to input a string directly.
		//Removes chance of spelling mistakes and will improve error messages if any
		StartCoroutine(nameof(PerformSleep), duration);
    }

	private IEnumerator PerformSleep(float duration)
    {
		Time.timeScale = 0;
		yield return new WaitForSecondsRealtime(duration); //Must be Realtime since timeScale with be 0 
		Time.timeScale = 1;
	}
    #endregion

	//MOVEMENT METHODS
    #region RUN METHODS
    private void Run(float lerpAmount)
	{
		//Calculate the direction we want to move in and our desired velocity
		float targetSpeed = _moveInput.x * Data.runMaxSpeed;
		//We can reduce are control using Lerp() this smooths changes to are direction and speed
		targetSpeed = Mathf.Lerp(RB.linearVelocity.x, targetSpeed, lerpAmount);

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
		if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(RB.linearVelocity.y) < Data.jumpHangTimeThreshold)
		{
			accelRate *= Data.jumpHangAccelerationMult;
			targetSpeed *= Data.jumpHangMaxSpeedMult;
		}
		#endregion

		#region Conserve Momentum
		//We won't slow the player down if they are moving in their desired direction but at a greater speed than their maxSpeed
		if(Data.doConserveMomentum && Mathf.Abs(RB.linearVelocity.x) > Mathf.Abs(targetSpeed) && Mathf.Sign(RB.linearVelocity.x) == Mathf.Sign(targetSpeed) && Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
		{
			//Prevent any deceleration from happening, or in other words conserve are current momentum
			//You could experiment with allowing for the player to slightly increae their speed whilst in this "state"
			accelRate = 0; 
		}
		#endregion

		//Calculate difference between current velocity and desired velocity
		float speedDif = targetSpeed - RB.linearVelocity.x;
		//Calculate force along x-axis to apply to thr player

		float movement = speedDif * accelRate;

		//Convert this to a vector and apply to rigidbody
		RB.AddForce(movement * Vector2.right, ForceMode2D.Force);
	}

	private void Turn()
	{
		//stores scale and flips the player along the x axis, 
		Vector3 scale = transform.localScale; 
		scale.x *= -1;
		transform.localScale = scale;

		IsFacingRight = !IsFacingRight;
	}
    #endregion

    #region JUMP METHODS
    private void Jump()
	{
		//Ensures we can't call Jump multiple times from one press
		LastPressedJumpTime = 0;
		LastOnGroundTime = 0;

		#region Perform Jump
		//We increase the force applied if we are falling
		//This means we'll always feel like we jump the same amount 
		//(setting the player's Y velocity to 0 beforehand will likely work the same, but I find this more elegant :D)
		float force = Data.jumpForce;
		if (RB.linearVelocity.y < 0)
			force -= RB.linearVelocity.y;

		RB.AddForce(Vector2.up * force, ForceMode2D.Impulse);
		#endregion
	}

	private void WallJump(int dir)
	{
		//Ensures we can't call Wall Jump multiple times from one press
		LastPressedJumpTime = 0;
		LastOnGroundTime = 0;
		LastOnWallRightTime = 0;
		LastOnWallLeftTime = 0;

		#region Perform Wall Jump
		Vector2 force = new Vector2(Data.wallJumpForce.x, Data.wallJumpForce.y);
		force.x *= dir; //apply force in opposite direction of wall

		if (Mathf.Sign(RB.linearVelocity.x) != Mathf.Sign(force.x))
			force.x -= RB.linearVelocity.x;

		if (RB.linearVelocity.y < 0) //checks whether player is falling, if so we subtract the velocity.y (counteracting force of gravity). This ensures the player always reaches our desired jump force or greater
			force.y -= RB.linearVelocity.y;

		//Unlike in the run we want to use the Impulse mode.
		//The default mode will apply are force instantly ignoring masss
		RB.AddForce(force, ForceMode2D.Impulse);
		#endregion
	}
    #endregion

    #region DASH METHODS

    private void Dash()
    {
        
        //if (!canDash || currentState == PlayerState.Stun) return;
        if (!canDash) return;

        IsDashing = true;
        canDash = false;
       // playerIsMoveAble = false;
       /*
        if (isKnockback)
        {
            rb.velocity = Vector2.zero;
            StopCoroutine("KnockbackEffect");
            isKnockback = false;
        }*/

        Vector2 dashDir = _moveInput.x != 0 ? new Vector2(_moveInput.x, 0).normalized : _lastMoveInput.normalized;
        Vector2 startPos = RB.position;
        Vector2 targetPos = startPos + (dashDir * Data.dashForce * Data.dashDuration);

        RaycastHit2D hit = Physics2D.Raycast(startPos, dashDir, Data.dashForce * Data.dashDuration, LayerMask.GetMask("NoAccessZone"));
        if (hit.collider != null)
            targetPos = hit.point;

        RB.linearVelocity = dashDir * Data.dashForce;
        ChangeState(PlayerState.Dash);

        StartCoroutine(StopDash(targetPos));
    }

    private IEnumerator StopDash(Vector2 stopPos)
    {
        float time = 0f;

        while (time < Data.dashDuration)
        {
            if (Vector2.Distance(RB.position, stopPos) < 0.1f)
                break;

            time += Time.deltaTime;
            yield return null;
        }

        RB.linearVelocity = Vector2.zero;
        RB.position = stopPos;
        IsDashing = false;
       // playerIsMoveAble = true;

        ChangeState(PlayerState.Idle); // veya Run

        yield return new WaitForSeconds(Data.dashCooldown);
        canDash = true;
    }



    private void StartAttack()
    {
        RB.linearVelocity = Vector2.zero;
        isAttacking = true;
        attackTimer = attackDelay;
        attackIndex++;

        animator.SetInteger("AttackComboCount", attackIndex);
        animator.SetTrigger("AttackT");

        // İleri itme kuvveti combo adımına göre değişiyor
        float attackPushForce = GetAttackPushForce(attackIndex);
        Vector2 pushDirection = new Vector2(transform.localScale.x * attackPushForce, 0);
        RB.AddForce(pushDirection, ForceMode2D.Impulse);

        if (attackIndex >= maxComboCount)
            attackIndex = 0;
    }
    private float GetAttackPushForce(int comboStep)
    {
        switch (comboStep)
        {
            case 1:
                return 5f; // 1. saldırı adımı
            case 2:
                return 6.5f; // 2. saldırı adımı daha güçlü
            case 3:
                return 8f; // 3. saldırı adımı en güçlü
            default:
                return 5f; // Hata olursa varsayılan
        }
    }
    public void AttackTarget()
    {
        Debug.Log("AttackTargeted");
    }

    void AttackHandlerBasic()
    {
        if (basicAttackAviable)
        {
            if (isAttacking)
            {
                attackTimer -= Time.deltaTime;

                if (attackTimer <= 0)
                {
                    isAttacking = false;

                    // Eğer basılı tutmaya devam ediyorsa bir sonrakini sıraya al
                    if (Input.GetButton("Fire1"))
                    {
                        attackQueued = true;
                    }
                }
            }
            if (attackQueued && !isAttacking)
            {
                attackQueued = false;
                StartAttack();
            }
        }
    }






    //Dash Coroutine
    /*
	private IEnumerator StartDash(Vector2 dir)
	{
		//Overall this method of dashing aims to mimic Celeste, if you're looking for
		// a more physics-based approach try a method similar to that used in the jump

		LastOnGroundTime = 0;
		LastPressedDashTime = 0;

		float startTime = Time.time;

		_dashesLeft--;
		_isDashAttacking = true;

		SetGravityScale(0);

		//We keep the player's velocity at the dash speed during the "attack" phase (in celeste the first 0.15s)
		while (Time.time - startTime <= Data.dashAttackTime)
		{
			RB.linearVelocity = dir.normalized * Data.dashSpeed;
			//Pauses the loop until the next frame, creating something of a Update loop. 
			//This is a cleaner implementation opposed to multiple timers and this coroutine approach is actually what is used in Celeste :D
			yield return null;
		}

		startTime = Time.time;

		_isDashAttacking = false;

		//Begins the "end" of our dash where we return some control to the player but still limit run acceleration (see Update() and Run())
		SetGravityScale(Data.gravityScale);
		RB.linearVelocity = Data.dashEndSpeed * dir.normalized;

		while (Time.time - startTime <= Data.dashEndTime)
		{
			yield return null;
		}

		//Dash over
		IsDashing = false;
	}

	//Short period before the player is able to dash again
	private IEnumerator RefillDash(int amount)
	{
		//SHoet cooldown, so we can't constantly dash along the ground, again this is the implementation in Celeste, feel free to change it up
		_dashRefilling = true;
		yield return new WaitForSeconds(Data.dashRefillTime);
		_dashRefilling = false;
		_dashesLeft = Mathf.Min(Data.dashAmount, _dashesLeft + 1);
	}
    */
    #endregion

    #region OTHER MOVEMENT METHODS
    private void Slide()
	{
		//We remove the remaining upwards Impulse to prevent upwards sliding
		if(RB.linearVelocity.y > 0)
		{
		    RB.AddForce(-RB.linearVelocity.y * Vector2.up,ForceMode2D.Impulse);
		}
	
		//Works the same as the Run but only in the y-axis
		//THis seems to work fine, buit maybe you'll find a better way to implement a slide into this system
		float speedDif = Data.slideSpeed - RB.linearVelocity.y;	
		float movement = speedDif * Data.slideAccel;
		//So, we clamp the movement here to prevent any over corrections (these aren't noticeable in the Run)
		//The force applied can't be greater than the (negative) speedDifference * by how many times a second FixedUpdate() is called. For more info research how force are applied to rigidbodies.
		movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif)  * (1 / Time.fixedDeltaTime), Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));

		RB.AddForce(movement * Vector2.up);
	}
    #endregion


    #region CHECK METHODS
    public void CheckDirectionToFace(bool isMovingRight)
	{
		if (isMovingRight != IsFacingRight)
			Turn();
	}

    private bool CanJump()
    {
		return LastOnGroundTime > 0 && !IsJumping;
    }

	private bool CanWallJump()
    {
		return LastPressedJumpTime > 0 && LastOnWallTime > 0 && LastOnGroundTime <= 0 && (!IsWallJumping ||
			 (LastOnWallRightTime > 0 && _lastWallJumpDir == 1) || (LastOnWallLeftTime > 0 && _lastWallJumpDir == -1));
	}

	private bool CanJumpCut()
    {
		return IsJumping && RB.linearVelocity.y > 0;
    }

	private bool CanWallJumpCut()
	{
		return IsWallJumping && RB.linearVelocity.y > 0;
	}

	public bool CanSlide()
    {
		if (LastOnWallTime > 0 && !IsJumping && !IsWallJumping && !IsDashing && LastOnGroundTime <= 0)
			return true;
		else
			return false;
	}
    #endregion


    #region EDITOR METHODS
    private void OnDrawGizmosSelected()
    {
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(_groundCheckPoint.position, _groundCheckSize);
		Gizmos.color = Color.blue;
		Gizmos.DrawWireCube(_frontWallCheckPoint.position, _wallCheckSize);
		Gizmos.DrawWireCube(_backWallCheckPoint.position, _wallCheckSize);
	}
    #endregion
}

// created by Dawnosaur :D
