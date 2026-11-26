using System;
using TMPro;
using UnityEngine;
using KillFeedback.Core;
using KillFeedback.Models;
using KillFeedback.Utils;

namespace KillFeedback.Views
{
    /// <summary>
    /// One kill line: the text plus its experience value.
    /// <para>
    /// The prefab's own script was stripped while loading (a prefab cannot reference a script from
    /// an assembly the game does not know), so this component is added at runtime and the TMP
    /// texts are found by the names they have in the prefab.
    /// </para>
    /// <para>
    /// Visibility and lifetime are driven from code rather than from the prefab's animator: the
    /// state machine animates the row's CanvasGroup, and an injected component does not reliably
    /// take part in it, which left rows permanently transparent. The animator is therefore
    /// disabled and the row fades in, holds, fades out and removes itself here. Stacking still
    /// comes from the prefab's VerticalLayoutGroup.
    /// </para>
    /// </summary>
    public class KillUIView : KillFeedbackBehaviour
    {
        /// <summary>Set when the line should start playing.</summary>
        public bool active;

        /// <summary>True for the main kill line (the one with the victim's name).</summary>
        public bool isKillInfo;

        /// <summary>True when this line should show an experience value.</summary>
        public bool hasXp;

        /// <summary>Set when the line has reached the end of its life and may be destroyed.</summary>
        public bool canDestroy;

        /// <summary>Text shown first ("Enemy Down").</summary>
        public string text;

        /// <summary>Text shown after the first one ("[weapon] [faction] name [level] [part]").</summary>
        public string text2;

        /// <summary>Experience gained by this line.</summary>
        public int xp;

        /// <summary>Colour of the experience value.</summary>
        public Color xpColor;

        /// <summary>Font style of the main text.</summary>
        public FontStyles textFontStyles;

        /// <summary>The line that follows this one; it is destroyed when this one is.</summary>
        public KillUIView after;

        private const float FadeInTime = 0.08f;
        private const float FadeOutTime = 0.25f;
        private const float HoldAfterTyping = 3f;
        private const float MaxLifetime = 12f;
        private const float MinVisible = 2.5f;

        private const int PhaseIdle = 0;
        private const int PhaseRevealFirst = 1;
        private const int PhaseWaitSecond = 2;
        private const int PhaseRevealSecond = 3;
        private const int PhaseDone = 4;

        private TMP_Text _textValue;
        private TMP_Text _xpValue;
        private CanvasGroup _group;
        private bool _initialized;
        private bool _destroyed;
        private bool _fadingOut;
        private bool _destroyRequested;
        private bool _holdSet;
        private float _holdUntil;
        private int _phase;
        private int _charCount;
        private int _charIndex;
        private float _nextChar;
        private float _spawnTime;
        private int _lastFrame = -1;

        private static int _diagnosedCount;
        private bool _firstTickLogged;

        /// <summary>Raised when a line finishes playing, so the experience counter can settle.</summary>
        public static event Action HasWaitInfoMinus;

        /// <summary>Raised when a line is destroyed.</summary>
        public static event Action HasInfoMinus;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _spawnTime = Time.unscaledTime;

            // The row's fade is driven here; a half-bound state machine would only fight it.
            var animator = GetComponent<Animator>();

            if (animator != null)
            {
                animator.enabled = false;
            }

            if (_group != null)
            {
                _group.alpha = 0f;
            }
        }

        private void Start()
        {
            Initialize();
        }

        /// <summary>Unity's own tick; the most reliable way to drive an injected component.</summary>
        private void Update()
        {
            Tick();
        }

        /// <summary>
        /// Driven by <see cref="KillHUDView"/> as well: a component added at runtime does not
        /// always receive Unity's Update message, so the list owns the rows and ticks them.
        /// </summary>
        public void TickFromHud()
        {
            Tick();
        }

        /// <summary>Kept so the plugin's own fan-out also works; ticks at most once per frame.</summary>
        public override void CustomUpdate()
        {
            Tick();
        }

        /// <summary>Resolves the prefab's TMP texts and applies the values set before the tick.</summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _textValue = FindText("Text");
            _xpValue = FindText("Xp");

            var font = FontHelper.DefaultFont;

            if (font != null)
            {
                FontHelper.Apply(_textValue, font);
                FontHelper.Apply(_xpValue, font);
            }

            if (_textValue != null)
            {
                _textValue.fontStyle = textFontStyles;
            }

