using System.Xml.Serialization;
using Torch;

namespace TorchCQBridge
{
    public class TorchCqBridgeConfig : ViewModel
    {
        private bool _sessionLoaded = false;
        private string _address = "ws://localhost:6700";
        private string _accessToken = "";
        private long _groupId;
        private bool _removeCqCode = true;
        private bool _ignoreEmptyMessage = true;
        private string _qqChatAuthorFormat = "[QQ]{name}";
        private string _qqChatMessageFormat = "{message}";
        private string _gameChatFormat = "{name}: {message}";

        [XmlIgnore]
        public bool SessionLoaded
        {
            get => _sessionLoaded;
            set => SetValue(ref _sessionLoaded, value);
        }

        public string Address
        {
            get => _address;
            set => SetValue(ref _address, value);
        }

        public string AccessToken
        {
            get => _accessToken;
            set => SetValue(ref _accessToken, value);
        }

        public long GroupId
        {
            get => _groupId;
            set => SetValue(ref _groupId, value);
        }

        public bool RemoveCqCode
        {
            get => _removeCqCode;
            set => SetValue(ref _removeCqCode, value);
        }

        public bool IgnoreEmptyMessage
        {
            get => _ignoreEmptyMessage;
            set => SetValue(ref _ignoreEmptyMessage, value);
        }

        public string QqChatAuthorFormat
        {
            get => _qqChatAuthorFormat;
            set => SetValue(ref _qqChatAuthorFormat, value);
        }

        public string QqChatMessageFormat
        {
            get => _qqChatMessageFormat;
            set => SetValue(ref _qqChatMessageFormat, value);
        }

        public string GameChatFormat
        {
            get => _gameChatFormat;
            set => SetValue(ref _gameChatFormat, value);
        }
    }
}