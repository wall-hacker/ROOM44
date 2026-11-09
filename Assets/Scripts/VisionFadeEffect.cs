using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class VisionFadeEffect : MonoBehaviour
{
    [Header("References")]
    public Image image;

    [Header("Initial Fade")]
    public float colorFadeDuration = 1.5f;
    public float alphaFadeDuration = 1.5f;

    [Header("Blur Vision")]
    public bool enableBlurVision = true;
    public float blurAlpha = 0.45f;
    public float blurFadeSpeed = 1f;
    public float blurIntervalMin = 1.5f;
    public float blurIntervalMax = 4f;

    [Header("Blink")]
    public bool enableBlinks = true;
    public float blinkIntervalMin = 4f;
    public float blinkIntervalMax = 10f;
    public float blinkDuration = 0.08f;

    private bool started = false;

    [Header("Loop Movement")]
    public Transform playerTransform;
    public Transform startPoint;
    public Transform endPoint;
    public float walkSpeed = 2f;

    private bool isTeleporting = false;

    void Start()
    {
        if (image == null)
            image = GetComponent<Image>();
        
        playerTransform.GetComponent<FirstPersonController>().enabled = false;
        playerTransform.GetComponent<Animator>().SetBool("IsMoving", true);
    }

  void Update()
    {
        if (!started && Input.anyKeyDown)
        {
            started = true;
            StartCoroutine(MainSequence());
        }

        if (started && playerTransform != null && !isTeleporting)
        {
            playerTransform.position += Vector3.back * walkSpeed * Time.deltaTime;

            if (playerTransform.position.z <= endPoint.position.z)
            {
                StartCoroutine(TeleportLoop());
            }
        }
    }
    IEnumerator TeleportLoop()
    {
        if (isTeleporting)
            yield break;

        isTeleporting = true;

        // Close eyes
        yield return StartCoroutine(FadeCurrentAlpha(1f, 0.06f));

        // Hold blink briefly
        yield return new WaitForSeconds(0.05f);

        // Teleport player while fully black
        Vector3 pos = playerTransform.position;
        pos.x = startPoint.position.x;
        pos.y = startPoint.position.y;
        pos.z = startPoint.position.z;

        playerTransform.position = pos;

        // Keep eyes closed for one frame after teleport
        yield return null;

        // Open eyes
        yield return StartCoroutine(FadeCurrentAlpha(0f, 0.06f));

        isTeleporting = false;
    }
    IEnumerator MainSequence()
    {
        yield return StartCoroutine(FadeColorToBlack());
        yield return StartCoroutine(FadeAlpha(0f));

        if (enableBlurVision)
            StartCoroutine(BlurVisionRoutine());

        if (enableBlinks)
            StartCoroutine(BlinkRoutine());
    }

    IEnumerator FadeColorToBlack()
    {
        Color start = image.color;
        float startAlpha = start.a;

        float t = 0f;

        while (t < colorFadeDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / colorFadeDuration);

            Color c = Color.Lerp(
                new Color(start.r, start.g, start.b, startAlpha),
                new Color(0f, 0f, 0f, startAlpha),
                p);

            image.color = c;

            yield return null;
        }
    }

    IEnumerator FadeAlpha(float targetAlpha)
    {
        Color start = image.color;

        float t = 0f;

        while (t < alphaFadeDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / alphaFadeDuration);

            Color c = image.color;
            c.a = Mathf.Lerp(start.a, targetAlpha, p);
            image.color = c;

            yield return null;
        }

        Color final = image.color;
        final.a = targetAlpha;
        image.color = final;
    }

    IEnumerator BlurVisionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(blurIntervalMin, blurIntervalMax));

            yield return StartCoroutine(FadeCurrentAlpha(blurAlpha));

            yield return new WaitForSeconds(
                Random.Range(0.2f, 1f));

            yield return StartCoroutine(FadeCurrentAlpha(0f));
        }
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(blinkIntervalMin, blinkIntervalMax));

            // Fast close
            yield return StartCoroutine(FadeCurrentAlpha(1f, blinkDuration));

            // Fast open
            yield return StartCoroutine(FadeCurrentAlpha(0f, blinkDuration));
        }
    }

    IEnumerator FadeCurrentAlpha(float targetAlpha, float durationOverride = -1f)
    {
        float duration = durationOverride > 0f
            ? durationOverride
            : (1f / blurFadeSpeed);

        Color start = image.color;
        float startAlpha = start.a;

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float p = Mathf.SmoothStep(0f, 1f, t / duration);

            Color c = image.color;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, p);
            image.color = c;

            yield return null;
        }

        Color final = image.color;
        final.a = targetAlpha;
        image.color = final;
    }
}