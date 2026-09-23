using System.Collections.Generic;

namespace Sma5h.Mods.Music.Helpers
{
    public class MusicConstants
    {
        public const string VersionSma5hMusic = "2.2";
        public const string VersionSma5hMusicOverride = "2.2";

        public class MusicModFiles
        {
            public const string MUSIC_MOD_METADATA_JSON_FILE = "metadata_mod.json";
            public const string MUSIC_OVERRIDE_ORDER_JSON_FILE = "order_override.json";
            public const string MUSIC_OVERRIDE_PLAYLIST_JSON_FILE = "playlist_override.json";
            public const string MUSIC_OVERRIDE_CORE_BGM_JSON_FILE = "core_bgm_override.json";
            public const string MUSIC_OVERRIDE_CORE_GAME_JSON_FILE = "core_game_override.json";
            public const string MUSIC_OVERRIDE_CORE_SERIES_JSON_FILE = "core_series_override.json";
            public const string MUSIC_OVERRIDE_STAGE_JSON_FILE = "stage_override.json";
        }

        public class Resources
        {
            public const string NUS3AUDIO_EXE_FILE = "Nus3Audio/nus3audio.exe";
            public const string NUS3BANK_TEMPLATE_FILE = "template.nus3bank";
            public const string NUS3BANK_IDS_FILE = "nusbank_ids.csv";
            public const string NUS3AUDIO_TEMP_FILE = "temp.{0}";
            public const string AUDIO_FILE = "bgm_{0}{1}";
        }

        public class GameResources
        {
            public const string NUS3AUDIO_FILE = "bgm_{0}.nus3audio";
            public const string NUS3BANK_FILE = "bgm_{0}.nus3bank";

            public const int ToneIdMinimumSize = 1; //TOVERIFY
            public const int ToneIdMaximumSize = 47;

            public const int DbRootIdMinimumSize = ToneIdMinimumSize + 7;
            public const int DbRootIdMaximumSize = ToneIdMaximumSize + 7;

            public const int StreamSetIdMinimumSize = ToneIdMinimumSize + 4;
            public const int StreamSetIdMaximumSize = ToneIdMaximumSize + 4;

            public const int AssignedInfoIdMinimumSize = ToneIdMinimumSize + 5;
            public const int AssignedInfoIdMaximumSize = ToneIdMaximumSize + 5;

            public const int StreamIdMinimumSize = ToneIdMinimumSize + 7;
            public const int StreamIdMaximumSize = ToneIdMaximumSize + 7;

            public const int GameTitleMinimumSize = 14; //TO VERIFY
            public const int GameTitleMaximumSize = 62; //TO VERIFY

            public const int SeriesMinimumSize = 13; //TO VERIFY
            public const int SeriesMaximumSize = 62; //TO VERIFY
        }

        public class InternalIds
        {
            public const string NUS3AUDIO_FILE_PREFIX = "bgm_";

            public const string UI_BGM_ID_PREFIX = "ui_bgm_";
            public const string STREAM_SET_PREFIX = "set_";
            public const string INFO_ID_PREFIX = "info_";
            public const string STREAM_PREFIX = "stream_";

            public const string GAME_TITLE_ID_PREFIX = "ui_gametitle_";
            public const string SERIES_ID_PREFIX = "ui_series_";
            public const string RECORD_TYPE_PREFIX = "record_";

            public const string PLAYLIST_PREFIX = "bgm";

            public const string MSBT_GAME_TITLE_PREFIX = "tit_";
            public const string MSBT_GAME_TITLE = "tit_{0}";
            public const string MSBT_SERIES_TITLE = "tit_series_snd_{0}";
            public const string MSBT_BGM_TITLE = "bgm_title_{0}";
            public const string MSBT_BGM_AUTHOR = "bgm_author_{0}";
            public const string MSBT_BGM_COPYRIGHT = "bgm_copyright_{0}";

