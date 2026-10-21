namespace PBTruong.BuildAnalyzer.Editor
{
    public enum AssetCategory
    {
        Texture,
        Mesh,
        Audio,
        Animation,
        Shader,
        Material,
        Font,
        Video,
        ScriptData,
        Prefab,
        Scene,
        Other
    }

    public static class AssetCategoryExtensions
    {
        public static string DisplayName(this AssetCategory category) =>
            category == AssetCategory.ScriptData ? "Script & Data" : category.ToString();
    }
}
