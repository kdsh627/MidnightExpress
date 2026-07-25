using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using VContainer;

public class SceneLoader : IDisposable
{
    private List<string> _scenePathList;
    public IReadOnlyList<string> LoadedScenes => _scenePathList;

    [Inject]
    public SceneLoader()
    {
        _scenePathList = new();
    }

    public virtual void Dispose()
    {
        if (_scenePathList != null)
        {
            _scenePathList.Clear();
        }
    }

    /// <summary>
    /// 씬 열기
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public async UniTask LoadSceneByPath(string path, CancellationToken token)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);

        try
        {
            // 로딩 대기
            await asyncLoad.ToUniTask(cancellationToken: token);

            Scene scene = SceneManager.GetSceneByPath(path);

            SceneManager.SetActiveScene(scene);
            _scenePathList.Add(path);
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"로딩 취소됨: {path}. 좀비 씬 제거 실행");

            UnloadZombieScene(asyncLoad, path).Forget();

            throw;
        }
    }

    private async UniTaskVoid UnloadZombieScene(AsyncOperation asyncLoad, string path)
    {
        await asyncLoad;

        // 다 로딩되면 바로 언로드
        await SceneManager.UnloadSceneAsync(path);
    }

    /// <summary>
    /// 해당 경로의 씬 닫기
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public async UniTask UnloadSceneByPath(string path, CancellationToken token)
    {
        Scene scene = SceneManager.GetSceneByPath(path);

        //해당 씬이 유효한지 확인
        if (scene.IsValid())
        {
            _scenePathList.Remove(path);

            //해당 씬이 활성화 씬이면
            if (SceneManager.GetActiveScene() == scene)
            {
                // [수정 포인트] 리스트에 남은 씬이 있는지 먼저 확인합니다.
                if (_scenePathList.Count > 0)
                {
                    string lastScenePath = _scenePathList.Last();

                    //활성화 씬 변경
                    scene = SceneManager.GetSceneByPath(lastScenePath);
                    SceneManager.SetActiveScene(scene);
                }
                else
                {
                    // 관리 리스트가 비어있다면, 가장 기본 씬(BaseScene)을 활성화 상태로 둡니다.
                    SceneManager.SetActiveScene(SceneManager.GetSceneAt(0));
                }
            }

            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(path);

            await asyncUnload.ToUniTask(cancellationToken: token);
        }
    }


    /// <summary>
    /// 가장 마지막에 열린 씬만 닫기
    /// </summary>
    /// <returns></returns>
    public async UniTask UnloadLastScene(CancellationToken token)
    {
        if (_scenePathList.Count < 1) return;

        string lastScenePath = _scenePathList.Last();

        await UnloadSceneByPath(lastScenePath, token);
    }


    /// <summary>
    /// 리스트 내의 모든 씬 닫기
    /// </summary>
    /// <returns></returns>
    public async UniTask UnloadAllScene(CancellationToken token)
    {
        int last = _scenePathList.Count - 1;

        for (int i = last; i >= 0; i--)
        {
            await UnloadLastScene(token);
        }
    }
}
