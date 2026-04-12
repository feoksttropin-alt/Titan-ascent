using UnityEditor;
using UnityEngine;

/// <summary>
/// Minimal build method used by CI Script Validation.
/// Unity must compile all scripts before it can locate and invoke this method,
/// so any compilation error will cause the CI job to fail before this runs.
/// The method itself does nothing — its only purpose is to be a valid target
/// for the game-ci unity-builder action.
/// </summary>
public static class ValidationBuild
{
    public static void Validate()
    {
        Debug.Log("[CI] Script validation passed — all scripts compiled successfully.");
        EditorApplication.Exit(0);
    }
}
