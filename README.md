# PBTruong Build Analyzer

Editor tool to see what goes into a build and how much each thing costs.
Open it from **Window > Analysis > Build Analyzer**.

Supports Unity 2022.3+ and Unity 6. No runtime code, no dependency on game code. Addressables support is optional: it compiles only when
`com.unity.addressables` 1.21+ is installed.

## Installation

Add to `Packages/manifest.json`:

```json
"com.pbtruong.buildanalyzer": "git@github.com:pbtruong/com.pbtruong.buildanalyzer.git#0.2.2"
```

## Data sources

| Source | Where it comes from | How to get it |
| --- | --- | --- |
| Player build | `Library/LastBuild.buildreport` (`BuildReport.GetLatestReport()` on Unity 6) | Any player build |
| Addressables | `Library/com.unity.addressables/BuildReports/buildlayout_*.json` | Enable *Generate Build Layout* (toolbar Addressables menu or Edit > Preferences > Addressables), then build content |

Asset sizes are the uncompressed data written into the build. Bundle "On Disk" sizes are the real compressed file sizes.

## Tabs

- **Overview**: platform, player output size, Addressables on-disk size, duplicated waste, size by category, largest assets, output files by role, included engine modules.
- **Player Assets**: every asset in the player build, sortable by any column, filter by category / location (Resources, Scenes, Global) / search. Click pings, double-click selects. Build with *Detailed Build Report* to see which scenes use an asset.
- **Addressables**: Group > Bundle > Asset tree, flat asset list, and duplicated assets (implicit assets copied into several bundles, with wasted size).
- **Insights**: large/uncompressed textures, mipmaps on sprites, PCM/ADPCM audio, stereo clips, duplicated bundle assets, assets in Resources, uncompressed bundles. Thresholds live in **Preferences > PBTruong > Build Analyzer**.
- **Compare**: diff any two of: current data, saved snapshots, Addressables build layout history. Shows added / removed / grew / shrank per asset and per category.
- **Usages**: pick an asset (or right-click it in the Project window / any table > *Find Usages In Build*) to see which scenes, Addressables groups, Resources or preloaded assets pull it into the build, through which prefabs/materials, and which GameObject, component and field reference it. See [Usages](#usages).
- **Textures**: batch texture compression for a folder. See [Textures](#textures).

Every table can be exported to CSV (toolbar > Export CSV).

## Usages

- The dependency graph starts from the build roots: enabled scenes in Build Settings, preloaded assets, everything under
  `Resources/` (outside `Editor/`), and every entry of Addressables groups that are included in the build (folder entries expanded).
  It follows `AssetDatabase.GetDependencies(path, false)`; scripts and assemblies are skipped.
- The first scan can take a while on big projects. The result is cached in `Library/PBTruongBuildAnalyzer/DependencyGraph.json`,
  and **Rebuild Graph** only rescans assets whose dependency hash changed. The status line turns orange when assets changed after the last scan.
- **Used by** lists the referencers recursively (capped at 5000 rows / 12 levels). Select a row to scan that prefab, scene or asset for the
  fields pointing at the child asset; nested prefab references are marked *inherited from*. Scenes that are not open are opened additively,
  scanned and closed again, only after you press *Scan Scene*.
- The evidence block compares the graph with the loaded build data: player containers, scenes (Detailed Build Report),
  Addressables bundles and the explicit assets that pull an implicit asset in.
- Some references are not exposed to `SerializedObject` (scene lighting settings, Sprite Atlas packables). The asset-level chain still shows them.

## Textures

1. Pick a folder in the folder tree on the left (numbers are texture counts, subfolders included; folders without textures are hidden unless you tick the option), or select one in the Project window and press *Use Selection*.
2. Filter by search, texture type, *In build only*, *Hide atlas sprites*. Tick rows, or use **All / None / Invert** on the visible rows
   (*All* skips sprites packed in a Sprite Atlas, because the atlas settings decide their compression).
3. In the **Compression** panel choose a platform (Default + every installed build target). Every field starts at *(keep)*;
   only the fields you change are written. On a platform, setting any field turns the override on; *Remove override* turns it off.
4. *Current / New / Est.* columns preview the result for the selected platform; **Apply** writes the pending changes of all platforms
   to the ticked textures in one batch and reimports them.

*Est.* is the in-memory size from source size, max size (halved like Unity does) and the format's bits per pixel; crunched formats are
approximate. *Build Size* comes from the loaded build data and only changes after the next build. Changes go to the `.meta` files,
so use version control to revert.

## Snapshots

- **Save Snapshot** stores the loaded player + Addressables data as JSON in `BuildAnalyzerReports/Snapshots/` (project root, configurable).
- Auto snapshot after player builds is off by default. Turn it on in Preferences to archive the build report to `BuildAnalyzerReports/Player/` and save a snapshot after each successful player build.
- Batch-mode builds that exit right away (`-quit`) do not run the automatic snapshot; open the window afterwards and save one manually.

Decide whether `BuildAnalyzerReports/` should be committed (shared history) or added to `.gitignore`.

## Extending

- New insight: implement `ISizeInsightRule` (public parameterless constructor). It is discovered through `TypeCache`.
- Another bundle system: implement `IBundleLayoutProvider` and convert its data to `BundleLayoutData`.
- Another kind of build root (e.g. assets loaded by a custom build step): implement `IBuildRootProvider`.
