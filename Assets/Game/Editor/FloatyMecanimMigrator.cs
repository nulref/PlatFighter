using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class FloatyMecanimMigrator
{
	const string FloatyFolder = "Assets/Game/Art/Characters/Floaty";
	const string FloatyMaterialPath = "Assets/Game/Art/Materials/Floaty.mat";
	const string WallGrabNestPath = FloatyFolder + "/floaty_wall_grab_nest.fbx";
	const string ControllerPath = FloatyFolder + "/Floaty.controller";
	const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/Player.prefab";
	const string DemoScenePath = "Assets/Game/Demo/Game.unity";
	const string PlayerName = "Player";
	const float FloatySlideModelOffsetY = -1.26f;
	const float FloatySlideContactOffset = 0.04f;
	const float FloatyFootSoleOffset = 0.11f;
	const float FloatyMaxFootGroundingAdjustment = 0.75f;
	const float FloatyIdleGroundingOffset = -0.03f;
	const bool FloatyAlignSlideToGroundSlope = true;
	const float FloatyMaxGroundSlopeLeanAngle = 8.0f;
	const float FloatyGroundSlopeLeanSmoothTime = 0.15f;
	const float FloatyGroundContactTolerance = 0.08f;
	const float FloatyGroundSnapDistance = 0.3f;
	const float FloatyWallJumpDetachTime = 0.12f;

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
		new FloatyClip("sprint", FloatyFolder + "/floaty_sprint.fbx", true),
		new FloatyClip("dash", FloatyFolder + "/floaty_dash.fbx", true),
		new FloatyClip("jump", FloatyFolder + "/floaty_jump.fbx", false),
		new FloatyClip("leap", FloatyFolder + "/floaty_leap.fbx", true),
		new FloatyClip("wall_grab", FloatyFolder + "/floaty_wall_grab.fbx", false),
		new FloatyClip("slide", FloatyFolder + "/floaty_slide.fbx", false),
		new FloatyClip("taunt", FloatyFolder + "/floaty_taunt.fbx", true),
		new FloatyClip("die", FloatyFolder + "/floaty_die.fbx", false)
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

		ConfigureWallGrabNestImporter();
		AssetDatabase.Refresh();

		AnimatorController controller = CreateController();
		GameObject floaty = showDialog
			? AssignFloatyToPlayer(controller)
			: ConfigureDemoScene(controller);
		bool configuredPlayerPrefab = ConfigurePlayerPrefab(controller);

		AssetDatabase.SaveAssets();
		if (floaty != null)
		{
			EditorSceneManager.MarkAllScenesDirty();
			EditorSceneManager.SaveOpenScenes();
		}

		string sceneMessage;
		if (floaty != null && configuredPlayerPrefab)
			sceneMessage = "Controller, Player prefab, Floaty model, and wall-grab nest configured.";
		else if (configuredPlayerPrefab)
			sceneMessage = "Controller and Player prefab configured. Open the demo scene to refresh its Player instance.";
		else
			sceneMessage = "Controller created, but the Player prefab could not be configured.";

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
		importer.indexFormat = ModelImporterIndexFormat.UInt32;
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

	static void ConfigureWallGrabNestImporter()
	{
		ModelImporter importer = AssetImporter.GetAtPath(WallGrabNestPath) as ModelImporter;
		if (importer == null)
		{
			Debug.LogError("Could not find the wall-grab nest FBX importer at " + WallGrabNestPath + ".");
			return;
		}

		importer.animationType = ModelImporterAnimationType.None;
		importer.importAnimation = false;
		importer.importCameras = false;
		importer.importLights = false;
		importer.addCollider = false;
		importer.indexFormat = ModelImporterIndexFormat.UInt32;
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
			if (Clips[i].StateName == "walk")
			{
				state.speedParameterActive = true;
				state.speedParameter = "NormalizedSpeed";
			}

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

		return ConfigurePlayer(player, controller);
	}

	static GameObject ConfigureDemoScene(RuntimeAnimatorController controller)
	{
		if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
		{
			Debug.LogError("Could not find the demo scene at " + DemoScenePath + ".");
			return null;
		}

		UnityEngine.SceneManagement.Scene demoScene =
			EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
		GameObject player = GameObject.Find(PlayerName);
		if (player == null)
		{
			Debug.LogError("Could not find a GameObject named '" + PlayerName + "' in " + DemoScenePath + ".");
			return null;
		}

		GameObject floaty = ConfigurePlayer(player, controller);
		if (floaty != null)
		{
			EditorSceneManager.MarkSceneDirty(demoScene);
			EditorSceneManager.SaveScene(demoScene);
		}

		return floaty;
	}

	static bool ConfigurePlayerPrefab(RuntimeAnimatorController controller)
	{
		GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
		if (prefabRoot == null)
		{
			Debug.LogError("Could not load the Player prefab at " + PlayerPrefabPath + ".");
			return false;
		}

		try
		{
			GameObject floaty = ConfigurePlayer(prefabRoot, controller);
			if (floaty == null)
				return false;

			PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
			return true;
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(prefabRoot);
		}
	}

	static GameObject ConfigurePlayer(GameObject player, RuntimeAnimatorController controller)
	{
		Transform existingFloaty = player.transform.Find("Floaty");
		GameObject floaty = existingFloaty != null ? existingFloaty.gameObject : InstantiateFloatyModel(player.transform);
		if (floaty == null)
			return null;

		GameObject wallGrabNest = InstantiateWallGrabNest(floaty.transform);

		Animator animator = floaty.GetComponent<Animator>();
		if (animator == null)
			animator = floaty.AddComponent<Animator>();

		animator.runtimeAnimatorController = controller;
		animator.applyRootMotion = false;
		animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

		PlatformerAnimation platformerAnimation = player.GetComponent<PlatformerAnimation>();
		PlatformerPhysics platformerPhysics = player.GetComponent<PlatformerPhysics>();
		if (platformerPhysics != null)
		{
			platformerPhysics.groundContactTolerance = FloatyGroundContactTolerance;
			platformerPhysics.groundSnapDistance = FloatyGroundSnapDistance;
			platformerPhysics.wallJumpDetachTime = FloatyWallJumpDetachTime;
			EditorUtility.SetDirty(platformerPhysics);
		}

		if (platformerAnimation != null)
		{
			platformerAnimation.animatedPlayerModel = floaty.transform;
			platformerAnimation.animatedPlayerAnimator = animator;
			platformerAnimation.animatorController = controller;
			platformerAnimation.preferAnimator = true;
			platformerAnimation.sprintState = "sprint";
			platformerAnimation.dashState = "dash";
			platformerAnimation.deathState = "die";
			platformerAnimation.wallState = "wall_grab";
			platformerAnimation.slideModelOffset = new Vector3(0.0f, FloatySlideModelOffsetY, 0.0f);
			platformerAnimation.slideContactOffset = FloatySlideContactOffset;
			platformerAnimation.groundLocomotionFeet = true;
			platformerAnimation.footSoleOffset = FloatyFootSoleOffset;
			platformerAnimation.maxFootGroundingAdjustment = FloatyMaxFootGroundingAdjustment;
			platformerAnimation.idleGroundingOffset = FloatyIdleGroundingOffset;
			platformerAnimation.alignSlideToGroundSlope = FloatyAlignSlideToGroundSlope;
			platformerAnimation.leanWithGroundSlope = true;
			platformerAnimation.maxGroundSlopeLeanAngle = FloatyMaxGroundSlopeLeanAngle;
			platformerAnimation.groundSlopeLeanSmoothTime = FloatyGroundSlopeLeanSmoothTime;
			platformerAnimation.wallGrabProp = wallGrabNest;
			EditorUtility.SetDirty(platformerAnimation);
		}

		DisableOldNinja(player.transform, floaty.transform);
		ApplyFloatyMaterial(floaty, wallGrabNest != null ? wallGrabNest.transform : null);
		ApplyWallGrabNestMaterials(wallGrabNest);
		EditorUtility.SetDirty(floaty);
		return floaty;
	}

	static GameObject InstantiateWallGrabNest(Transform floatyTransform)
	{
		Transform existingNest = floatyTransform.Find("WallGrabNest");
		if (existingNest != null)
		{
			ConfigureWallGrabNestContents(existingNest.gameObject);
			existingNest.gameObject.SetActive(false);
			return existingNest.gameObject;
		}

		GameObject nestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallGrabNestPath);
		if (nestPrefab == null)
		{
			Debug.LogError("Could not load the wall-grab nest model at " + WallGrabNestPath + ".");
			return null;
		}

		GameObject nest = PrefabUtility.InstantiatePrefab(nestPrefab, floatyTransform) as GameObject;
		if (nest == null)
		{
			nest = Object.Instantiate(nestPrefab);
			nest.transform.SetParent(floatyTransform, false);
		}

		nest.name = "WallGrabNest";
		nest.transform.localPosition = Vector3.zero;
		nest.transform.localRotation = Quaternion.identity;
		nest.transform.localScale = Vector3.one;
		ConfigureWallGrabNestContents(nest);
		nest.SetActive(false);
		return nest;
	}

	static void ConfigureWallGrabNestContents(GameObject wallGrabNest)
	{
		Renderer[] renderers = wallGrabNest.GetComponentsInChildren<Renderer>(true);
		bool hasNamedNestRenderer = false;
		Renderer fallbackRenderer = null;
		for (int i = 0; i < renderers.Length; i++)
		{
			if (renderers[i].name.ToLowerInvariant().Contains("nest"))
				hasNamedNestRenderer = true;

			if (fallbackRenderer == null ||
				renderers[i].sharedMaterials.Length > fallbackRenderer.sharedMaterials.Length)
			{
				fallbackRenderer = renderers[i];
			}
		}

		for (int i = 0; i < renderers.Length; i++)
		{
			bool isNestRenderer = hasNamedNestRenderer
				? renderers[i].name.ToLowerInvariant().Contains("nest")
				: renderers[i] == fallbackRenderer;
			renderers[i].enabled = isNestRenderer;
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderers[i]);
			EditorUtility.SetDirty(renderers[i]);
		}

		Animator[] animators = wallGrabNest.GetComponentsInChildren<Animator>(true);
		for (int i = 0; i < animators.Length; i++)
			animators[i].enabled = false;

		Animation[] legacyAnimations = wallGrabNest.GetComponentsInChildren<Animation>(true);
		for (int i = 0; i < legacyAnimations.Length; i++)
			legacyAnimations[i].enabled = false;
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

	static void ApplyFloatyMaterial(GameObject floaty, Transform excludedRoot)
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
			if (excludedRoot != null &&
				(renderers[i].transform == excludedRoot || renderers[i].transform.IsChildOf(excludedRoot)))
			{
				continue;
			}

			Material[] materials = renderers[i].sharedMaterials;
			for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
				materials[materialIndex] = material;

			renderers[i].sharedMaterials = materials;
			PrefabUtility.RecordPrefabInstancePropertyModifications(renderers[i]);
			EditorUtility.SetDirty(renderers[i]);
		}
	}

	static void ApplyWallGrabNestMaterials(GameObject wallGrabNest)
	{
		if (wallGrabNest == null)
			return;

		GameObject nestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallGrabNestPath);
		if (nestPrefab == null)
			return;

		Renderer[] sourceRenderers = nestPrefab.GetComponentsInChildren<Renderer>(true);
		Renderer[] instanceRenderers = wallGrabNest.GetComponentsInChildren<Renderer>(true);
		int rendererCount = Mathf.Min(sourceRenderers.Length, instanceRenderers.Length);
		for (int i = 0; i < rendererCount; i++)
		{
			instanceRenderers[i].sharedMaterials = sourceRenderers[i].sharedMaterials;
			PrefabUtility.RecordPrefabInstancePropertyModifications(instanceRenderers[i]);
			EditorUtility.SetDirty(instanceRenderers[i]);
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
