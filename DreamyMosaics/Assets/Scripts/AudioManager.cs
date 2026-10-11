using UnityEngine;
using UnityEngine.UIElements;

public class AudioManager : MonoBehaviour
{
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource sfxSource;

    public AudioClip Music;
    public AudioClip btnGeneralSFX;
    public AudioClip btnExitSFX;
    public AudioClip wrongSFX;
    public AudioClip rightSFX;

    float musicVolume = 1f;
    float sfxVolume = 1f;
    bool volumeConfigured;

    public void Configure(AudioClip musicClip, AudioClip general, AudioClip exit, AudioClip wrong, AudioClip right)
    {
        EnsureSources();
        Music = musicClip;
        btnGeneralSFX = general;
        btnExitSFX = exit;
        wrongSFX = wrong;
        rightSFX = right;
        if (musicSource != null && Music != null)
        {
            musicSource.clip = Music;
            musicSource.loop = true;
            if (!musicSource.isPlaying)
                musicSource.Play();
        }
    }

    public void SetMusicVolume(float value)
    {
        volumeConfigured = true;
        musicVolume = Mathf.Clamp01(value);
        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    public void SetSfxVolume(float value)
    {
        volumeConfigured = true;
        sfxVolume = Mathf.Clamp01(value);
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    public void SetMuted(bool muted)
    {
        if (musicSource != null)
            musicSource.mute = muted;
        if (sfxSource != null)
            sfxSource.mute = muted;
    }

    void Start()
    {
        if (musicSource == null || Music == null)
            return;
        musicSource.clip = Music;
        musicSource.loop = true;
        if (!musicSource.isPlaying)
            musicSource.Play();
        if (volumeConfigured)
        {
            musicSource.volume = musicVolume;
            if (sfxSource != null)
                sfxSource.volume = sfxVolume;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayBtnGeneralSFX()
    {
        PlayClip(btnGeneralSFX);
    }

    public void PlayBtnExitSFX() 
    {
        PlayClip(btnExitSFX);
    }

    public void PlayWrongSFX()
    {
        PlayClip(wrongSFX);
    }

    public void PlayRightSFX()
    {
        PlayClip(rightSFX);
    }

    void PlayClip(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;
        sfxSource.PlayOneShot(clip);
    }

    void EnsureSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }
    }

    //This might not work for the buttons, but I am testing it out
    public void MuteMusic(GameObject barMute)
    {
        if (musicSource == null)
            return;
        if(musicSource.mute == false) 
        {
            barMute.SetActive(true);
            musicSource.mute = true;
        }
        else
        {
            barMute.SetActive(false);
            musicSource.mute = false;
        }
        
    }
}
