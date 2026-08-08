using UnityEngine;
using VContainer.Unity;

public abstract class ManagedSceneScope : LifetimeScope
{
    protected override void Awake()
    {
        var baseScope = LifetimeScope.Find<BaseScope>();

#if UNITY_EDITOR
        if (!ToolbarPlayButtonsView.OnGetCoreMode && baseScope == null)
        {
            autoRun = false;
            Debug.Log(
                $"[{GetType().Name}] Bootstrap auto-start is disabled. " +
                "The scene is running as a visual-only standalone preview without DI initialization.");
        }
#endif

        if (baseScope != null)
        {
            DisableSceneAudioListeners();
        }

        base.Awake();
    }

    private void DisableSceneAudioListeners()
    {
        var roots = gameObject.scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var listeners = roots[rootIndex].GetComponentsInChildren<AudioListener>(true);
            for (var listenerIndex = 0; listenerIndex < listeners.Length; listenerIndex++)
            {
                listeners[listenerIndex].enabled = false;
            }
        }
    }

#if UNITY_EDITOR
    protected virtual void Reset()
    {
        parentReference = ParentReference.Create<BaseScope>();
        autoRun = true;
    }

    protected virtual void OnValidate()
    {
        if (parentReference.TypeName != typeof(BaseScope).FullName)
        {
            parentReference = ParentReference.Create<BaseScope>();
        }
    }
#endif
}
