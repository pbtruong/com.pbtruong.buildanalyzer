using System;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    public enum PlayerLocation
    {
        Resources,
        Scenes,
        Global,
        Other
    }

    public static class PlayerLocations
    {
        public static PlayerLocation Classify(string container)
        {
            if (container.StartsWith("resources.", StringComparison.OrdinalIgnoreCase))
                return PlayerLocation.Resources;
            if (container.StartsWith("level", StringComparison.OrdinalIgnoreCase) || container.StartsWith("sharedassets", StringComparison.OrdinalIgnoreCase))
                return PlayerLocation.Scenes;
            if (container.StartsWith("globalgamemanagers", StringComparison.OrdinalIgnoreCase))
                return PlayerLocation.Global;
            return PlayerLocation.Other;
        }

        public static bool IsIn(SizeEntry entry, PlayerLocation location) =>
            entry.Containers.Any(container => Classify(container) == location);
    }
}
