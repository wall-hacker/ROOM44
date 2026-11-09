using System.Collections;
using UnityEngine;
using TMPro;

namespace DialogueSystem.UI
{
    public class HintUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private float fadeDuration = 0.15f;

        private Coroutine _fadeCoroutine;
        private int _fadeVersion;
        private bool _isShowing;

        public void Show(string text)
        {
            _isShowing = true;

            if (hintText != null)
                hintText.text = text;

            gameObject.SetActive(true);
            StartFade(1f);
        }

        public void Hide()
        {
            _isShowing = false;
            StartFade(0f, deactivateOnFinish: true);
        }

        private void StartFade(float target, bool deactivateOnFinish = false)
        {
            _fadeVersion++;
            int version = _fadeVersion;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            if (!gameObject.activeInHierarchy)
                gameObject.SetActive(true);

            _fadeCoroutine = StartCoroutine(FadeCoroutine(target, deactivateOnFinish, version));
        }

        private IEnumerator FadeCoroutine(float target, bool deactivateOnFinish, int version)
        {
            if (canvasGroup == null)
                yield break;

            float start = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                if (version != _fadeVersion)
                    yield break;

                canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (version != _fadeVersion)
                yield break;

            canvasGroup.alpha = target;

            if (deactivateOnFinish && target <= 0f && !_isShowing)
                gameObject.SetActive(false);
        }
    }
}