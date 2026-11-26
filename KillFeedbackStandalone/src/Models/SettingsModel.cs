using System;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using EFT;
using KillFeedback.Core;
using TMPro;
using UnityEngine;

namespace KillFeedback.Models
{
    /// <summary>
    /// Every user-facing setting, bound to the BepInEx config file.
    /// <para>
    /// The original mod used a custom <c>EFTConfigurationPlugin</c> to draw these in the game's own
    /// settings screen, which required the separate <c>KmyTarkovApi</c> plugin. The standalone port
    /// drops that dependency, so the options are plain BepInEx config entries and are edited with
    /// the standard Configuration Manager that ships with SPT.
    /// </para>
    /// <para>
    /// Keys and section names stay in English - they are the storage format of the config file.
    /// The label the manager draws is supplied separately through <see cref="UiText"/>, which is
    /// what makes the panel switchable between English and Chinese without losing any value.
    /// </para>
    /// </summary>
    internal static class SettingsModel
    {
        public const string MainSection = "Main Settings";
        public const string PositionSection = "Position Rotation Scale Settings";
        public const string ColorSection = "Kill Color Settings";
        public const string FontSection = "Font Styles Settings";
        public const string SpeedSection = "Animation Speed Settings";
        public const string DistanceSection = "Distance Settings";

        public static ConfigEntry<bool> KillHUDSw;
        public static ConfigEntry<bool> ExpHUDSw;
        public static ConfigEntry<bool> KillWaitBottom;
        public static ConfigEntry<bool> KillHasDistance;
        public static ConfigEntry<bool> KillHasStreak;
        public static ConfigEntry<bool> KillHasOther;
        public static ConfigEntry<bool> KillHasXp;
        public static ConfigEntry<bool> KillHasLevel;
        public static ConfigEntry<bool> KillHasPart;
        public static ConfigEntry<bool> KillHasWeapon;
        public static ConfigEntry<bool> KillHasSide;
        public static ConfigEntry<bool> KillScavEn;

        /// <summary>Interface language of the settings panel itself.</summary>
        public static ConfigEntry<HudLanguage> Language;

        public static Vector2Setting KillAnchoredPosition;
        public static Vector2Setting KillSizeDelta;
        public static Vector2Setting KillLocalScale;
        public static Vector2Setting KillExpAnchoredPosition;
        public static Vector2Setting KillExpSizeDelta;
        public static Vector2Setting KillExpLocalScale;

        public static ConfigEntry<float> ExpWaitSpeed;
        public static ConfigEntry<float> KillWriteWaitTime;
        public static ConfigEntry<float> KillWrite2WaitTime;
        public static ConfigEntry<float> KillWaitTime;

        public static ConfigEntry<float> KillDistance;

        public static ColorSetting KillNameColor;
        public static ColorSetting KillWeaponColor;
        public static ColorSetting KillDistanceColor;
        public static ColorSetting KillLevelColor;
        public static ColorSetting KillXpColor;
        public static ColorSetting KillPartColor;
        public static ColorSetting KillMetersColor;
        public static ColorSetting KillStreakColor;
        public static ColorSetting KillLvlColor;
        public static ColorSetting KillStatsStreakColor;
        public static ColorSetting KillOtherColor;
        public static ColorSetting KillEnemyDownColor;
        public static ColorSetting KillUsecColor;
        public static ColorSetting KillBearColor;
        public static ColorSetting KillScavColor;
        public static ColorSetting KillBossColor;
        public static ColorSetting KillFollowerColor;
        public static ColorSetting KillBracketColor;
        public static ColorSetting ExpColor;

        public static ConfigEntry<FontStyles> KillInfoStyles;
        public static ConfigEntry<FontStyles> KillDistanceStyles;
        public static ConfigEntry<FontStyles> KillStreakStyles;
        public static ConfigEntry<FontStyles> KillOtherStyles;
        public static ConfigEntry<FontStyles> KillXpStyles;
        public static ConfigEntry<FontStyles> ExpStyles;

        /// <summary>True when the HUD should stay visible outside a raid (for the test kill).</summary>
        public static ConfigEntry<bool> AlwaysShowHud;

        /// <summary>
        /// Writes the per-kill diagnostics to the log. Off by default; only useful when the HUD
        /// does not show up where it should.
        /// </summary>
        public static ConfigEntry<bool> DebugLog;

