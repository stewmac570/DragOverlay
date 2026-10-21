using System;
using System.Collections.Generic;

namespace CastleOverlayV2.Plot
{
    /// <summary>Maps channel names to chart pane indexes, defaulting safely to the first pane.</summary>
    public sealed class ChannelPaneMap
    {
        private readonly Dictionary<string, int> _paneIndexes = new(StringComparer.OrdinalIgnoreCase);

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

            return _paneIndexes.TryGetValue(channelName, out int paneIndex) ? paneIndex : 0;
        }

        public void Assign(string channelName, int paneIndex)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
            ArgumentOutOfRangeException.ThrowIfNegative(paneIndex);
            _paneIndexes[channelName] = paneIndex;
        }
    }
}
