using UnityEngine;

public class PeriodicSound : MonoBehaviour
{
    public AudioSource audioSource;
    public float interval = 10f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= interval)
        {
            audioSource.Play();
            timer = 0f;
        }
    }
}