// ------------------------------------------------------------------------------------------------
// MappingCommands.cs: the cgf_reloadmapping and cgf_keylog console commands (F9).
// ------------------------------------------------------------------------------------------------

using System;
using Jotunn.Entities;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// "cgf_reloadmapping": reads the mapping file again, for when the file watcher can't run or
    /// missed a save.
    /// </summary>
    internal sealed class ReloadMappingCommand : ConsoleCommand
    {
        /// <summary>Reloads the mapping and returns the status line to print.</summary>
        private readonly Func<string> m_reload;

        /// <summary>
        /// Creates the command.
        /// </summary>
        /// <param name="reload">Reloads the mapping and returns the new status line.</param>
        internal ReloadMappingCommand(Func<string> reload)
        {
            m_reload = reload ?? throw new ArgumentNullException(nameof(reload));
        }

        /// <summary>Command name typed in the console.</summary>
        public override string Name => "cgf_reloadmapping";

        /// <summary>Help text shown by the console's help command.</summary>
        public override string Help => "ControllerGlyphFixer: read the key mapping file again";

        /// <summary>
        /// Reloads and prints the result.
        /// </summary>
        /// <param name="args">Unused.</param>
        public override void Run(string[] args)
        {
            string status = m_reload();
            if (Console.instance)
            {
                Console.instance.Print(status);
            }
        }
    }

    /// <summary>
    /// "cgf_keylog": switches the key finder (<see cref="KeyLog"/>) on or off.
    /// </summary>
    internal sealed class KeyLogCommand : ConsoleCommand
    {
        /// <summary>The key finder.</summary>
        private readonly KeyLog m_keyLog;

        /// <summary>
        /// Creates the command.
        /// </summary>
        /// <param name="keyLog">The key finder.</param>
        internal KeyLogCommand(KeyLog keyLog)
        {
            m_keyLog = keyLog ?? throw new ArgumentNullException(nameof(keyLog));
        }

        /// <summary>Command name typed in the console.</summary>
        public override string Name => "cgf_keylog";

        /// <summary>Help text shown by the console's help command.</summary>
        public override string Help =>
            "ControllerGlyphFixer: show each keyboard key the game receives (top left), to find the keys your Steam buttons send";

        /// <summary>
        /// Toggles the key finder and says which way it went.
        /// </summary>
        /// <param name="args">Unused.</param>
        public override void Run(string[] args)
        {
            string state = m_keyLog.Toggle()
                ? "Key finder on: close the console and press your controller buttons; run cgf_keylog again to stop"
                : "Key finder off";
            if (Console.instance)
            {
                Console.instance.Print(state);
            }
        }
    }
}
