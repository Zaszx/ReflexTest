#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Creates the editable, fixed campaign only when it is absent. Explicit regeneration asks before replacement.</summary>
public static class CampaignTestGenerator
{
    private const string CampaignPath = "Assets/Resources/NeonReflexCampaign.asset";
    private const string LevelFolder = "Assets/Levels/Campaign";
    private const string GeneratorVersion = "campaign-generator-v1";
    [InitializeOnLoadMethod] private static void ScheduleMissingCampaignGeneration() { EditorApplication.delayCall += () => { if (AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath) == null) Generate(false); }; }
    [MenuItem("Neon Reflex/Generate Missing Campaign")] public static void GenerateMissing() => Generate(false);
    [MenuItem("Neon Reflex/Regenerate Campaign (Replace Generated Assets)")] public static void Regenerate() { if(EditorUtility.DisplayDialog("Regenerate campaign?", "This replaces only the 100 known generated campaign assets and the generated campaign definition. Manual assets are not deleted.", "Regenerate", "Cancel")) Generate(true); }
    private static void Generate(bool replace)
    {
        var existing=AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath); if(existing!=null&&!replace){Debug.Log("Neon Reflex campaign already exists; generator did not overwrite it.");return;} if(!AssetDatabase.IsValidFolder("Assets/Levels"))AssetDatabase.CreateFolder("Assets","Levels");if(!AssetDatabase.IsValidFolder(LevelFolder))AssetDatabase.CreateFolder("Assets/Levels","Campaign");
        for(int i=1;i<=100;i++){string candidate=Path.Combine(LevelFolder,$"Level{i:000}.asset").Replace('\\','/');var present=AssetDatabase.LoadAssetAtPath<LevelData>(candidate);if(present!=null&&present.stableId!=$"neon-reflex-level-{i:000}"){Debug.LogWarning("Campaign generator stopped: a manual LevelData asset occupies "+candidate+". It was not changed.");return;}}
        if(!replace && AssetDatabase.FindAssets("t:LevelData",new[]{LevelFolder}).Length>0){Debug.LogWarning("Campaign generator stopped because level assets already exist without a campaign definition; it will not overwrite them.");return;}
        if(replace){for(int i=1;i<=100;i++){string path=Path.Combine(LevelFolder,$"Level{i:000}.asset").Replace('\\','/');var level=AssetDatabase.LoadAssetAtPath<LevelData>(path);if(level!=null&&level.stableId==$"neon-reflex-level-{i:000}")AssetDatabase.DeleteAsset(path);}if(existing!=null&&existing.generationVersion==GeneratorVersion)AssetDatabase.DeleteAsset(CampaignPath);}
        var campaign=AssetDatabase.LoadAssetAtPath<CampaignDefinition>(CampaignPath);if(campaign==null){campaign=ScriptableObject.CreateInstance<CampaignDefinition>();AssetDatabase.CreateAsset(campaign,CampaignPath);}campaign.campaignId="neon-reflex-campaign";campaign.campaignVersion=1;campaign.completionBonusCoins=500;campaign.generationVersion=GeneratorVersion;campaign.levels.Clear();
        for(int index=1;index<=100;index++){string path=Path.Combine(LevelFolder,$"Level{index:000}.asset").Replace('\\','/');var level=AssetDatabase.LoadAssetAtPath<LevelData>(path);if(level==null){level=ScriptableObject.CreateInstance<LevelData>();AssetDatabase.CreateAsset(level,path);}Configure(level,index);campaign.levels.Add(level);EditorUtility.SetDirty(level);}var config=AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");if(config!=null){UpgradeCatalog.EnsureDefaults(config);EditorUtility.SetDirty(config);}EditorUtility.SetDirty(campaign);AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("Generated Neon Reflex campaign with 100 fixed levels.");
    }
    private static void Configure(LevelData level,int number)
    {
        level.stableId=$"neon-reflex-level-{number:000}";level.levelNumber=number;float progression=(number-1)/99f;level.gridSize=number<6?3:number<=10?4:number<18?3:number<52?4:5;level.requiredCorrectClicks=2*(15+Mathf.RoundToInt((number-1)*65f/99f));level.timeLimit=Mathf.Lerp(14.4f,7.5f,progression);level.completionCoinReward=12+number*3;level.reverseEnabled=false;level.rotateSpeed=0;level.scaleEnabled=false;level.minimumGridScale=.9f;level.maximumGridScale=1f;level.scaleCycleDuration=5f;level.movementEnabled=false;level.movementSpeedNormalized=0;level.movementTravelPaddingNormalized=.03f;
        if(number==2)level.reverseEnabled=true; else if(number==3)level.rotateSpeed=45f; else if(number==4){level.scaleEnabled=true;level.minimumGridScale=.9f;level.maximumGridScale=1.02f;level.scaleCycleDuration=1.6f;}else if(number==5){level.movementEnabled=true;level.movementSpeedNormalized=.135f;}else if(number>=6){int pattern=(number-6)%4;int plateau=(number-1)/10;float rotationMagnitude=45f+plateau*7.5f+((number-1)%10)*.25f;if(number<=20){level.reverseEnabled=pattern==0||pattern==1;level.rotateSpeed=(pattern==0||pattern==2)?(number%2==0?1:-1)*rotationMagnitude:0;level.scaleEnabled=pattern==1||pattern==3;level.movementEnabled=pattern==2||pattern==3;}else if(number<=40){level.reverseEnabled=pattern!=0&&pattern!=3;level.rotateSpeed=pattern==0?0:(number%2==0?1:-1)*rotationMagnitude;level.scaleEnabled=pattern!=1;level.movementEnabled=pattern!=2;}else{level.reverseEnabled=true;level.rotateSpeed=(number%2==0?1:-1)*rotationMagnitude;level.scaleEnabled=true;level.movementEnabled=true;}if(level.scaleEnabled){level.minimumGridScale=.9f-.06f*progression;level.maximumGridScale=1.02f+.05f*progression;level.scaleCycleDuration=Mathf.Lerp(1.6f,1.1f,progression);}if(level.movementEnabled)level.movementSpeedNormalized=.135f+.12f*progression;}
    }
}
#endif
