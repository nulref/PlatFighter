using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartLevelOnContact : MonoBehaviour
{
	void OnTriggerEnter(Collider other)
	{
		if (!other.gameObject.GetComponent<PlatformerPhysics>())
			return;

		Scene activeScene = SceneManager.GetActiveScene();
		SceneManager.LoadScene(activeScene.buildIndex);
	}
}