        /// <summary>
        /// Sorting order of the HUD canvas. Raise it if the kill feed ends up behind the game's
        /// own interface.
        /// </summary>
        public static ConfigEntry<int> CanvasSortingOrder;

        /// <summary>
        /// Draws the "Test Kill" button, the same way the original mod did. It lives on a dummy
        /// entry so it shows up in the standard Configuration Manager window.
        /// </summary>
        private static void DrawTestKill(ConfigEntryBase entry)
        {
            if (GUILayout.Button(UiText.Pick("Test Kill", "测试击杀"), GUILayout.ExpandWidth(true)))
            {
                TestKill();
            }
        }

        public static void Create(ConfigFile configFile)
        {
            // The panel language has to be known before the first label is built, and the config
            // entry that holds it can only be read once it is bound - so it is read straight from
            // the file instead. Binding it last also puts it right under "Test Kill".
            UiText.Language = ReadLanguage(configFile);

            KillHUDSw = configFile.Bind(MainSection, "Kill HUD display", true,
                UiText.Describe(MainSection, "Kill HUD display", "显示击杀列表",
                    "Show the kill feed", "显示击杀列表"));

            ExpHUDSw = configFile.Bind(MainSection, "Exp HUD display", true,
                UiText.Describe(MainSection, "Exp HUD display", "显示经验累计",
                    "Show the experience counter", "显示经验累计"));

            KillWaitBottom = configFile.Bind(MainSection, "Wait Bottom Kill Destroy", true,
                UiText.Describe(MainSection, "Wait Bottom Kill Destroy", "底部击杀优先消失",
                    "Let the oldest (bottom) line disappear first",
                    "让最旧的一行（底部）先消失"));

            KillHasDistance = configFile.Bind(MainSection, "Kill Distance display", true,
                UiText.Describe(MainSection, "Kill Distance display", "显示距离行",
                    "Show the distance line", "显示距离行"));

            KillHasOther = configFile.Bind(MainSection, "Kill Other display", true,
                UiText.Describe(MainSection, "Kill Other display", "显示爆头行",
                    "Show the headshot marker", "显示爆头标记"));

            KillHasStreak = configFile.Bind(MainSection, "Kill Streak display", true,
                UiText.Describe(MainSection, "Kill Streak display", "显示连胜行",
                    "Show the streak line", "显示连胜行"));

            KillHasXp = configFile.Bind(MainSection, "Kill Xp display", true,
                UiText.Describe(MainSection, "Kill Xp display", "显示击杀经验",
                    "Show the experience gained by this kill", "显示单次击杀获得的经验"));

            KillHasLevel = configFile.Bind(MainSection, "Kill Level display", false,
                UiText.Describe(MainSection, "Kill Level display", "显示等级",
                    "Show the level of the killed player", "显示被击杀者的等级"));

            KillHasPart = configFile.Bind(MainSection, "Kill Part display", false,
                UiText.Describe(MainSection, "Kill Part display", "显示命中部位",
                    "Show the body part that was hit", "显示命中的部位"));

            KillHasSide = configFile.Bind(MainSection, "Kill Faction display", false,
                UiText.Describe(MainSection, "Kill Faction display", "显示阵营",
                    "Show the faction (USEC / BEAR / Scav)", "显示阵营（USEC / BEAR / 清道夫）"));

            KillHasWeapon = configFile.Bind(MainSection, "Kill Weapon display", true,
                UiText.Describe(MainSection, "Kill Weapon display", "显示武器",
                    "Show the weapon that was used", "显示使用的武器"));

            KillScavEn = configFile.Bind(MainSection, "Kill Scav Name To En", true,
                UiText.Describe(MainSection, "Kill Scav Name To En", "清道夫名字转英文",
                    "Transliterate scav names into Latin letters", "把清道夫名字转成英文字母"));

            AlwaysShowHud = configFile.Bind(MainSection, "Always Show HUD (out of raid)", false,
                UiText.Describe(MainSection, "Always Show HUD (out of raid)", "局外也显示 HUD",
                    "Keep the HUD on screen outside a raid, so the test kill stays visible",
                    "局外也保持 HUD 显示，方便查看测试击杀"));

            DebugLog = configFile.Bind(MainSection, "Debug logging", false,
                UiText.Describe(MainSection, "Debug logging", "调试日志",
                    "Writes kill line details to BepInEx\\LogOutput.log",
                    "把击杀行的详细信息写入 BepInEx\\LogOutput.log"));

            CanvasSortingOrder = configFile.Bind(MainSection, "Canvas Sorting Order", 1,
                UiText.Describe(MainSection, "Canvas Sorting Order", "画布渲染层级",
                    "Raise this if the kill feed is hidden behind the game UI",
                    "如果击杀列表被游戏界面挡住，调高这个值",
                    new AcceptableValueRange<int>(0, 32767)));

            configFile.Bind(MainSection, "Test Kill", false,
                UiText.Describe(MainSection, "Test Kill", "测试击杀",
                    "Shows a fake kill so the HUD can be checked without a raid",
                    "显示一条假击杀，不用进局就能检查 HUD",
                    null, DrawTestKill));

            Language = configFile.Bind(MainSection, "Language", UiText.Language,
                UiText.Describe(MainSection, "Language", "语言 / Language",
                    "Interface language of this panel (English / Chinese)",
                    "本设置面板的界面语言（英文 / 中文）"));

            Language.SettingChanged += OnLanguageChanged;

            KillAnchoredPosition = new Vector2Setting(configFile, PositionSection,
                "Kill Anchored Position", "击杀列表位置", new Vector2(80, -110),
                "Offset from the top-left corner", "相对屏幕左上角的偏移");

            KillSizeDelta = new Vector2Setting(configFile, PositionSection,
                "Kill Size Delta", "击杀列表尺寸", new Vector2(500, 180),
                "Size of the kill list area", "击杀列表区域的大小");

            KillLocalScale = new Vector2Setting(configFile, PositionSection,
                "Kill Local Scale", "击杀列表缩放", new Vector2(1, 1),
                "Scale of the kill list area", "击杀列表的缩放");

            KillExpAnchoredPosition = new Vector2Setting(configFile, PositionSection,
                "Exp Anchored Position", "经验累计位置", new Vector2(100, -105),
                "Offset from the top-left corner", "相对屏幕左上角的偏移");

            KillExpSizeDelta = new Vector2Setting(configFile, PositionSection,
                "Exp Size Delta", "经验累计尺寸", new Vector2(80, 30),
                "Size of the experience counter", "经验累计区域的大小");

            KillExpLocalScale = new Vector2Setting(configFile, PositionSection,
                "Exp Local Scale", "经验累计缩放", new Vector2(1, 1),
                "Scale of the experience counter", "经验累计的缩放");

            ExpWaitSpeed = configFile.Bind(SpeedSection, "Exp Wait Speed", 1f,
                UiText.Describe(SpeedSection, "Exp Wait Speed", "经验数字速度",
                    "Experience counter animation speed", "经验数字的动画速度",
                    new AcceptableValueRange<float>(0, 10)));

            KillWriteWaitTime = configFile.Bind(SpeedSection, "Kill Text Input Speed", 0.01f,
                UiText.Describe(SpeedSection, "Kill Text Input Speed", "第一段逐字速度",
                    "Single character input speed (unit s)", "逐字显示速度，每个字符的间隔（秒）",
                    new AcceptableValueRange<float>(0, 10)));

            KillWrite2WaitTime = configFile.Bind(SpeedSection, "Kill Text 2 Input Speed", 0.01f,
                UiText.Describe(SpeedSection, "Kill Text 2 Input Speed", "第二段逐字速度",
                    "Single character input speed (unit s)", "逐字显示速度，每个字符的间隔（秒）",
                    new AcceptableValueRange<float>(0, 10)));

            KillWaitTime = configFile.Bind(SpeedSection, "Kill Text 2 Wait Time", 0.5f,
                UiText.Describe(SpeedSection, "Kill Text 2 Wait Time", "两段之间等待时间",
                    "Play Text to Text2 ago Wait Time (unit s)", "第一段与第二段之间的等待时间（秒）",
                    new AcceptableValueRange<float>(0, 10)));

            KillDistance = configFile.Bind(DistanceSection, "Kill Distance display", 100f,
                UiText.Describe(DistanceSection, "Kill Distance display", "显示距离阈值（米）",
                    "When Kill distance >= How many meters display",
                    "击杀距离大于等于多少米时才显示距离",
                    new AcceptableValueRange<float>(0, 1000)));

            KillInfoStyles = configFile.Bind(FontSection, "Kill Info", FontStyles.Bold,
                UiText.Describe(FontSection, "Kill Info", "击杀信息",
                    "Font style of the kill info line", "击杀信息行的字体样式"));

            KillDistanceStyles = configFile.Bind(FontSection, "Kill Distance", FontStyles.Bold,
                UiText.Describe(FontSection, "Kill Distance", "距离",
                    "Font style of the distance", "距离的字体样式"));

            KillStreakStyles = configFile.Bind(FontSection, "Kill Streak", FontStyles.Normal,
                UiText.Describe(FontSection, "Kill Streak", "连胜",
                    "Font style of the streak line", "连胜行的字体样式"));

            KillOtherStyles = configFile.Bind(FontSection, "Kill Other", FontStyles.Normal,
                UiText.Describe(FontSection, "Kill Other", "爆头标记",
                    "Font style of the headshot marker", "爆头标记的字体样式"));

            KillXpStyles = configFile.Bind(FontSection, "Kill Xp", FontStyles.Normal,
                UiText.Describe(FontSection, "Kill Xp", "击杀经验",
                    "Font style of the experience gained", "击杀经验的字体样式"));

            ExpStyles = configFile.Bind(FontSection, "Exp", FontStyles.Normal,
                UiText.Describe(FontSection, "Exp", "经验累计",
                    "Font style of the experience counter", "经验累计的字体样式"));

            KillNameColor = new ColorSetting(configFile, ColorSection, "Name", "玩家名字",
                new Color(1f, 0.2f, 0.2f));
            KillWeaponColor = new ColorSetting(configFile, ColorSection, "Weapon", "武器",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            KillDistanceColor = new ColorSetting(configFile, ColorSection, "Distance", "距离数值",
                new Color(1f, 1f, 0f));
            KillLevelColor = new ColorSetting(configFile, ColorSection, "Level", "等级数值",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            KillStreakColor = new ColorSetting(configFile, ColorSection, "Streak", "连胜数字",
                new Color(1f, 1f, 0f));
            KillPartColor = new ColorSetting(configFile, ColorSection, "Body Part", "命中部位",
                new Color(0.6039216f, 0.827451f, 0.1372549f));
            KillMetersColor = new ColorSetting(configFile, ColorSection, "Meters", "距离单位",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            KillLvlColor = new ColorSetting(configFile, ColorSection, "Lvl", "Lv. 前缀",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            KillXpColor = new ColorSetting(configFile, ColorSection, "Xp", "击杀经验",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            KillStatsStreakColor = new ColorSetting(configFile, ColorSection, "Stats Streak", "连胜文字",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            KillOtherColor = new ColorSetting(configFile, ColorSection, "Other", "爆头文字",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            KillEnemyDownColor = new ColorSetting(configFile, ColorSection, "Enemy Down", "「敌人倒下」文字",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            KillUsecColor = new ColorSetting(configFile, ColorSection, "Kill Usec", "USEC 阵营",
                new Color(0f, 0.8f, 1f));
            KillBearColor = new ColorSetting(configFile, ColorSection, "Kill Bear", "BEAR 阵营",
                new Color(1f, 0.5f, 0f));
            KillScavColor = new ColorSetting(configFile, ColorSection, "Kill Scav", "清道夫",
                new Color(1f, 0.8f, 0f));
            KillBossColor = new ColorSetting(configFile, ColorSection, "Kill Boss", "Boss",
                new Color(1f, 0f, 0f));
            KillFollowerColor = new ColorSetting(configFile, ColorSection, "Kill Follower", "Boss 随从",
                new Color(0.8f, 0f, 0f));
            KillBracketColor = new ColorSetting(configFile, ColorSection, "Bracket", "括号",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
            ExpColor = new ColorSetting(configFile, ColorSection, "Exp", "经验累计",
                new Color(0.8901961f, 0.8901961f, 0.8392157f));
        }

        /// <summary>
        /// Reads the panel language straight out of the config file, before any entry is bound.
        /// The language decides the label of every other entry, so it cannot wait for its own
        /// <see cref="ConfigEntry{T}"/> to be created.
        /// </summary>
        private static HudLanguage ReadLanguage(ConfigFile configFile)
        {
            try
            {
                var path = configFile.ConfigFilePath;

                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return HudLanguage.English;
                }

                foreach (var line in File.ReadAllLines(path))
                {
                    var text = line.Trim();

                    if (!text.StartsWith("Language", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var separator = text.IndexOf('=');

                    if (separator < 0)
                    {
                        continue;
                    }

                    var value = text.Substring(separator + 1).Trim();

                    if (value.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase))
                    {
                        return HudLanguage.Chinese;
                    }

                    if (value.StartsWith("English", StringComparison.OrdinalIgnoreCase))
                    {
                        return HudLanguage.English;
                    }
                }
            }
            catch (Exception exception)
            {
                KillFeedbackPlugin.Logger?.LogWarning(
                    $"Could not read the panel language, staying in English: {exception.Message}");
            }

            return HudLanguage.English;
        }

        private static void OnLanguageChanged(object sender, EventArgs eventArgs)
        {
            UiText.Language = Language.Value;
            UiText.Relocalize();

            // The manager caches the list it draws, so it has to be rebuilt before the new labels
            // show up. It is rebuilt on the next frame: rebuilding it from inside its own drawing
            // pass would modify the list while it is being iterated.
            KillFeedbackPlugin.RequestPanelRefresh();

            KillFeedbackPlugin.Logger?.LogInfo($"Settings panel language set to {Language.Value}.");
        }

        /// <summary>
        /// Last-resort fallback: binds every setting to a throwaway in-memory file so the rest of
        /// the plugin still has usable values when the real config could not be created.
        /// </summary>
        public static void CreateDefaults()
        {
            try
            {
                var scratch = new ConfigFile(
                    Path.Combine(Path.GetTempPath(), "killfeedback-defaults.cfg"), false);

                Create(scratch);
            }
            catch (Exception exception)
            {
                KillFeedbackPlugin.Logger?.LogError($"Could not build fallback settings: {exception}");
            }
        }

        /// <summary>Builds a synthetic kill so the HUD can be checked outside a raid.</summary>
        public static void TestKill()
        {
            if (KillHUDModel.ShowKill == null)
            {
                KillFeedbackPlugin.Logger?.LogWarning("The kill HUD is not ready yet; no test kill shown.");

                return;
            }

            var role = Enum.GetValues(typeof(WildSpawnType));
            var side = Enum.GetValues(typeof(EPlayerSide));
            var part = Enum.GetValues(typeof(EBodyPart));

            var allowRole = (WildSpawnType)role.GetValue(UnityEngine.Random.Range(0, role.Length));

            var killModel = new KillModel
            {
                PlayerName = TestNames[UnityEngine.Random.Range(0, TestNames.Length)],
                WeaponName = TestWeapons[UnityEngine.Random.Range(0, TestWeapons.Length)],
                Part = (EBodyPart)part.GetValue(UnityEngine.Random.Range(0, part.Length)),
                Distance = UnityEngine.Random.Range(0f, 100f),
                Level = UnityEngine.Random.Range(1, 79),
                Side = (EPlayerSide)side.GetValue(UnityEngine.Random.Range(0, side.Length)),
                Exp = UnityEngine.Random.Range(100, 1001),
                Role = allowRole,
                KillCount = UnityEngine.Random.Range(0, 11),
                ScavKillExpPenalty = 0.5f,
                HasMarkOfUnknown = false,
                IsAI = true,
                IsTest = true
            };

            KillHUDModel.ShowKill?.Invoke(killModel);
        }

        private static readonly string[] TestNames =
        {
            "Player", "Me", "Camper", "Profile", "The City", "Escape from Tarkov", "C Disk",
            "Windows", "Display", "Controller", "Ram", "Cpu", "Wifi", "Phone", "Computer", "Bed",
            "Discord", "Graphics Card", "Power", "Home", "World", "Moon", "Joker", "Galaxy",
            "Game Panel HUD", "Game Program", "TypeScript", "NoBody", "AnyOne", "TheTestText", "Unity"
        };

        private static readonly string[] TestWeapons =
        {
            "F-1", "M18", "Zarya", "MPL-50", "C100", "SP-81", "AS-VAL", "MPX", "Vector .45ACP", "M4A1",
            "HK-416", "AR-15", "AK-104", "SV-98", "ST-AR-15", "MCX", "MDR", "FN40GL", "MSGL", "P90",
            "ASh-12", "Glock 18C", "USP .45 Expert", "MP5", "M1911", "MK-14", "AK-47", "AS-50", "AR-57",
            "MAG-7", "Mosin", "M1903", "Gew98", "Ro-635", "UMP 9", "M16", "G11", "UMP 45", "AK-12",
            "RPG-7V2", "AEK-971", "L115A3", "CS/LR-4", "L96A1", "M200", "R90", "725", "AA-12",
            "R8", "AWP", "DesertEagle", "GM6", "Kolibri", "SVT-38", "M40A5", "M14"
        };
    }
}
