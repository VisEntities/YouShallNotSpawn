/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Facepunch;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("You Shall Not Spawn", "VisEntities", "1.1.2")]
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

            [JsonProperty("Entity Keyword Blocklist (prefab or type keywords to block from spawning or remove if spawned)")]
            public List<string> EntityKeywordBlocklist { get; set; }

            [JsonProperty("Entity Keyword Exception List (keywords to ignore even if matched in blocklist)")]
            public List<string> EntityKeywordExceptionList { get; set; }
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
                EntityKeywordBlocklist = new List<string>(),
                EntityKeywordExceptionList = new List<string>()
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
                && _config.EntityKeywordBlocklist != null
                && _config.EntityKeywordBlocklist.Count > 0)
            {
                CoroutineUtil.StartCoroutine("KillEntitiesOnStartup", KillEntitiesOnStartupCoroutine());
            }
        }

        private void OnEntitySpawned(BaseNetworkable networkableEntity)
        {
            if (networkableEntity == null)
                return;

            if (PassesKeywordFilters(networkableEntity as BaseEntity, _config.EntityKeywordBlocklist, _config.EntityKeywordExceptionList))
            {
                NextTick(() => networkableEntity.Kill());
            }
        }

        #endregion Oxide Hooks

        #region Core

        private IEnumerator KillEntitiesOnStartupCoroutine()
        {
            if (_config.EntityKeywordBlocklist == null || _config.EntityKeywordBlocklist.Count == 0)
                yield break;

            List<BaseEntity> toKill = Pool.Get<List<BaseEntity>>();

            foreach (BaseNetworkable networkable in BaseNetworkable.serverEntities)
            {
                BaseEntity entity = networkable as BaseEntity;
                if (entity != null
                    && PassesKeywordFilters(entity, _config.EntityKeywordBlocklist, _config.EntityKeywordExceptionList))
                {
                    toKill.Add(entity);
                }
            }

            for (int i = 0; i < toKill.Count; i++)
            {
                toKill[i].Kill();
                yield return null;
            }

            Pool.FreeUnmanaged(ref toKill);
        }

        private static bool PassesKeywordFilters(BaseEntity entity, IReadOnlyCollection<string> blocklist, IReadOnlyCollection<string> exceptionList)
        {
            if (entity == null)
                return false;

            if (blocklist == null)
                return false;

            if (blocklist.Count == 0)
                return false;

            string prefabName;
            if (entity.ShortPrefabName != null)
                prefabName = entity.ShortPrefabName.ToLowerInvariant();
            else
                prefabName = string.Empty;

            string typeName = entity.GetType().Name.ToLowerInvariant();

            bool matchesBlock = blocklist.Where(k => !string.IsNullOrEmpty(k))
                                         .Any(k => prefabName.Contains(k.ToLowerInvariant()) ||
                                                   typeName.Contains(k.ToLowerInvariant()));
            if (!matchesBlock)
                return false;

            if (exceptionList != null && exceptionList.Count > 0)
            {
                bool matchesException = exceptionList.Where(k => !string.IsNullOrEmpty(k))
                                                     .Any(k => prefabName.Contains(k.ToLowerInvariant()) ||
                                                               typeName.Contains(k.ToLowerInvariant()));
                if (matchesException)
                    return false;
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