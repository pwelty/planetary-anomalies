using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace PlanetaryAnomalies
{
    /// <summary>
    /// The mod's memory of which rules each galaxy was first seen under. One line per galaxy,
    /// kept beside the config, readable and editable by a person.
    ///
    /// This is the only thing the mod has ever written to disk about a game, and it is deliberately
    /// not in the save. Anomalies are recomputed from the seed on every load, so a save carries
    /// nothing of the mod's and removing the mod leaves it clean; that promise is worth more than
    /// portability. What recomputation cannot survive on its own is a change to the rules -- 1.0
    /// will change them -- so the one fact worth remembering is which rules a galaxy started with.
    /// Not the anomalies. Just the rulebook.
    ///
    /// The cost of keeping it here rather than in the save is that it does not travel: hand someone
    /// your save and their copy of the mod will see the galaxy for the first time under whatever
    /// rules are current for them. Documented, and accepted.
    ///
    /// A galaxy is identified by seed, star count and generation algorithm, which together are what
    /// DSP itself uses to lay out a galaxy. Two saves of the same galaxy share one pin, correctly.
    /// </summary>
    internal static class AnomalyPins
    {
        private const string FileName = "com.planetaryanomalies.dsp.pins";

        private sealed class Pin
        {
            internal int Version;
            internal string FirstSeen;
            internal string ModVersion;
        }

        private static Dictionary<string, Pin> _pins;
        private static bool _loadFailed;
        private static bool _errorLogged;

        private static string Path
        {
            get { return System.IO.Path.Combine(Paths.ConfigPath, FileName); }
        }

        private static string Key(int seed, int starCount, int algorithm)
        {
            return seed + "|" + starCount + "|" + algorithm;
        }

        internal static bool TryGet(int seed, int starCount, int algorithm, out int version)
        {
            Load();
            Pin pin;
            if (_pins.TryGetValue(Key(seed, starCount, algorithm), out pin))
            {
                version = pin.Version;
                return true;
            }

            version = 0;
            return false;
        }

        /// <summary>
        /// Records the rules in force for a galaxy. Writes only when something changed, so a load
        /// under an already-pinned galaxy touches nothing.
        /// </summary>
        internal static void Set(int seed, int starCount, int algorithm, int version)
        {
            Load();

            string key = Key(seed, starCount, algorithm);
            Pin existing;
            if (_pins.TryGetValue(key, out existing) && existing.Version == version)
            {
                return;
            }

            Pin pin = new Pin();
            pin.Version = version;
            pin.FirstSeen = existing != null ? existing.FirstSeen : DateTime.Now.ToString("yyyy-MM-dd");
            pin.ModVersion = Plugin.PluginVersion;
            _pins[key] = pin;

            Save();
        }

        internal static void Reset()
        {
            _pins = null;
            _loadFailed = false;
        }

        private static void Load()
        {
            if (_pins != null)
            {
                return;
            }

            _pins = new Dictionary<string, Pin>();

            try
            {
                if (!File.Exists(Path))
                {
                    return;
                }

                foreach (string raw in File.ReadAllLines(Path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }

                    Dictionary<string, string> fields = ParseFields(line);
                    int seed, stars, algo, version;
                    if (!TryInt(fields, "seed", out seed) || !TryInt(fields, "stars", out stars) ||
                        !TryInt(fields, "algo", out algo) || !TryInt(fields, "version", out version))
                    {
                        Plugin.Log.LogWarning("Ignoring an unreadable line in " + FileName + ": " + line);
                        continue;
                    }

                    Pin pin = new Pin();
                    pin.Version = version;
                    pin.FirstSeen = fields.ContainsKey("first_seen") ? fields["first_seen"] : "";
                    pin.ModVersion = fields.ContainsKey("mod") ? fields["mod"] : "";
                    _pins[Key(seed, stars, algo)] = pin;
                }
            }
            catch (Exception e)
            {
                // Fail loudly and empty rather than half-read. The caller's rule for "no pin" is
                // conservative -- an old game is assumed to predate pinning -- so an unreadable
                // file degrades to the safest answer instead of a scramble.
                _loadFailed = true;
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Could not read " + Path + "; treating every galaxy as unpinned this session. " + e.Message);
                }
            }
        }

        private static void Save()
        {
            if (_loadFailed)
            {
                // Never overwrite a file we could not read. Whatever is in it may be recoverable
                // by hand; a rewrite from a partial view would not be.
                return;
            }

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Planetary Anomalies -- which rules each galaxy was first seen under.");
                sb.AppendLine("# Written by the mod. One line per galaxy; safe to edit.");
                sb.AppendLine("#");
                sb.AppendLine("# A galaxy keeps rolling its anomalies under the rules recorded here even after the");
                sb.AppendLine("# mod's rules change, so an update never rewrites a galaxy you are playing.");
                sb.AppendLine("# To roll ONE galaxy under the newest rules, raise its version number and reload.");
                sb.AppendLine("# To do that for EVERY galaxy, set AnomalyRules = Latest in the config instead.");
                sb.AppendLine();

                foreach (KeyValuePair<string, Pin> entry in _pins)
                {
                    string[] parts = entry.Key.Split('|');
                    sb.Append("seed=").Append(parts[0])
                      .Append(" stars=").Append(parts[1])
                      .Append(" algo=").Append(parts[2])
                      .Append(" version=").Append(entry.Value.Version)
                      .Append(" first_seen=").Append(entry.Value.FirstSeen)
                      .Append(" mod=").Append(entry.Value.ModVersion)
                      .AppendLine();
                }

                // Write beside, then replace, so a crash mid-write cannot leave a truncated file.
                string temp = Path + ".tmp";
                File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(false));
                File.Copy(temp, Path, true);
                File.Delete(temp);
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogError("Could not write " + Path + ": " + e.Message);
                }
            }
        }

        private static Dictionary<string, string> ParseFields(string line)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>();
            foreach (string token in line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = token.IndexOf('=');
                if (eq > 0)
                {
                    fields[token.Substring(0, eq)] = token.Substring(eq + 1);
                }
            }
            return fields;
        }

        private static bool TryInt(Dictionary<string, string> fields, string name, out int value)
        {
            value = 0;
            string text;
            return fields.TryGetValue(name, out text) && int.TryParse(text, out value);
        }
    }
}