            public const string BGM_ID_RANDOM = "ui_bgm_random";
            public const string SERIES_ID_DEFAULT = "ui_series_none";
            public const string GAME_TITLE_ID_DEFAULT = "ui_gametitle_none";
            public const string RECORD_TYPE_DEFAULT = "record_none";
            public const string RARITY_DEFAULT = "bgm_rarity_0";
            public const string SOUND_CONDITION = "sound_condition_none";

            public const string PLAYLIST_AUTO_BGM_SELECTOR = "bgmhiddenselector";

            public const string SERIES_ICON_VARIANT_PRIMARY = "series_0";
            public const string SERIES_ICON_VARIANT_SECONDARY = "series_1";

            public const string PLAYLIST_SMASH_BATTLE = "bgmsmashbtl";
        }

        public static string[] INVALID_SERIES = new string[]
        {
            "ui_series_random",
            "ui_series_mymusic",
            "ui_series_all"
        };

        public static string[] DLC_SERIES = new string[]
        {
            "ui_series_persona",
            "ui_series_dragonquest",
            "ui_series_banjokazooie",
            "ui_series_fatalfury",
            "ui_series_arms",
            "ui_series_minecraft",
            "ui_series_tekken",
            "ui_series_kingdomhearts"
        };

        public static string[] VALID_RECORD_TYPES = new string[]
        {
            "record_arrange",
            "record_new_arrange",
            "record_original"
        };

        public static string[] VALID_MUSIC_EXTENSIONS = new string[]
        {
            ".idsp",
            ".lopus",
            ".brstm"
        };

        public static string[] VALID_AUDIO_EXTENSIONS = new string[]
        {
            ".mp3",
            ".flac",
            ".wav",
            ".ogg"
        };

        public static string[] EXTENSIONS_NEED_CONVERSION = new string[]
        {
            ".brstm"
        };

        public readonly static Dictionary<string, int> DEFAULT_SERIES_DISP_ORDER_SOUND = new Dictionary<string, int>()
        {
            {"ui_series_smashbros", 1 },
            {"ui_series_mario", 2 },
            {"ui_series_mariokart", 3 },
            {"ui_series_donkeykong", 4 },
            {"ui_series_zelda", 5 },
            {"ui_series_metroid", 6 },
            {"ui_series_yoshi", 7 },
            {"ui_series_kirby", 8 },
            {"ui_series_starfox", 9 },
            {"ui_series_pokemon", 10 },
            {"ui_series_fzero", 11 },
            {"ui_series_mother", 12 },
            {"ui_series_fireemblem", 13 },
            {"ui_series_gamewatch", 14 },
            {"ui_series_palutena", 15 },
            {"ui_series_wario", 16 },
            {"ui_series_pikmin", 17 },
            {"ui_series_doubutsu", 18 },
            {"ui_series_wiifit", 19 },
            {"ui_series_punchout", 20 },
            {"ui_series_xenoblade", 21 },
            {"ui_series_splatoon", 22 },
            {"ui_series_metalgear", 23 },
            {"ui_series_sonic", 24 },
            {"ui_series_rockman", 25 },
            {"ui_series_pacman", 26 },
            {"ui_series_streetfighter", 27 },
            {"ui_series_finalfantasy", 28 },
            {"ui_series_bayonetta", 29 },
            {"ui_series_castlevania", 30 },
            {"ui_series_persona", 31 },
            {"ui_series_dragonquest", 32 },
            {"ui_series_banjokazooie", 33 },
            {"ui_series_fatalfury", 34 },
            {"ui_series_arms", 35 },
            {"ui_series_minecraft", 36 },
            {"ui_series_tekken", 37 },
            {"ui_series_kingdomhearts", 38 },
            {"ui_series_etc", 99 }
        };

