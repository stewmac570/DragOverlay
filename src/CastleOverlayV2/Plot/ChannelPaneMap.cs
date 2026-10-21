using System;
using System.Collections.Generic;
using System.Linq;

namespace CastleOverlayV2.Plot
{
    /// <summary>Maps channel names to chart pane indexes, defaulting safely to the first pane.</summary>
    public sealed class ChannelPaneMap
    {
        private readonly Dictionary<string, int> _paneIndexes = new(StringComparer.OrdinalIgnoreCase);

        public int PaneCount { get; private set; } = 1;

        public ChannelPaneMap(IEnumerable<string> channels)
        {
            ArgumentNullException.ThrowIfNull(channels);

            foreach (string channel in channels)
                _paneIndexes[channel] = 0;
        }

        public int GetPaneIndex(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return 0;

            return _paneIndexes.TryGetValue(channelName, out int paneIndex) && paneIndex < PaneCount
                ? paneIndex
                : 0;
        }

        public void Assign(string channelName, int paneIndex)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
            if (paneIndex < 0 || paneIndex >= PaneCount)
                throw new ArgumentOutOfRangeException(nameof(paneIndex));
            _paneIndexes[channelName] = paneIndex;
        }

        public void SetPaneCount(int paneCount)
        {
            if (paneCount is < 1 or > 3)
                throw new ArgumentOutOfRangeException(nameof(paneCount));

            PaneCount = paneCount;
            foreach (string channel in _paneIndexes.Keys.ToList())
                if (_paneIndexes[channel] >= paneCount)
                    _paneIndexes[channel] = 0;
        }

        public int MoveToNextPane(string channelName)
        {
            int next = (GetPaneIndex(channelName) + 1) % PaneCount;
            Assign(channelName, next);
            return next;
        }

        public void ApplyBySource()
        {
            SetPaneCount(3);
            foreach (string channel in _paneIndexes.Keys.ToList())
            {
                int paneIndex = channel.StartsWith("RaceBox ", StringComparison.OrdinalIgnoreCase) ? 1
                    : channel.StartsWith("Logger ", StringComparison.OrdinalIgnoreCase) ? 2
                    : 0;
                _paneIndexes[channel] = paneIndex;
            }
        }

        public void ApplyAssignments(IReadOnlyDictionary<string, int>? paneNumbers)
        {
            if (paneNumbers == null) return;

            foreach ((string channel, int paneNumber) in paneNumbers)
                if (paneNumber >= 1 && paneNumber <= PaneCount)
                    _paneIndexes[channel] = paneNumber - 1;
        }

        public Dictionary<string, int> GetAssignments() =>
            _paneIndexes.ToDictionary(pair => pair.Key, pair => GetPaneIndex(pair.Key) + 1,
                StringComparer.OrdinalIgnoreCase);
    }
}
