using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioClip swordSwingClip;
    private AudioSource audioSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Play(string soundName)
    {
        if (soundName == "SwordSwing" && swordSwingClip != null)
        {
            audioSource.PlayOneShot(swordSwingClip);
        }
        // Add more sounds as needed
    }
}
