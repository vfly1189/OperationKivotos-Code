using System.Collections.Generic;
using UnityEngine;

public class SoundManager
{
    AudioSource[] _audioSources = new AudioSource[(int)Define.Sound.MaxCount];


    // MP3 player     -> AudioSource
    // MP3 음원       -> AudioClip
    // 관객(귀)       -> AudioListener

    float _bgmVolume = 0.2f;
    float _voiceVolume = 0.7f;
    float _sfxVolume = 0.2f;

    public float BgmVolume => _bgmVolume;
    public float SfxVolume => _sfxVolume;
    public float VoiceVolume => _voiceVolume;


    public void Init()
    {
        GameObject root = GameObject.Find("@Sound");
        if (root == null)
        {
            root = new GameObject { name = "@Sound" };
            Object.DontDestroyOnLoad(root);

            string[] soundNames = System.Enum.GetNames(typeof(Define.Sound));

            for (int i = 0; i < soundNames.Length - 1; i++)
            {
                GameObject go = new GameObject { name = soundNames[i] };
                _audioSources[i] = go.AddComponent<AudioSource>();
                go.transform.parent = root.transform;
            }

            _audioSources[(int)Define.Sound.Bgm].loop = true;
        }
    }

    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);

        // 현재 재생 중인 BGM AudioSource에도 즉시 반영
        AudioSource bgmSource = _audioSources[(int)Define.Sound.Bgm];
        if (bgmSource != null)
        {
            bgmSource.volume = _bgmVolume;
        }

        // (선택) 저장: PlayerPrefs.SetFloat("BGM_VOL", _bgmVolume);
    }

    // SFX (효과음) 볼륨 조절
    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);

        // SFX는 PlayOneShot으로 재생되므로 AudioSource의 볼륨 자체를 미리 바꿔둠
        AudioSource sfxSource = _audioSources[(int)Define.Sound.Effect];
        if (sfxSource != null)
        {
            sfxSource.volume = _sfxVolume;
        }
    }

    public void SetVoiceVolume(float volume)
    {
        _voiceVolume = Mathf.Clamp01(volume);

        // SFX는 PlayOneShot으로 재생되므로 AudioSource의 볼륨 자체를 미리 바꿔둠
        AudioSource voiceSource = _audioSources[(int)Define.Sound.Voice];
        if (voiceSource != null)
        {
            voiceSource.volume = _voiceVolume;
        }
    }

    public void Clear()
    {
        foreach (AudioSource audioSource in _audioSources)
        {
            audioSource.clip = null;
            audioSource.Stop();
        }
    }

    public void Play(AudioClip audioClip, Define.Sound type = Define.Sound.Effect, float pitch = 1.0f)
    {
        if (audioClip == null)
            return;

        //BGM
        if (type == Define.Sound.Bgm)
        {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Bgm];

            if (audioSource.isPlaying)
                audioSource.Stop();

            audioSource.volume = _bgmVolume;
            audioSource.pitch = pitch;
            audioSource.clip = audioClip;
            audioSource.Play();
        }
        else if(type == Define.Sound.Voice)
        {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Voice];

            audioSource.volume = _voiceVolume;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(audioClip);
        }
        //other
        else if(type == Define.Sound.Effect)
        {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Effect];

            audioSource.volume = _sfxVolume;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(audioClip);
        }
    }

    public void StopBgm()
    {
        AudioSource bgmSource = _audioSources[(int)Define.Sound.Bgm];
        if (bgmSource.isPlaying)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    // 특정 타입만 정지
    public void Stop(Define.Sound type)
    {
        AudioSource audioSource = _audioSources[(int)type];
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
            if (type == Define.Sound.Bgm)
                audioSource.clip = null;
        }
    }

    // 모든 사운드 정지 (캐시는 유지)
    public void StopAll()
    {
        foreach (AudioSource audioSource in _audioSources)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
        }
        // 캐시는 유지
    }
}