        public readonly static Dictionary<string, bool> DEFAULT_SERIES_SHOWN_AS_SERIES_IN_DIRECTORY = new Dictionary<string, bool>()
        {
            {"ui_series_smashbros", true },
            {"ui_series_mario", true },
            {"ui_series_mariokart", true },
            {"ui_series_donkeykong", true },
            {"ui_series_zelda", true },
            {"ui_series_metroid", true },
            {"ui_series_yoshi", true },
            {"ui_series_kirby", true },
            {"ui_series_starfox", true },
            {"ui_series_pokemon", true },
            {"ui_series_fzero", true },
            {"ui_series_mother", true },
            {"ui_series_fireemblem", true },
            {"ui_series_gamewatch", true },
            {"ui_series_palutena", true },
            {"ui_series_wario", true },
            {"ui_series_pikmin", true },
            {"ui_series_doubutsu", true },
            {"ui_series_wiifit", true },
            {"ui_series_punchout", true },
            {"ui_series_xenoblade", true },
            {"ui_series_splatoon", true },
            {"ui_series_metalgear", true },
            {"ui_series_sonic", true },
            {"ui_series_rockman", true },
            {"ui_series_pacman", true },
            {"ui_series_streetfighter", true },
            {"ui_series_finalfantasy", true },
            {"ui_series_bayonetta", true },
            {"ui_series_castlevania", true },
            {"ui_series_persona", true },
            {"ui_series_dragonquest", true },
            {"ui_series_banjokazooie", true },
            {"ui_series_fatalfury", true },
            {"ui_series_arms", false },
            {"ui_series_minecraft", true },
            {"ui_series_tekken", true },
            {"ui_series_kingdomhearts", true },
            {"ui_series_etc", false }
        };

