using System;
using System.Collections.Generic;
using EFT;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using KillFeedback.Core;
using KillFeedback.Models;
using KillFeedback.Utils;

namespace KillFeedback.Views
{
    /// <summary>
    /// Owns the kill list: turns a <see cref="KillModel"/> into a stack of animated lines and
    /// keeps the running experience counter in step with them.
    /// <para>
    /// Re-implementation of the original <c>GamePanelHUDKill.Views.KillHUDView</c>. Two things
    /// had to change for IL2CPP: the prefab's own script was dropped while loading (so the
    /// serialized references to the list and the counter are gone and are looked up by name
    /// instead), and the visibility rules that used to come from <c>GamePanelHUDCore</c> are now
    /// decided here.
    /// </para>
    /// </summary>
    public class KillHUDView : KillFeedbackBehaviour
    {
        private int _currentHasInfo;
        private int _waitInfo;
        private int _lastXp;
        private KillUIView _previous;

        private Transform _killsRoot;
        private ExpUIView _expUIView;
        private RectTransform _killsRect;
        private RectTransform _expRect;
        private CanvasGroup _killGroup;
        private CanvasGroup _expGroup;

        private bool _initialized;
        private bool _ready;
        private bool _fontApplied;
        private int _lastFrame = -1;
        private int _linesLogged;

        private readonly List<KillUIView> _rows = new List<KillUIView>();
        private int _heartbeats;
        private float _nextHeartbeat;

        private const float LineHeight = 22f;
        private const float XpWidth = 80f;

        /// <summary>Unscaled time until which a test kill keeps the HUD visible out of a raid.</summary>
        private float _testVisibleUntil;

        /// <summary>
        /// Resolves the children of the prefab and hooks the view up to the plugin. Safe to call
        /// more than once; the plugin calls it right after adding the component.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            var kills = transform.Find("Kills");
            var exp = transform.Find("Exp");

            if (kills == null || exp == null)
            {
                KillFeedbackPlugin.Logger?.LogError(
                    "The kill HUD prefab is missing its 'Kills' or 'Exp' child; the HUD is disabled.");

                return;
            }

            _killsRoot = kills;

            _killsRect = kills.GetComponent<RectTransform>();
            _expRect = exp.GetComponent<RectTransform>();
            _killGroup = kills.GetComponent<CanvasGroup>();
            _expGroup = exp.GetComponent<CanvasGroup>();

            _expUIView = exp.GetComponent<ExpUIView>();

            if (_expUIView == null)
            {
                _expUIView = exp.gameObject.AddComponent<ExpUIView>();
            }

            if (_killGroup == null || _expGroup == null || _killsRect == null || _expRect == null)
            {
                KillFeedbackPlugin.Logger?.LogError(
                    "The kill HUD prefab is missing a CanvasGroup or RectTransform; the HUD is disabled.");

                return;
            }

            FontHelper.ReplaceAllFont(gameObject);

            KillUIView.HasWaitInfoMinus += WaitInfoMinus;
            KillUIView.HasInfoMinus += InfoMinus;

            KillHUDModel.ShowKill = ShowKill;

            KillFeedbackRuntime.UpdateManager.Register(this);

            _ready = true;

            KillFeedbackPlugin.Logger?.LogInfo(
                $"KillHUDView ready (scene '{gameObject.scene.name}', root '{transform.root.name}')");
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            KillFeedbackPlugin.Logger?.LogWarning(
                $"KillHUDView destroyed (scene '{gameObject.scene.name}'); the HUD will be rebuilt.");

            KillUIView.HasWaitInfoMinus -= WaitInfoMinus;
            KillUIView.HasInfoMinus -= InfoMinus;

            if (KillHUDModel.ShowKill == ShowKill)
            {
                KillHUDModel.ShowKill = null;
            }

            KillFeedbackRuntime.UpdateManager.Remove(this);
        }

        /// <summary>Unity's own tick; the most reliable way to drive an injected component.</summary>
        private void Update()
        {
            Tick();
        }

