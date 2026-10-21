using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class AutoSnapshotOnBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!BuildAnalyzerSettings.instance.AutoSnapshotOnBuild)
                return;

            var platform = report.summary.platform;
            EditorApplication.delayCall += () => Capture(platform);
        }

        private static void Capture(BuildTarget platform)
        {
            PlayerReportArchive.ArchiveLatest(platform);
            var player = PlayerReportReader.LoadLatest();
            var provider = BundleLayoutProviderLocator.Find();
            var layoutFile = provider?.FindLayoutFiles().FirstOrDefault();
            var layout = layoutFile != null ? provider.Load(layoutFile.Path) : null;
            SnapshotStore.Save(SnapshotBuilder.Build($"{platform} {PlayerSettings.bundleVersion}", player, layout));
        }
    }
}