        public readonly static Dictionary<string, string> DEFAULT_STAGE_BGM_SET_ID = new Dictionary<string, string>()
        {
            {"ui_stage_75m", "bgmdk" },
            {"ui_stage_animal_city", "bgmanimal" },
            {"ui_stage_animal_island", "bgmanimal" },
            {"ui_stage_animal_village", "bgmanimal" },
            {"ui_stage_balloonfight", "bgmother" },
            {"ui_stage_battle_field", "bgmsmashbtl" },
            {"ui_stage_battle_field_l", "bgmsmashbtl" },
            {"ui_stage_battle_field_s", "bgmsmashbtl" },
            {"ui_stage_bayo_clock", "bgmbeyo" },
            {"ui_stage_bonus_game", "bgmsmashmode" },
            {"ui_stage_boss_dracula", "bgmboss" },
            {"ui_stage_boss_final", "bgmboss" },
            {"ui_stage_boss_final2", "bgmboss" },
            {"ui_stage_boss_final3", "bgmboss" },
            {"ui_stage_boss_galleom", "bgmboss" },
            {"ui_stage_boss_ganon", "bgmboss" },
            {"ui_stage_boss_marx", "bgmboss" },
            {"ui_stage_boss_rathalos", "bgmboss" },
            {"ui_stage_brave_altar", "bgmbrave" },
            {"ui_stage_buddy_spiral", "bgmbuddy" },
            {"ui_stage_campaign_map", "bgmadventure" },
            {"ui_stage_demon_dojo", "bgmdemon" },
            {"ui_stage_dk_jungle", "bgmdk" },
            {"ui_stage_dk_lodge", "bgmdk" },
            {"ui_stage_dk_waterfall", "bgmdk" },
            {"ui_stage_dolly_stadium", "bgmdolly" },
            {"ui_stage_dracula_castle", "bgmdracula" },
            {"ui_stage_duckhunt", "bgmother" },
            {"ui_stage_edit", "bgmstageedit" },
            {"ui_stage_end", "bgmsmashbtl" },
            {"ui_stage_fe_arena", "bgmfe" },
            {"ui_stage_fe_colloseum", "bgmfe" },
            {"ui_stage_fe_shrine", "bgmmaster" },
            {"ui_stage_fe_siege", "bgmfe" },
            {"ui_stage_ff_cave", "bgmedge" },
            {"ui_stage_ff_midgar", "bgmff" },
            {"ui_stage_flatzonex", "bgmgamewatch" },
            {"ui_stage_fox_corneria", "bgmfox" },
            {"ui_stage_fox_lylatcruise", "bgmfox" },
            {"ui_stage_fox_venom", "bgmfox" },
            {"ui_stage_fzero_bigblue", "bgmfzero" },
            {"ui_stage_fzero_mutecity3ds", "bgmfzero" },
            {"ui_stage_fzero_porttown", "bgmfzero" },
            {"ui_stage_general_all", "bgmsmashmode" },
            {"ui_stage_homerun", "bgmsmashmode" },
            {"ui_stage_icarus_angeland", "bgmicaros" },
            {"ui_stage_icarus_skyworld", "bgmicaros" },
            {"ui_stage_icarus_uprising", "bgmicaros" },
            {"ui_stage_ice_top", "bgmother" },
            {"ui_stage_jack_mementoes", "bgmjack" },
            {"ui_stage_kart_circuitfor", "bgmmkart" },
            {"ui_stage_kart_circuitx", "bgmmkart" },
            {"ui_stage_kirby_cave", "bgmkirby" },
            {"ui_stage_kirby_fountain", "bgmkirby" },
            {"ui_stage_kirby_gameboy", "bgmkirby" },
            {"ui_stage_kirby_greens", "bgmkirby" },
            {"ui_stage_kirby_halberd", "bgmkirby" },
            {"ui_stage_kirby_pupupu64", "bgmkirby" },
            {"ui_stage_luigimansion", "bgmmario" },
            {"ui_stage_mario_3dland", "bgmmario" },
            {"ui_stage_mario_castle64", "bgmmario" },
            {"ui_stage_mario_castledx", "bgmmario" },
            {"ui_stage_mario_dolpic", "bgmmario" },
            {"ui_stage_mario_galaxy", "bgmmario" },
            {"ui_stage_mario_maker", "bgmmario" },
            {"ui_stage_mario_newbros2", "bgmmario" },
            {"ui_stage_mario_odyssey", "bgmmario" },
            {"ui_stage_mario_paper", "bgmmario" },
            {"ui_stage_mario_past64", "bgmmario" },
            {"ui_stage_mario_pastusa", "bgmmario" },
            {"ui_stage_mario_pastx", "bgmmario" },
            {"ui_stage_mario_rainbow", "bgmmario" },
            {"ui_stage_mario_uworld", "bgmmario" },
            {"ui_stage_mariobros", "bgmmario" },
            {"ui_stage_menu_music", "bgmsmashmenu" },
            {"ui_stage_metroid_kraid", "bgmmetroid" },
            {"ui_stage_metroid_norfair", "bgmmetroid" },
            {"ui_stage_metroid_orpheon", "bgmmetroid" },
            {"ui_stage_metroid_zebesdx", "bgmmetroid" },
            {"ui_stage_mg_shadowmoses", "bgmmetalgear" },
            {"ui_stage_mother_fourside", "bgmmother" },
            {"ui_stage_mother_magicant", "bgmmother" },
            {"ui_stage_mother_newpork", "bgmmother" },
            {"ui_stage_mother_onett", "bgmmother" },
            {"ui_stage_nintendogs", "bgmother" },
            {"ui_stage_pac_land", "bgmpacman" },
            {"ui_stage_pickel_world", "bgmpickel" },
            {"ui_stage_pictochat2", "bgmother" },
            {"ui_stage_pikmin_garden", "bgmpikmin" },
            {"ui_stage_pikmin_planet", "bgmpikmin" },
            {"ui_stage_pilotwings", "bgmother" },
            {"ui_stage_plankton", "bgmother" },
            {"ui_stage_poke_kalos", "bgmpokemon" },
            {"ui_stage_poke_stadium", "bgmpokemon" },
            {"ui_stage_poke_stadium2", "bgmpokemon" },
            {"ui_stage_poke_tengam", "bgmpokemon" },
            {"ui_stage_poke_tower", "bgmpokemon" },
            {"ui_stage_poke_unova", "bgmpokemon" },
            {"ui_stage_poke_yamabuki", "bgmpokemon" },
            {"ui_stage_punchoutsb", "bgmpunchout" },
            {"ui_stage_punchoutw", "bgmpunchout" },
            {"ui_stage_random", "bgmmario" },
            {"ui_stage_random_battle_field", "bgmmario" },
            {"ui_stage_random_end", "bgmmario" },
            {"ui_stage_random_normal", "bgmmario" },
            {"ui_stage_rock_wily", "bgmrockman" },
            {"ui_stage_setting_stage", "bgmsmashmode" },
            {"ui_stage_sf_suzaku", "bgmsf" },
            {"ui_stage_sham_fight", "bgmsmashmode" },
            {"ui_stage_sonic_greenhill", "bgmsonic" },
            {"ui_stage_sonic_windyhill", "bgmsonic" },
            {"ui_stage_spla_parking", "bgmspla" },
            {"ui_stage_streetpass", "bgmother" },
            {"ui_stage_tantan_spring", "bgmtantan" },
            {"ui_stage_tomodachi", "bgmother" },
            {"ui_stage_trail_castle", "bgmtrail" },
            {"ui_stage_training", "bgmsmashbtl" },
            {"ui_stage_wario_gamer", "bgmwario" },
            {"ui_stage_wario_madein", "bgmwario" },
            {"ui_stage_wiifit", "bgmwiifit" },
            {"ui_stage_wreckingcrew", "bgmother" },
            {"ui_stage_wufuisland", "bgmother" },
            {"ui_stage_xeno_alst", "bgmelement" },
            {"ui_stage_xeno_gaur", "bgmxenoblade" },
            {"ui_stage_yoshi_cartboard", "bgmyoshi" },
            {"ui_stage_yoshi_island", "bgmyoshi" },
            {"ui_stage_yoshi_story", "bgmyoshi" },
            {"ui_stage_yoshi_yoster", "bgmyoshi" },
            {"ui_stage_zelda_gerudo", "bgmzelda" },
            {"ui_stage_zelda_greatbay", "bgmzelda" },
            {"ui_stage_zelda_hyrule", "bgmzelda" },
            {"ui_stage_zelda_oldin", "bgmzelda" },
            {"ui_stage_zelda_pirates", "bgmzelda" },
            {"ui_stage_zelda_skyward", "bgmzelda" },
            {"ui_stage_zelda_temple", "bgmzelda" },
            {"ui_stage_zelda_tower", "bgmzelda" },
            {"ui_stage_zelda_train", "bgmzelda" }
        };

