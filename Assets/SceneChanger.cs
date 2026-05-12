using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneChanger : MonoBehaviour
{
    public string nextSceneName; // The name of the scene to load
    public void SceneMove()
    {
        SceneManager.LoadScene(nextSceneName); // Load the specified scene
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
}
