using Newtonsoft.Json;

namespace TorchCQBridge.Model
{
    public class SendGroupMessage
    {
        [JsonProperty("action")] public string Action = "send_group_msg";
        [JsonProperty("params")] public Params Param;

        public SendGroupMessage(long groupId, string message)
        {
            Param = new Params(groupId, message);
        }

        public class Params
        {
            [JsonProperty("group_id")] public long GroupId;
            [JsonProperty("message")] public string Message;
            [JsonProperty("auto_escape")] public bool AutoEscape = true;

            public Params(long groupId, string message)
            {
                GroupId = groupId;
                Message = message;
            }
        }
    }
}