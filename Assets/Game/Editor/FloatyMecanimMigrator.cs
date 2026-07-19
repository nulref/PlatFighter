using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class FloatyMecanimMigrator
{
	const string FloatyFolder = "Assets/Game/Art/Characters/Floaty";
	const string FloatyMaterialPath = "Assets/Game/Art/Materials/Floaty.mat";
	const string ControllerPath = FloatyFolder + "/Floaty.controller";
	const string PlayerName = "Player";
	const float FloatySlideModelOffsetY = -1.26f;

	struct FloatyClip
	{
		public string StateName;
		public string AssetPath;
		public bool Loop;

		public FloatyClip(string stateName, string assetPath, bool loop)
		{
			StateName = stateName;
			AssetPath = assetPath;
			Loop = loop;
		}
	}

	static readonly FloatyClip[] Clips = new FloatyClip[]
	{
		new FloatyClip("idle", FloatyFolder + "/floaty_idle.fbx", true),
		new FloatyClip("walk", FloatyFolder + "/floaty_walk.fbx", true),
		new FloatyClip("run", FloatyFolder + "/floaty_run.fbx", true),
		new FloatyClip("jump", FloatyFolder + "/floaty_jump.fbx", false),
		new FloatyClip("leap", FloatyFolder + "/floaty_leap.fbx", false),
		new FloatyClip("slide", FloatyFolder + "/floaty_slide.fbx", false),
		new FloatyClip("taunt", FloatyFolder + "/floaty_taunt.fbx", true)
	};

	[MenuItem("Tools/Platfighter/Setup Floaty Mecanim")]
	public static void SetupFloatyMecanim()
	{
		SetupFloatyMecanimInternal(true);
	}

	public static bool SetupFloatyMecanimWithoutDialog()
	{
		return SetupFloatyMecanimInternal(false);
	}

	static bool SetupFloatyMecanimInternal(bool showDialog)
	{
		if (!AssetDatabase.IsValidFolder(FloatyFolder))
		{
			if (showDialog)
				EditorUtility.DisplayDialog("Floaty Mecanim", "Could not find " + FloatyFolder + ".", "OK");

			Debug.LogError("Could not find " + FloatyFolder + ".");
			return false;
		}

		for (int i = 0; i < Clips.Length; i++)
			ConfigureImporter(Clips[i]);

		AssetDatabase.Refresh();

		AnimatorController controller = CreateController();
		GameObject floaty = AssignFloatyToPlayer(controller);

		AssetDatabase.SaveAssets();
		EditorSceneManager.MarkAllScenesDirty();
		EditorSceneManager.SaveOpenScenes();

		string sceneMessage = floaty == null
			? "Controller created. Open the demo scene and run this command again to wire the Player."
			: "Controller created and assigned to the Player's Floaty model.";

		Debug.Log(sceneMessage);

		if (showDialog)
			EditorUtility.DisplayDialog("Floaty Mecanim", sceneMessage, "OK");

		return true;
	}

	static void ConfigureImporter(FloatyClip clipInfo)
	{
		ModelImporter importer = AssetImporter.GetAtPath(clipInfo.AssetPath) as ModelImporter;
		if (importer == null)
		{
			Debug.LogError("Could not find Floaty FBX importer at " + clipInfo.AssetPath + ".");
			return;
		}

		importer.animationType = ModelImporterAnimationType.Generic;
		importer.importAnimation = true;
		importer.optimizeGameObjects = false;
		importer.animationWrapMode = clipInfo.Loop ? WrapMode.Loop : WrapMode.Default;

		ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;
		if (defaultClips != null && defaultClips.Length > 0)
		{
			ModelImporterClipAnimation sourceClip = defaultClips[0];
			sourceClip.name = clipInfo.StateName;
			sourceClip.loopTime = clipInfo.Loop;
			sourceClip.wrapMode = clipInfo.Loop ? WrapMode.Loop : WrapMode.Default;
			importer.clipAnimations = new ModelImporterClipAnimation[] { sourceClip };
		}

		importer.SaveAndReimport();
	}

	static AnimatorController CreateController()
	{
		if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
			AssetDatabase.DeleteAsset(ControllerPath);

		AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
		AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
		AddParameter(controller, "NormalizedSpeed", AnimatorControllerParameterType.Float);
		AddParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
		AddParameter(controller, "OnWall", AnimatorControllerParameterType.Bool);
		AddParameter(controller, "Crouching", AnimatorControllerParameterType.Bool);
		AddParameter(controller, "Sprinting", AnimatorControllerParameterType.Bool);
		AddParameter(controller, "Dead", AnimatorControllerParameterType.Bool);

		AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
		stateMachine.states = new ChildAnimatorState[0];
		stateMachine.anyStateTransitions = new AnimatorStateTransition[0];
		stateMachine.entryTransitions = new AnimatorTransition[0];

		AnimatorState idleState = null;
		for (int i = 0; i < Clips.Length; i++)
		{
			AnimationClip clip = LoadAnimationClip(Clips[i].AssetPath, Clips[i].StateName);
			if (clip == null)
			{
				Debug.LogError("Could not load animation clip '" + Clips[i].StateName + "' from " + Clips[i].AssetPath + ".");
				continue;
			}

			AnimatorState state = stateMachine.AddState(Clips[i].StateName, new Vector3(250, 60 + (i * 70), 0));
			state.motion = clip;
			if (Clips[i].StateName == "idle")
				idleState = state;
		}

		if (idleState != null)
			stateMachine.defaultState = idleState;

		EditorUtility.SetDirty(controller);
		return controller;
	}

	static void AddParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType parameterType)
	{
		for (int i = 0; i < controller.parameters.Length; i++)
		{
			if (controller.parameters[i].name == parameterName)
				return;
		}

		controller.AddParameter(parameterName, parameterType);
	}

	static AnimationClip LoadAnimationClip(string path, string preferredName)
	{
		Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
		AnimationClip firstClip = null;

		for (int i = 0; i < assets.Length; i++)
		{
			AnimationClip clip = assets[i] as AnimationClip;
			if (clip == null || clip.name.StartsWith("__preview__"))
				continue;

			if (clip.name == preferredName)
				return clip;

			if (firstClip == null)
				firstClip = clip;
		}

		return firstClip;
	}

	static GameObject AssignFloatyToPlayer(RuntimeAnimatorController controller)
	{
		GameObject player = GameObject.Find(PlayerName);
		if (player == null)
		{
			Debug.LogWarning("Could not find a GameObject named '" + PlayerName + "' in the open scene.");
			return null;
		}

		Transform existingFloaty = player.transform.Find("Floaty");
		GameObject floaty = existingFloaty != null ? existingFloaty.gameObject : InstantiateFloatyModel(player.transform);
		if (floaty == null)
			return null;

		Animator animator = floaty.GetComponent<Animator>();
		if (animator == null)
			animator = floaty.AddComponent<Animator>();

		animator.runtimeAnimatorController = controller;
		animator.applyRootMotion = false;
		animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

		PlatformerAnimation platformerAnimation = player.GetComponent<PlatformerAnimation>();
		if (platformerAnimation != null)
		{
			platformerAnimation.animatedPlayerModel = floaty.transform;
			platformerAnimation.animatedPlayerAnimator = animator;
			platformerAnimation.animatorController = controller;
			platformerAnimation.preferAnimator = true;
			platformerAnimation.slideModelOffset = new Vector3(0.0f, FloatySlideModelOffsetY, 0.0f);
			EditorUtility.SetDirty(platformerAnimation);
		}

		DisableOldNinja(player.transform, floaty.transform);
		ApplyFloatyMaterial(floaty);
		EditorUtility.SetDirty(floaty);
		return floaty;
	}

	static GameObject InstantiateFloatyModel(Transform playerTransform)
	{
		GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloatyFolder + "/floaty_idle.fbx");
		if (modelPrefab == null)
		{
			Debug.LogError("Could not load Floaty model prefab.");
			return null;
		}

		GameObject floaty = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
		if (floaty == null)
			floaty = Object.Instantiate(modelPrefab);

		floaty.name = "Floaty";
		floaty.transform.SetParent(playerTransform, false);
		floaty.transform.localPosition = Vector3.zero;
		floaty.transform.localRotation = Quaternion.Euler(0, 90, 0);
		floaty.transform.localScale = Vector3.one;
		return floaty;
	}

	static void ApplyFloatyMaterial(GameObject floaty)
	{
		Material material = AssetDatabase.LoadAssetAtPath<Material>(FloatyMaterialPath);
		if (material == null)
		{
			Debug.LogWarning("Could not load Floaty material at " + FloatyMaterialPath + ".");
			return;
		}

		Renderer[] renderers = floaty.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			Material[] materials = renderers[i].sharedMaterials;
			for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
				materials[materialIndex] = material;

			renderers[i].sharedMaterials = materials;
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderers[i]);
			EditorUtility.SetDirty(renderers[i]);
		}
	}

	static void DisableOldNinja(Transform playerTransform, Transform floatyTransform)
	{
		for (int i = 0; i < playerTransform.childCount; i++)
		{
			Transform child = playerTransform.GetChild(i);
			if (child == floatyTransform)
				continue;

			if (child.name == "ninja_animated")
				child.gameObject.SetActive(false);
		}
	}
}
