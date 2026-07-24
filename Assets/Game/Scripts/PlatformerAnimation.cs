using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;

public class PlatformerAnimation : MonoBehaviour
{
	public Transform animatedPlayerModel; //Visible model root. Used for facing and wall offsets.
	public Animator animatedPlayerAnimator;
	public RuntimeAnimatorController animatorController;
	public bool preferAnimator = true;

	public string idleState = "idle";
	public string walkState = "walk";
	[FormerlySerializedAs("runState")]
	public string sprintState = "sprint";
	public string dashState = "dash";
	public string jumpState = "jump";
	public string doubleJumpState = "leap";
	public string slideInState = "slide";
	public string slideOutState = "slide";
	public string wallState = "wall_grab";
	public string tauntState = "taunt";
	public string deathState = "die";

	public float idleSpeedThreshold = 0.1f;
	[FormerlySerializedAs("runSpeedThreshold")]
	public float sprintSpeedThreshold = 0.1f;
	public float crossFadeTime = 0.08f;
	[Tooltip("Fixed-duration blend, in seconds, used when entering jump, double-jump, and wall-jump animations.")]
	public float airborneCrossFadeTime = 0.02f;
	[Tooltip("Time, in seconds, skipped at the start of the regular and wall-jump animation.")]
	public float jumpAnimationStartTime = 0.25f;
	public float walkPlaybackScale = 0.075f;
	[Tooltip("Minimum walk playback speed while movement is pressed, preventing a zero-speed animation transition.")]
	public float minimumWalkPlaybackSpeed = 0.1f;
	[FormerlySerializedAs("tauntRotationY")]
	[Tooltip("Visual yaw offset applied while taunting and facing right.")]
	public float tauntRightRotationY = 90.0f;
	[Tooltip("Visual yaw offset applied while taunting and facing left.")]
	public float tauntLeftRotationY = 270.0f;
	[Tooltip("Degrees per second used to return the model to its normal yaw after taunting.")]
	public float tauntRotationReturnSpeed = 720.0f;
	public bool holdSlideWhileCrouching = true;
	public float slideHoldNormalizedTime = 0.5f;
	[Tooltip("Visual-only local offset applied to the animated model while sliding.")]
	public Vector3 slideModelOffset = new Vector3(0.0f, -1.26f, 0.0f);
	[Tooltip("Keeps animated ground-contact bones aligned with the physics ground.")]
	public bool groundLocomotionFeet = true;
	[Tooltip("Distance from the Floaty foot/toe bones to the bottom of the boot.")]
	public float footSoleOffset = 0.11f;
	[Tooltip("Distance from the lowest slide contact bone to the visible edge of the model.")]
	public float slideContactOffset = 0.04f;
	[Tooltip("Maximum visual grounding correction applied in one frame.")]
	public float maxFootGroundingAdjustment = 0.75f;
	[Tooltip("Additional visual-only vertical offset while standing idle.")]
	public float idleGroundingOffset = -0.03f;
	[Tooltip("Rotates the slide pose to follow the full angle of the supporting ground.")]
	public bool alignSlideToGroundSlope = true;
	[Tooltip("Leans the visible model toward the downhill side while moving on a slope.")]
	public bool leanWithGroundSlope = true;
	[Tooltip("Maximum visual lean at the steepest walkable ground angle.")]
	public float maxGroundSlopeLeanAngle = 8.0f;
	[Tooltip("Approximate time in seconds for slope lean to settle.")]
	public float groundSlopeLeanSmoothTime = 0.15f;
	[Tooltip("Visual prop shown only while the character is actively grabbing a wall.")]
	public GameObject wallGrabProp;

	Animation mLegacyAnimation;
	Rigidbody mRigidbody;
	PlatformerPhysics mPhysics;
	bool mPlayerDead = false;
	bool mIdle = false;
	bool mUseAnimator = false;
	bool mAnimatorPaused = false;
	bool mTaunting = false;
	float mAnimatorSpeedBeforePause = 1.0f;
	Vector3 mBaseModelLocalPosition = Vector3.zero;
	Quaternion mBaseModelLocalRotation = Quaternion.identity;
	Transform[] mFootGroundingPoints = new Transform[0];
	Transform[] mSlideGroundingPoints = new Transform[0];
	float mGroundSlopeLeanAngle = 0.0f;
	float mGroundSlopeLeanVelocity = 0.0f;
	string mCurrentAnimatorState = "";
	string mAirborneAnimatorState = "";

