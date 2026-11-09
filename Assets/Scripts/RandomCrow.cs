using UnityEngine;
using System.Collections;

public class RandomCrow : MonoBehaviour
{
    public AudioSource source;
    public float minDelay = 20f;
    public float maxDelay = 60f;

    IEnumerator Start()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));
            source.Play();
        }
    }
}