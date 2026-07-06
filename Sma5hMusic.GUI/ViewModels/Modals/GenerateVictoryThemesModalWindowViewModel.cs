using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Sma5h.Mods.Music;
using Sma5h.Mods.Music.Helpers;
using Sma5hMusic.GUI.Interfaces;
using Sma5hMusic.GUI.Models;
using Sma5hMusic.GUI.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sma5hMusic.GUI.ViewModels
{
    public class GenerateVictoryThemesModalWindowViewModel : ViewModelBase
    {
        private const string CustomToneIdPrefix = "zzc_f_";
        private static readonly Regex ToneIdRegex = new Regex(@"^[a-z0-9_]+$", RegexOptions.Compiled);

        private readonly IFileDialog _fileDialog;
        private readonly IMessageDialog _messageDialog;
        private readonly IVictoryThemeGeneratorService _victoryThemeGenerator;
        private readonly ILogger _logger;
        private readonly float _defaultVolume;
        private readonly string _loadTempRoot;
        private readonly string _outputRoot;

        public ObservableCollection<VictoryThemeEntryViewModel> Entries { get; }
        public IReadOnlyList<VictoryThemeFighterOption> FighterOptions { get; }

        [Reactive]
        public bool IsGenerating { get; set; }

        public ReactiveCommand<Unit, Unit> ActionAddEntry { get; }
        public ReactiveCommand<Window, Unit> ActionLoadVictoryThemes { get; }
        public ReactiveCommand<VictoryThemeEntryViewModel, Unit> ActionRemoveEntry { get; }
        public ReactiveCommand<VictoryThemeEntryViewModel, Unit> ActionChooseAudio { get; }
        public ReactiveCommand<Window, Unit> ActionGenerate { get; }
        public ReactiveCommand<Window, Unit> ActionCancel { get; }

        public GenerateVictoryThemesModalWindowViewModel(
            IFileDialog fileDialog,
            IMessageDialog messageDialog,
            IVictoryThemeGeneratorService victoryThemeGenerator,
            IOptionsMonitor<ApplicationSettings> config,
            ILogger<GenerateVictoryThemesModalWindowViewModel> logger)
        {
            _fileDialog = fileDialog;
            _messageDialog = messageDialog;
            _victoryThemeGenerator = victoryThemeGenerator;
            _logger = logger;
            _defaultVolume = RoundVolume((float)config.CurrentValue.Sma5hMusicGUI.DefaultSongVolume);
            _loadTempRoot = Path.GetFullPath(Path.Combine(config.CurrentValue.TempPath, "VictoryThemesLoad"));
            _outputRoot = Path.GetFullPath(Path.Combine(config.CurrentValue.OutputPath, "Victory Themes"));

            FighterOptions = CreateFighterTemplate();
            Entries = new ObservableCollection<VictoryThemeEntryViewModel>();

            ActionAddEntry = ReactiveCommand.Create(AddEntry);
            ActionLoadVictoryThemes = ReactiveCommand.CreateFromTask<Window>(LoadVictoryThemes);
            ActionRemoveEntry = ReactiveCommand.Create<VictoryThemeEntryViewModel>(RemoveEntry);
            ActionChooseAudio = ReactiveCommand.CreateFromTask<VictoryThemeEntryViewModel>(ChooseAudio);
            ActionGenerate = ReactiveCommand.CreateFromTask<Window>(Generate);
            ActionCancel = ReactiveCommand.Create<Window>(Cancel);

            AddEntry();
        }

        private void AddEntry()
        {
            Entries.Add(new VictoryThemeEntryViewModel(GetToneIdForCharaName)
            {
                SelectedFighter = GetCustomFighterOption(),
                UseDefaultName = false,
                Volume = _defaultVolume
            });
        }

        private void RemoveEntry(VictoryThemeEntryViewModel entry)
        {
            if (entry == null)
                return;

            Entries.Remove(entry);
            if (Entries.Count == 0)
                AddEntry();
        }

        private async Task ChooseAudio(VictoryThemeEntryViewModel entry)
        {
            if (entry == null)
                return;

            var file = await _fileDialog.OpenFileDialogAudioSingle();
            if (string.IsNullOrWhiteSpace(file))
                return;

            entry.SourceFile = file;
            entry.SourceFileName = "Selected";
        }

        private async Task LoadVictoryThemes(Window window)
        {
            if (IsGenerating)
                return;

            try
            {
                var folder = await _fileDialog.OpenFolderDialog(window);
                if (string.IsNullOrWhiteSpace(folder))
                    return;

                EnsureDirectoriesDoNotOverlap(folder, _loadTempRoot, "selected Victory Themes", "temporary load");
                EnsureDirectoriesDoNotOverlap(_outputRoot, _loadTempRoot, "output", "temporary load");
                ClearDirectory(_loadTempRoot);
                var loadedEntries = LoadVictoryThemeEntries(folder);
                if (loadedEntries.Count == 0)
                    throw new InvalidOperationException("No victory theme NUS3AUDIO files were found in the selected folder.");

                Entries.Clear();
                foreach (var entry in loadedEntries)
                    Entries.Add(entry);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not load victory themes.");
                await _messageDialog.ShowError("Load Victory Themes", e.Message);
            }
        }

        private async Task Generate(Window window)
        {
            if (IsGenerating)
                return;

            ScriptProgressModalWindow progressWindow = null;
            Task progressDialogTask = null;

            try
            {
                var entries = ValidateEntries();
                IsGenerating = true;
                Action<int, int, string> normalizationProgress = null;
                if (entries.Any(p => p.ApplyNormalization))
                {
                    var progressVm = new ScriptProgressModalWindowViewModel
                    {
                        Footer = "Please wait. Do not close this window."
                    };
                    progressVm.SetPreparing("Preparing victory theme normalization...");

                    progressWindow = new ScriptProgressModalWindow
                    {
                        DataContext = progressVm,
                        Title = "Victory Theme Normalization",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    progressDialogTask = progressWindow.ShowDialog(window);

                    normalizationProgress = (current, total, toneId) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            progressVm.SetProgress("Normalizing audio", toneId, current, total);
                            progressVm.IsIndeterminate = true;
                        });
                    };
                }

                var outputFolder = await _victoryThemeGenerator.Generate(entries, normalizationProgress);
                await CloseProgressWindow();
                DeleteDirectoryIfExists(_loadTempRoot, outputFolder);
                await _messageDialog.ShowInformation("Victory Themes Generated", $"Generated files in:\r\n{outputFolder}");
                window.Close(window);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not generate victory themes.");
                await _messageDialog.ShowError("Generate Victory Themes", e.Message);
            }
            finally
            {
                await CloseProgressWindow();
                IsGenerating = false;
            }

            async Task CloseProgressWindow()
            {
                if (progressWindow != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (progressWindow.IsVisible)
                            progressWindow.Close();
                    });
                    progressWindow = null;
                }

                if (progressDialogTask != null)
                {
                    await progressDialogTask;
                    progressDialogTask = null;
                }
            }
        }

        private IReadOnlyCollection<VictoryThemeGenerationEntry> ValidateEntries()
        {
            if (Entries.Count == 0)
                throw new InvalidOperationException("Add at least one entry.");

            var output = new List<VictoryThemeGenerationEntry>();
            foreach (var entry in Entries)
            {
                var customName = SanitizeIdPart(entry.CustomName);
                var isCustomFighter = entry.SelectedFighter == null || entry.SelectedFighter.IsCustom;
                var charaName = isCustomFighter ? customName : entry.SelectedFighter.CharaName;
                if (string.IsNullOrWhiteSpace(charaName))
                    throw new InvalidOperationException("Every entry must have a character.");

                if (string.IsNullOrWhiteSpace(entry.SourceFile))
                    throw new InvalidOperationException($"Choose an audio file for '{charaName}'.");

                var toneId = !isCustomFighter && entry.UseDefaultName
                    ? entry.SelectedFighter.ToneId
                    : entry.ToneId?.Trim();
                ValidateToneId(toneId);

                if (output.Any(p => string.Equals(p.ToneId, toneId, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Tone ID '{toneId}' was added more than once.");

                output.Add(new VictoryThemeGenerationEntry
                {
                    CharaName = charaName,
                    SourceFile = entry.SourceFile,
                    ToneId = toneId,
                    PatchFighterJingle = isCustomFighter || !entry.UseDefaultName, //if custom character or unshared victory theme
                    ApplyNormalization = entry.ApplyNormalization,
                    Volume = RoundVolume(entry.Volume)
                });
            }

            return output;
        }

        private void Cancel(Window window)
        {
            DeleteDirectoryIfExists(_loadTempRoot, _outputRoot);
            window.Close();
        }

        private IReadOnlyList<VictoryThemeEntryViewModel> LoadVictoryThemeEntries(string folder)
        {
            var bgmFolder = Path.Combine(folder, "stream;", "sound", "bgm");
            if (!Directory.Exists(bgmFolder))
                throw new InvalidOperationException($"The selected folder does not contain stream;/sound/bgm:\r\n{folder}");

            var toneIds = Directory.GetFiles(bgmFolder, "*.nus3audio")
                .Select(GetToneIdFromNus3AudioFileName)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var fighterJingleByToneId = ReadFighterJingleByToneId(folder);

            var entries = new List<VictoryThemeEntryViewModel>();
            foreach (var toneId in toneIds)
            {
                var nus3AudioFile = Path.Combine(bgmFolder, string.Format(MusicConstants.GameResources.NUS3AUDIO_FILE, toneId));
                if (!File.Exists(nus3AudioFile))
                    continue;

                var tempNus3AudioFile = Path.Combine(_loadTempRoot, Path.GetFileName(nus3AudioFile));
                File.Copy(nus3AudioFile, tempNus3AudioFile, true);

                var entry = new VictoryThemeEntryViewModel(GetToneIdForCharaName)
                {
                    SourceFile = tempNus3AudioFile,
                    SourceFileName = "Selected",
                    Volume = _defaultVolume
                };

                ApplyLoadedEntryIdentity(entry, toneId, fighterJingleByToneId.TryGetValue(toneId, out var charaName) ? charaName : null);

                entries.Add(entry);
            }

            return entries;
        }

        private void ApplyLoadedEntryIdentity(VictoryThemeEntryViewModel entry, string toneId, string charaName)
        {
            if (!string.IsNullOrWhiteSpace(charaName))
            {
                var fighterByCharaName = FighterOptions.FirstOrDefault(p => !p.IsCustom && string.Equals(p.CharaName, charaName, StringComparison.OrdinalIgnoreCase));
                if (fighterByCharaName != null)
                {
                    entry.SelectedFighter = fighterByCharaName;
                    entry.UseDefaultName = string.Equals(fighterByCharaName.ToneId, toneId, StringComparison.OrdinalIgnoreCase);
                    entry.ToneId = toneId;
                    return;
                }

                entry.SelectedFighter = GetCustomFighterOption();
                entry.UseDefaultName = false;
                entry.CustomName = charaName;
                entry.ToneId = toneId;
                return;
            }

            var fighterByToneId = FighterOptions.FirstOrDefault(p => !p.IsCustom && string.Equals(p.ToneId, toneId, StringComparison.OrdinalIgnoreCase));
            if (fighterByToneId != null)
            {
                entry.SelectedFighter = fighterByToneId;
                return;
            }

            var customName = GetCustomNameFromToneId(toneId);
            var fighterByCustomName = FighterOptions.FirstOrDefault(p => !p.IsCustom && string.Equals(p.CharaName, customName, StringComparison.OrdinalIgnoreCase));
            entry.SelectedFighter = fighterByCustomName ?? GetCustomFighterOption();
            entry.UseDefaultName = false;
            entry.CustomName = customName;
            entry.ToneId = toneId;
        }

        private static IReadOnlyDictionary<string, string> ReadFighterJingleByToneId(string folder)
        {
            var jsonFile = Path.Combine(folder, "database", "victory.json");
            if (!File.Exists(jsonFile))
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var songData = JObject.Parse(File.ReadAllText(jsonFile));
            if (songData["fighter_jingle"] is not JObject fighterJingle)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in fighterJingle.Properties())
            {
                var toneId = (string)property.Value;
                var charaName = GetCharaNameFromCharaId(property.Name);
                if (!string.IsNullOrWhiteSpace(toneId) &&
                    !string.IsNullOrWhiteSpace(charaName) &&
                    !output.ContainsKey(toneId))
                {
                    output[toneId] = charaName;
                }
            }

            return output;
        }

        private static string GetCharaNameFromCharaId(string charaId)
        {
            const string prefix = "ui_chara_";
            return charaId != null && charaId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? charaId.Substring(prefix.Length)
                : charaId;
        }

        private static string GetToneIdFromNus3AudioFileName(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            const string prefix = MusicConstants.InternalIds.NUS3AUDIO_FILE_PREFIX;
            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(prefix.Length)
                : name;
        }

        private string GetCustomNameFromToneId(string toneId)
        {
            var fighter = FighterOptions.FirstOrDefault(p => string.Equals(p.ToneId, toneId, StringComparison.OrdinalIgnoreCase));
            if (fighter != null)
                return fighter.CharaName;

            if (toneId.StartsWith(CustomToneIdPrefix, StringComparison.OrdinalIgnoreCase))
                return toneId.Substring(CustomToneIdPrefix.Length);

            return toneId;
        }

        private string GetToneIdForCharaName(string charaName)
        {
            var sanitizedName = SanitizeIdPart(charaName);
            if (string.IsNullOrWhiteSpace(sanitizedName))
                return string.Empty;

            var fighter = FighterOptions.FirstOrDefault(p => !p.IsCustom && string.Equals(p.CharaName, sanitizedName, StringComparison.OrdinalIgnoreCase));
            return fighter?.ToneId ?? $"{CustomToneIdPrefix}{sanitizedName}";
        }

        private VictoryThemeFighterOption GetCustomFighterOption()
        {
            return FighterOptions.First(p => p.IsCustom);
        }

        private static void ValidateToneId(string toneId)
        {
            if (string.IsNullOrWhiteSpace(toneId))
                throw new InvalidOperationException("Enter a Tone ID.");

            if (toneId.Length > MusicConstants.GameResources.ToneIdMaximumSize)
                throw new InvalidOperationException($"Tone ID '{toneId}' is too long. Maximum is {MusicConstants.GameResources.ToneIdMaximumSize}.");

            if (toneId.Length < MusicConstants.GameResources.ToneIdMinimumSize)
                throw new InvalidOperationException($"Tone ID '{toneId}' is too short. Minimum is {MusicConstants.GameResources.ToneIdMinimumSize}.");

            if (!ToneIdRegex.IsMatch(toneId))
                throw new InvalidOperationException($"Tone ID '{toneId}' can only contain lowercase letters, digits and underscore.");
        }

        private static void ClearDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            Directory.CreateDirectory(path);
        }

        private static void DeleteDirectoryIfExists(string path, string protectedPath)
        {
            try
            {
                if (Directory.Exists(path) && !PathsOverlap(path, protectedPath))
                    Directory.Delete(path, true);
            }
            catch
            {
                // Best-effort cleanup for temporary loaded victory themes.
            }
        }

        private static void EnsureDirectoriesDoNotOverlap(string firstPath, string secondPath, string firstName, string secondName)
        {
            if (PathsOverlap(firstPath, secondPath))
                throw new InvalidOperationException($"The {secondName} folder cannot be inside the {firstName} folder, or the other way around:\r\n{firstPath}\r\n{secondPath}");
        }

        private static bool PathsOverlap(string firstPath, string secondPath)
        {
            var first = NormalizeDirectoryPath(firstPath);
            var second = NormalizeDirectoryPath(secondPath);
            return first.Equals(second, StringComparison.OrdinalIgnoreCase)
                   || first.StartsWith(second, StringComparison.OrdinalIgnoreCase)
                   || second.StartsWith(first, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            var fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath + Path.DirectorySeparatorChar;
        }

        //TODO: maybe hardcoding this isn't the cleanest implementation
        private static IReadOnlyList<VictoryThemeFighterOption> CreateFighterTemplate()
        {
            return new[]
            {
                new VictoryThemeFighterOption("Custom", string.Empty, string.Empty, true),
                new VictoryThemeFighterOption("Mario", "mario", "z01_f_mario"),
                new VictoryThemeFighterOption("Donkey Kong", "donkey", "z02_f_donkey"),
                new VictoryThemeFighterOption("Link", "link", "z03_f_link"),
                new VictoryThemeFighterOption("Samus", "samus", "z04_f_samus"),
                new VictoryThemeFighterOption("Dark Samus", "samusd", "z94_f_ridley"),
                new VictoryThemeFighterOption("Yoshi", "yoshi", "z05_f_yoshi"),
                new VictoryThemeFighterOption("Kirby", "kirby", "z06_f_kirby"),
                new VictoryThemeFighterOption("Fox", "fox", "z07_f_fox"),
                new VictoryThemeFighterOption("Pikachu", "pikachu", "z08_f_pikachu"),
                new VictoryThemeFighterOption("Luigi", "luigi", "z09_f_luigi"),
                new VictoryThemeFighterOption("Ness", "ness", "z11_f_ness"),
                new VictoryThemeFighterOption("Captain Falcon", "captain", "z10_f_captain"),
                new VictoryThemeFighterOption("Jigglypuff", "purin", "z37_f_purin"),
                new VictoryThemeFighterOption("Peach", "peach", "z13_f_peach"),
                new VictoryThemeFighterOption("Daisy", "daisy", "z13_f_peach"),
                new VictoryThemeFighterOption("Bowser", "koopa", "z12_f_koopa"),
                new VictoryThemeFighterOption("Ice Climbers", "ice_climber", "z16_f_iceclimber"),
                new VictoryThemeFighterOption("Sheik", "sheik", "z15_f_sheik"),
                new VictoryThemeFighterOption("Zelda", "zelda", "z14_f_zelda"),
                new VictoryThemeFighterOption("Dr. Mario", "mariod", "z60_f_mariod"),
                new VictoryThemeFighterOption("Pichu", "pichu", "z88_f_pichu"),
                new VictoryThemeFighterOption("Falco", "falco", "z19_f_falco"),
                new VictoryThemeFighterOption("Marth", "marth", "z17_f_marth"),
                new VictoryThemeFighterOption("Lucina", "lucina", "z61_f_lucina"),
                new VictoryThemeFighterOption("Young Link", "younglink", "z89_f_younglink"),
                new VictoryThemeFighterOption("Ganondorf", "ganon", "z20_f_ganon"),
                new VictoryThemeFighterOption("Mewtwo", "mewtwo", "z80_f_mewtwo"),
                new VictoryThemeFighterOption("Roy", "roy", "z83_f_roy"),
                new VictoryThemeFighterOption("Chrom", "chrom", "z61_f_lucina"),
                new VictoryThemeFighterOption("Game & Watch", "gamewatch", "z18_f_gamewatch"),
                new VictoryThemeFighterOption("Meta Knight", "metaknight", "z22_f_metaknight"),
                new VictoryThemeFighterOption("Pit", "pit", "z23_f_pit"),
                new VictoryThemeFighterOption("Dark Pit", "pitb", "z62_f_pitb"),
                new VictoryThemeFighterOption("Zero Suit Samus", "szerosuit", "z24_f_zerosamus"),
                new VictoryThemeFighterOption("Wario", "wario", "z21_f_wario"),
                new VictoryThemeFighterOption("Snake", "snake", "z46_f_snake"),
                new VictoryThemeFighterOption("Ike", "ike", "z34_f_ike"),
                new VictoryThemeFighterOption("Pokémon Trainer", "ptrainer", "z28_f_ptrainer"),
                new VictoryThemeFighterOption("Squirtle", "pzenigame", "z28_f_ptrainer"),
                new VictoryThemeFighterOption("Ivysaur", "pfushigisou", "z28_f_ptrainer"),
                new VictoryThemeFighterOption("Charizard", "plizardon", "z59_f_lizardon"),
                new VictoryThemeFighterOption("Diddy Kong", "diddy", "z27_f_diddy"),
                new VictoryThemeFighterOption("Lucas", "lucas", "z82_f_lucas"),
                new VictoryThemeFighterOption("Sonic", "sonic", "z47_f_sonic"),
                new VictoryThemeFighterOption("King DeDeDe", "dedede", "z32_f_dedede"),
                new VictoryThemeFighterOption("Olimar", "pikmin", "z25_f_pikmin"),
                new VictoryThemeFighterOption("Lucario", "lucario", "z33_f_lucario"),
                new VictoryThemeFighterOption("R.O.B.", "robot", "z35_f_robot"),
                new VictoryThemeFighterOption("Toon Link", "toonlink", "z41_f_toonlink"),
                new VictoryThemeFighterOption("Wolf", "wolf", "z07_f_fox"),
                new VictoryThemeFighterOption("Villager", "murabito", "z66_f_murabito"),
                new VictoryThemeFighterOption("Mega Man", "rockman", "z74_f_rockman"),
                new VictoryThemeFighterOption("Wii Fit Trainer", "wiifit", "z64_f_wiifit"),
                new VictoryThemeFighterOption("Rosalina & Luma", "rosetta", "z63_f_rosetta"),
                new VictoryThemeFighterOption("Little Mac", "littlemac", "z65_f_littlemac"),
                new VictoryThemeFighterOption("Greninja", "gekkouga", "z72_f_gekkouga"),
                new VictoryThemeFighterOption("Mii Brawler", "miifighter", "z00_f_miifighter"),
                new VictoryThemeFighterOption("Mii Swordfighter", "miiswordsman", "z00_f_miifighter"),
                new VictoryThemeFighterOption("Mii Gunner", "miigunner", "z00_f_miifighter"),
                new VictoryThemeFighterOption("Palutena", "palutena", "z67_f_palutena"),
                new VictoryThemeFighterOption("PAC-MAN", "pacman", "z73_f_pacman"),
                new VictoryThemeFighterOption("Robin", "reflet", "z68_f_reflet"),
                new VictoryThemeFighterOption("Shulk", "shulk", "z71_f_shulk"),
                new VictoryThemeFighterOption("Bowser Jr.", "koopajr", "z70_f_koopajr"),
                new VictoryThemeFighterOption("Duck Hunt", "duckhunt", "z69_f_duckhunt"),
                new VictoryThemeFighterOption("Ryu", "ryu", "z81_f_ryu"),
                new VictoryThemeFighterOption("Ken", "ken", "z81_f_ryu"),
                new VictoryThemeFighterOption("Cloud", "cloud", "z85_f_cloud"),
                new VictoryThemeFighterOption("Corrin", "kamui", "z87_f_kamui"),
                new VictoryThemeFighterOption("Bayonetta", "bayonetta", "z86_f_bayonetta"),
                new VictoryThemeFighterOption("Inkling", "inkling", "z93_f_inkling"),
                new VictoryThemeFighterOption("Ridley", "ridley", "z94_f_ridley"),
                new VictoryThemeFighterOption("King K. Rool", "krool", "z95_f_krool"),
                new VictoryThemeFighterOption("Simon", "simon", "z96_f_simon"),
                new VictoryThemeFighterOption("Richter", "richter", "z96_f_simon"),
                new VictoryThemeFighterOption("Isabelle", "shizue", "z90_f_shizue"),
                new VictoryThemeFighterOption("Incineroar", "gaogaen", "z91_f_gaogaen"),
                new VictoryThemeFighterOption("Piranha Plant", "packun", "z92_f_packun"),
                new VictoryThemeFighterOption("Joker", "jack", "z97a_f_jack_p5"),
                new VictoryThemeFighterOption("Hero", "brave", "z98_f_brave"),
                new VictoryThemeFighterOption("Banjo-Kazooie", "buddy", "z99_f_buddy"),
                new VictoryThemeFighterOption("Terry", "dolly", "zz01_f_dolly"),
                new VictoryThemeFighterOption("Byleth", "master", "zz02_f_master"),
                new VictoryThemeFighterOption("Min Min", "tantan", "zz03_f_tantan"),
                new VictoryThemeFighterOption("Steve", "pickel", "zz04_f_pickel"),
                new VictoryThemeFighterOption("Sephiroth", "edge", "zz05_f_edge"),
                new VictoryThemeFighterOption("Pyra / Mythra", "element", "zz06_f_element"),
                new VictoryThemeFighterOption("Pyra (First)", "flame_first", "zz06_f_element"),
                new VictoryThemeFighterOption("Mythra (First)", "light_first", "zz06_f_element"),
                new VictoryThemeFighterOption("Pyra (Only)", "flame_only", "zz06_f_element"),
                new VictoryThemeFighterOption("Mythra (Only)", "light_only", "zz06_f_element"),
                new VictoryThemeFighterOption("Kazuya", "demon", "zz07_f_demon"),
                new VictoryThemeFighterOption("Sora", "trail", "zz08_f_trail")
            };
        }

        private static string SanitizeToneId(string value)
        {
            return SanitizeIdPart(value);
        }

        private static string SanitizeIdPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Regex.Replace(value.Replace(" ", "_"), @"[^a-zA-Z0-9_]", string.Empty)
                .ToLower(CultureInfo.InvariantCulture);
        }

        private static float RoundVolume(float volume)
        {
            return (float)Math.Round(Math.Clamp(volume, -20f, 20f), 1, MidpointRounding.AwayFromZero);
        }
    }

    public class VictoryThemeEntryViewModel : ViewModelBase
    {
        private const int CustomNameMaximumSize = MusicConstants.GameResources.ToneIdMaximumSize - 6;
        private const int ToneIdMaximumSize = MusicConstants.GameResources.ToneIdMaximumSize;

        private readonly Func<string, string> _toneIdResolver;
        private VictoryThemeFighterOption _selectedFighter;
        private string _customName;
        private string _toneId;
        private bool _useDefaultName = true;
        private float _volume = 2.7f;

        public VictoryThemeEntryViewModel(Func<string, string> toneIdResolver)
        {
            _toneIdResolver = toneIdResolver ?? throw new ArgumentNullException(nameof(toneIdResolver));
        }

        public VictoryThemeFighterOption SelectedFighter
        {
            get => _selectedFighter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedFighter, value);
                if (value?.IsCustom == true)
                    UseDefaultName = false;
                else if (value != null)
                    CustomName = value.CharaName;

                UpdateToneId();
                this.RaisePropertyChanged(nameof(IsCustomNameEnabled));
                this.RaisePropertyChanged(nameof(IsDefaultNameEnabled));
                this.RaisePropertyChanged(nameof(IsToneIdEnabled));
            }
        }

        public string CustomName
        {
            get => _customName;
            set
            {
                var limitedValue = value?.Length > CustomNameMaximumSize
                    ? value.Substring(0, CustomNameMaximumSize)
                    : value;
                this.RaiseAndSetIfChanged(ref _customName, limitedValue);
                UpdateToneId();
            }
        }

        public bool UseDefaultName
        {
            get => _useDefaultName;
            set
            {
                var resolvedValue = SelectedFighter?.IsCustom == true ? false : value;
                this.RaiseAndSetIfChanged(ref _useDefaultName, resolvedValue);
                if (resolvedValue && SelectedFighter != null)
                    CustomName = SelectedFighter.CharaName;
                UpdateToneId();
                this.RaisePropertyChanged(nameof(IsCustomNameEnabled));
                this.RaisePropertyChanged(nameof(IsToneIdEnabled));
            }
        }

        public bool IsCustomNameEnabled => SelectedFighter == null || SelectedFighter.IsCustom;
        public bool IsDefaultNameEnabled => SelectedFighter != null && !SelectedFighter.IsCustom;
        public bool IsToneIdEnabled => SelectedFighter == null || SelectedFighter.IsCustom || !UseDefaultName;

        public string ToneId
        {
            get => _toneId;
            set
            {
                var limitedValue = value?.Length > ToneIdMaximumSize
                    ? value.Substring(0, ToneIdMaximumSize)
                    : value;
                this.RaiseAndSetIfChanged(ref _toneId, limitedValue);
            }
        }

        [Reactive]
        public string SourceFile { get; set; }

        [Reactive]
        public string SourceFileName { get; set; }

        [Reactive]
        public bool ApplyNormalization { get; set; }

        public float Volume
        {
            get => _volume;
            set
            {
                var roundedValue = (float)Math.Round(Math.Clamp(value, -20f, 20f), 1, MidpointRounding.AwayFromZero);
                this.RaiseAndSetIfChanged(ref _volume, roundedValue);
            }
        }

        private void UpdateToneId()
        {
            var name = SanitizeIdPart(CustomName);
            if (SelectedFighter?.IsCustom != true && UseDefaultName)
            {
                var defaultName = SelectedFighter?.CharaName ?? name;
                ToneId = string.IsNullOrWhiteSpace(defaultName) ? string.Empty : _toneIdResolver(defaultName);
                return;
            }

            var customName = SelectedFighter?.IsCustom == false
                ? SelectedFighter.CharaName
                : name;
            ToneId = string.IsNullOrWhiteSpace(customName) ? string.Empty : $"zzc_f_{customName}";
        }

        private static string SanitizeIdPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Regex.Replace(value.Replace(" ", "_"), @"[^a-zA-Z0-9_]", string.Empty)
                .ToLower(CultureInfo.InvariantCulture);
        }
    }

    public class VictoryThemeFighterOption
    {
        public string DisplayName { get; }
        public string CharaName { get; }
        public string ToneId { get; }
        public bool IsCustom { get; }

        public VictoryThemeFighterOption(string displayName, string charaName, string toneId, bool isCustom = false)
        {
            DisplayName = displayName;
            CharaName = charaName;
            ToneId = toneId;
            IsCustom = isCustom;
        }
    }
}
