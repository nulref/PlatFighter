using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections;

public class PlatformerController : MonoBehaviour
{
	public bool enableThumbstickUpJump = false;
	public float movementDeadZone = 0.25f;
	public float jumpBufferTime = 0.12f;
	public float slideStickDeadZone = 0.65f;
	public float slideAngleTolerance = 25.0f;
	public float slideDownReleaseThreshold = 0.35f;
	public float dPadDeadZone = 0.5f;

	const float RightSlideAngle = 135.0f;
	const float LeftSlideAngle = 225.0f;

	PlatformerPhysics mPlayer;
	bool mHasControl;
	bool mControllerSlideActive;
	bool mTaunting;
	bool mDPadPressedLastFrame;
	bool mSprintHeldLastFrame;
	bool mKeyboardCrouchHeldLastFrame;
	bool mJumpHeld;
	bool mJumpHeldLastFrame;
	float mJumpBufferTimeLeft;
	Vector2 mMovementInput;

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

		UpdateInputState();
		bool movementPressed = HasMovementInput(mMovementInput);

		HandleTauntInput(movementPressed, mJumpHeld);
		if (mTaunting)
			return;

		bool sprintHeld = IsSprintHeld();
		bool keyboardCrouchHeld = IsKeyboardCrouchHeld();

		if (sprintHeld && !mSprintHeldLastFrame)
			mPlayer.StartSprint();

		if (!sprintHeld && mSprintHeldLastFrame)
			mPlayer.StopSprint();

		if (keyboardCrouchHeld && !mKeyboardCrouchHeldLastFrame)
			mPlayer.Crouch();

		if (!keyboardCrouchHeld && mKeyboardCrouchHeldLastFrame && !mControllerSlideActive)
			mPlayer.UnCrouch();

		mSprintHeldLastFrame = sprintHeld;
		mKeyboardCrouchHeldLastFrame = keyboardCrouchHeld;

		bool controllerSlideRequested = IsSlideGesture(mMovementInput);
		if (controllerSlideRequested)
		{
			if (!mPlayer.IsCrouching())
				mPlayer.Crouch();

			mControllerSlideActive = true;
		}
		else if (mControllerSlideActive && !IsStickDownHeld(mMovementInput))
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
			bool movementPressed = HasMovementInput(mMovementInput);

			if (mTaunting)
			{
				if (movementPressed || mJumpHeld)
					StopTaunt();
				else
				{
					mPlayer.Walk(0.0f);
					return;
				}
			}

			if ((mJumpHeld || mJumpBufferTimeLeft > 0.0f) && mPlayer.Jump())
				mJumpBufferTimeLeft = 0.0f;

			if (mJumpBufferTimeLeft > 0.0f)
				mJumpBufferTimeLeft = Mathf.Max(0.0f, mJumpBufferTimeLeft - Time.fixedDeltaTime);

			mPlayer.Walk(mMovementInput.x);
		}
	}

	public void GiveControl() { mHasControl = true; }
	public void RemoveControl()
	{
		StopTaunt();
		mHasControl = false;
		mDPadPressedLastFrame = false;
		mSprintHeldLastFrame = false;
		mKeyboardCrouchHeldLastFrame = false;
		mJumpHeld = false;
		mJumpHeldLastFrame = false;
		mJumpBufferTimeLeft = 0.0f;
		mMovementInput = Vector2.zero;
	}
	public bool HasControl() { return mHasControl; }

	void UpdateInputState()
	{
		mMovementInput = ReadMovementInput();
		mJumpHeld = IsJumpHeld(mMovementInput);

		if (IsJumpPressedThisFrame(mMovementInput, mJumpHeld))
			mJumpBufferTimeLeft = jumpBufferTime;

		mJumpHeldLastFrame = mJumpHeld;
	}

	Vector2 ReadMovementInput()
	{
		Vector2 input = Vector2.zero;
		Keyboard keyboard = Keyboard.current;
		if (keyboard != null)
		{
			if (IsPressed(keyboard.leftArrowKey) || IsPressed(keyboard.aKey))
				input.x -= 1.0f;
			if (IsPressed(keyboard.rightArrowKey) || IsPressed(keyboard.dKey))
				input.x += 1.0f;
			if (IsPressed(keyboard.downArrowKey) || IsPressed(keyboard.sKey))
				input.y -= 1.0f;
			if (IsPressed(keyboard.upArrowKey) || IsPressed(keyboard.wKey))
				input.y += 1.0f;
		}

		Gamepad gamepad = Gamepad.current;
		if (gamepad != null)
		{
			Vector2 stickInput = gamepad.leftStick.ReadValue();
			if (Mathf.Abs(stickInput.x) > movementDeadZone)
				input.x += stickInput.x;
			if (Mathf.Abs(stickInput.y) > movementDeadZone)
				input.y += stickInput.y;
		}

		input.x = Mathf.Clamp(input.x, -1.0f, 1.0f);
		input.y = Mathf.Clamp(input.y, -1.0f, 1.0f);
		return input;
	}

	bool IsJumpHeld(Vector2 movementInput)
	{
		Keyboard keyboard = Keyboard.current;
		if (keyboard != null && IsPressed(keyboard.spaceKey))
			return true;

		Gamepad gamepad = Gamepad.current;
		if (gamepad != null && (IsPressed(gamepad.buttonSouth) || IsPressed(gamepad.buttonNorth)))
			return true;

		return enableThumbstickUpJump && movementInput.y > 0.8f && Mathf.Abs(movementInput.x) < 0.5f;
	}

	bool IsJumpPressedThisFrame(Vector2 movementInput, bool jumpHeld)
	{
		Keyboard keyboard = Keyboard.current;
		if (keyboard != null && WasPressedThisFrame(keyboard.spaceKey))
			return true;

		Gamepad gamepad = Gamepad.current;
		if (gamepad != null && (WasPressedThisFrame(gamepad.buttonSouth) || WasPressedThisFrame(gamepad.buttonNorth)))
			return true;

		return enableThumbstickUpJump && jumpHeld && !mJumpHeldLastFrame;
	}

	bool HasMovementInput(Vector2 movementInput)
	{
		return Mathf.Abs(movementInput.x) > movementDeadZone || IsSlideGesture(movementInput) || IsKeyboardCrouchHeld();
	}

	bool IsKeyboardCrouchHeld()
	{
		Keyboard keyboard = Keyboard.current;
		return keyboard != null && (IsPressed(keyboard.sKey) || IsPressed(keyboard.downArrowKey));
	}

	bool IsSprintHeld()
	{
		Keyboard keyboard = Keyboard.current;
		return keyboard != null && (IsPressed(keyboard.leftShiftKey) || IsPressed(keyboard.rightShiftKey));
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
		Gamepad gamepad = Gamepad.current;
		if (gamepad == null)
			return false;

		return gamepad.dpad.ReadValue().sqrMagnitude > dPadDeadZone * dPadDeadZone;
	}

	bool IsPressed(ButtonControl control)
	{
		return control != null && control.isPressed;
	}

	bool WasPressedThisFrame(ButtonControl control)
	{
		return control != null && control.wasPressedThisFrame;
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