        public readonly static Dictionary<string, byte> DEFAULT_STAGE_BGM_SETTING_NO = new Dictionary<string, byte>()
        {
            {"ui_stage_75m", 3 },
            {"ui_stage_animal_city", 2 },
            {"ui_stage_animal_island", 1 },
            {"ui_stage_animal_village", 0 },
            {"ui_stage_balloonfight", 3 },
            {"ui_stage_battle_field", 0 },
            {"ui_stage_battle_field_l", 1 },
            {"ui_stage_battle_field_s", 0 },
            {"ui_stage_bayo_clock", 0 },
            {"ui_stage_bonus_game", 0 },
            {"ui_stage_boss_dracula", 3 },
            {"ui_stage_boss_final", 5 },
            {"ui_stage_boss_final2", 6 },
            {"ui_stage_boss_final3", 7 },
            {"ui_stage_boss_galleom", 4 },
            {"ui_stage_boss_ganon", 0 },
            {"ui_stage_boss_marx", 2 },
            {"ui_stage_boss_rathalos", 1 },
            {"ui_stage_brave_altar", 0 },
            {"ui_stage_buddy_spiral", 0 },
            {"ui_stage_campaign_map", 0 },
            {"ui_stage_demon_dojo", 0 },
            {"ui_stage_dk_jungle", 0 },
            {"ui_stage_dk_lodge", 2 },
            {"ui_stage_dk_waterfall", 1 },
            {"ui_stage_dolly_stadium", 0 },
            {"ui_stage_dracula_castle", 0 },
            {"ui_stage_duckhunt", 1 },
            {"ui_stage_edit", 0 },
            {"ui_stage_end", 2 },
            {"ui_stage_fe_arena", 1 },
            {"ui_stage_fe_colloseum", 2 },
            {"ui_stage_fe_shrine", 0 },
            {"ui_stage_fe_siege", 0 },
            {"ui_stage_ff_cave", 0 },
            {"ui_stage_ff_midgar", 0 },
            {"ui_stage_flatzonex", 0 },
            {"ui_stage_fox_corneria", 0 },
            {"ui_stage_fox_lylatcruise", 2 },
            {"ui_stage_fox_venom", 1 },
            {"ui_stage_fzero_bigblue", 0 },
            {"ui_stage_fzero_mutecity3ds", 2 },
            {"ui_stage_fzero_porttown", 1 },
            {"ui_stage_general_all", 3 },
            {"ui_stage_homerun", 2 },
            {"ui_stage_icarus_angeland", 2 },
            {"ui_stage_icarus_skyworld", 0 },
            {"ui_stage_icarus_uprising", 1 },
            {"ui_stage_ice_top", 0 },
            {"ui_stage_jack_mementoes", 0 },
            {"ui_stage_kart_circuitfor", 1 },
            {"ui_stage_kart_circuitx", 0 },
            {"ui_stage_kirby_cave", 5 },
            {"ui_stage_kirby_fountain", 1 },
            {"ui_stage_kirby_gameboy", 4 },
            {"ui_stage_kirby_greens", 2 },
            {"ui_stage_kirby_halberd", 3 },
            {"ui_stage_kirby_pupupu64", 0 },
            {"ui_stage_luigimansion", 7 },
            {"ui_stage_mario_3dland", 9 },
            {"ui_stage_mario_castle64", 0 },
            {"ui_stage_mario_castledx", 2 },
            {"ui_stage_mario_dolpic", 5 },
            {"ui_stage_mario_galaxy", 13 },
            {"ui_stage_mario_maker", 14 },
            {"ui_stage_mario_newbros2", 10 },
            {"ui_stage_mario_odyssey", 15 },
            {"ui_stage_mario_paper", 11 },
            {"ui_stage_mario_past64", 1 },
            {"ui_stage_mario_pastusa", 4 },
            {"ui_stage_mario_pastx", 6 },
            {"ui_stage_mario_rainbow", 3 },
            {"ui_stage_mario_uworld", 12 },
            {"ui_stage_mariobros", 8 },
            {"ui_stage_menu_music", 0 },
            {"ui_stage_metroid_kraid", 1 },
            {"ui_stage_metroid_norfair", 2 },
            {"ui_stage_metroid_orpheon", 3 },
            {"ui_stage_metroid_zebesdx", 0 },
            {"ui_stage_mg_shadowmoses", 0 },
            {"ui_stage_mother_fourside", 1 },
            {"ui_stage_mother_magicant", 3 },
            {"ui_stage_mother_newpork", 2 },
            {"ui_stage_mother_onett", 0 },
            {"ui_stage_nintendogs", 4 },
            {"ui_stage_pac_land", 0 },
            {"ui_stage_pickel_world", 0 },
            {"ui_stage_pictochat2", 7 },
            {"ui_stage_pikmin_garden", 1 },
            {"ui_stage_pikmin_planet", 0 },
            {"ui_stage_pilotwings", 9 },
            {"ui_stage_plankton", 8 },
            {"ui_stage_poke_kalos", 6 },
            {"ui_stage_poke_stadium", 1 },
            {"ui_stage_poke_stadium2", 2 },
            {"ui_stage_poke_tengam", 3 },
            {"ui_stage_poke_tower", 5 },
            {"ui_stage_poke_unova", 4 },
            {"ui_stage_poke_yamabuki", 0 },
            {"ui_stage_punchoutsb", 0 },
            {"ui_stage_punchoutw", 1 },
            {"ui_stage_random", 0 },
            {"ui_stage_random_battle_field", 0 },
            {"ui_stage_random_end", 0 },
            {"ui_stage_random_normal", 0 },
            {"ui_stage_rock_wily", 0 },
            {"ui_stage_setting_stage", 4 },
            {"ui_stage_sf_suzaku", 0 },
            {"ui_stage_sham_fight", 6 },
            {"ui_stage_sonic_greenhill", 0 },
            {"ui_stage_sonic_windyhill", 1 },
            {"ui_stage_spla_parking", 0 },
            {"ui_stage_streetpass", 5 },
            {"ui_stage_tantan_spring", 0 },
            {"ui_stage_tomodachi", 6 },
            {"ui_stage_trail_castle", 0 },
            {"ui_stage_training", 3 },
            {"ui_stage_wario_gamer", 1 },
            {"ui_stage_wario_madein", 0 },
            {"ui_stage_wiifit", 0 },
            {"ui_stage_wreckingcrew", 2 },
            {"ui_stage_wufuisland", 10 },
            {"ui_stage_xeno_alst", 0 },
            {"ui_stage_xeno_gaur", 0 },
            {"ui_stage_yoshi_cartboard", 2 },
            {"ui_stage_yoshi_island", 3 },
            {"ui_stage_yoshi_story", 0 },
            {"ui_stage_yoshi_yoster", 1 },
            {"ui_stage_zelda_gerudo", 5 },
            {"ui_stage_zelda_greatbay", 1 },
            {"ui_stage_zelda_hyrule", 0 },
            {"ui_stage_zelda_oldin", 3 },
            {"ui_stage_zelda_pirates", 4 },
            {"ui_stage_zelda_skyward", 7 },
            {"ui_stage_zelda_temple", 2 },
            {"ui_stage_zelda_tower", 8 },
            {"ui_stage_zelda_train", 6 },
        };

