using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.Interfaces
{
    public interface IVictoryThemeGeneratorService
    {
        Task<string> Generate(IReadOnlyCollection<VictoryThemeGenerationEntry> entries);
    }

    public class VictoryThemeGenerationEntry
    {
        public string CharaName { get; set; }
        public string SourceFile { get; set; }
        public string ToneId { get; set; }
        public bool PatchFighterJingle { get; set; }
        public bool ApplyNormalization { get; set; }
        public float Volume { get; set; }
    }
}
