# Changelog

## [0.2.1] - 2026-10-15

### Changed
- Textures tab picks the folder from a folder tree (with texture counts) instead of the flat object picker.

## [0.2.0] - 2026-10-15

### Added
- **Usages** tab: why an asset ends up in the build. Shows every referencer up to the build roots (scenes in build,
  Addressables groups, Resources, preloaded assets), the shortest path from each root, the player/bundle evidence from
  the loaded build data, and the exact GameObject / component / field holding the reference inside a prefab, scene or asset.
  Double-click opens the prefab or scene and selects the object.
- **Find Usages In Build** in the Project window context menu and in the context menu of every table.
- Dependency graph cache in `Library/PBTruongBuildAnalyzer/DependencyGraph.json`; rebuilding only rescans changed assets.
- Addressables layout now keeps which explicit assets pull each asset into a bundle.
- **Textures** tab: pick a folder, list its textures with size in build and estimated imported size, select all/none/invert,
  and batch-apply Default settings (compression, crunch) or per-platform overrides (format, max size, quality, resize) for the
  installed build targets. Sprites packed in a Sprite Atlas are flagged.
- `BuildPlatforms` helper and `IBuildRootProvider` extension point.

## [0.1.1] - 2026-10-09

### Changed
- Auto snapshot after player builds is now off by default. It wrote report and snapshot files into the
  project after every build; turn it on in **Preferences > PBTruong > Build Analyzer** if you want it.

## [0.1.0] - 2026-10-01

### Added
- Build Analyzer window: Overview, Player Assets, Addressables, Insights, Compare tabs.
- Player BuildReport reader with archived report support.
- Optional Addressables build layout provider (groups, bundles, implicit assets, duplicates).
- Snapshots (manual and automatic after player builds) and snapshot comparison.
- CSV export and user preferences for insight thresholds.
- "What goes into the build" breakdown that hides intermediate IL2CPP sources, symbols and managed DLLs.
- Insight and overview card for assets shipped both in the player and in Addressables bundles.
- Readable names for unnamed built-in assets; bundle copy count on the largest assets list.
