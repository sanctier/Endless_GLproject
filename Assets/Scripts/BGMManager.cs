using UnityEngine;

/// <summary>
/// Simple persistent background-music manager.
/// - Attach to a GameObject in your initial scene and assign `bgmClip` in the Inspector.
/// - The GameObject will persist across scenes via DontDestroyOnLoad.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [Tooltip("Background music clip to play.")]
    public AudioClip bgmClip;

    [Tooltip("Start playing automatically on Start().")]
    public bool playOnStart = true;

    [Range(0f,1f)]
    public float volume = 1f;

    private AudioSource source;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D music
        source.volume = Mathf.Clamp01(volume);
        if (bgmClip != null)
        {
            source.clip = bgmClip;
            if (playOnStart) source.Play();
        }
    }

    /// <summary>
    /// Play the assigned BGM clip.
    /// </summary>
    public void Play()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source.clip == null && bgmClip != null) source.clip = bgmClip;
        if (source.clip != null && !source.isPlaying)
            source.Play();
    }

    /// <summary>
    /// Stop playback.
    /// </summary>
    public void Stop()
    {
        if (source != null && source.isPlaying) source.Stop();
    }

    /// <summary>
    /// Set the music volume (0-1).
    /// </summary>
    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (source != null) source.volume = volume;
    }

    /// <summary>
    /// Mute/unmute the music source.
    /// </summary>
    public void SetMute(bool mute)
    {
        if (source != null) source.mute = mute;
    }
}
