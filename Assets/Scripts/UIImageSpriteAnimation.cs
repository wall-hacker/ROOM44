using UnityEngine;
using UnityEngine.UI;

public class UIImageSpriteAnimation : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 12f;

    private Image image;
    private int currentFrame;
    private float timer;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    void Update()
    {
        if (frames.Length == 0)
            return;

        timer += Time.deltaTime;

        float frameTime = 1f / framesPerSecond;

        if (timer >= frameTime)
        {
            timer -= frameTime;

            currentFrame++;
            currentFrame %= frames.Length;

            image.sprite = frames[currentFrame];
        }
    }
}