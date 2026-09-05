using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NeonReflexCampaign", menuName = "NeonReflex/Campaign Definition")]
public sealed class CampaignDefinition : ScriptableObject
{
    public string campaignId = "neon-reflex-campaign";
    public int campaignVersion = 1;
    public long completionBonusCoins = 250;
    public string generationVersion = "campaign-generator-v1";
    public List<LevelData> levels = new List<LevelData>();

    public int LevelCount => levels == null ? 0 : levels.Count;
    public LevelData GetLevel(int index) => index >= 0 && index < LevelCount ? levels[index] : null;
}
