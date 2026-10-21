using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class LayoutFileInfo
    {
        public LayoutFileInfo(string path, DateTime timestamp)
        {
            Path = path;
            Timestamp = timestamp;
        }

        public string Path { get; }
        public DateTime Timestamp { get; }
        public string Label => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public interface IBundleLayoutProvider
    {
        bool IsLayoutGenerationEnabled { get; set; }
        IReadOnlyList<LayoutFileInfo> FindLayoutFiles();
        BundleLayoutData Load(string path);
    }

    public static class BundleLayoutProviderLocator
    {
        public static IBundleLayoutProvider Find()
        {
            var type = TypeCache.GetTypesDerivedFrom<IBundleLayoutProvider>()
                .FirstOrDefault(candidate => !candidate.IsAbstract && !candidate.IsInterface && candidate.GetConstructor(Type.EmptyTypes) != null);
            return type == null ? null : (IBundleLayoutProvider)Activator.CreateInstance(type);
        }
    }
}
