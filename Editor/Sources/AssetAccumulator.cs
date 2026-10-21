using System.Collections.Generic;
using System.Linq;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class AssetAccumulator
    {
        private readonly Dictionary<string, long> _sizeByType = new();
        private readonly List<string> _containers = new();
        private long _size;
        private bool _isImplicit = true;

        public void Add(string typeName, long size, string container, bool isImplicit)
        {
            _size += size;
            _sizeByType.TryGetValue(typeName, out var current);
            _sizeByType[typeName] = current + size;
            if (!_containers.Contains(container))
                _containers.Add(container);
            _isImplicit &= isImplicit;
        }

        public SizeEntry ToEntry(string assetPath, SizeSource source)
        {
            var dominantType = _sizeByType.OrderByDescending(pair => pair.Value).First().Key;
            return new SizeEntry(
                assetPath,
                dominantType,
                AssetCategoryResolver.Resolve(assetPath, dominantType),
                _size,
                source,
                string.Join(SizeEntry.ContainerSeparator, _containers),
                _isImplicit);
        }
    }
}
