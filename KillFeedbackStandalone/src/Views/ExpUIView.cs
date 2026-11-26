using TMPro;
using UnityEngine;
using KillFeedback.Core;
using KillFeedback.Models;
using KillFeedback.Utils;

namespace KillFeedback.Views
{
    /// <summary>
    /// The running experience counter shown next to the kill list.
    /// <para>
    /// Re-implementation of the original <c>GamePanelHUDKill.Views.ExpUIView</c> for IL2CPP; see
    /// <see cref="KillUIView"/> for why the prefab script had to be replaced. Like the kill rows,
    /// the counter no longer depends on the prefab's animator: the animator is disabled and the
    /// number is simply kept up to date.
    /// </para>
    /// <para>
    /// The original reset the counter from a "Clear" clip through an animation event. That event
    /// can never fire here because the animator is off, so the counter used to keep its last value
    /// on screen forever - a number left over from a test kill would still be sitting there at the
    /// start of the next raid. The lifetime is now driven by a timer instead: the number stays for
    /// a few seconds after the last kill, then fades out and resets itself.
    /// </para>
    /// </summary>
    public class ExpUIView : KillFeedbackBehaviour
    {
        /// <summary>How long the total stays on screen after the last kill.</summary>
        private const float HoldSeconds = 5f;

        /// <summary>How long the fade out takes.</summary>
        private const float FadeOutSeconds = 0.4f;

        /// <summary>How long the fade in takes.</summary>
        private const float FadeInSeconds = 0.15f;

        /// <summary>Set when the counter should take its queued value.</summary>
        public bool active;

        /// <summary>Experience already shown.</summary>
        public int xp;

        /// <summary>Experience queued while the counter was busy.</summary>
        public int waitXp;

        /// <summary>Colour of the counter.</summary>
        public Color xpColor;

        /// <summary>Animation speed of the counter.</summary>
        public float xpWaitSpeed;

        /// <summary>Font style of the counter.</summary>
        public FontStyles xpStyles;

        private TMP_Text _xpValue;
        private string _lastText;
        private bool _initialized;
        private int _lastFrame = -1;

        private float _alpha;
        private float _holdUntil;

        /// <summary>The counter updates instantly, so it never reports itself as animating.</summary>
        public bool IsAnim => false;

        private void Awake()
        {
            var animator = GetComponent<Animator>();

            if (animator != null)
            {
                animator.enabled = false;
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

        /// <summary>Kept so the plugin's own fan-out also works; ticks at most once per frame.</summary>
        public override void CustomUpdate()
        {
            Tick();
        }

        /// <summary>Resolves the prefab's TMP text and applies the current settings.</summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            var texts = GetComponentsInChildren<TMP_Text>(true);

            for (var i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].gameObject.name == "Xp")
                {
                    _xpValue = texts[i];
                    break;
                }
            }

            FontHelper.Apply(_xpValue, FontHelper.DefaultFont);

            Apply();
        }

        private void Tick()
        {
            if (_lastFrame == Time.frameCount)
            {
                return;
            }

            _lastFrame = Time.frameCount;

            Initialize();

            if (_xpValue != null && _xpValue.font == null)
            {
                FontHelper.Apply(_xpValue, FontHelper.DefaultFont);
            }

            if (active)
            {
                active = false;

                xp += waitXp;
                waitXp = 0;
            }

            UpdateAlpha();

            Apply();
        }

        /// <summary>
        /// Fades the counter in when it has something to show, and fades it out - then zeroes it -
        /// once the hold time has passed. This is the replacement for the "Clear" animation event.
        /// </summary>
        private void UpdateAlpha()
        {
            var deltaTime = Time.unscaledDeltaTime;

            if (xp <= 0)
            {
                _alpha = 0f;

                return;
            }

            if (Time.unscaledTime < _holdUntil)
            {
                _alpha = FadeInSeconds <= 0f
                    ? 1f
                    : Mathf.MoveTowards(_alpha, 1f, deltaTime / FadeInSeconds);

                return;
            }

            _alpha = FadeOutSeconds <= 0f
                ? 0f
                : Mathf.MoveTowards(_alpha, 0f, deltaTime / FadeOutSeconds);

            if (_alpha > 0f)
            {
                return;
            }

            // Fully faded out: forget the total so the next raid starts from nothing.
            xp = 0;
            waitXp = 0;
        }

        private void Apply()
        {
            if (_xpValue == null)
            {
                return;
            }

            _xpValue.fontStyle = xpStyles;
            _xpValue.color = xpColor;
            _xpValue.alpha = _alpha;

            var text = xp > 0 ? xp.ToString() : string.Empty;

            if (text != _lastText)
            {
                _lastText = text;
                _xpValue.text = text;
            }
        }

        /// <summary>Adds experience to the counter and restarts its hold time.</summary>
        public void XpUp(int up, int lastXp)
        {
            xp += up + lastXp;

            active = true;
            _holdUntil = Time.unscaledTime + HoldSeconds;
        }

        /// <summary>Ends the counter's animation; the counter has none, so nothing to do.</summary>
        public void XpComplete()
        {
        }

        /// <summary>Clears the counter immediately, without waiting for the hold time.</summary>
        public void ClearXp()
        {
            xp = 0;
            waitXp = 0;
            _alpha = 0f;
        }
    }
}
