#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only opt-in profile isolation for presentation capture sessions.</summary>
public static class NeonPresentationQAProfile
{
    public const string SessionKey = "NeonReflex.PresentationQA.Profile";
    public static string DirectoryPath { get; private set; }
    public static bool IsActive => !string.IsNullOrEmpty(DirectoryPath);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        DirectoryPath = SessionState.GetString(SessionKey, string.Empty);
    }
}

/// <summary>Coroutine host exists only in editor play mode and is never serialized.</summary>
public sealed class NeonPresentationQARunner : MonoBehaviour { }
#endif