        /// <summary>Kept so the plugin's own fan-out also works; ticks at most once per frame.</summary>
        public override void CustomUpdate()
        {
            Tick();
        }

        private void Tick()
        {
            if (_lastFrame == Time.frameCount)
            {
                return;
            }

            _lastFrame = Time.frameCount;

            if (!_ready)
            {
                Initialize();

                if (!_ready)
                {
                    return;
                }
            }

            // The bundle's texts point at a font asset from a bundle this plugin does not ship.
            // TMP may not have loaded its settings yet when the HUD is built, so this keeps
            // retrying until the game's font is available.
            if (!_fontApplied && FontHelper.DefaultFont != null)
            {
                FontHelper.ReplaceAllFont(gameObject);

                _fontApplied = true;
            }

            // The original asked GamePanelHUDCore whether the battle UI was up; standalone, the
            // local player existing is the same signal.
            var show = KillFeedbackRuntime.HasPlayer ||
                       SettingsModel.AlwaysShowHud.Value ||
                       Time.unscaledTime < _testVisibleUntil;

            _killGroup.alpha = show && SettingsModel.KillHUDSw.Value ? 1f : 0f;
            _expGroup.alpha = show && SettingsModel.ExpHUDSw.Value ? 1f : 0f;

            _killsRect.anchoredPosition = SettingsModel.KillAnchoredPosition.Value;
            _killsRect.sizeDelta = SettingsModel.KillSizeDelta.Value;
            _killsRect.localScale = SettingsModel.KillLocalScale.Value;

            _expRect.anchoredPosition = SettingsModel.KillExpAnchoredPosition.Value;
            _expRect.sizeDelta = SettingsModel.KillExpSizeDelta.Value;
            _expRect.localScale = SettingsModel.KillExpLocalScale.Value;

            _expUIView.xpColor = SettingsModel.ExpColor.Value;
            _expUIView.xpStyles = SettingsModel.ExpStyles.Value;
            _expUIView.xpWaitSpeed = SettingsModel.ExpWaitSpeed.Value;

            TickRows();
        }

        /// <summary>
        /// Advances every live row. The rows are ordinary components added at runtime, and those
        /// do not reliably receive Unity's Update message on this client, so the list ticks them
        /// instead. <see cref="KillUIView.TickFromHud"/> is idempotent within a frame.
        /// </summary>
        private void TickRows()
        {
            for (var i = _rows.Count - 1; i >= 0; i--)
            {
                var row = _rows[i];

                if (row == null)
                {
                    _rows.RemoveAt(i);

                    continue;
                }

                row.TickFromHud();
            }

            if (SettingsModel.DebugLog.Value && _heartbeats < 3 && Time.unscaledTime >= _nextHeartbeat)
            {
                _heartbeats++;
                _nextHeartbeat = Time.unscaledTime + 3f;

                KillFeedbackPlugin.Logger?.LogInfo(
                    $"Kill HUD tick: rows={_rows.Count}, children={_killsRoot.childCount}, " +
                    $"killsRect={_killsRect.rect.size}, alpha={_killGroup.alpha:F2}, " +
                    $"show={Time.unscaledTime < _testVisibleUntil}");
            }
        }

