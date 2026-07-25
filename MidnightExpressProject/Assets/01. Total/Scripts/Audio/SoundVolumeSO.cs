using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundVolmueSO", menuName = "Scriptable Objects/SoundVolmueSO")]
public class SoundVolumeSO : ScriptableObject
{
    [SerializeField] private float _masterVolume;
    [SerializeField] private float _sfxVolume;
    [SerializeField] private float _bgmVolume;

    public event Action<float> OnChangeMasterVolume;
    public event Action<float> OnChangeSfxVolume;
    public event Action<float> OnChangeBgmVolume;

    public float MasterVolume => _masterVolume;
    public float SfxVolume => _sfxVolume;
    public float BgmVolume => _bgmVolume;
    public void InitStart()
    {
        OnChangeMasterVolume?.Invoke(_masterVolume);
        OnChangeSfxVolume?.Invoke(_sfxVolume);
        OnChangeBgmVolume?.Invoke(_bgmVolume);
    }

    public void UpdateMasterVolume(float volume)
    {
        Debug.Log("전체음 변경");
        _masterVolume = volume;
        OnChangeMasterVolume?.Invoke(volume);
    }

    public void UpdateSfxVolume(float volume)
    {
        _sfxVolume = volume;
        OnChangeSfxVolume?.Invoke(volume);
    }

    public void UpdateBgmVolume(float volume)
    {
        _bgmVolume = volume;
        OnChangeBgmVolume?.Invoke(volume);
    }
}
