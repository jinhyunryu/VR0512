using System.Collections;
using UnityEngine;

public class LoadingScene : MonoBehaviour
{
    CanvasGroup canvasGroup;
    public float fadetime = 2f;
    Camera loadingCamera;
    Transform camTarget;

    private void Awake()
    {
        loadingCamera = transform.parent.GetComponent<Camera>();
        camTarget = Camera.main.transform;
        
        DontDestroyOnLoad(loadingCamera);
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        if(camTarget != null)
        {
            loadingCamera.transform.position = camTarget.position;
            loadingCamera.transform.rotation = camTarget.rotation;
        }
    }

    private void Start()
    {
        StartCoroutine(Fade(false));
    }
    IEnumerator Fade(bool isOut)
    {
        float startAlpha = isOut ? 0 : 1;
        float endAlpha = isOut ? 1 : 0;
        float timer = 0;

        while(true)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadetime);
            if(timer >= fadetime)
            {
                break;
            }
            yield return null;
        }
        canvasGroup.alpha = endAlpha;
        if(canvasGroup.alpha == 0)
        {
           gameObject.SetActive(false);
        }
    
    }

}
