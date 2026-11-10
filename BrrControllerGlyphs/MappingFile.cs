// ------------------------------------------------------------------------------------------------
// MappingFile.cs: loads and reloads the YAML key mapping, watches it, and shows its status (F9).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs
{
    /// <summary>
    /// Owns the mapping file: writes the template on first run, parses it, and writes what happened
    /// to the read-only Mapping settings. A failed reload keeps the last good mapping, so a typo
    /// doesn't wipe the labels mid-session. Watches the file so saves can be picked up without a
    /// restart; the watcher only raises a flag, and the plugin reloads on the main thread.
    /// </summary>
    internal sealed class MappingFile : IDisposable
    {
        /// <summary>Full path of the mapping file.</summary>
        private readonly string m_path;

        /// <summary>Config "Mapping status": loaded key count or the parse error.</summary>
        private readonly ConfigEntry<string> m_status;

        /// <summary>Config "Keys missing from mapping", one line (the .cfg and Jotunn's Mod Settings).</summary>
        private readonly ConfigEntry<string> m_missingKeys;

        /// <summary>Watches the file for saves; null until <see cref="Watch"/> or if it couldn't start.</summary>
        private FileSystemWatcher m_watcher;

        /// <summary>Set by the watcher's thread when the file changed; cleared by <see cref="TakeChange"/>.</summary>
        private volatile bool m_changed;

        /// <summary>Key count of the mapping in use, or null if none loaded yet (for error messages).</summary>
        private int? m_lastGoodCount;

        /// <summary>
        /// Creates the file handler and shows the path in the settings.
        /// </summary>
        /// <param name="path">Full path of the mapping file.</param>
        /// <param name="file">Config "Mapping file" (read-only), set to the path.</param>
        /// <param name="status">Config "Mapping status" (read-only).</param>
        /// <param name="missingKeys">Config "Keys missing from mapping" (read-only).</param>
        internal MappingFile(string path, ConfigEntry<string> file, ConfigEntry<string> status,
            ConfigEntry<string> missingKeys)
        {
            m_path = path ?? throw new ArgumentNullException(nameof(path));
            m_status = status ?? throw new ArgumentNullException(nameof(status));
            m_missingKeys = missingKeys ?? throw new ArgumentNullException(nameof(missingKeys));
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            SetQuietly(file, path);
            SetQuietly(m_missingKeys, "Checked when the main menu first opens");
            MissingKeysTree = m_missingKeys.Value;
        }

        /// <summary>The current status line ("Mapping status").</summary>
        internal string Status => m_status.Value;

        /// <summary>The current "Keys missing from mapping" text (one line).</summary>
        internal string MissingKeysSummary => m_missingKeys.Value;

        /// <summary>The same as a tree, for ConfigurationManager's monospace drawer.</summary>
        internal string MissingKeysTree { get; private set; }

        /// <summary>Whether saves to the file are being watched.</summary>
        internal bool IsWatching => m_watcher != null;

        /// <summary>
        /// Loads the mapping at startup, writing the commented template first if the file doesn't exist.
        /// </summary>
        /// <returns>The parsed map, or an empty map if the file can't be read or parsed.</returns>
        internal KeyMap LoadFirst()
        {
            try
            {
                if (!File.Exists(m_path))
                {
                    File.WriteAllText(m_path, KeyMapTemplate.Yaml);
                    Jotunn.Logger.LogInfo($"Wrote an empty key mapping template to {m_path}; edit it to add your buttons");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Jotunn.Logger.LogError($"Couldn't write the key mapping template to {m_path}: {ex.Message}");
            }

            return TryLoad(out KeyMap map) ? map : KeyMap.Empty;
        }

        /// <summary>
        /// Parses the file again.
        /// </summary>
        /// <param name="map">The new mapping, or null when the file is invalid (keep the current one).</param>
        /// <returns>True if the file parsed.</returns>
        internal bool TryLoad(out KeyMap map)
        {
            try
            {
                map = KeyMap.Parse(File.ReadAllText(m_path));
                m_lastGoodCount = map.Count;
                SetQuietly(m_status, MappingStatus.Loaded(map.Count));
                Jotunn.Logger.LogInfo($"Loaded {map.Count} key mappings from {m_path}");
                return true;
            }
            catch (Exception ex) when (ex is KeyMapException || ex is IOException || ex is UnauthorizedAccessException)
            {
                map = null;
                SetQuietly(m_status, MappingStatus.Failed(ex.Message, m_lastGoodCount));
                Jotunn.Logger.LogError($"Couldn't load key mapping {m_path}: {m_status.Value}");
                return false;
            }
        }

        /// <summary>
        /// Shows the keys the mapping doesn't cover: one line in the setting, a tree for the drawer.
        /// </summary>
        /// <param name="missing">From <see cref="MissingKeys.Find"/>.</param>
        internal void ShowMissingKeys(IReadOnlyList<MissingKey> missing)
        {
            SetQuietly(m_missingKeys, MissingKeys.Summary(missing));
            MissingKeysTree = MissingKeys.Tree(missing);
        }

        /// <summary>
        /// Starts watching the file for saves. Editors often write a file in several steps, so the
        /// plugin waits a moment after <see cref="TakeChange"/> before reloading.
        /// </summary>
        internal void Watch()
        {
            try
            {
                m_watcher = new FileSystemWatcher(Path.GetDirectoryName(m_path), Path.GetFileName(m_path))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                };
                m_watcher.Changed += OnFileEvent;
                m_watcher.Created += OnFileEvent;
                m_watcher.Renamed += OnFileEvent;
                m_watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is PlatformNotSupportedException)
            {
                Jotunn.Logger.LogWarning($"Can't watch {m_path} for changes ({ex.Message}); use {ReloadMappingCommand.CommandName} after editing it");
                Dispose();
            }
        }

        /// <summary>
        /// Returns whether the file changed since the last call, and clears the flag.
        /// </summary>
        /// <returns>True once per batch of changes.</returns>
        internal bool TakeChange()
        {
            if (!m_changed)
            {
                return false;
            }

            m_changed = false;
            return true;
        }

        /// <summary>
        /// Sets a read-only value without saving the .cfg: the mod rewrites these every start, and a
        /// save could otherwise run (and fail on a locked file) from inside a frame. The .cfg picks up
        /// the latest values whenever something else saves it.
        /// </summary>
        /// <param name="entry">A read-only setting.</param>
        /// <param name="value">Its new value.</param>
        private static void SetQuietly(ConfigEntry<string> entry, string value)
        {
            ConfigFile config = entry.ConfigFile;
            bool saveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                entry.Value = value;
            }
            finally
            {
                config.SaveOnConfigSet = saveOnSet;
            }
        }

        /// <summary>Stops watching the file.</summary>
        public void Dispose()
        {
            if (m_watcher == null)
            {
                return;
            }

            m_watcher.EnableRaisingEvents = false;
            m_watcher.Changed -= OnFileEvent;
            m_watcher.Created -= OnFileEvent;
            m_watcher.Renamed -= OnFileEvent;
            m_watcher.Dispose();
            m_watcher = null;
        }

        /// <summary>
        /// Watcher callback, on a thread-pool thread: only raises the flag the main thread polls.
        /// </summary>
        /// <param name="sender">The watcher.</param>
        /// <param name="args">Which file changed.</param>
        private void OnFileEvent(object sender, FileSystemEventArgs args)
        {
            m_changed = true;
        }
    }
}