            if (_xpValue != null)
            {
                _xpValue.fontStyle = SettingsModel.KillXpStyles.Value;
                _xpValue.color = xpColor;
                _xpValue.text = hasXp ? xp.ToString() : string.Empty;
            }
        }

        private TMP_Text FindText(string name)
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);

            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].gameObject.name == name)
                {
                    return texts[i];
                }
            }

            return null;
        }

        private void Tick()
        {
            if (_destroyed || _lastFrame == Time.frameCount)
            {
                return;
            }

            _lastFrame = Time.frameCount;

            Initialize();

            // The font may only become available a few frames after the row is created.
            if (_textValue != null && _textValue.font == null)
            {
                var font = FontHelper.DefaultFont;

                if (font != null)
                {
                    FontHelper.Apply(_textValue, font);
                    FontHelper.Apply(_xpValue, font);
                }
            }

            if (active)
            {
                active = false;

                StartTyping();
            }

            AdvanceTyping();

            if (canDestroy)
            {
                canDestroy = false;

                // The original prefab let the animator decide when to leave its "wait" state, so
                // setting this flag at spawn time was harmless. Here it only means "you may go
                // once you have been read", otherwise the very first row is destroyed before it
                // has finished fading in.
                _destroyRequested = true;
            }

            if (!_fadingOut && _holdSet &&
                ((_destroyRequested && Time.unscaledTime - _spawnTime >= MinVisible) ||
                 Time.unscaledTime >= _holdUntil))
            {
                BeginFadeOut();
            }

            // A row must never linger if the typing state machine cannot finish.
            if (!_fadingOut && Time.unscaledTime - _spawnTime > MaxLifetime)
            {
                BeginFadeOut();
            }

            if (_group != null)
            {
                if (_fadingOut)
                {
                    _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / FadeOutTime);

                    if (_group.alpha <= 0.001f)
                    {
                        DestroyLine();
                    }
                }
                else
                {
                    _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime / FadeInTime);
                }
            }

            DiagnoseOnce();
        }

        private void BeginFadeOut()
        {
            _fadingOut = true;
        }

        /// <summary>
        /// Starts the typing animation. It is a small state machine driven from
        /// <see cref="Tick"/> instead of a coroutine, because <c>StartCoroutine</c> on a component
        /// injected at runtime is not guaranteed to run here.
        /// </summary>
        private void StartTyping()
        {
            if (_textValue == null)
            {
                FinishTyping();

                return;
            }

            _textValue.text = text;
            _textValue.maxVisibleCharacters = int.MaxValue;
            _textValue.ForceMeshUpdate();

            _charCount = _textValue.textInfo.characterCount;
            _charIndex = _charCount;
            _textValue.firstVisibleCharacter = _charIndex;

            _phase = PhaseRevealFirst;
            _nextChar = Time.unscaledTime;
        }

        private void AdvanceTyping()
        {
            if (_phase == PhaseIdle || _phase == PhaseDone || Time.unscaledTime < _nextChar)
            {
                return;
            }

            switch (_phase)
            {
                case PhaseRevealFirst:
                {
                    _charIndex--;

                    if (_charIndex <= 0)
                    {
                        _textValue.firstVisibleCharacter = 0;

                        if (isKillInfo)
                        {
                            _phase = PhaseWaitSecond;
                            _nextChar = Time.unscaledTime + SettingsModel.KillWaitTime.Value;
                        }
                        else
                        {
                            _phase = PhaseDone;
                            FinishTyping();
                        }

                        return;
                    }

                    _textValue.firstVisibleCharacter = _charIndex;
                    _nextChar = Time.unscaledTime + SettingsModel.KillWriteWaitTime.Value;

                    return;
                }

                case PhaseWaitSecond:
                {
                    _textValue.text = text2;
                    _textValue.ForceMeshUpdate();

                    _charCount = _textValue.textInfo.characterCount;
                    _textValue.maxVisibleCharacters = 0;
                    _charIndex = 0;

                    _phase = PhaseRevealSecond;
                    _nextChar = Time.unscaledTime;

                    return;
                }

                case PhaseRevealSecond:
                {
                    _charIndex++;

                    if (_charIndex >= _charCount)
                    {
                        _textValue.maxVisibleCharacters = _charCount;

                        _phase = PhaseDone;
                        FinishTyping();

                        return;
                    }

                    _textValue.maxVisibleCharacters = _charIndex;
                    _nextChar = Time.unscaledTime + SettingsModel.KillWrite2WaitTime.Value;

                    return;
                }
            }
        }

        private void FinishTyping()
        {
            if (_holdSet)
            {
                return;
            }

            HasWaitInfoMinus?.Invoke();

            _holdSet = true;
            _holdUntil = Time.unscaledTime + HoldAfterTyping;
        }

        /// <summary>
        /// Animation event on the "Destroy" clip. Kept because the clip in the bundle calls it by
        /// name; the row no longer depends on it.
        /// </summary>
        private void Destroy()
        {
            DestroyLine();
        }

        private void DestroyLine()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;

            // The next line in the stack may only be destroyed once this one is gone.
            if (after != null)
            {
                after.canDestroy = true;
            }

            HasInfoMinus?.Invoke();

            UnityEngine.Object.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;

            if (after != null)
            {
                after.canDestroy = true;
            }

            HasInfoMinus?.Invoke();
        }

        /// <summary>
        /// Prints what a freshly spawned row actually looks like, so "nothing on screen" can be
        /// told apart from a missing font, an unticked component or a row outside the viewport.
        /// </summary>
        private void DiagnoseOnce()
        {
            if (!SettingsModel.DebugLog.Value)
            {
                return;
            }

            if (!_firstTickLogged)
            {
                _firstTickLogged = true;

                LogState("first tick");

                return;
            }

            if (_diagnosedCount >= 3 || Time.unscaledTime - _spawnTime < 0.5f)
            {
                return;
            }

            _diagnosedCount++;

            LogState("0.5s");
        }

        private void LogState(string stage)
        {
            try
            {
                var screen = RectTransformUtility.WorldToScreenPoint(null, transform.position);
                var alpha = _group != null ? _group.alpha : -1f;
                var font = _textValue != null && _textValue.font != null ? _textValue.font.name : "null";
                var parentRect = transform.parent as RectTransform;

                KillFeedbackPlugin.Logger?.LogInfo(
                    $"Kill line diag [{stage}]: alpha={alpha:F2}, text='{_textValue?.text}' (len " +
                    $"{(_textValue != null ? _textValue.text.Length : -1)}, font={font}), " +
                    $"xp='{_xpValue?.text}', screen=({screen.x:F0},{screen.y:F0}) of " +
                    $"{Screen.width}x{Screen.height}, rowRect={((RectTransform)transform).rect.size}, " +
                    $"parentRect={(parentRect != null ? parentRect.rect.size.ToString() : "none")}, " +
                    $"active={gameObject.activeInHierarchy}");
            }
            catch (Exception exception)
            {
                KillFeedbackPlugin.Logger?.LogWarning($"Kill line diagnostics failed: {exception.Message}");
            }
        }
    }
}
