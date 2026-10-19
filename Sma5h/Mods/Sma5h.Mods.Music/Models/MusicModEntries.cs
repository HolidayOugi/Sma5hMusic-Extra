using System.Collections.Generic;

namespace Sma5h.Mods.Music.Models
{
    public class MusicModEntries
    {
        public List<BgmDbRootEntry> BgmDbRootEntries { get; }
        public List<BgmAssignedInfoEntry> BgmAssignedInfoEntries { get; }
        public List<BgmStreamSetEntry> BgmStreamSetEntries { get; }
        public List<BgmStreamPropertyEntry> BgmStreamPropertyEntries { get; }
        public List<BgmPropertyEntry> BgmPropertyEntries { get; }
        public List<GameTitleEntry> GameTitleEntries { get; }
        public List<SeriesEntry> SeriesEntries { get; }
        public List<MusicModSeriesEntries> OrderedSeries { get; }

        public MusicModEntries()
        {
            BgmDbRootEntries = new List<BgmDbRootEntry>();
            BgmAssignedInfoEntries = new List<BgmAssignedInfoEntry>();
            BgmStreamSetEntries = new List<BgmStreamSetEntry>();
            BgmStreamPropertyEntries = new List<BgmStreamPropertyEntry>();
            BgmPropertyEntries = new List<BgmPropertyEntry>();
            GameTitleEntries = new List<GameTitleEntry>();
            SeriesEntries = new List<SeriesEntry>();
            OrderedSeries = new List<MusicModSeriesEntries>();
        }

        public MusicModDeleteEntries GetMusicModDeleteEntries()
        {
            var output = new MusicModDeleteEntries();
            if (BgmDbRootEntries != null)
            {
                foreach (var entry in BgmDbRootEntries)
                {
                    output.BgmDbRootEntries.Add(entry.UiBgmId);
                }
            }
            if (BgmAssignedInfoEntries != null)
            {
                foreach (var entry in BgmAssignedInfoEntries)
                {
                    output.BgmAssignedInfoEntries.Add(entry.InfoId);
                }
            }
            if (BgmStreamSetEntries != null)
            {
                foreach (var entry in BgmStreamSetEntries)
                {
                    output.BgmStreamSetEntries.Add(entry.StreamSetId);
                }
            }
            if (BgmStreamPropertyEntries != null)
            {
                foreach (var entry in BgmStreamPropertyEntries)
                {
                    output.BgmStreamPropertyEntries.Add(entry.StreamId);
                }
            }
            if (BgmPropertyEntries != null)
            {
                foreach (var entry in BgmPropertyEntries)
                {
                    output.BgmPropertyEntries.Add(entry.NameId);
                }
            }
            return output;
        }
    }

    public sealed class MusicModSeriesEntries
    {
        public SeriesEntry Series { get; }
        public List<MusicModGameEntries> Games { get; }

        public MusicModSeriesEntries(SeriesEntry series)
        {
            Series = series;
            Games = new List<MusicModGameEntries>();
        }
    }

    public sealed class MusicModGameEntries
    {
        public GameTitleEntry Game { get; }
        public List<MusicModBgmEntries> Bgms { get; }

        public MusicModGameEntries(GameTitleEntry game)
        {
            Game = game;
            Bgms = new List<MusicModBgmEntries>();
        }
    }

    public sealed class MusicModBgmEntries
    {
        public BgmDbRootEntry Database { get; }
        public BgmStreamSetEntry StreamSet { get; }
        public BgmAssignedInfoEntry AssignedInfo { get; }
        public BgmStreamPropertyEntry StreamProperty { get; }
        public BgmPropertyEntry Property { get; }

        public MusicModBgmEntries(
            BgmDbRootEntry database,
            BgmStreamSetEntry streamSet,
            BgmAssignedInfoEntry assignedInfo,
            BgmStreamPropertyEntry streamProperty,
            BgmPropertyEntry property)
        {
            Database = database;
            StreamSet = streamSet;
            AssignedInfo = assignedInfo;
            StreamProperty = streamProperty;
            Property = property;
        }
    }
}