        /// <summary>Builds the lines for one kill and pushes them into the list.</summary>
        private void ShowKill(KillModel killModel)
        {
            if (!_ready)
            {
                return;
            }

            // A test kill clicked from the Configuration Manager should be visible right away,
            // without having to enable the "Always Show HUD" switch first.
            if (killModel.IsTest && !KillFeedbackRuntime.HasPlayer)
            {
                _testVisibleUntil = Time.unscaledTime + 8f;
            }

            var baseExp = ExperienceHelper.GetBaseExp(killModel.Exp, killModel.Side, killModel.Role,
                killModel.ScavKillExpPenalty, killModel.HasMarkOfUnknown, killModel.IsAI);

            var allExp = baseExp;
            var allInfo = 1;
            var lines = new List<KillUIView>();

            if (SettingsModel.KillHasDistance.Value && killModel.Distance >= SettingsModel.KillDistance.Value)
            {
                lines.Add(AddDistanceInfo(killModel));
            }

            if (SettingsModel.KillHasStreak.Value && killModel.KillCount > 1)
            {
                var streakXp = ExperienceHelper.GetStreakExp(baseExp, killModel.KillCount);

                lines.Add(AddStreakInfo(killModel, streakXp));

                allExp += streakXp;
                allInfo++;
            }

            if (SettingsModel.KillHasOther.Value && killModel.Part == EBodyPart.Head)
            {
                var headXp = ExperienceHelper.GetHeadExp(baseExp, killModel.Side);

                lines.Add(AddOtherInfo(LocalizedHelper.Localized("StatsHeadshot"), headXp));

                allExp += headXp;
                allInfo++;
            }

            lines.Add(AddKillInfo(killModel, baseExp));

            var hasKillsCount = lines.Count;

            _currentHasInfo += hasKillsCount;
            _waitInfo += hasKillsCount;

            var count = hasKillsCount - 1;

            if (!SettingsModel.KillWaitBottom.Value)
            {
                if (_previous != null)
                {
                    _previous.after = lines[lines.Count - 1];

                    count -= 1;
                }

                lines[0].canDestroy = true;
            }
            else
            {
                if (_previous != null)
                {
                    _previous.after = lines[0];

                    lines[lines.Count - 1].after = _previous;
                }
                else
                {
                    lines[0].canDestroy = true;
                }
            }

            _previous = lines[lines.Count - 1];

            if (hasKillsCount > 1)
            {
                for (var i = 0; i < count; i++)
                {
                    lines[i].after = lines[i + 1];
                }
            }

            var isAnim = _expUIView.IsAnim;

            if (_currentHasInfo != 0 && !isAnim)
            {
                _expUIView.XpUp(allExp, _lastXp);
            }
            else if (isAnim || allInfo > 1)
            {
                _expUIView.XpUp(allExp, 0);
            }
            else
            {
                _lastXp = allExp;
            }
        }

        /// <summary>
        /// Builds one line of the kill list.
        /// <para>
        /// The row is created from scratch rather than instantiated from the bundle's "Kill"
        /// prefab. That prefab carries a MonoBehaviour whose script was stripped while loading,
        /// and a component added to such an object never received Unity's Update message here, so
        /// the rows stayed at their serialized alpha of 0 forever. Two TMP texts and a
        /// CanvasGroup are all a row needs; stacking comes from the VerticalLayoutGroup on the
        /// "Kills" container.
        /// </para>
        /// </summary>
        private KillUIView CreateLine()
        {
            var row = new GameObject("KillLine", Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasGroup>());

            row.layer = _killsRoot.gameObject.layer;
            row.transform.SetParent(_killsRoot, false);

            var rect = (RectTransform)row.transform;

            // A zero-width row would squeeze the text to nothing when the layout group does not
            // control the child width, so the container's width is copied over.
            var width = _killsRect != null && _killsRect.rect.width > 1f ? _killsRect.rect.width : 500f;

            rect.sizeDelta = new Vector2(width, LineHeight);

            var layout = row.AddComponent<LayoutElement>();

            layout.minHeight = LineHeight;
            layout.preferredHeight = LineHeight;
            layout.flexibleHeight = 0f;

            var view = row.AddComponent<KillUIView>();

            // Main text: fills the row, leaving room for the experience value on the right.
            var textRect = CreateText(row.transform, "Text", TextAlignmentOptions.Left, 24f);
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(0f, 0f);
            textRect.offsetMax = new Vector2(-XpWidth, 0f);

            // Experience value: fixed width strip on the right edge.
            var xpRect = CreateText(row.transform, "Xp", TextAlignmentOptions.Left, 26f);
            xpRect.anchorMin = new Vector2(1f, 0f);
            xpRect.anchorMax = new Vector2(1f, 1f);
            xpRect.pivot = new Vector2(1f, 0.5f);
            xpRect.sizeDelta = new Vector2(XpWidth, 0f);
            xpRect.anchoredPosition = Vector2.zero;

            row.transform.SetAsFirstSibling();

            _rows.Add(view);

            if (SettingsModel.DebugLog.Value && _linesLogged < 3)
            {
                _linesLogged++;

                KillFeedbackPlugin.Logger?.LogInfo(
                    $"Kill line created: view={view != null}, active={row.activeInHierarchy}, " +
                    $"parent={row.transform.parent.name}, siblings={_killsRoot.childCount}, " +
                    $"font={(FontHelper.DefaultFont != null ? FontHelper.DefaultFont.name : "null")}");
            }

            return view;
        }

