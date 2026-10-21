using System;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    public enum SizeSource
    {
        Player,
        Addressables
    }

    [Serializable]
    public sealed class SizeEntry
    {
        public const string ContainerSeparator = ", ";

        [SerializeField] private string _assetPath;
        [SerializeField] private string _typeName;
        [SerializeField] private AssetCategory _category;
        [SerializeField] private long _size;
        [SerializeField] private SizeSource _source;
        [SerializeField] private string _container;
        [SerializeField] private bool _isImplicit;

        public SizeEntry(string assetPath, string typeName, AssetCategory category, long size, SizeSource source, string container, bool isImplicit)
        {
            _assetPath = assetPath;
            _typeName = typeName;
            _category = category;
            _size = size;
            _source = source;
            _container = container;
            _isImplicit = isImplicit;
        }

        public string AssetPath => _assetPath;
        public string TypeName => _typeName;
        public AssetCategory Category => _category;
        public long Size => _size;
        public SizeSource Source => _source;
        public string Container => _container;
        public bool IsImplicit => _isImplicit;
        public string Name => AssetPaths.DisplayName(_assetPath);
        public string Key => $"{_source}|{_assetPath}";

        public string[] Containers => _container.Split(ContainerSeparator, StringSplitOptions.RemoveEmptyEntries);
    }
}
