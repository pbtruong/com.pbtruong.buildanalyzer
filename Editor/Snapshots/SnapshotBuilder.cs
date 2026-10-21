using System;
using System.Collections.Generic;

namespace PBTruong.BuildAnalyzer.Editor
{
    public static class SnapshotBuilder
    {
        public static BuildSnapshot Build(string label, PlayerBuildData player, BundleLayoutData layout)
        {
            var entries = new List<SizeEntry>();
            if (player != null)
                entries.AddRange(player.Assets);
            if (layout != null)
                entries.AddRange(layout.Assets);

            var platform = player != null ? player.Platform.ToString() : layout?.Platform ?? string.Empty;
            return new BuildSnapshot(
                label,
                DateTime.Now,
                platform,
                player?.OutputSize ?? 0,
                layout?.TotalCompressedSize ?? 0,
                entries);
        }
    }
}
