using UnityEngine;

[CreateAssetMenu(fileName = "SceneData", menuName = "Scriptable Objects/SceneData")]
public class SceneData : ScriptableObject
{
    [SerializeField] private string _homeScenePath;
    [SerializeField] private string[] _gameScenePath;
    public string HomeScenePath =>  _homeScenePath;
    public string[] GameScenePath =>  _gameScenePath;
}
