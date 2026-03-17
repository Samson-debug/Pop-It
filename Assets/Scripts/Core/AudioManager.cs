using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour that owns all audio playback.
///
/// Two AudioSource children are used:
///   bgMusicSource  — loops the background music track
///   sfxSource      — plays one-shot SFX (letter-complete jingle)
///
/// Individual Bubble objects have their own local AudioSource for pop sounds
/// so that rapid successive pops can overlap naturally.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Singleton
    // ------------------------------------------------------------------ //
    public static AudioManager Instance { get; private set; }

    // ------------------------------------------------------------------ //
    //  Inspector references
    // ------------------------------------------------------------------ //
    [Header("Audio Sources (assign child AudioSource components)")]
    [Tooltip("Looping background music source.")]
    public AudioSource bgMusicSource;

    [Tooltip("One-shot SFX source used for letter-complete sound.")]
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    [Tooltip("BGMusic.ogg — played on loop throughout the game.")]
    public AudioClip bgMusicClip;

    [Tooltip("Pop.ogg — fallback global pop sound (individual bubbles use a local source).")]
    public AudioClip popClip;

    [Tooltip("Letter completed.ogg — played when all bubbles on a letter are popped.")]
    public AudioClip letterCompleteClip;

    [Header("Volume")]
    [Range(0f, 1f)]
    public float bgMusicVolume = 0.4f;

    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (bgMusicSource != null && bgMusicClip != null)
        {
            bgMusicSource.clip = bgMusicClip;
            bgMusicSource.loop = true;
            bgMusicSource.volume = bgMusicVolume;
            bgMusicSource.Play();
        }
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>Play the letter-completion jingle once.</summary>
    public void PlayLetterComplete()
    {
        if (sfxSource != null && letterCompleteClip != null)
            sfxSource.PlayOneShot(letterCompleteClip, sfxVolume);
    }

    /// <summary>
    /// Global fallback pop sound. Prefer the Bubble's local AudioSource
    /// for overlapping pops; this is here as a safety net.
    /// </summary>
    public void PlayPop()
    {
        if (sfxSource != null && popClip != null)
            sfxSource.PlayOneShot(popClip, sfxVolume);
    }

    /// <summary>Adjust background music volume at runtime (e.g. settings panel).</summary>
    public void SetBGMVolume(float volume)
    {
        bgMusicVolume = Mathf.Clamp01(volume);
        if (bgMusicSource != null)
            bgMusicSource.volume = bgMusicVolume;
    }

    /// <summary>Adjust SFX volume at runtime.</summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }
}
