using System.Collections.Generic;
using UnityEngine;

public class SoundManager
{
    AudioSource[] _audioSources = new AudioSource[(int)Define.Sound.MaxCount];

    Dictionary<string, AudioClip> _audioClips = new Dictionary<string, AudioClip>();

    // MP3 player     -> AudioSource
    // MP3 음원       -> AudioClip
    // 관객(귀)       -> AudioListener

    float _bgmVolume = 0.2f;
    float _narrationVolume = 0.5f;


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

    public void Clear()
    {
        foreach (AudioSource audioSource in _audioSources)
        {
            audioSource.clip = null;
            audioSource.Stop();
        }
        _audioClips.Clear();
    }

    public void Play(string path, Define.Sound type = Define.Sound.Effect, float pitch = 1.0f)
    {
        AudioClip audioClip = GetOrAddAudioClip(path, type);
        Play(audioClip, type, pitch);
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
        else if(type == Define.Sound.Narration)
        {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Narration];

            audioSource.volume = _narrationVolume;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(audioClip);
        }
        //other
        else if(type == Define.Sound.Effect)
        {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Effect];

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

    AudioClip GetOrAddAudioClip(string path, Define.Sound type = Define.Sound.Effect)
    {
        if (path.Contains("Sounds/") == false)
            path = $"Sounds/{path}";

        AudioClip audioClip = null;

        //BGM
        if (type == Define.Sound.Bgm)
        {
            audioClip = Managers.Resource.Load<AudioClip>(path);
        }
        //other
        else
        {
            if (_audioClips.TryGetValue(path, out audioClip) == false)
            {
                audioClip = Managers.Resource.Load<AudioClip>(path);
                _audioClips.Add(path, audioClip);
            }
        }

        if (audioClip == null)
            Debug.Log($"AudioClip Missing ! {path}");

        return audioClip;
    }
}
