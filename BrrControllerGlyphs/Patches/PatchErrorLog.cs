// ------------------------------------------------------------------------------------------------
// PatchErrorLog.cs: logs a patch's first error only, so a broken patch doesn't flood the log.
// ------------------------------------------------------------------------------------------------

using System;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Patches
{
    /// <summary>
    /// Logs the first error a patch hits and ignores the rest. Patches catch their own errors so a
    /// failure leaves the game's behaviour in place; many run every frame, hence once only.
    /// </summary>
    internal sealed class PatchErrorLog
    {
        /// <summary>What failed and what's left in place, e.g. "X failed, leaving key text unchanged".</summary>
        private readonly string m_message;

        /// <summary>Set after the first error.</summary>
        private bool m_logged;

        /// <summary>
        /// Creates the log.
        /// </summary>
        /// <param name="message">What failed and what's left in place; the exception is appended.</param>
        internal PatchErrorLog(string message)
        {
            m_message = message;
        }

        /// <summary>
        /// Logs the error if it's the first one.
        /// </summary>
        /// <param name="ex">The error the patch caught.</param>
        internal void LogOnce(Exception ex)
        {
            if (m_logged)
            {
                return;
            }

            m_logged = true;
            Jotunn.Logger.LogError($"{m_message}: {ex}");
        }
    }
}
