using System;
using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class PlayerBundleDuplicate
    {
        public PlayerBundleDuplicate(SizeEntry player, SizeEntry bundles)
        {
            Player = player;
            Bundles = bundles;
        }

        public SizeEntry Player { get; }
        public SizeEntry Bundles { get; }
        public string AssetPath => Player.AssetPath;
        public string Name => Player.Name;
        public AssetCategory Category => Player.Category;
        public long WastedSize => Math.Min(Player.Size, Bundles.Size);
    }

    public static class PlayerBundleDuplicates
    {
        public static IReadOnlyList<PlayerBundleDuplicate> Find(PlayerBuildData player, BundleLayoutData layout)
        {
            if (player == null || layout == null)
                return Array.Empty<PlayerBundleDuplicate>();

            var playerByPath = player.Assets
                .Where(entry => AssetPaths.IsProjectAsset(entry.AssetPath))
                .ToDictionary(entry => entry.AssetPath);

            return layout.Assets
                .Where(entry => playerByPath.ContainsKey(entry.AssetPath))
                .Select(entry => new PlayerBundleDuplicate(playerByPath[entry.AssetPath], entry))
                .OrderByDescending(duplicate => duplicate.WastedSize)
                .ToList();
        }
    }
}
