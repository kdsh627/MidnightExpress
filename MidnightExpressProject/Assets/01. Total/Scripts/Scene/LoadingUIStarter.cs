using System;
using UnityEngine;

public struct LoadingUIStarter : IDisposable
{
    private GameObject _loadingUI;

    public LoadingUIStarter(GameObject loadingUI)
    {
        _loadingUI = loadingUI;
        _loadingUI.SetActive(true);
    }

    public void Dispose()
    {
        _loadingUI.SetActive(false);
    }
}