        private static RectTransform CreateText(Transform parent, string name, TextAlignmentOptions alignment,
            float size)
        {
            var go = new GameObject(name, Il2CppType.Of<RectTransform>());

            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();

            text.font = FontHelper.DefaultFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.richText = true;
            text.raycastTarget = false;
            text.text = string.Empty;

            return text.rectTransform;
        }

        private KillUIView AddKillInfo(KillModel killModel, int exp)
        {
            var killUI = CreateLine();

            var isScav = killModel.Side == EPlayerSide.Savage;

            var playerName = isScav && SettingsModel.KillScavEn.Value
                ? LocalizedHelper.Transliterate(killModel.PlayerName)
                : killModel.PlayerName;

            // The patch already resolves the weapon through the game's localization helpers; a raw
            // key is only passed through here when an older caller supplied one.
            var weaponName = string.IsNullOrEmpty(killModel.WeaponName)
                ? null
                : LocalizedHelper.IsResolved(killModel.WeaponName)
                    ? killModel.WeaponName
                    : LocalizedHelper.Localized(killModel.WeaponName);

            string sideKey;

            if (isScav && killModel.Role.IsBossOrFollower())
            {
                sideKey = killModel.Role.GetScavRoleKey();
            }
            else
            {
                sideKey = killModel.Side.ToString();
            }

            var sideName = LocalizedHelper.Localized(sideKey);

            var nameColor = SettingsModel.KillNameColor.Value.ColorToHtml();
            var partColor = SettingsModel.KillPartColor.Value.ColorToHtml();
            var weaponColor = SettingsModel.KillWeaponColor.Value.ColorToHtml();
            var lvlColor = SettingsModel.KillLvlColor.Value.ColorToHtml();
            var levelColor = SettingsModel.KillLevelColor.Value.ColorToHtml();
            var bracketColor = SettingsModel.KillBracketColor.Value.ColorToHtml();
            var enemyDownColor = SettingsModel.KillEnemyDownColor.Value.ColorToHtml();

            string sideColor;

            if (isScav && killModel.Role.IsBoss())
            {
                sideColor = SettingsModel.KillBossColor.Value.ColorToHtml();
            }
            else if (isScav && killModel.Role.IsFollower())
            {
                sideColor = SettingsModel.KillFollowerColor.Value.ColorToHtml();
            }
            else
            {
                switch (killModel.Side)
                {
                    case EPlayerSide.Usec:
                        sideColor = SettingsModel.KillUsecColor.Value.ColorToHtml();
                        break;
                    case EPlayerSide.Bear:
                        sideColor = SettingsModel.KillBearColor.Value.ColorToHtml();
                        break;
                    case EPlayerSide.Savage:
                        sideColor = SettingsModel.KillScavColor.Value.ColorToHtml();
                        break;
                    default:
                        sideColor = Color.black.ColorToHtml();
                        break;
                }
            }

            var part = SettingsModel.KillHasPart.Value
                ? $"<color={bracketColor}>[</color><color={partColor}>{LocalizedHelper.Localized(killModel.Part.ToString())}</color><color={bracketColor}>]</color>"
                : string.Empty;

            var side = SettingsModel.KillHasSide.Value
                ? $"<color={bracketColor}>[</color><color={sideColor}>{sideName}</color><color={bracketColor}>]</color>"
                : string.Empty;

            var weapon = SettingsModel.KillHasWeapon.Value && !string.IsNullOrEmpty(weaponName)
                ? $"<color={bracketColor}>[</color><color={weaponColor}>{weaponName}</color><color={bracketColor}>]</color>"
                : string.Empty;

            var level = !isScav && SettingsModel.KillHasLevel.Value
                ? $"<color={bracketColor}>[</color><color={lvlColor}>{LocalizedHelper.Localized("LVLKILLLIST")}.</color><color={levelColor}>{killModel.Level}</color><color={bracketColor}>]</color>"
                : string.Empty;

            killUI.xp = exp;
            killUI.text = $"<color={enemyDownColor}>{LocalizedHelper.Localized("ENEMYDOWN")}</color>";
            killUI.text2 = $"{weapon}{side} <color={nameColor}>{playerName}</color> {level}{part}";
            killUI.textFontStyles = SettingsModel.KillInfoStyles.Value;
            killUI.isKillInfo = true;
            killUI.hasXp = SettingsModel.KillHasXp.Value && ExperienceHelper.HasSettings;
            killUI.xpColor = SettingsModel.KillXpColor.Value;
            killUI.active = true;

            return killUI;
        }

