using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public Animator TransitionAnim;

    public void Transition(string sceneName)
    {
        StartCoroutine(LoadScene(sceneName));
    }

    public void TocarFade(string triggerName) 
    { if (TransitionAnim != null) 
        { 
            TransitionAnim.SetTrigger(triggerName); 
        } 
    
    }

    IEnumerator LoadScene(string sceneName)
    {
        TransitionAnim.SetTrigger("Start");

        yield return new WaitForSeconds(1f);

        SceneManager.LoadScene(sceneName);
    }
}
