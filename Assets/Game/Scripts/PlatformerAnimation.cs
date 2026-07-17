using UnityEngine;
using System.Collections;

public class PlatformerAnimation : MonoBehaviour
{
	public Transform animatedPlayerModel; //Visible model root. Used for facing and wall offsets.
	public Animator animatedPlayerAnimator;
	public RuntimeAnimatorController animatorController;
	public bool preferAnimator = true;

	public string idleState = "idle";
	public string walkState = "walk";
	public string runState = "run";
	public string jumpState = "jump";
	public string slideInState = "slide";
	public string slideOutState = "slide";
	public string wallState = "jump";
	public string tauntState = "taunt";
	public string deathState = "";

	public float idleSpeedThreshold = 0.1f;
	public float runSpeedThreshold = 16.0f;
	public float crossFadeTime = 0.08f;
	public float walkPlaybackScale = 0.075f;
	public bool holdSlideWhileCrouching = true;
	public float slideHoldNormalizedTime = 0.5f;

	Animation mLegacyAnimation;
	Rigidbody mRigidbody;
	PlatformerPhysics mPhysics;
	bool mPlayerDead = false;
	bool mIdle = false;
	bool mUseAnimator = false;
	bool mAnimatorPaused = false;
	bool mTaunting = false;
	float mAnimatorSpeedBeforePause = 1.0f;
	string mCurrentAnimatorState = "";

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

	void UpdateAnimatorMode()
	{
		if (animatedPlayerAnimator == null || mRigidbody == null)
			return;

		float speed = Mathf.Abs(mRigidbody.linearVelocity.x);
		bool grounded = mPhysics != null && mPhysics.IsOnGround();
		bool onWall = mPhysics != null && mPhysics.IsOnWall();
		bool crouching = mPhysics != null && mPhysics.IsCrouching();
		bool sprinting = mPhysics != null && mPhysics.IsSprinting();

		SetAnimatorFloat(mHasSpeedParameter, SpeedHash, speed);
		SetAnimatorFloat(mHasNormalizedSpeedParameter, NormalizedSpeedHash, speed * walkPlaybackScale);
		SetAnimatorBool(mHasGroundedParameter, GroundedHash, grounded);
		SetAnimatorBool(mHasOnWallParameter, OnWallHash, onWall);
		SetAnimatorBool(mHasCrouchingParameter, CrouchingHash, crouching);
		SetAnimatorBool(mHasSprintingParameter, SprintingHash, sprinting);
		SetAnimatorBool(mHasDeadParameter, DeadHash, mPlayerDead);

		if (mPlayerDead)
			return;

		if (mTaunting)
		{
			PlayAnimatorState(tauntState, crossFadeTime, false);
			return;
		}

		if (crouching)
		{
			UpdateCrouchHold();
			return;
		}

		ResumeAnimator();

		if (onWall)
		{
			PlayAnimatorState(wallState, crossFadeTime, false);
			return;
		}

		if (!grounded)
		{
			PlayAnimatorState(jumpState, crossFadeTime, false);
			return;
		}

		if (speed <= idleSpeedThreshold)
		{
			PlayAnimatorState(idleState, crossFadeTime, false);
			return;
		}

		if (sprinting && !string.IsNullOrEmpty(runState) && speed >= runSpeedThreshold)
			PlayAnimatorState(runState, crossFadeTime, false);
		else
			PlayAnimatorState(walkState, crossFadeTime, false);
	}

	void UpdateLegacyAnimationMode()
	{
		if (mLegacyAnimation == null || mRigidbody == null)
			return;

		if (mTaunting)
		{
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

	void PlayAnimatorState(string stateName, float fadeTime, bool forceRestart)
	{
		if (animatedPlayerAnimator == null || string.IsNullOrEmpty(stateName))
			return;

		if (!forceRestart && mCurrentAnimatorState == stateName)
			return;

		ResumeAnimator();
		mCurrentAnimatorState = stateName;

		if (fadeTime <= 0.0f)
			animatedPlayerAnimator.Play(stateName, 0, 0.0f);
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
			animatedPlayerModel.transform.localPosition = Vector3.zero; //reset any position change made by on wall anim
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
		mPlayerDead = false;
		SetAnimatorBool(mHasDeadParameter, DeadHash, false);
		PlayLocomotionState();
	}

	//MESSAGES CALLED BY PlatformerPhysics.cs:
	void StartedJump()
	{
		mTaunting = false;
		PlayAnimation(jumpState);
	}

	void StartedWallJump()
	{
		mTaunting = false;
		PlayAnimation(jumpState);
	}

	void StartedCrouching()
	{
		mTaunting = false;

		if (mUseAnimator)
			PlayAnimatorState(slideInState, crossFadeTime, true);
		else
			PlayLegacyAnimation("slidein");
	}

	void StoppedCrouching()
	{
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
		if (!mTaunting && mPhysics != null && !mPhysics.IsCrouching())
			PlayLocomotionState();
	}

	void LandedOnWall()
	{
		if (mTaunting)
			return;

		if (mPhysics != null && !mPhysics.IsCrouching())
		{
			PlayAnimation(mUseAnimator ? wallState : "onwall");

			if (!mPhysics.IsWallOnRightSide())
			{
				animatedPlayerModel.transform.localPosition = new Vector3(0.45f, 0, 0);
				GoLeft();
			}
			else
			{
				animatedPlayerModel.transform.localPosition = new Vector3(-0.45f, 0, 0);
				GoRight();
			}
		}
	}

	void ReleasedWall()
	{
		if (mTaunting)
			return;

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

	void StartedTaunt()
	{
		if (mPlayerDead)
			return;

		ResumeAnimator();
		mTaunting = true;

		if (animatedPlayerModel != null)
			animatedPlayerModel.transform.localPosition = Vector3.zero;

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
}
