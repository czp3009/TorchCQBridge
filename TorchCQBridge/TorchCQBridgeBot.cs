using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using Sandbox.Game.Gui;
using Torch.API.Managers;
using TorchCQBridge.Model;
using VRage.Game;

namespace TorchCQBridge
{
    public class TorchCqBridgeBot
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly IChatManagerServer _chatManagerServer;
        private readonly string _address;
        private readonly string _accessToken;
        private readonly long _groupId;
        private readonly bool _removeCqCode;
        private readonly bool _ignoreEmptyMessage;
        private readonly string _qqChatAuthorFormat;
        private readonly string _qqChatMessageFormat;
        private readonly string _gameChatFormat;

        private readonly ClientWebSocket _clientWebSocket = new ClientWebSocket();

        public TorchCqBridgeBot(
            IChatManagerServer chatManagerServer,
            string address, string accessToken, long groupId,
            bool removeCqCode, bool ignoreEmptyMessage,
            string qqChatAuthorFormat, string qqChatMessageFormat,
            string gameChatFormat)
        {
            _chatManagerServer = chatManagerServer;
            _address = address;
            _accessToken = accessToken;
            _groupId = groupId;
            _removeCqCode = removeCqCode;
            _ignoreEmptyMessage = ignoreEmptyMessage;
            _qqChatAuthorFormat = qqChatAuthorFormat;
            _qqChatMessageFormat = qqChatMessageFormat;
            _gameChatFormat = gameChatFormat;
        }

        private async Task ReceiveWebsocketMessageTask(WebSocket clientWebSocket)
        {
            Log.Info("Receiving message");
            var buffer = new ArraySegment<byte>(new byte[1024]);
            while (clientWebSocket.State == WebSocketState.Open)
            {
                using (var memoryStream = new MemoryStream())
                {
                    try
                    {
                        //message may in chunks
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await clientWebSocket.ReceiveAsync(buffer, CancellationToken.None);
                            // ReSharper disable once AssignNullToNotNullAttribute
                            memoryStream.Write(buffer.Array, buffer.Offset, result.Count);
                        } while (!result.EndOfMessage);

                        //websocket close message
                        if (result.MessageType != WebSocketMessageType.Text) continue;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        break;
                    }

                    memoryStream.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
                    {
                        JToken jToken;
                        try
                        {
                            jToken = JToken.ReadFrom(new JsonTextReader(reader));
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                            continue;
                        }

                        //Log.Info(jToken.ToString());
                        var postType = jToken["post_type"];
                        if (postType == null)
                        {
                            if ((string) jToken["status"] != "ok")
                            {
                                // ReSharper disable once StringLiteralTypo
                                Log.Error($"API error with code {(string) jToken["retcode"]}");
                            }
                        }
                        else if ((string) postType == "message" &&
                                 (string) jToken["message_type"] == "group" &&
                                 (long) jToken["group_id"] == _groupId)
                        {
                            var sender = jToken["sender"];
                            var name = (string) sender["card"];
                            if (name.Length == 0) name = (string) sender["nickname"];
                            if (name.Length == 0) name = (string) sender["user_id"];
                            var message = (string) jToken["message"];
                            ProcessQqMessage(name, message);
                        }
                    }
                }
            }
        }

        private void ProcessQqMessage(string name, string message)
        {
            if (_removeCqCode)
            {
                message = Regex.Replace(message, @"\[CQ:.*?]", "");
            }

            if (_ignoreEmptyMessage && message.Trim().Length == 0) return;
            var qqChatAuthor = _qqChatAuthorFormat.Replace("{name}", name);
            var qqChatMessage = _qqChatMessageFormat.Replace("{message}", message);
            _chatManagerServer.SendMessageAsOther(qqChatAuthor, qqChatMessage, MyFontEnum.White);
        }

        private void ReceiveGameMessageTask(TorchChatMessage msg, ref bool consumed)
        {
            if (consumed || msg.Channel != ChatChannel.Global) return;
            var gameChat = _gameChatFormat
                .Replace("{name}", msg.Author ?? msg.AuthorSteamId?.ToString() ?? "Unknown")
                .Replace("{message}", msg.Message);
            ProcessGameMessage(gameChat);
        }

        private void ProcessGameMessage(string message)
        {
            if (_clientWebSocket.State != WebSocketState.Open) return;
            var request = JsonConvert.SerializeObject(new SendGroupMessage(_groupId, message));
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(request));
            _clientWebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public Task Start()
        {
            lock (_clientWebSocket)
            {
                if (_clientWebSocket.State == WebSocketState.Open)
                {
                    Log.Warn("Bot already connected");
                    return Task.CompletedTask;
                }

                Log.Info("Starting bot");
                var uri = new Uri(_accessToken.Length == 0 ? _address : $"{_address}?access_token={_accessToken}");
                return _clientWebSocket.ConnectAsync(uri, CancellationToken.None)
                    .ContinueWith(task =>
                    {
                        if (task.IsFaulted || task.IsCanceled)
                        {
                            Log.Error("Open websocket failed");
                            Log.Error(task.Exception?.GetBaseException());
                            return;
                        }

                        Log.Info("Websocket opened");
                        _chatManagerServer.MessageProcessing += ReceiveGameMessageTask;
                        Task.Run(async () =>
                        {
                            try
                            {
                                await ReceiveWebsocketMessageTask(_clientWebSocket);
                            }
                            finally
                            {
                                _chatManagerServer.MessageProcessing -= ReceiveGameMessageTask;
                            }
                        });
                    });
            }
        }

        public Task Stop()
        {
            lock (_clientWebSocket)
            {
                if (_clientWebSocket.State == WebSocketState.Closed || _clientWebSocket.State == WebSocketState.Aborted)
                {
                    Log.Warn("Bot already stopped");
                    return Task.CompletedTask;
                }

                Log.Info("Stopping bot");
                return _clientWebSocket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, "User close", CancellationToken.None)
                    .ContinueWith(task =>
                    {
                        if (task.IsFaulted || task.IsCanceled)
                        {
                            Log.Error("Close websocket with exception");
                            Log.Error(task.Exception?.GetBaseException());
                        }
                        else
                        {
                            Log.Info("Websocket closed");
                        }
                    });
            }
        }
    }
}