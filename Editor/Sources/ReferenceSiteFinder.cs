using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PBTruong.BuildAnalyzer.Editor
{
    public sealed class ReferenceSite
    {
        public ReferenceSite(string containerPath, string objectPath, IReadOnlyList<int> siblingPath, string componentType, string property, string targetName, string note)
        {
            ContainerPath = containerPath;
            ObjectPath = objectPath;
            SiblingPath = siblingPath;
            ComponentType = componentType;
            Property = property;
            TargetName = targetName;
            Note = note;
        }

        public string ContainerPath { get; }
        public string ObjectPath { get; }
        public IReadOnlyList<int> SiblingPath { get; }
        public string ComponentType { get; }
        public string Property { get; }
        public string TargetName { get; }
        public string Note { get; }
    }

    public static class ReferenceSiteFinder
    {
        private const string PrefabInstanceProperty = "(Prefab Instance)";
        private static readonly HashSet<string> IgnoredProperties = new(StringComparer.Ordinal)
        {
            "m_Script", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject"
        };
        private static readonly HashSet<string> PrimitiveArrayElements = new(StringComparer.Ordinal)
        {
            "int", "float", "double", "bool", "char", "UInt8", "SInt8", "UInt16", "SInt16", "unsigned int", "UInt32", "SInt64", "UInt64", "string"
        };

        public static bool IsScene(string path) => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        public static bool IsPrefab(string path) => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        public static bool IsSceneLoaded(string path) => SceneManager.GetSceneByPath(path).isLoaded;

        public static IReadOnlyList<ReferenceSite> Find(string containerPath, string targetPath)
        {
            if (IsPrefab(containerPath))
                return FindInPrefab(containerPath, targetPath);
            if (IsScene(containerPath))
                return FindInScene(containerPath, targetPath);
            return FindInAsset(containerPath, targetPath);
        }

        public static void Reveal(ReferenceSite site)
        {
            if (IsPrefab(site.ContainerPath))
                RevealInPrefab(site);
            else if (IsScene(site.ContainerPath))
                RevealInScene(site);
            else
                RevealInAsset(site);
        }

        private static IReadOnlyList<ReferenceSite> FindInPrefab(string prefabPath, string targetPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                return ScanHierarchy(prefabPath, new[] { root }, targetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static IReadOnlyList<ReferenceSite> FindInScene(string scenePath, string targetPath)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.isLoaded)
                return ScanHierarchy(scenePath, scene.GetRootGameObjects(), targetPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode to inspect scenes that are not loaded.");

            var wasInHierarchy = scene.IsValid();
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                return ScanHierarchy(scenePath, scene.GetRootGameObjects(), targetPath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, !wasInHierarchy);
            }
        }

        private static IReadOnlyList<ReferenceSite> FindInAsset(string assetPath, string targetPath)
        {
            var sites = new List<ReferenceSite>();
            var matcher = new TargetMatcher(targetPath);
            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath).Where(obj => obj != null && obj is not GameObject && obj is not Component).ToList();
            if (AssetImporter.GetAtPath(assetPath) is { } importer)
                objects.Add(importer);

            foreach (var obj in objects)
            {
                var label = obj is AssetImporter ? "(Import Settings)" : $"{obj.name} ({obj.GetType().Name})";
                if (obj is Material material)
                    ScanMaterial(assetPath, material, label, matcher, sites);
                else
                    ScanObject(assetPath, obj, label, null, matcher, null, sites);
            }

            return sites;
        }

        private static IReadOnlyList<ReferenceSite> ScanHierarchy(string containerPath, IReadOnlyList<GameObject> roots, string targetPath)
        {
            var sites = new List<ReferenceSite>();
            var matcher = new TargetMatcher(targetPath);
            var targetIsPrefab = IsPrefab(targetPath);

            for (var i = 0; i < roots.Count; i++)
            {
                foreach (var transform in roots[i].GetComponentsInChildren<Transform>(true))
                {
                    var go = transform.gameObject;
                    var objectPath = HierarchyPath(transform);
                    var siblingPath = SiblingPath(transform, i);

                    if (targetIsPrefab && PrefabUtility.IsAnyPrefabInstanceRoot(go) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == targetPath)
                        sites.Add(new ReferenceSite(containerPath, objectPath, siblingPath, nameof(GameObject), PrefabInstanceProperty, AssetPaths.DisplayName(targetPath), null));

                    foreach (var component in go.GetComponents<Component>())
                    {
                        if (component != null)
                            ScanObject(containerPath, component, objectPath, siblingPath, matcher, NestedSource(component), sites);
                    }
                }
            }

            return sites;
        }

        private static void ScanObject(string containerPath, Object obj, string objectPath, IReadOnlyList<int> siblingPath, TargetMatcher matcher, string nestedSource, List<ReferenceSite> sites)
        {
            using var serialized = new SerializedObject(obj);
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = ShouldEnter(property);
                if (property.propertyType != SerializedPropertyType.ObjectReference || IgnoredProperties.Contains(property.propertyPath))
                    continue;

                var value = property.objectReferenceValue;
                if (!matcher.Matches(value))
                    continue;

                var note = nestedSource != null && !property.prefabOverride ? $"inherited from {nestedSource}" : null;
                sites.Add(new ReferenceSite(containerPath, objectPath, siblingPath, obj.GetType().Name, property.propertyPath, value.name, note));
            }
        }

        private static void ScanMaterial(string containerPath, Material material, string label, TargetMatcher matcher, List<ReferenceSite> sites)
        {
            foreach (var name in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(name);
                if (matcher.Matches(texture))
                    sites.Add(new ReferenceSite(containerPath, label, null, nameof(Material), name, texture.name, null));
            }

            if (matcher.Matches(material.shader))
                sites.Add(new ReferenceSite(containerPath, label, null, nameof(Material), "Shader", material.shader.name, null));
        }

        private static bool ShouldEnter(SerializedProperty property)
        {
            if (!property.hasChildren)
                return false;
            if (property.propertyType == SerializedPropertyType.String)
                return false;
            if (property.isArray && PrimitiveArrayElements.Contains(property.arrayElementType))
                return false;
            return property.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference;
        }

        private static string NestedSource(Component component)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(component))
                return null;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component);
            return string.IsNullOrEmpty(path) ? null : Path.GetFileName(path);
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new List<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static IReadOnlyList<int> SiblingPath(Transform transform, int rootIndex)
        {
            var indices = new List<int>();
            for (var current = transform; current.parent != null; current = current.parent)
                indices.Add(current.GetSiblingIndex());
            indices.Add(rootIndex);
            indices.Reverse();
            return indices;
        }

        private static GameObject Resolve(IReadOnlyList<GameObject> roots, IReadOnlyList<int> siblingPath)
        {
            if (siblingPath == null || siblingPath.Count == 0 || siblingPath[0] >= roots.Count)
                return null;

            var transform = roots[siblingPath[0]].transform;
            for (var i = 1; i < siblingPath.Count; i++)
            {
                if (siblingPath[i] >= transform.childCount)
                    return null;
                transform = transform.GetChild(siblingPath[i]);
            }

            return transform.gameObject;
        }

        private static void RevealInPrefab(ReferenceSite site)
        {
            var stage = PrefabStageUtility.OpenPrefab(site.ContainerPath);
            if (stage == null)
                return;
            SelectAndPing(Resolve(new[] { stage.prefabContentsRoot }, site.SiblingPath));
        }

        private static void RevealInScene(ReferenceSite site)
        {
            var scene = SceneManager.GetSceneByPath(site.ContainerPath);
            if (!scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(site.ContainerPath, OpenSceneMode.Single);
            }

            SelectAndPing(Resolve(scene.GetRootGameObjects(), site.SiblingPath));
        }

        private static void RevealInAsset(ReferenceSite site)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath(site.ContainerPath)
                            .FirstOrDefault(obj => obj != null && site.ObjectPath == $"{obj.name} ({obj.GetType().Name})")
                        ?? AssetDatabase.LoadMainAssetAtPath(site.ContainerPath);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void SelectAndPing(GameObject go)
        {
            if (go == null)
                return;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private sealed class TargetMatcher
        {
            private readonly string _targetPath;
            private readonly Dictionary<Object, bool> _cache = new();

            public TargetMatcher(string targetPath) => _targetPath = targetPath;

            public bool Matches(Object value)
            {
                if (value == null)
                    return false;
                if (_cache.TryGetValue(value, out var matches))
                    return matches;

                matches = AssetDatabase.GetAssetPath(value) == _targetPath;
                _cache.Add(value, matches);
                return matches;
            }
        }
    }
}
