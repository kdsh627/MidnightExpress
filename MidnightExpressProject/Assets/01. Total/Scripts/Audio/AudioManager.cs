using UnityEngine;
using UnityEngine.Audio;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    public AudioMixer MasterAudioMixer;
    public AudioMixerGroup SFXAudioMixer;
    public AudioMixerGroup BGMAudioMixer;

    [Header("Volume")]
    [SerializeField] private SoundVolumeSO _volumeData;
    public SoundVolumeSO VolumData => _volumeData;

    [Header("BGM")]
    public AudioClip[] BgmClips;
    public int BGMChannels;
    private AudioSource _bgmPlayer;

    [Header("SFX")]
    public AudioClip[] SfxClips;
    public int SFXChannels;
    private AudioSource[] _sfxPlayers;
    private int _sfxChannelIndex;

    [Header("Looping SFX")]
    public AudioClip[] LoopingSfxClips;
    public int LoopingChannels;
    private AudioSource[] _loopingSfxPlayers;
    private int _loopingChannelIndex;

    private GameObject _target;

    public enum Bgm
    {
        None = -1,
        InGame = 0,
        OutGame = 1,
        Result = 2
    }

    public enum Sfx
    {
        None = -1,
        Click = 0,
        BossEffectCoin = 1,
        BossEffect = 2,
        Gacha = 3,
        Coin = 4,
        Kick = 5,
        Punch = 6,
        Dodge = 7,
        GameOver = 8,
        Fever = 9,
        Skill = 10
    }

    public enum LoopSfx
    {
        None = -1
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AudioManager] More than one AudioManager is active.", this);
        }

        Instance = this;

        if (_volumeData != null)
        {
            _volumeData.OnChangeMasterVolume += UpdateMasterVolmue;
            _volumeData.OnChangeSfxVolume += UpdateSfxVolmue;
            _volumeData.OnChangeBgmVolume += UpdateBgmVolmue;
        }

        InitializePlayers();
    }

    private void Start()
    {
        _volumeData?.InitStart();
    }

    private void OnDestroy()
    {
        if (_volumeData != null)
        {
            _volumeData.OnChangeMasterVolume -= UpdateMasterVolmue;
            _volumeData.OnChangeSfxVolume -= UpdateSfxVolmue;
            _volumeData.OnChangeBgmVolume -= UpdateBgmVolmue;
        }

        StopBgm();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        if (_target != null)
        {
            transform.SetPositionAndRotation(
                _target.transform.position,
                _target.transform.rotation);
        }
    }

    public void SetListenerTarget(GameObject target)
    {
        _target = target;
    }

    private void InitializePlayers()
    {
        var bgmObject = new GameObject("BgmPlayer");
        bgmObject.transform.SetParent(transform, false);

        _bgmPlayer = bgmObject.AddComponent<AudioSource>();
        _bgmPlayer.playOnAwake = false;
        _bgmPlayer.loop = true;
        _bgmPlayer.outputAudioMixerGroup = BGMAudioMixer;

        var sfxObject = new GameObject("SfxPlayer");
        sfxObject.transform.SetParent(transform, false);
        _sfxPlayers = new AudioSource[Mathf.Max(0, SFXChannels)];

        for (var index = 0; index < _sfxPlayers.Length; index++)
        {
            _sfxPlayers[index] = sfxObject.AddComponent<AudioSource>();
            _sfxPlayers[index].playOnAwake = false;
            _sfxPlayers[index].outputAudioMixerGroup = SFXAudioMixer;
        }

        var loopingSfxObject = new GameObject("LoopingSfxPlayer");
        loopingSfxObject.transform.SetParent(transform, false);
        _loopingSfxPlayers = new AudioSource[Mathf.Max(0, LoopingChannels)];

        for (var index = 0; index < _loopingSfxPlayers.Length; index++)
        {
            _loopingSfxPlayers[index] = loopingSfxObject.AddComponent<AudioSource>();
            _loopingSfxPlayers[index].playOnAwake = false;
            _loopingSfxPlayers[index].outputAudioMixerGroup = SFXAudioMixer;
            _loopingSfxPlayers[index].loop = true;
        }
    }

    public void UpdateMasterVolmue(float volume)
    {
        SetMixerVolume("Master", volume);
    }

    public void UpdateSfxVolmue(float volume)
    {
        SetMixerVolume("SFX", volume);
    }

    public void UpdateBgmVolmue(float volume)
    {
        SetMixerVolume("BGM", volume);
    }

    public void StopBgm()
    {
        if (_bgmPlayer != null && _bgmPlayer.isPlaying)
        {
            _bgmPlayer.Stop();
        }
    }

    public void PlayBgm(Bgm bgm)
    {
        TryPlayBgm(bgm);
    }

    public bool TryPlayBgm(Bgm bgm)
    {
        var clipIndex = (int)bgm;
        if (_bgmPlayer == null || !TryGetClip(BgmClips, clipIndex, out var clip))
        {
            return false;
        }

        _bgmPlayer.clip = clip;
        _bgmPlayer.Play();
        return true;
    }

    public void PlayLoopingSfx(LoopSfx sfx)
    {
        var clipIndex = (int)sfx;
        if (!TryGetClip(LoopingSfxClips, clipIndex, out var clip) ||
            _loopingSfxPlayers == null ||
            _loopingSfxPlayers.Length == 0)
        {
            return;
        }

        for (var index = 0; index < _loopingSfxPlayers.Length; index++)
        {
            var loopIndex = (index + _loopingChannelIndex) % _loopingSfxPlayers.Length;
            if (_loopingSfxPlayers[loopIndex].isPlaying)
            {
                continue;
            }

            _loopingChannelIndex = loopIndex;
            _loopingSfxPlayers[loopIndex].clip = clip;
            _loopingSfxPlayers[loopIndex].Play();
            break;
        }
    }

    public void StopLoopingSfx(LoopSfx sfx)
    {
        var clipIndex = (int)sfx;
        if (!TryGetClip(LoopingSfxClips, clipIndex, out var clip) || _loopingSfxPlayers == null)
        {
            return;
        }

        for (var index = 0; index < _loopingSfxPlayers.Length; index++)
        {
            var player = _loopingSfxPlayers[index];
            if (player != null && player.isPlaying && player.clip == clip)
            {
                player.Stop();
            }
        }
    }

    public void StopSfx(Sfx sfx)
    {
        var clipIndex = (int)sfx;
        if (!TryGetClip(SfxClips, clipIndex, out var clip) || _sfxPlayers == null)
        {
            return;
        }

        for (var index = 0; index < _sfxPlayers.Length; index++)
        {
            var player = _sfxPlayers[index];
            if (player != null && player.isPlaying && player.clip == clip)
            {
                player.Stop();
            }
        }
    }

    public void PlaySfx(Sfx sfx)
    {
        var clipIndex = (int)sfx;
        if (!TryGetClip(SfxClips, clipIndex, out var clip) ||
            _sfxPlayers == null ||
            _sfxPlayers.Length == 0)
        {
            return;
        }

        for (var index = 0; index < _sfxPlayers.Length; index++)
        {
            var loopIndex = (index + _sfxChannelIndex) % _sfxPlayers.Length;
            if (_sfxPlayers[loopIndex].isPlaying)
            {
                continue;
            }

            _sfxChannelIndex = loopIndex;
            _sfxPlayers[loopIndex].clip = clip;
            _sfxPlayers[loopIndex].Play();
            break;
        }
    }

    private void SetMixerVolume(string exposedParameter, float volume)
    {
        if (MasterAudioMixer == null)
        {
            return;
        }

        var decibels = volume <= 0f
            ? -80f
            : Mathf.Log10(Mathf.Clamp01(volume)) * 20f;

        MasterAudioMixer.SetFloat(exposedParameter, decibels);
    }

    private static bool TryGetClip(AudioClip[] clips, int index, out AudioClip clip)
    {
        if (clips != null && index >= 0 && index < clips.Length && clips[index] != null)
        {
            clip = clips[index];
            return true;
        }

        clip = null;
        return false;
    }
}
