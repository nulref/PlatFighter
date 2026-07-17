using UnityEngine;
using System.Collections;

public class PlatformerController : MonoBehaviour
{
	public bool enableThumbstickUpJump = false;
	public float movementDeadZone = 0.25f;
	public float slideStickDeadZone = 0.65f;
	public float slideAngleTolerance = 25.0f;
	public float slideDownReleaseThreshold = 0.35f;
	public string dPadHorizontalAxis = "XboxDPadX";
	public string dPadVerticalAxis = "XboxDPadY";
	public float dPadDeadZone = 0.5f;

	const float RightSlideAngle = 135.0f;
	const float LeftSlideAngle = 225.0f;

	PlatformerPhysics mPlayer;
	bool mHasControl;
	bool mControllerSlideActive;
	bool mTaunting;
	bool mDPadPressedLastFrame;

	void Start () 
	{
		mHasControl = true;
		mPlayer = GetComponent<PlatformerPhysics>();
		if (mPlayer == null)
			Debug.LogError("This object also needs a PlatformerPhysics component attached for the controller to function properly");
	}

	void Update () 
	{
		//here are the actions that are triggered by a press or a release
		if (!mPlayer || !mHasControl)
			return;

		Vector2 movementInput = ReadMovementInput();
		bool jumpHeld = IsJumpHeld(movementInput);
		bool movementPressed = HasMovementInput(movementInput);

		HandleTauntInput(movementPressed, jumpHeld);
		if (mTaunting)
			return;

		if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
			mPlayer.StartSprint();

		if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
			mPlayer.StopSprint();

		if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
			mPlayer.Crouch();

		if ((Input.GetKeyUp(KeyCode.S) || Input.GetKeyUp(KeyCode.DownArrow)) && !mControllerSlideActive)
			mPlayer.UnCrouch();

		bool controllerSlideRequested = IsSlideGesture(movementInput);
		if (controllerSlideRequested)
		{
			if (!mPlayer.IsCrouching())
				mPlayer.Crouch();

			mControllerSlideActive = true;
		}
		else if (mControllerSlideActive && !IsStickDownHeld(movementInput))
		{
			mControllerSlideActive = false;
			if (!IsKeyboardCrouchHeld())
				mPlayer.UnCrouch();
		}
	}

	void FixedUpdate()
	{
		//here are actions where the buttons can be held for a longer period
		if (mPlayer && mHasControl)
		{
			Vector2 movementInput = ReadMovementInput();
			bool jumpHeld = IsJumpHeld(movementInput);
			bool movementPressed = HasMovementInput(movementInput);

			if (mTaunting)
			{
				if (movementPressed || jumpHeld)
					StopTaunt();
				else
				{
					mPlayer.Walk(0.0f);
					return;
				}
			}

			if (jumpHeld)
				mPlayer.Jump();

			mPlayer.Walk(movementInput.x);
		}
	}

	public void GiveControl() { mHasControl = true; }
	public void RemoveControl() { StopTaunt(); mHasControl = false; }
	public bool HasControl() { return mHasControl; }

	Vector2 ReadMovementInput()
	{
		return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
	}

	bool IsJumpHeld(Vector2 movementInput)
	{
		if (Input.GetButton("Jump"))
			return true;

		return enableThumbstickUpJump && movementInput.y > 0.8f && Mathf.Abs(movementInput.x) < 0.5f;
	}

	bool HasMovementInput(Vector2 movementInput)
	{
		return Mathf.Abs(movementInput.x) > movementDeadZone || IsSlideGesture(movementInput) || IsKeyboardCrouchHeld();
	}

	bool IsKeyboardCrouchHeld()
	{
		return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
	}

	bool IsStickDownHeld(Vector2 movementInput)
	{
		return movementInput.y <= -slideDownReleaseThreshold;
	}

	bool IsSlideGesture(Vector2 movementInput)
	{
		if (movementInput.sqrMagnitude < slideStickDeadZone * slideStickDeadZone)
			return false;

		// Stick angle is 0 up, 90 right, 180 down, 270 left.
		float angle = Mathf.Atan2(movementInput.x, movementInput.y) * Mathf.Rad2Deg;
		if (angle < 0.0f)
			angle += 360.0f;

		return IsAngleNear(angle, RightSlideAngle) || IsAngleNear(angle, LeftSlideAngle);
	}

	bool IsAngleNear(float angle, float targetAngle)
	{
		return Mathf.Abs(Mathf.DeltaAngle(angle, targetAngle)) <= slideAngleTolerance;
	}

	void HandleTauntInput(bool movementPressed, bool jumpHeld)
	{
		bool dPadPressed = IsDPadPressed();

		if (mTaunting && (movementPressed || jumpHeld))
			StopTaunt();

		if (!mTaunting && dPadPressed && !mDPadPressedLastFrame && !movementPressed && !jumpHeld)
			StartTaunt();

		mDPadPressedLastFrame = dPadPressed;
	}

	bool IsDPadPressed()
	{
		float dPadX = string.IsNullOrEmpty(dPadHorizontalAxis) ? 0.0f : Input.GetAxisRaw(dPadHorizontalAxis);
		float dPadY = string.IsNullOrEmpty(dPadVerticalAxis) ? 0.0f : Input.GetAxisRaw(dPadVerticalAxis);

		return Mathf.Abs(dPadX) > dPadDeadZone ||
			Mathf.Abs(dPadY) > dPadDeadZone ||
			Input.GetKey(KeyCode.JoystickButton10) ||
			Input.GetKey(KeyCode.JoystickButton11) ||
			Input.GetKey(KeyCode.JoystickButton12) ||
			Input.GetKey(KeyCode.JoystickButton13);
	}

	void StartTaunt()
	{
		mTaunting = true;
		SendMessage("StartedTaunt", SendMessageOptions.DontRequireReceiver);
	}

	void StopTaunt()
	{
		if (!mTaunting)
			return;

		mTaunting = false;
		SendMessage("StoppedTaunt", SendMessageOptions.DontRequireReceiver);
	}
}

