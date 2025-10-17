using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [SerializeField] AudioClip musicClip;
    [Range(0f, 1f)][SerializeField] float volume = 0.75f;

    AudioSource src;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; } // prevent duplicates
        Instance = this;
        DontDestroyOnLoad(gameObject);                         // <- persist across scenes

        src = GetComponent<AudioSource>();
        src.clip = musicClip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D
        src.volume = volume;

        if (musicClip) src.Play();
    }

    // Optional helpers
    public void SetVolume(float v) => src.volume = Mathf.Clamp01(v);
    public void PlayClip(AudioClip clip, bool loop = true)
    {
        if (!clip) return;
        src.Stop(); src.clip = clip; src.loop = loop; src.Play();
    }
}