	bool mHasSpeedParameter = false;
	bool mHasNormalizedSpeedParameter = false;
	bool mHasGroundedParameter = false;
	bool mHasOnWallParameter = false;
	bool mHasCrouchingParameter = false;
	bool mHasSprintingParameter = false;
	bool mHasDeadParameter = false;

	static readonly int SpeedHash = Animator.StringToHash("Speed");
	static readonly int NormalizedSpeedHash = Animator.StringToHash("NormalizedSpeed");
	static readonly int GroundedHash = Animator.StringToHash("Grounded");
	static readonly int OnWallHash = Animator.StringToHash("OnWall");
	static readonly int CrouchingHash = Animator.StringToHash("Crouching");
	static readonly int SprintingHash = Animator.StringToHash("Sprinting");
	static readonly int DeadHash = Animator.StringToHash("Dead");

	string[] mRequiredLegacyAnimations = new string[] { "walk", "jump", "slidein", "slideout", "death", "onwall", "idle" };

	void Start ()
	{
		mRigidbody = GetComponent<Rigidbody>();
		mPhysics = GetComponent<PlatformerPhysics>();

		if (animatedPlayerModel == null)
		{
			Debug.LogError("The animated player model is not set.");
			enabled = false;
			return;
		}

		mBaseModelLocalPosition = animatedPlayerModel.transform.localPosition;
		mBaseModelLocalRotation = animatedPlayerModel.transform.localRotation;
		CacheFootGroundingPoints();
		SetWallGrabPropActive(false);

		if (preferAnimator && StartAnimatorMode())
			return;

		if (!StartLegacyAnimationMode())
			enabled = false;
	}

	bool StartAnimatorMode()
	{
		if (animatedPlayerAnimator == null)
			animatedPlayerAnimator = animatedPlayerModel.GetComponent<Animator>();

		if (animatedPlayerAnimator == null)
			return false;

		if (animatorController != null)
			animatedPlayerAnimator.runtimeAnimatorController = animatorController;

		if (animatedPlayerAnimator.runtimeAnimatorController == null)
		{
			Debug.LogError("The animated player model has an Animator, but no RuntimeAnimatorController is assigned.");
			return false;
		}

		animatedPlayerAnimator.applyRootMotion = false;
		animatedPlayerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		CacheAnimatorParameters();
		mUseAnimator = true;
		SetAnimatorBool(mHasDeadParameter, DeadHash, false);
		PlayAnimatorState(idleState, 0.0f, true);
		return true;
	}

	bool StartLegacyAnimationMode()
	{
		mLegacyAnimation = animatedPlayerModel.GetComponent<Animation>();
		if (mLegacyAnimation == null)
		{
			Debug.LogError("The animated player model needs an Animator with a controller, or a legacy Animation component.");
			return false;
		}

		if (!CheckLegacyAnimations())
		{
			Debug.LogError("The animated player model does not seem to have the appropriate legacy animations needed.");
			return false;
		}

		mLegacyAnimation["idle"].speed = 0;
		mLegacyAnimation.Play("idle");
		return true;
	}

	void CacheAnimatorParameters()
	{
		mHasSpeedParameter = HasAnimatorParameter("Speed", AnimatorControllerParameterType.Float);
		mHasNormalizedSpeedParameter = HasAnimatorParameter("NormalizedSpeed", AnimatorControllerParameterType.Float);
		mHasGroundedParameter = HasAnimatorParameter("Grounded", AnimatorControllerParameterType.Bool);
		mHasOnWallParameter = HasAnimatorParameter("OnWall", AnimatorControllerParameterType.Bool);
		mHasCrouchingParameter = HasAnimatorParameter("Crouching", AnimatorControllerParameterType.Bool);
		mHasSprintingParameter = HasAnimatorParameter("Sprinting", AnimatorControllerParameterType.Bool);
		mHasDeadParameter = HasAnimatorParameter("Dead", AnimatorControllerParameterType.Bool);
	}

	bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
	{
		if (animatedPlayerAnimator == null || animatedPlayerAnimator.runtimeAnimatorController == null)
			return false;

		AnimatorControllerParameter[] parameters = animatedPlayerAnimator.parameters;
		for (int i = 0; i < parameters.Length; i++)
		{
			if (parameters[i].name == parameterName && parameters[i].type == parameterType)
				return true;
		}

		return false;
	}

	bool CheckLegacyAnimations()
	{
		if (mLegacyAnimation == null)
			return false;

		for (int i = 0; i < mRequiredLegacyAnimations.Length; i++)
		{
			if (mLegacyAnimation[mRequiredLegacyAnimations[i]] == null)
			{
				Debug.LogError("Missing legacy animation clip '" + mRequiredLegacyAnimations[i] + "' on " + animatedPlayerModel.name + ".");
				return false;
			}
		}

		return true;
	}

	void Update ()
	{
		if (mUseAnimator)
		{
			UpdateAnimatorMode();
			return;
		}

		UpdateLegacyAnimationMode();
	}

	void LateUpdate()
	{
		if (animatedPlayerModel == null)
			return;

		float targetSlopeLeanAngle = GetTargetGroundSlopeLeanAngle();
		mGroundSlopeLeanAngle = Mathf.SmoothDampAngle(
			mGroundSlopeLeanAngle,
			targetSlopeLeanAngle,
			ref mGroundSlopeLeanVelocity,
			Mathf.Max(0.01f, groundSlopeLeanSmoothTime));

		Quaternion normalRotation =
			Quaternion.AngleAxis(mGroundSlopeLeanAngle, Vector3.forward) * mBaseModelLocalRotation;
		bool facingLeft = animatedPlayerModel.localScale.z < 0.0f;
		float tauntRotationY = facingLeft ? tauntLeftRotationY : tauntRightRotationY;
		Quaternion tauntRotation = mBaseModelLocalRotation * Quaternion.Euler(0.0f, tauntRotationY, 0.0f);
		if (mTaunting)
		{
			animatedPlayerModel.localRotation = tauntRotation;
		}
		else
		{
			animatedPlayerModel.localRotation = Quaternion.RotateTowards(
				animatedPlayerModel.localRotation,
				normalRotation,
				tauntRotationReturnSpeed * Time.deltaTime);
		}

		ApplyGroundedFootOffset();
	}

	float GetTargetGroundSlopeLeanAngle()
	{
		if (!mUseAnimator || mPhysics == null || mPlayerDead || mTaunting || !mPhysics.IsOnGround())
		{
			return 0.0f;
		}

		float groundSlopeAngle;
		if (!mPhysics.TryGetGroundSlopeAngle(out groundSlopeAngle))
			return 0.0f;

		if (mPhysics.IsCrouching() && IsSlideState())
			return alignSlideToGroundSlope ? groundSlopeAngle : 0.0f;

		if (!leanWithGroundSlope || !mPhysics.HasMovementInput() || !IsGroundedLocomotionState())
			return 0.0f;

		float slopeRatio = Mathf.Clamp(
			groundSlopeAngle / Mathf.Max(0.01f, mPhysics.maxGroundWalkingAngle),
			-1.0f,
			1.0f);
		return slopeRatio * maxGroundSlopeLeanAngle;
	}