        public static Dictionary<string, string> SPECIAL_CATEGORY_LABELS = new Dictionary<string, string>()
        {
            {"0x16ff8d1375", "mario_3dland_scenelink" },
            {"0x15e9235a3d", "mario_paper_scenelink" },
            {"0x1b2d134791", "mario_pastusa_situationlink" },
            {"0x1ad6262d31", "mario_past64_situationlink" },
            {"0x16fcec16b2", "mario_odyssey_partlink" },
            {"0x1b2901643f", "mario_odyssey_kinopiotaicho" },
            {"0x1686642302", "yoshi_island_scenelink" },
            {"0x17b37fd636", "kirby_gameboy_scenelink" },
            {"0x16ea1970ce", "wario_madein_minigames" },
            {"0x0d009c712f", "totakeke_live" },
            {"0x105274ba4f", "sf_situationlink" },
            {"0x12111561a7", "splaparking_bg_fes" }, //Moray Towers
            {"0x150281ae07", "mario_maker_scenelink" },
            {"0x11ff737d4d", "jack_mementoes_p3" },
            {"0x116117e8ee", "jack_mementoes_p4" },
            {"0x111610d878", "jack_mementoes_p5" },
            {"0x1609de57c3", "dolly_stadium_theme_01" },
            {"0x1690d70679", "dolly_stadium_theme_02" },
            {"0x16e7d036ef", "dolly_stadium_theme_03" },
            {"0x1679b4a34c", "dolly_stadium_theme_04" },
            {"0x160eb393da", "dolly_stadium_theme_05" },
            {"0x1697bac260", "dolly_stadium_theme_06" },
            {"0x16e0bdf2f6", "dolly_stadium_theme_07" },
            {"0x167002ef67", "dolly_stadium_theme_08" },
            {"0x160705dff1", "dolly_stadium_theme_09" },
            {"0x1667c25614", "dolly_stadium_theme_10" },
            {"0x1610c56682", "dolly_stadium_theme_11" },
            {"0x1689cc3738", "dolly_stadium_theme_12" },
            {"0x16fecb07ae", "dolly_stadium_theme_13" }
        };

