// ------------------------------------------------------------------------------------------------
// MessageRoom.cs: moves Valheim's top-left messages down below prefixes under AzuEPI's bar (F5).
// ------------------------------------------------------------------------------------------------

using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Keeps Valheim's top-left messages ("You are exposed") clear of the group prefixes drawn under
    /// AzuEPI's quick-slot bar, which sits right above them. Moves the message text's parent (the
    /// object AzuEPI itself lowers by 75 units in MessageHud.Awake) and remembers only its own
    /// change, so restoring undoes exactly that and leaves AzuEPI's move in place.
    /// </summary>
    // Asked for in game 2026-11-08: only while the quick-slot row has prefixes
    internal sealed class MessageRoom
    {
        /// <summary>Space kept between the lowest prefix backing and the messages, in UI units.</summary>
        private const float Gap = 2f;

        /// <summary>The object moved: the top-left message text's parent.</summary>
        private RectTransform m_target;

        /// <summary>How far <see cref="m_target"/> is moved down now, in its own parent's units.</summary>
        private float m_applied;

        /// <summary>How far the messages are moved down now, in their parent's units (for the self-test).</summary>
        internal float Applied => m_applied;

        /// <summary>
        /// Moves the messages down by how far prefixes reach below the quick-slot bar's slots, or back
        /// up when they reach nowhere (no prefixes).
        /// </summary>
        /// <param name="bar">The quick-slot bar the prefixes are drawn in.</param>
        /// <param name="depthBelow">How far the prefixes reach below the slots, in the bar's units; 0 for none.</param>
        internal void MakeRoom(Transform bar, float depthBelow)
        {
            MessageHud messages = MessageHud.instance;
            RectTransform target = messages && messages.m_messageText
                ? messages.m_messageText.transform.parent as RectTransform
                : null;
            if (target != m_target)
            {
                // A new MessageHud (each world load) starts where Valheim and AzuEPI put it
                Restore();
                m_target = target;
            }

            if (!m_target || !bar)
            {
                return;
            }

            float wanted = 0f;
            if (depthBelow > 0f)
            {
                // Through world space: the bar and the messages may sit under differently scaled parents
                Transform space = m_target.parent ? m_target.parent : m_target;
                wanted = (depthBelow + Gap) * bar.lossyScale.y / space.lossyScale.y;
            }

            Shift(wanted);
        }

        /// <summary>
        /// Puts the messages back where Valheim and AzuEPI had them.
        /// </summary>
        internal void Restore()
        {
            Shift(0f);
        }

        /// <summary>
        /// Moves the target so it's <paramref name="wanted"/> below where it was before any of our moves.
        /// </summary>
        /// <param name="wanted">Total downward move, in the target's parent's units.</param>
        private void Shift(float wanted)
        {
            if (!m_target)
            {
                m_applied = 0f;
                return;
            }

            if (Mathf.Approximately(wanted, m_applied))
            {
                return;
            }

            m_target.anchoredPosition += new Vector2(0f, m_applied - wanted);
            m_applied = wanted;
        }
    }
}
