using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerPrefabBuilder
{
	const string DemoScenePath = "Assets/Game/Demo/Game.unity";
	const string PlayerName = "Player";
	const string PrefabFolder = "Assets/Game/Prefabs/Characters";
	const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";

	[MenuItem("Tools/Platfighter/Build Player Prefab From Open Scene")]
	public static void BuildPlayerPrefabFromOpenScene()
	{
		BuildPlayerPrefab();
	}

	public static void BuildPlayerPrefabFromDemoScene()
	{
		EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
		BuildPlayerPrefab();
	}

	static void BuildPlayerPrefab()
	{
		EnsureFolder(PrefabFolder);

		GameObject player = GameObject.Find(PlayerName);
		if (player == null)
		{
			Debug.LogError("Could not find a GameObject named '" + PlayerName + "' in the open scene.");
			return;
		}

		bool success;
		PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath, out success);
		if (!success)
		{
			Debug.LogError("Could not save player prefab to " + PlayerPrefabPath + ".");
			return;
		}

		AssetDatabase.SaveAssets();
		Debug.Log("Saved reusable player prefab to " + PlayerPrefabPath + ".");
	}

	static void EnsureFolder(string folderPath)
	{
		folderPath = folderPath.Replace("\\", "/");
		if (AssetDatabase.IsValidFolder(folderPath))
			return;

		string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
		string folderName = Path.GetFileName(folderPath);

		EnsureFolder(parent);
		AssetDatabase.CreateFolder(parent, folderName);
	}
}