        private KillUIView AddDistanceInfo(KillModel killModel)
        {
            var killUI = CreateLine();

            var meters = LocalizedHelper.Localized("meters");
            var metersColor = SettingsModel.KillMetersColor.Value.ColorToHtml();
            var distanceColor = SettingsModel.KillDistanceColor.Value.ColorToHtml();
            var distanceText = killModel.Distance.ToString("F2");

            killUI.text = $"<color={distanceColor}>{distanceText}</color> <color={metersColor}>{meters}</color>";
            killUI.textFontStyles = SettingsModel.KillDistanceStyles.Value;
            killUI.active = true;

            return killUI;
        }

        private KillUIView AddStreakInfo(KillModel killModel, int exp)
        {
            var killUI = CreateLine();

            var statsStreakColor = SettingsModel.KillStatsStreakColor.Value.ColorToHtml();
            var streakColor = SettingsModel.KillStreakColor.Value.ColorToHtml();
            var bracketColor = SettingsModel.KillBracketColor.Value.ColorToHtml();

            killUI.text =
                $"<color={statsStreakColor}>{LocalizedHelper.Localized("StatsStreak")}</color> <color={bracketColor}>[</color><color={streakColor}>{killModel.KillCount}</color><color={bracketColor}>]</color>";
            killUI.textFontStyles = SettingsModel.KillStreakStyles.Value;
            killUI.xp = exp;
            killUI.hasXp = SettingsModel.KillHasXp.Value && ExperienceHelper.HasSettings;
            killUI.xpColor = SettingsModel.KillXpColor.Value;
            killUI.active = true;

            return killUI;
        }

        private KillUIView AddOtherInfo(string text, int exp)
        {
            var killUI = CreateLine();

            var otherColor = SettingsModel.KillOtherColor.Value.ColorToHtml();

            killUI.text = $"<color={otherColor}>{text}</color>";
            killUI.textFontStyles = SettingsModel.KillOtherStyles.Value;
            killUI.xp = exp;
            killUI.hasXp = SettingsModel.KillHasXp.Value && ExperienceHelper.HasSettings;
            killUI.xpColor = SettingsModel.KillXpColor.Value;
            killUI.active = true;

            return killUI;
        }

        private void InfoMinus()
        {
            _currentHasInfo -= 1;
        }

        private void WaitInfoMinus()
        {
            _waitInfo -= 1;

            if (_waitInfo != 0)
            {
                return;
            }

            _expUIView?.XpComplete();
        }
    }
}
