using UnityEngine;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";

    private void Start()
    {
        if (!SceneManager.GetSceneByName(gameSceneName).isLoaded)
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
