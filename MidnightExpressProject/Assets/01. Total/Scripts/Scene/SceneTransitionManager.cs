using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public class SceneTransitionManager
{
    private readonly GameObject _loadingScreen;
    private readonly SceneLoader _sceneLoader;

    [Inject]
    public SceneTransitionManager(GameObject loadingScreen, SceneLoader sceneLoader)
    {
        _loadingScreen = loadingScreen;
        _sceneLoader = sceneLoader;
    }

    // ✨ [수정] forceReload 매개변수를 추가해 줍니다. 기본값은 false.
    public async UniTask TransitionToScenes(List<string> requestedScenes, CancellationToken token = default, bool forceReload = false)
    {
        using (var loading = new LoadingUIStarter(_loadingScreen))
        {
            try
            {
                // ✨ [수정] 내부 함수로 forceReload 값을 넘겨줍니다.
                await ApplySceneChangesAsync(requestedScenes, forceReload, token);
                await UniTask.Delay(TimeSpan.FromSeconds(1));
            }
            catch (OperationCanceledException)
            {
                Debug.Log("<color=yellow>[SceneTransition]</color> 씬 전환 작업 취소됨.");
            }
            catch (Exception e)
            {
                Debug.LogError($"<color=red>[SceneTransition]</color> 씬 전환 중 오류 발생 상세 로그:");
                Debug.LogException(e); 
            }
        }
    }

    // ✨ [수정] forceReload 매개변수 추가
    private async UniTask ApplySceneChangesAsync(List<string> requestedScenes, bool forceReload, CancellationToken token)
    {
        var currentScenes = _sceneLoader.LoadedScenes.ToList();

        List<string> scenesToUnload;
        List<string> scenesToLoad;

        if (forceReload)
        {
            // ✨ 강제 재시작: 현재 씬을 모두 언로드 리스트에 넣고, 요청받은 씬을 전부 로드 리스트에 넣음
            scenesToUnload = currentScenes.ToList(); 
            scenesToLoad = requestedScenes.ToList();
        }
        else
        {
            // 기본 동작: 차집합 계산 (지울 씬, 새로 열 씬)
            scenesToUnload = currentScenes.Except(requestedScenes).ToList();
            scenesToLoad = requestedScenes.Except(currentScenes).ToList();
        }

        // 불필요한 (혹은 재시작할) 씬 역순 언로드
        for (int i = currentScenes.Count - 1; i >= 0; i--)
        {
            string scenePath = currentScenes[i];
            if (scenesToUnload.Contains(scenePath))
            {
                await _sceneLoader.UnloadSceneByPath(scenePath, token);
            }
        }

        // 새로운 (혹은 재시작할) 씬 로드
        foreach (var scenePath in scenesToLoad)
        {
            await _sceneLoader.LoadSceneByPath(scenePath, token);
        }
    }
}