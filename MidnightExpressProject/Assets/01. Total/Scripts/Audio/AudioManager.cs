using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("#AudioMixer")]
    public AudioMixer MasterAudioMixer;
    public AudioMixerGroup SFXAudioMixer;
    public AudioMixerGroup BGMAudioMixer;

    [Header("#Volmue")]
    [SerializeField] private SoundVolumeSO _volumeData;
    public SoundVolumeSO VolumData => _volumeData;

    [Header("#BGM")]
    public AudioClip[] BgmClips;
    public int BGMChannels;
    private AudioSource _bgmPlayer;

    [Header("#SFX")]
    public AudioClip[] SfxClips;
    public int SFXChannels;
    private AudioSource[] _sfxPlayers;
    private int _sfxChannelIndex;

    [Header("#LoopingSFX")]
    public AudioClip[] LoopingSfxClips;
    public int LoopingChannels;
    private AudioSource[] _loopingSfxPlayers;
    private int _loopingChannelIndex;

    private GameObject _target = null;

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
        Skill = 10,

    }
    public enum LoopSfx
    {
        None = -1
    }

    private void Awake()
    {
        Instance = this;

        _volumeData.OnChangeMasterVolume += UpdateMasterVolmue;
        _volumeData.OnChangeSfxVolume += UpdateSfxVolmue;
        _volumeData.OnChangeBgmVolume += UpdateBgmVolmue;

        Init();
    }

    private void Start()
    {
        _volumeData.InitStart();
    }

    private void OnDestroy()
    {
        _volumeData.OnChangeMasterVolume -= UpdateMasterVolmue;
        _volumeData.OnChangeSfxVolume -= UpdateSfxVolmue;
        _volumeData.OnChangeBgmVolume -= UpdateBgmVolmue;

        StopBgm();
    }

    private void LateUpdate()
    {
        if (_target != null)
        {
            transform.position = _target.transform.position;
        }
    }

    private void Init()
    {
        //배경음 플레이어 초기화
        GameObject bgmObject = new GameObject("BgmPlayer");
        bgmObject.transform.parent = transform;

        _bgmPlayer = bgmObject.AddComponent<AudioSource>();
        _bgmPlayer.playOnAwake = false;
        _bgmPlayer.loop = true;
        _bgmPlayer.outputAudioMixerGroup = BGMAudioMixer;

        //효과음 플레이어 초기화
        GameObject sfxObject = new GameObject("SfxPlayer");
        sfxObject.transform.parent = transform;
        _sfxPlayers = new AudioSource[SFXChannels];

        for (int index = 0; index < _sfxPlayers.Length; index++)
        {
            _sfxPlayers[index] = sfxObject.AddComponent<AudioSource>();
            _sfxPlayers[index].playOnAwake = false;
            _sfxPlayers[index].outputAudioMixerGroup = SFXAudioMixer;
        }

        //반복 재생이 필요한 효과음 플레이어 초기화
        GameObject loopingSfxObject = new GameObject("LoopingSfxPlayer");
        loopingSfxObject.transform.parent = transform;
        _loopingSfxPlayers = new AudioSource[LoopingChannels];

        for (int index = 0; index < _loopingSfxPlayers.Length; index++)
        {
            _loopingSfxPlayers[index] = loopingSfxObject.AddComponent<AudioSource>();
            _loopingSfxPlayers[index].playOnAwake = false;
            _loopingSfxPlayers[index].outputAudioMixerGroup = SFXAudioMixer;
            _loopingSfxPlayers[index].loop = true;
        }
    }
    public void UpdateMasterVolmue(float volume)
    {
        MasterAudioMixer.SetFloat("Master", Mathf.Log10(volume) * 20);
    }

    public void UpdateSfxVolmue(float volume)
    {
        MasterAudioMixer.SetFloat("SFX", Mathf.Log10(volume) * 20);
    }

    public void UpdateBgmVolmue(float volume)
    {
        MasterAudioMixer.SetFloat("BGM", Mathf.Log10(volume) * 20);
    }

    public void StopBgm()
    {
        if (_bgmPlayer.isPlaying)
        {
            _bgmPlayer.Stop();
        }
    }

    public void PlayBgm(Bgm bgm)
    {
        if (bgm == Bgm.None) return;

        _bgmPlayer.clip = BgmClips[(int)bgm];

        // 새로운 BGM 재생
        _bgmPlayer.Play();
    }

    public void PlayLoopingSfx(LoopSfx sfx)
    {
        for (int index = 0; index < _loopingSfxPlayers.Length; index++)
        {
            int loopIndex = (index + _loopingChannelIndex) % _loopingSfxPlayers.Length;

            if (_loopingSfxPlayers[loopIndex].isPlaying)
            {
                continue;
            }

            _loopingChannelIndex = loopIndex;

            _loopingSfxPlayers[loopIndex].clip = LoopingSfxClips[(int)sfx];
            _loopingSfxPlayers[loopIndex].Play();

            break;
        }
    }

    public void StopLoopingSfx(LoopSfx sfx)
    {
        AudioSource source = _loopingSfxPlayers[0];
        if (source == null) return;

        AudioClip targetClip = source.clip;

        for (int index = 0; index < _loopingSfxPlayers.Length; index++)
        {
            int loopIndex = (index + _loopingChannelIndex) % _loopingSfxPlayers.Length;

            AudioSource player = _loopingSfxPlayers[loopIndex];

            if (player.isPlaying && player.clip == targetClip)
            {
                player.Stop();
                break;
            }
        }
    }

    public void StopSfx(Sfx sfx)
    {
        if (sfx == Sfx.None) return;

        // 1. 멈추고자 하는 SFX의 오디오 클립을 가져옵니다.
        AudioClip targetClip = SfxClips[(int)sfx];

        // 2. 전체 SFX 플레이어를 순회합니다.
        for (int i = 0; i < _sfxPlayers.Length; i++)
        {
            AudioSource player = _sfxPlayers[i];

            // 3. 해당 플레이어가 재생 중이고, 클립이 타겟 클립과 일치하면 정지합니다.
            if (player.isPlaying && player.clip == targetClip)
            {
                player.Stop();
                
                // 참고: 만약 같은 효과음이 여러 채널에서 겹쳐서 재생 중일 때 
                // "전부" 끄고 싶다면 break를 걸지 마시고, 
                // "하나"만 끄고 싶다면 아래 주석을 해제하여 break를 걸어주세요.
                // break; 
            }
        }
    }
    public void PlaySfx(Sfx sfx)
    {
        for (int index = 0; index < _sfxPlayers.Length; index++)
        {
            int loopIndex = (index + _sfxChannelIndex) % _sfxPlayers.Length;

            if (_sfxPlayers[loopIndex].isPlaying)
            {
                continue;
            }

            _sfxChannelIndex = loopIndex;

            _sfxPlayers[loopIndex].clip = SfxClips[(int)sfx];
            _sfxPlayers[loopIndex].Play();

            break;
        }
    }
}
