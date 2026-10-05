using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VTG.Pool.Tests
{
    /// <summary>Unloads scenes loaded by a test so later tests start from an empty world.</summary>
    public static class SceneTestUtility
    {
        public static IEnumerator UnloadAllScenes()
        {
            Scene empty = SceneManager.CreateScene("TestEmpty_" + Time.frameCount);
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded && !scene.name.StartsWith("InitTestScene"))
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            yield return null;
            Time.timeScale = 1f;
        }
    }
}
