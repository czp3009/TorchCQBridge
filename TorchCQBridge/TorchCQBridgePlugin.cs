using System.IO;
using System.Windows.Controls;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;

namespace TorchCQBridge
{
    public class TorchCqBridgePlugin : TorchPluginBase, IWpfPlugin
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private Persistent<TorchCqBridgeConfig> _config;
        public TorchCqBridgeConfig Config => _config.Data;

        private TorchCqBridgeControl _control;
        public UserControl GetControl() => _control ?? (_control = new TorchCqBridgeControl(this));

        public TorchCqBridgeBot Bot { get; private set; }
        private readonly object _botLock = new object();

        public void Save()
        {
            try
            {
                _config.Save();
                Log.Info("Configuration Saved.");
            }
            catch (IOException e)
            {
                Log.Warn(e, "Configuration failed to save");
            }
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            SetupConfig();
            torch.Managers.GetManager<TorchSessionManager>().SessionStateChanged += SessionChanged;
        }

        private void SetupConfig()
        {
            var configFilePath = Path.Combine(StoragePath, $"{Name}.cfg");
            _config = Persistent<TorchCqBridgeConfig>.Load(configFilePath);
        }

        private void SessionChanged(ITorchSession session, TorchSessionState newState)
        {
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (newState == TorchSessionState.Loaded)
            {
                StartBot();
                Config.SessionLoaded = true;
            }
            else if (newState == TorchSessionState.Unloading)
            {
                Config.SessionLoaded = false;
                StopBot();
            }
        }

        public void StartBot()
        {
            lock (_botLock)
            {
                if (Bot != null) return;
                Bot = new TorchCqBridgeBot(
                    Torch.CurrentSession.Managers.GetManager<IChatManagerServer>(),
                    Config.Address,
                    Config.AccessToken,
                    Config.GroupId,
                    Config.RemoveCqCode,
                    Config.IgnoreEmptyMessage,
                    Config.QqChatAuthorFormat,
                    Config.QqChatMessageFormat,
                    Config.GameChatFormat
                );
                Bot.Start();
            }
        }

        public void StopBot()
        {
            lock (_botLock)
            {
                if (Bot == null) return;
                Bot.Stop();
                Bot = null;
            }
        }

        public void RestartBot()
        {
            StopBot();
            StartBot();
        }
    }
}