        public readonly static Dictionary<string, string> CONVERTER_CORE_PLAYLISTS = new Dictionary<string, string>()
        {
            {"bgmsmashbtl", "Smash Battle" },
            {"bgmsmashmenu", "Smash Menu" },
            {"bgmsmashmode", "Smash Mode" },
            {"bgmstageedit", "Stage Edit" },
            {"bgmboss", "Boss" },
            {"bgmadventure", "Adventure" },
            {"bgmmario", "Mario" },
            {"bgmmkart", "Mario Kart" },
            {"bgmdk", "Donkey Kong" },
            {"bgmkirby", "Kirby" },
            {"bgmzelda", "The Legend of Zelda" },
            {"bgmmetroid", "Metroid" },
            {"bgmfzero", "F-Zero" },
            {"bgmyoshi", "Yoshi" },
            {"bgmfox", "Starfox" },
            {"bgmpokemon", "Pok\u00E9mon" },
            {"bgmmother", "Mother" },
            {"bgmfe", "Fire Emblem" },
            {"bgmgamewatch", "Game & Watch" },
            {"bgmicaros", "Kid Icarus" },
            {"bgmwario", "Wario" },
            {"bgmpikmin", "Pikmin" },
            {"bgmanimal", "Animal Crossing" },
            {"bgmwiifit", "Wii-Fit" },
            {"bgmpunchout", "Punch-Out!!" },
            {"bgmxenoblade", "Xenoblade" },
            {"bgmspla", "Splatoon" },
            {"bgmmetalgear", "Metal Gear" },
            {"bgmsonic", "Sonic" },
            {"bgmrockman", "Megaman" },
            {"bgmpacman", "Pacman" },
            {"bgmsf", "Street Fighter" },
            {"bgmff", "Final Fantasy" },
            {"bgmbeyo", "Bayonetta" },
            {"bgmdracula", "Castlevania" },
            {"bgmother", "Other" },
            {"bgmjack", "Persona" },
            {"bgmbrave", "Dragon Quest" },
            {"bgmbuddy", "Banjo-Kazooie" },
            {"bgmdolly", "Fatal Fury" },
            {"bgmmaster", "Fire Emblem Three Houses" },
            {"bgmtantan", "Arms" },
            {"bgmpickel", "Minecraft" },
            {"bgmedge", "Final Fantasy (Sephiroth)" },
            {"bgmelement", "Xenoblade 2 (Pyra & Mythra)" },
            {"bgmdemon", "Tekken" },
            {"bgmtrail", "Kingdom Hearts" },
            {"bgmplaylist", "Playlist" },
        };

        public static Dictionary<string, string> SOUND_CONDITION_LABELS = new Dictionary<string, string>()
        {
            {"0x147340113c", "sound_condition_none" },
            {"0x1d7feb1956", "sound_condition_xvillage_live" }
        };

        public static Dictionary<string, string> SOUND_CONDITION_PROCESS_LABELS = new Dictionary<string, string>()
        {
            {"0x1b9fe75d3f", "sound_condition_process_add" },
            {"0x21bee0c6ef", "sound_condition_process_exclusive" }
        };
    }
}
