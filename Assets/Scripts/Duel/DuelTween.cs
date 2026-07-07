using System.Collections;
using UnityEngine;

/// <summary>
/// Mini-librería de animaciones por corrutina para la escena de duelo 3D.
/// Sin dependencias externas: todo son lerps con easing suave (smoothstep).
/// Cada método es un IEnumerator que se consume con yield return.
/// </summary>
public static class DuelTween
{
    /// <summary>Easing suave estándar (smoothstep).</summary>
    private static float Ease(float t) => t * t * (3f - 2f * t);

    /// <summary>Mueve un transform a una posición de mundo.</summary>
    public static IEnumerator MoveTo(Transform t, Vector3 target, float duration)
    {
        Vector3 start = t.position;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            if (t == null) yield break;
            t.position = Vector3.LerpUnclamped(start, target, Ease(e / duration));
            yield return null;
        }
        if (t != null) t.position = target;
    }

    /// <summary>Mueve en arco (parábola) — para invocaciones y saltos de carta.</summary>
    public static IEnumerator Arc(Transform t, Vector3 from, Vector3 to, float height, float duration)
    {
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            if (t == null) yield break;
            float k = Ease(e / duration);
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += height * 4f * k * (1f - k);   // parábola
            t.position = p;
            yield return null;
        }
        if (t != null) t.position = to;
    }

    /// <summary>Rota un transform hacia una rotación destino.</summary>
    public static IEnumerator RotateTo(Transform t, Quaternion target, float duration)
    {
        Quaternion start = t.rotation;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            if (t == null) yield break;
            t.rotation = Quaternion.SlerpUnclamped(start, target, Ease(e / duration));
            yield return null;
        }
        if (t != null) t.rotation = target;
    }

    /// <summary>Escala un transform (aparecer/desaparecer/absorber).</summary>
    public static IEnumerator ScaleTo(Transform t, Vector3 target, float duration)
    {
        Vector3 start = t.localScale;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            if (t == null) yield break;
            t.localScale = Vector3.LerpUnclamped(start, target, Ease(e / duration));
            yield return null;
        }
        if (t != null) t.localScale = target;
    }

    /// <summary>Sacudida (impacto de ataque).</summary>
    public static IEnumerator Shake(Transform t, float amplitude, float duration)
    {
        Vector3 origin = t.position;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            if (t == null) yield break;
            float damp = 1f - (e / duration);
            t.position = origin + Random.insideUnitSphere * amplitude * damp;
            yield return null;
        }
        if (t != null) t.position = origin;
    }

    /// <summary>Embestida: va hacia el objetivo y regresa (ataque).</summary>
    public static IEnumerator Lunge(Transform t, Vector3 target, float duration)
    {
        Vector3 origin = t.position;
        float half = duration * 0.5f;
        // Ida (rápida, acelerada)
        for (float e = 0f; e < half; e += Time.deltaTime)
        {
            if (t == null) yield break;
            float k = e / half;
            t.position = Vector3.Lerp(origin, target, k * k);
            yield return null;
        }
        // Vuelta
        for (float e = 0f; e < half; e += Time.deltaTime)
        {
            if (t == null) yield break;
            t.position = Vector3.Lerp(target, origin, Ease(e / half));
            yield return null;
        }
        if (t != null) t.position = origin;
    }

    /// <summary>Giro continuo mientras se ejecuta (carta descartada).</summary>
    public static IEnumerator Spin(Transform t, Vector3 axis, float degreesPerSecond, float duration)
    {
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            if (t == null) yield break;
            t.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.World);
            yield return null;
        }
    }

    /// <summary>Fade de un CanvasGroup (cartas en canvas de mundo).</summary>
    public static IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
    {
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            if (cg == null) yield break;
            cg.alpha = Mathf.Lerp(from, to, e / duration);
            yield return null;
        }
        if (cg != null) cg.alpha = to;
    }

    /// <summary>Ejecuta varias corrutinas EN PARALELO y espera a que acaben todas.</summary>
    public static IEnumerator Parallel(MonoBehaviour host, params IEnumerator[] routines)
    {
        int running = routines.Length;
        foreach (var r in routines)
            host.StartCoroutine(Wrap(r, () => running--));
        while (running > 0) yield return null;
    }

    private static IEnumerator Wrap(IEnumerator inner, System.Action onDone)
    {
        yield return inner;
        onDone?.Invoke();
    }
}
