// ------------------------------------------------------------------------------------------------
// ReloadMappingCommand.cs: the brrcg_reloadmapping console command (F9).
// ------------------------------------------------------------------------------------------------

using System;
using BrrControllerGlyphs.Core;
using Jotunn.Entities;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs
{
    /// <summary>
    /// "brrcg_reloadmapping": reads the mapping file again, for when the file watcher can't run or
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

        /// <summary>Command name typed in the console; also named in log hints.</summary>
        internal const string CommandName = NamePrefix.Value + "reloadmapping";

        /// <summary>Command name typed in the console.</summary>
        public override string Name => CommandName;

        /// <summary>Help text shown by the console's help command.</summary>
        public override string Help => "BrrControllerGlyphs: read the key mapping file again";

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
}
