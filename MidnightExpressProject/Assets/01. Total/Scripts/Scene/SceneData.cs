using System;
using UnityEngine;

public enum GameSceneState
{
    Bootstrap,
    Title,
    GameEntry,
    Game
}

[CreateAssetMenu(fileName = "SceneData", menuName = "Scriptable Objects/SceneData")]
public sealed class SceneData : ScriptableObject
{
    [Header("Scene names registered in Build Settings")]
    [SerializeField] private string _bootstrapSceneName = "BootstrapScene";
    [SerializeField] private string _titleSceneName = "Title";
    [SerializeField] private string _gameEntrySceneName = "TrainCommuteScene";
    [SerializeField] private string _gameSceneName = "Play";

    public string BootstrapSceneName => _bootstrapSceneName;
    public string TitleSceneName => _titleSceneName;
    public string GameEntrySceneName => _gameEntrySceneName;
    public string GameSceneName => _gameSceneName;

    public string GetSceneName(GameSceneState state)
    {
        return state switch
        {
            GameSceneState.Bootstrap => _bootstrapSceneName,
            GameSceneState.Title => _titleSceneName,
            GameSceneState.GameEntry => _gameEntrySceneName,
            GameSceneState.Game => _gameSceneName,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    public bool TryGetState(string sceneName, out GameSceneState state)
    {
        if (SceneNameEquals(sceneName, _bootstrapSceneName))
        {
            state = GameSceneState.Bootstrap;
            return true;
        }

        if (SceneNameEquals(sceneName, _titleSceneName))
        {
            state = GameSceneState.Title;
            return true;
        }

        if (SceneNameEquals(sceneName, _gameEntrySceneName))
        {
            state = GameSceneState.GameEntry;
            return true;
        }

        if (SceneNameEquals(sceneName, _gameSceneName))
        {
            state = GameSceneState.Game;
            return true;
        }

        state = default;
        return false;
    }

    private static bool SceneNameEquals(string left, string right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _bootstrapSceneName = _bootstrapSceneName?.Trim();
        _titleSceneName = _titleSceneName?.Trim();
        _gameEntrySceneName = _gameEntrySceneName?.Trim();
        _gameSceneName = _gameSceneName?.Trim();
    }
#endif
}
