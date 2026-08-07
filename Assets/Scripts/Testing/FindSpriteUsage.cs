using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FindSpriteUsage : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(CheckNextFrame());
    }

    IEnumerator CheckNextFrame()
    {
        yield return new WaitForEndOfFrame();
        yield return null; // un frame extra de margen

        var images = FindObjectsOfType<Image>(true);
        //Debug.Log($"Total de Images encontradas en la escena: {images.Length}");

        foreach (var img in images)
        {
            if (img.sprite != null)
            {
                //Debug.Log($"Image '{img.gameObject.name}' (path: {GetPath(img.transform)}) usa sprite '{img.sprite.name}'", img);
            }
        }
    }

    string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}