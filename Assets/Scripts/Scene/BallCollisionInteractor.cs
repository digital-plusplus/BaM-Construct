using UnityEngine;

public class BallCollisionInteractor : MonoBehaviour
{
    AudioSource thump;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        thump = GetComponent<AudioSource>();
    }


    private void OnCollisionEnter(Collision collision)
    {
        thump.Play();
    }
}
