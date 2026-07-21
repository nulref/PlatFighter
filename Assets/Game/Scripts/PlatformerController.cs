using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;
using System.Collections;

public class PlatformerController : MonoBehaviour
{
	public bool enableThumbstickUpJump = false;
	public float movementDeadZone = 0.25f;
	public float jumpBufferTime = 0.12f;
	[FormerlySerializedAs("slideStickDeadZone")]
	public float crouchStickThreshold = 0.65f;
	[FormerlySerializedAs("slideDownReleaseThreshold")]
	public float crouchStickReleaseThreshold = 0.35f;
	public float dPadDeadZone = 0.5f;
	[Tooltip("Horizontal stick amount that activates sprinting.")]
	public float analogSprintThreshold = 0.95f;
	[Tooltip("Stick amount that counts as a directional tap for dashing.")]
	public float dashTapThreshold = 0.75f;
	public float dashTapReleaseThreshold = 0.35f;
	public float dashDoubleTapWindow = 0.28f;

	PlatformerPhysics mPlayer;
	bool mHasControl;
	bool mControllerDownHeld;
	bool mTaunting;
	bool mDPadPressedLastFrame;
	bool mSprintHeldLastFrame;
	bool mJumpHeld;
	bool mJumpHeldLastFrame;
	bool mDashTapHeld;
	int mLastDashTapDirection;
	float mLastDashTapTime = float.NegativeInfinity;
	float mJumpBufferTimeLeft;
	Vector2 mMovementInput;
	Vector2 mRawLeftStickInput;

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
		{
			mPlayer.SetFastFallHeld(false);
			return;
		}

		HandleDashInput();

		bool sprintHeld = IsSprintHeld();
		bool keyboardCrouchHeld = IsKeyboardCrouchHeld();
		UpdateControllerDownState();
		bool crouchHeld = keyboardCrouchHeld || mControllerDownHeld;
		bool grounded = mPlayer.IsOnGround();
		mPlayer.SetFastFallHeld(crouchHeld && !grounded);

		if (sprintHeld && !mSprintHeldLastFrame)
			mPlayer.StartSprint();

		if (!sprintHeld && mSprintHeldLastFrame)
			mPlayer.StopSprint();

		if (grounded && crouchHeld && !mPlayer.IsCrouching())
			mPlayer.Crouch();

		if (!crouchHeld && mPlayer.IsCrouching())
			mPlayer.UnCrouch();

		mSprintHeldLastFrame = sprintHeld;
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
		if (mPlayer != null && mSprintHeldLastFrame)
			mPlayer.StopSprint();
		if (mPlayer != null)
		{
			mPlayer.CancelDash();
			mPlayer.SetFastFallHeld(false);
		}

		mHasControl = false;
		mDPadPressedLastFrame = false;
		mSprintHeldLastFrame = false;
		mControllerDownHeld = false;
		mJumpHeld = false;
		mJumpHeldLastFrame = false;
		mDashTapHeld = false;
		mLastDashTapDirection = 0;
		mLastDashTapTime = float.NegativeInfinity;
		mJumpBufferTimeLeft = 0.0f;
		mMovementInput = Vector2.zero;
		mRawLeftStickInput = Vector2.zero;
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
		mRawLeftStickInput = Vector2.zero;
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
			mRawLeftStickInput = gamepad.leftStick.ReadValue();
			input.x += ApplyDeadZone(mRawLeftStickInput.x, movementDeadZone);
			if (Mathf.Abs(mRawLeftStickInput.y) > movementDeadZone)
				input.y += mRawLeftStickInput.y;
		}

		input.x = Mathf.Clamp(input.x, -1.0f, 1.0f);
		input.y = Mathf.Clamp(input.y, -1.0f, 1.0f);
		return input;
	}

	float ApplyDeadZone(float value, float deadZone)
	{
		float magnitude = Mathf.Abs(value);
		if (magnitude <= deadZone)
			return 0.0f;

		return Mathf.Sign(value) * Mathf.InverseLerp(deadZone, 1.0f, magnitude);
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
		return Mathf.Abs(movementInput.x) > 0.01f || movementInput.y <= -crouchStickThreshold || IsKeyboardCrouchHeld();
	}

	bool IsKeyboardCrouchHeld()
	{
		Keyboard keyboard = Keyboard.current;
		return keyboard != null && (IsPressed(keyboard.sKey) || IsPressed(keyboard.downArrowKey));
	}

	bool IsSprintHeld()
	{
		Keyboard keyboard = Keyboard.current;
		bool keyboardSprint = keyboard != null && (IsPressed(keyboard.leftShiftKey) || IsPressed(keyboard.rightShiftKey));
		bool stickSprint = Mathf.Abs(mRawLeftStickInput.x) >= analogSprintThreshold;
		return keyboardSprint || stickSprint;
	}

	void HandleDashInput()
	{
		float directionalInput = GetDashDirectionalInput();
		float inputMagnitude = Mathf.Abs(directionalInput);

		if (mDashTapHeld)
		{
			if (inputMagnitude <= dashTapReleaseThreshold)
				mDashTapHeld = false;

			return;
		}

		if (inputMagnitude < dashTapThreshold || mMovementInput.y <= -crouchStickThreshold)
			return;

		mDashTapHeld = true;
		int direction = directionalInput < 0.0f ? -1 : 1;
		float tapTime = Time.unscaledTime;
		bool isDoubleTap = direction == mLastDashTapDirection && tapTime - mLastDashTapTime <= dashDoubleTapWindow;

		if (isDoubleTap && mPlayer.Dash(direction))
		{
			mLastDashTapDirection = 0;
			mLastDashTapTime = float.NegativeInfinity;
			return;
		}

		mLastDashTapDirection = direction;
		mLastDashTapTime = tapTime;
	}

	float GetDashDirectionalInput()
	{
		Keyboard keyboard = Keyboard.current;
		if (keyboard != null)
		{
			bool leftPressed = IsPressed(keyboard.leftArrowKey) || IsPressed(keyboard.aKey);
			bool rightPressed = IsPressed(keyboard.rightArrowKey) || IsPressed(keyboard.dKey);
			if (leftPressed != rightPressed)
				return leftPressed ? -1.0f : 1.0f;
		}

		return mRawLeftStickInput.x;
	}

	void UpdateControllerDownState()
	{
		if (mControllerDownHeld)
		{
			if (mRawLeftStickInput.y > -crouchStickReleaseThreshold)
				mControllerDownHeld = false;

			return;
		}

		if (mRawLeftStickInput.y <= -crouchStickThreshold)
			mControllerDownHeld = true;
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

