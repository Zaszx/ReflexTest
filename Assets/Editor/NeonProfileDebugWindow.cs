using System;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only profile controls. No debug UI is created in the scene.</summary>
public sealed class NeonProfileDebugWindow : EditorWindow
{
    private string feedback;

    [MenuItem("Neon Reflex/Debug/Profile Tools")]
    public static void Open()
    {
        var window = GetWindow<NeonProfileDebugWindow>("Profile Debug");
        window.minSize = new Vector2(360, 360);
        window.Show();
    }

    private void OnInspectorUpdate() => Repaint();

    private void OnGUI()
    {
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("NEON REFLEX / PROFILE DEBUG", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Editor only — excluded from all player builds.", EditorStyles.miniLabel);
        EditorGUILayout.Space(12);

        GameManager manager = GameManager.Instance;
        bool ready = manager != null && manager.EditorProfileReady;
        if (!ready)
        {
            feedback = null;
            EditorGUILayout.HelpBox("Enter Play Mode in SampleScene to use these tools.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Banked coins", manager.EditorBankedCoins.ToString("N0"));
            foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(id.ToString()), "Tier " + manager.EditorUpgradeTier(id));
        }

        EditorGUILayout.Space(12);
        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("+1,000 COINS", GUILayout.Height(38)))
                feedback = manager.EditorDebugAddCoins();
            EditorGUILayout.Space(6);
            if (GUILayout.Button("RESET ALL UPGRADES", GUILayout.Height(38)))
                feedback = manager.EditorDebugResetUpgrades();
        }
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("Changes are saved to the current profile. Resetting upgrades keeps your coins; an active run keeps its starting upgrades.", MessageType.None);
        if (!string.IsNullOrEmpty(feedback))
            EditorGUILayout.HelpBox(feedback, MessageType.Info);
    }
}