	void CacheFootGroundingPoints()
	{
		List<Transform> footGroundingPoints = new List<Transform>();
		List<Transform> slideGroundingPoints = new List<Transform>();
		Transform[] modelTransforms = animatedPlayerModel.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < modelTransforms.Length; i++)
		{
			if (wallGrabProp != null && modelTransforms[i].IsChildOf(wallGrabProp.transform))
				continue;

			string transformName = modelTransforms[i].name;
			bool isFootContact = transformName == "LeftFoot" || transformName == "RightFoot" ||
				transformName == "LeftToeBase" || transformName == "RightToeBase" ||
				transformName == "LeftToeBase_end" || transformName == "RightToeBase_end";
			bool isHandContact = transformName == "LeftHand" || transformName == "RightHand" ||
				transformName == "LeftHand_end" || transformName == "RightHand_end";

			if (isFootContact)
				footGroundingPoints.Add(modelTransforms[i]);
			if (isFootContact || isHandContact)
				slideGroundingPoints.Add(modelTransforms[i]);
		}

		mFootGroundingPoints = footGroundingPoints.ToArray();
		mSlideGroundingPoints = slideGroundingPoints.ToArray();
	}

	void ApplyGroundedFootOffset()
	{
		bool sliding = mPhysics != null && mPhysics.IsCrouching() && IsSlideState();
		bool groundedLocomotion = mPhysics != null && !mPhysics.IsCrouching() && IsGroundedLocomotionState();
		Transform[] groundingPoints = sliding ? mSlideGroundingPoints : mFootGroundingPoints;
		if (!groundLocomotionFeet || !mUseAnimator || mPhysics == null ||
			!mPhysics.IsOnGround() || mPlayerDead || mTaunting ||
			groundingPoints.Length == 0 || (!sliding && !groundedLocomotion))
		{
			return;
		}

		float locomotionGroundHeight = 0.0f;
		if (!sliding && !mPhysics.TryGetGroundHeightAt(transform.position, out locomotionGroundHeight))
			return;

		float groundingAdjustment = float.NegativeInfinity;
		for (int i = 0; i < groundingPoints.Length; i++)
		{
			Transform groundingPoint = groundingPoints[i];
			float groundHeight = locomotionGroundHeight;
			if (sliding && !mPhysics.TryGetGroundHeightAt(groundingPoint.position, out groundHeight))
				continue;

			float contactOffset = sliding ? slideContactOffset : footSoleOffset;
			float contactHeight = groundingPoint.position.y - contactOffset;
			groundingAdjustment = Mathf.Max(groundingAdjustment, groundHeight - contactHeight);
		}

		if (float.IsNegativeInfinity(groundingAdjustment))
			return;

		groundingAdjustment = Mathf.Clamp(
			groundingAdjustment,
			-maxFootGroundingAdjustment,
			maxFootGroundingAdjustment);
		if (mCurrentAnimatorState == idleState)
			groundingAdjustment += idleGroundingOffset;

		animatedPlayerModel.position += Vector3.up * groundingAdjustment;
	}

	bool IsGroundedLocomotionState()
	{
		return mCurrentAnimatorState == idleState ||
			mCurrentAnimatorState == walkState ||
			mCurrentAnimatorState == sprintState ||
			mCurrentAnimatorState == dashState;
	}

	bool IsSlideState()
	{
		return mCurrentAnimatorState == slideInState || mCurrentAnimatorState == slideOutState;
	}

	void UpdateAnimatorMode()
	{
		if (animatedPlayerAnimator == null || mRigidbody == null)
			return;

		float speed = Mathf.Abs(mRigidbody.linearVelocity.x);
		bool hasMovementInput = mPhysics != null ? mPhysics.HasMovementInput() : speed > idleSpeedThreshold;
		bool grounded = mPhysics != null && mPhysics.IsOnGround();
		bool onWall = mPhysics != null && mPhysics.IsOnWall();
		bool crouching = grounded && mPhysics != null && mPhysics.IsCrouching();
		bool sprinting = mPhysics != null && mPhysics.IsSprinting();
		bool dashing = mPhysics != null && mPhysics.IsDashing();
		SetWallGrabPropActive(onWall && !mPlayerDead && !mTaunting && !crouching && !dashing);

		SetAnimatorFloat(mHasSpeedParameter, SpeedHash, speed);
		float normalizedWalkSpeed = speed * walkPlaybackScale;
		if (hasMovementInput)
			normalizedWalkSpeed = Mathf.Max(normalizedWalkSpeed, minimumWalkPlaybackSpeed);
		else
			normalizedWalkSpeed = 1.0f;

		SetAnimatorFloat(mHasNormalizedSpeedParameter, NormalizedSpeedHash, normalizedWalkSpeed);
		SetAnimatorBool(mHasGroundedParameter, GroundedHash, grounded);
		SetAnimatorBool(mHasOnWallParameter, OnWallHash, onWall);
		SetAnimatorBool(mHasCrouchingParameter, CrouchingHash, crouching);
		SetAnimatorBool(mHasSprintingParameter, SprintingHash, sprinting);
		SetAnimatorBool(mHasDeadParameter, DeadHash, mPlayerDead);

		if (mPlayerDead)
			return;

		if (mTaunting)
		{
			ResetModelOffset();
			PlayAnimatorState(tauntState, crossFadeTime, false);
			return;
		}

		if (dashing)
		{
			ResumeAnimator();
			ResetModelOffset();
			PlayAnimatorState(dashState, crossFadeTime, false);
			return;
		}

		if (crouching)
		{
			ApplyModelOffset(slideModelOffset);
			UpdateCrouchHold();
			return;
		}

		ResumeAnimator();

		if (onWall)
		{
			PlayAnimatorState(wallState, crossFadeTime, false);
			return;
		}

		ResetModelOffset();

		if (!grounded)
		{
			string airborneState = string.IsNullOrEmpty(mAirborneAnimatorState) ? jumpState : mAirborneAnimatorState;
			PlayAnimatorState(airborneState, crossFadeTime, false);
			return;
		}

		mAirborneAnimatorState = "";

		if (!hasMovementInput)
		{
			PlayAnimatorState(idleState, crossFadeTime, false);
			return;
		}

		if (sprinting && !string.IsNullOrEmpty(sprintState) && speed >= sprintSpeedThreshold)
			PlayAnimatorState(sprintState, crossFadeTime, false);
		else
			PlayAnimatorState(walkState, crossFadeTime, false);
	}

	void UpdateLegacyAnimationMode()
	{
		if (mLegacyAnimation == null || mRigidbody == null)
			return;

		SetWallGrabPropActive(
			mPhysics != null && mPhysics.IsOnWall() && !mPlayerDead && !mTaunting);

		if (mTaunting)
		{
			ResetModelOffset();
			if (mLegacyAnimation[tauntState] != null && !mLegacyAnimation[tauntState].enabled)
				PlayLegacyAnimation(tauntState);

			return;
		}

		float walkingSpeed = Mathf.Abs(mRigidbody.linearVelocity.x) * walkPlaybackScale;
		mLegacyAnimation["walk"].speed = walkingSpeed;

		if (walkingSpeed <= 0.01f && mLegacyAnimation["walk"].enabled)
		{
			mLegacyAnimation.Play("idle");
			mIdle = true;
		}

		if (walkingSpeed > 0.01f && mIdle)
		{
			mIdle = false;
			mLegacyAnimation.CrossFade("walk");
		}
	}

	void SetAnimatorFloat(bool hasParameter, int parameterHash, float value)
	{
		if (hasParameter)
			animatedPlayerAnimator.SetFloat(parameterHash, value);
	}

	void SetAnimatorBool(bool hasParameter, int parameterHash, bool value)
	{
		if (hasParameter)
			animatedPlayerAnimator.SetBool(parameterHash, value);
	}

	void PlayAnimation(string animName)
	{
		if (mUseAnimator)
		{
			PlayAnimatorState(animName, crossFadeTime, false);
			return;
		}

		PlayLegacyAnimation(animName);
	}

	void PlayAnimatorState(
		string stateName,
		float fadeTime,
		bool forceRestart,
		bool useFixedTransitionTime = false,
		float fixedTimeOffset = 0.0f)
	{
		if (animatedPlayerAnimator == null || string.IsNullOrEmpty(stateName))
			return;

		if (!forceRestart && mCurrentAnimatorState == stateName)
			return;

		ResumeAnimator();
		mCurrentAnimatorState = stateName;

		if (fadeTime <= 0.0f)
		{
			if (useFixedTransitionTime)
				animatedPlayerAnimator.PlayInFixedTime(stateName, 0, fixedTimeOffset);
			else
				animatedPlayerAnimator.Play(stateName, 0, 0.0f);
		}
		else if (useFixedTransitionTime)
			animatedPlayerAnimator.CrossFadeInFixedTime(stateName, fadeTime, 0, fixedTimeOffset);
		else
			animatedPlayerAnimator.CrossFade(stateName, fadeTime, 0);
	}

	void UpdateCrouchHold()
	{
		if (!holdSlideWhileCrouching || animatedPlayerAnimator == null || string.IsNullOrEmpty(slideInState))
			return;

		if (mCurrentAnimatorState != slideInState)
			PlayAnimatorState(slideInState, crossFadeTime, true);

		if (mAnimatorPaused || animatedPlayerAnimator.IsInTransition(0))
			return;

		AnimatorStateInfo stateInfo = animatedPlayerAnimator.GetCurrentAnimatorStateInfo(0);
		float holdTime = Mathf.Clamp01(slideHoldNormalizedTime);
		if (stateInfo.normalizedTime >= holdTime)
		{
			animatedPlayerAnimator.Play(slideInState, 0, holdTime);
			mAnimatorSpeedBeforePause = animatedPlayerAnimator.speed;
			animatedPlayerAnimator.speed = 0.0f;
			mAnimatorPaused = true;
			mCurrentAnimatorState = slideInState;
		}
	}

	void ResumeAnimator()
	{
		if (!mAnimatorPaused || animatedPlayerAnimator == null)
			return;

		animatedPlayerAnimator.speed = mAnimatorSpeedBeforePause;
		mAnimatorPaused = false;
	}

	void PlayLegacyAnimation(string animName)
	{
		if (!mPlayerDead && mLegacyAnimation != null && mLegacyAnimation[animName] != null)
		{
			mLegacyAnimation.Play(animName);
			ResetModelOffset();
		}
	}

	void PlayLocomotionState()
	{
		if (mTaunting)
			return;

		if (mUseAnimator)
		{
			UpdateAnimatorMode();
			return;
		}

		ResetModelOffset();
		PlayLegacyAnimation("walk");
	}

	void GoLeft()
	{
		Vector3 localScale = animatedPlayerModel.transform.localScale;
		localScale.z = -Mathf.Abs(localScale.z);
		animatedPlayerModel.transform.localScale = localScale;
	}

	void GoRight()
	{
		Vector3 localScale = animatedPlayerModel.transform.localScale;
		localScale.z = Mathf.Abs(localScale.z);
		animatedPlayerModel.transform.localScale = localScale;
	}

	public void PlayerDied()
	{
		mTaunting = false;
		SetWallGrabPropActive(false);
		ResetModelOffset();

		if (mUseAnimator)
		{
			ResumeAnimator();
			mPlayerDead = true;
			SetAnimatorBool(mHasDeadParameter, DeadHash, true);
			PlayAnimation(deathState);
			return;
		}

		PlayLegacyAnimation("death");
		mPlayerDead = true;
	}

	public void PlayerLives()
	{
		GoRight();
		ResumeAnimator();
		mTaunting = false;
		SetWallGrabPropActive(false);
		ResetModelOffset();
		mPlayerDead = false;
		SetAnimatorBool(mHasDeadParameter, DeadHash, false);
		PlayLocomotionState();
	}

	//MESSAGES CALLED BY PlatformerPhysics.cs:
	void StartedJump()
	{
		StartAirborneAnimation(jumpState, jumpAnimationStartTime);
	}

	void StartedDoubleJump()
	{
		StartAirborneAnimation(string.IsNullOrEmpty(doubleJumpState) ? jumpState : doubleJumpState);
	}

	void StartedWallJump()
	{
		StartAirborneAnimation(jumpState, jumpAnimationStartTime);
	}

	void StartAirborneAnimation(string stateName, float fixedTimeOffset = 0.0f)
	{
		mTaunting = false;
		SetWallGrabPropActive(false);
		ResetModelOffset();
		mAirborneAnimatorState = stateName;

		if (mUseAnimator)
			PlayAnimatorState(stateName, airborneCrossFadeTime, true, true, fixedTimeOffset);
		else
			PlayLegacyAnimation("jump");
	}

	void StartedCrouching()
	{
		mTaunting = false;
		SetWallGrabPropActive(false);

		if (mUseAnimator)
		{
			ApplyModelOffset(slideModelOffset);
			PlayAnimatorState(slideInState, crossFadeTime, true);
		}
		else
		{
			PlayLegacyAnimation("slidein");
			ApplyModelOffset(slideModelOffset);
		}
	}

	void StoppedCrouching()
	{
		ResetModelOffset();

		if (mUseAnimator)
		{
			ResumeAnimator();
			if (!string.IsNullOrEmpty(slideOutState) && slideOutState != slideInState)
				PlayAnimatorState(slideOutState, crossFadeTime, true);
		}
		else
			PlayLegacyAnimation("slideout");

		if (mPhysics != null && mPhysics.IsOnWall())
			LandedOnWall();
		else if (!mUseAnimator && mLegacyAnimation != null)
			mLegacyAnimation.CrossFade("walk", 2.0f);
		else
			PlayLocomotionState();
	}

	void LandedOnGround()
	{
		mAirborneAnimatorState = "";
		SetWallGrabPropActive(false);

		if (!mTaunting && mPhysics != null && !mPhysics.IsCrouching())
		{
			ResetModelOffset();
			PlayLocomotionState();
		}
	}

	void LandedOnWall()
	{
		if (mTaunting)
			return;

		mAirborneAnimatorState = "";
		SetWallGrabPropActive(true);

		if (mPhysics != null && !mPhysics.IsCrouching())
		{
			PlayAnimation(mUseAnimator ? wallState : "onwall");

			if (!mPhysics.IsWallOnRightSide())
			{
				ApplyModelOffset(new Vector3(0.45f, 0, 0));
				GoLeft();
			}
			else
			{
				ApplyModelOffset(new Vector3(-0.45f, 0, 0));
				GoRight();
			}
		}
	}

	void ReleasedWall()
	{
		if (mTaunting)
			return;

		SetWallGrabPropActive(false);
		ResetModelOffset();

		if (mUseAnimator)
		{
			PlayLocomotionState();
			return;
		}

		if (mLegacyAnimation != null && !mLegacyAnimation["jump"].enabled && mPhysics != null && !mPhysics.IsCrouching())
			PlayLegacyAnimation("walk");
	}

	void StartedSprinting()
	{
		PlayLocomotionState();
	}

	void StoppedSprinting()
	{
		PlayLocomotionState();
	}

	void StartedDash()
	{
		if (mPlayerDead)
			return;

		mTaunting = false;
		SetWallGrabPropActive(false);
		ResumeAnimator();
		ResetModelOffset();

		if (mUseAnimator)
			PlayAnimatorState(dashState, crossFadeTime, true);
		else
			PlayAnimation(dashState);
	}

	void StoppedDash()
	{
		if (!mPlayerDead)
			PlayLocomotionState();
	}

	void StartedTaunt()
	{
		if (mPlayerDead)
			return;

		ResumeAnimator();
		mTaunting = true;
		SetWallGrabPropActive(false);

		if (animatedPlayerModel != null)
			ResetModelOffset();

		PlayAnimation(tauntState);
	}

	void StoppedTaunt()
	{
		if (!mTaunting)
			return;

		mTaunting = false;
		ResumeAnimator();
		PlayLocomotionState();
	}

	void ApplyModelOffset(Vector3 offset)
	{
		if (animatedPlayerModel != null)
			animatedPlayerModel.transform.localPosition = mBaseModelLocalPosition + offset;
	}

	void ResetModelOffset()
	{
		ApplyModelOffset(Vector3.zero);
	}

	void SetWallGrabPropActive(bool active)
	{
		if (wallGrabProp != null && wallGrabProp.activeSelf != active)
			wallGrabProp.SetActive(active);
	}
}
