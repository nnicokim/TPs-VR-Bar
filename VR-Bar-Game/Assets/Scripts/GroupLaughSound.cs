using UnityEngine;

public class GroupLaughSound : MonoBehaviour
{
    public AudioSource audioSource;
    public float interval = 13f;

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