/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("You Shall Not Spawn", "VisEntities", "1.1.1")]
    [Description("Prevents certain entities from spawning.")]
    public class YouShallNotSpawn : RustPlugin
    {
        #region Fields

        private static YouShallNotSpawn _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Clean Up Existing Entities On Startup")]
            public bool CleanUpExistingEntitiesOnStartup { get; set; }

            [JsonProperty("Entity Keyword Whitelist (prefab or type substring)")]
            public List<string> EntityKeywordWhitelist { get; set; }

            [JsonProperty("Entity Keyword Blacklist (prefab or type substring)")]
            public List<string> EntityKeywordBlacklist { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                CleanUpExistingEntitiesOnStartup = false,
                EntityKeywordWhitelist = new List<string>(),
                EntityKeywordBlacklist = new List<string>()
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
        }

        private void Unload()
        {
            CoroutineUtil.StopAllCoroutines();
            _config = null;
            _plugin = null;
        }

        private void OnServerInitialized(bool isStartup)
        {
            if (_config.CleanUpExistingEntitiesOnStartup
                && _config.EntityKeywordWhitelist != null
                && _config.EntityKeywordWhitelist.Count > 0)
            {
                CoroutineUtil.StartCoroutine(
                    "KillEntitiesOnStartup",
                    KillEntitiesOnStartupCoroutine());
            }
        }

        private void OnEntitySpawned(BaseNetworkable networkableEntity)
        {
            if (networkableEntity == null)
                return;

            if (PassesKeywordFilters(networkableEntity as BaseEntity, _config.EntityKeywordWhitelist, _config.EntityKeywordBlacklist))
            {
                NextTick(() => networkableEntity.Kill());
            }
        }

        #endregion Oxide Hooks

        #region Core

        private IEnumerator KillEntitiesOnStartupCoroutine()
        {
            foreach (BaseNetworkable networkableEntity in BaseNetworkable.serverEntities)
            {
                BaseEntity entity = networkableEntity as BaseEntity;
                if (entity != null && PassesKeywordFilters(entity, _config.EntityKeywordWhitelist, _config.EntityKeywordBlacklist))
                    entity.Kill();

                yield return null;
            }
        }

        private static bool PassesKeywordFilters(BaseEntity entity, IReadOnlyCollection<string> whitelist, IReadOnlyCollection<string> blacklist)
        {
            if (entity == null)
                return false;

            string prefabName = string.Empty;
            if (entity.ShortPrefabName != null)
                prefabName = entity.ShortPrefabName.ToLowerInvariant();

            string typeName = entity.GetType().Name.ToLowerInvariant();

            if (whitelist != null && whitelist.Count > 0)
            {
                bool anyMatch = false;
                foreach (string keyword in whitelist)
                {
                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    string lowerKeyword = keyword.ToLowerInvariant();
                    if (prefabName.Contains(lowerKeyword) || typeName.Contains(lowerKeyword))
                    {
                        anyMatch = true;
                        break;
                    }
                }

                if (!anyMatch)
                    return false;
            }

            if (blacklist != null && blacklist.Count > 0)
            {
                foreach (string keyword in blacklist)
                {
                    if (string.IsNullOrEmpty(keyword))
                        continue;

                    string lowerKeyword = keyword.ToLowerInvariant();
                    if (prefabName.Contains(lowerKeyword) || typeName.Contains(lowerKeyword))
                        return false;
                }
            }

            return true;
        }

        #endregion Core

        #region Helper Classes

        public static class CoroutineUtil
        {
            private static readonly Dictionary<string, Coroutine> _activeCoroutines = new Dictionary<string, Coroutine>();

            public static Coroutine StartCoroutine(string baseCoroutineName, IEnumerator coroutineFunction, string uniqueSuffix = null)
            {
                string coroutineName;

                if (uniqueSuffix != null)
                    coroutineName = baseCoroutineName + "_" + uniqueSuffix;
                else
                    coroutineName = baseCoroutineName;

                StopCoroutine(coroutineName);

                Coroutine coroutine = ServerMgr.Instance.StartCoroutine(coroutineFunction);
                _activeCoroutines[coroutineName] = coroutine;
                return coroutine;
            }

            public static void StopCoroutine(string baseCoroutineName, string uniqueSuffix = null)
            {
                string coroutineName;

                if (uniqueSuffix != null)
                    coroutineName = baseCoroutineName + "_" + uniqueSuffix;
                else
                    coroutineName = baseCoroutineName;

                if (_activeCoroutines.TryGetValue(coroutineName, out Coroutine coroutine))
                {
                    if (coroutine != null)
                        ServerMgr.Instance.StopCoroutine(coroutine);

                    _activeCoroutines.Remove(coroutineName);
                }
            }

            public static void StopAllCoroutines()
            {
                foreach (string coroutineName in _activeCoroutines.Keys.ToArray())
                {
                    StopCoroutine(coroutineName);
                }
            }
        }

        #endregion Helper Classes
    }
}