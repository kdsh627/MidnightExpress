#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Stores the editor-only option that starts play mode from the base scene.
/// The option is exposed through the supported Unity menu extension API.
/// </summary>
[InitializeOnLoad]
public static class ToolbarPlayButtonsView
{
    private const string MenuPath = "Midnight Express/Play From Base Scene";
    private const string PreferenceKey = "MidnightExpress.PlayFromBaseScene";

    public static bool OnGetCoreMode => SessionState.GetBool(PreferenceKey, true);

    static ToolbarPlayButtonsView()
    {
        Menu.SetChecked(MenuPath, OnGetCoreMode);
    }

    [MenuItem(MenuPath)]
    private static void ToggleCoreMode()
    {
        var enabled = !OnGetCoreMode;
        SessionState.SetBool(PreferenceKey, enabled);
        Menu.SetChecked(MenuPath, enabled);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggleCoreMode()
    {
        Menu.SetChecked(MenuPath, OnGetCoreMode);
        return true;
    }
}
#endif
