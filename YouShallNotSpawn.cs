using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("You Shall Not Spawn", "VisEntities", "1.0.0")]
    [Description(" ")]
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

            [JsonProperty("Entity Short Prefab Names")]
            public List<string> EntityShortPrefabNames { get; set; }
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
                EntityShortPrefabNames = new List<string>
                {
                    "chicken"
                }
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
            if (_config.CleanUpExistingEntitiesOnStartup)
            {
                CoroutineUtil.StartCoroutine(Guid.NewGuid().ToString(), KillEntitiesOnStartupCoroutine());
            }
        }

        private void OnEntitySpawned(BaseNetworkable networkableEntity)
        {
            if (networkableEntity == null)
                return;

            if (_config.EntityShortPrefabNames.Contains(networkableEntity.ShortPrefabName))
            {
                NextTick(() =>
                {
                    networkableEntity.Kill();
                });
            }
        }

        #endregion Oxide Hooks

        #region Entity Cleanup On Startup

        private IEnumerator KillEntitiesOnStartupCoroutine()
        {
            foreach (BaseNetworkable networkableEntity in BaseNetworkable.serverEntities)
            {
                if (networkableEntity != null && _config.EntityShortPrefabNames.Contains(networkableEntity.ShortPrefabName))
                    networkableEntity.Kill();

                yield return null;
            }
        }

        #endregion Entity Cleanup On Startup

        #region Helper Classes

        public static class CoroutineUtil
        {
            private static readonly Dictionary<string, Coroutine> _activeCoroutines = new Dictionary<string, Coroutine>();

            public static void StartCoroutine(string coroutineName, IEnumerator coroutineFunction)
            {
                StopCoroutine(coroutineName);

                Coroutine coroutine = ServerMgr.Instance.StartCoroutine(coroutineFunction);
                _activeCoroutines[coroutineName] = coroutine;
            }

            public static void StopCoroutine(string coroutineName)
            {
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