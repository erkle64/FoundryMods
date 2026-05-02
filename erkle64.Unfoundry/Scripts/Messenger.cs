using System;
using System.Collections.Generic;
using TinyJSON;

namespace Unfoundry
{
    public static class Messenger
    {
        private delegate void MessageHandler(string message);
        private static Dictionary<string, Dictionary<string, MessageHandler>> _messageHandlers = new Dictionary<string, Dictionary<string, MessageHandler>>();

        public static void RegisterListener<T>(string messageName, string receiverName, System.Action<T> handler)
        {
            if (!_messageHandlers.TryGetValue(messageName, out var handlers))
            {
                _messageHandlers[messageName] = handlers = new Dictionary<string, MessageHandler>();
            }

            if (handlers.ContainsKey(receiverName))
            {
                UnityEngine.Debug.LogWarning($"Receiver '{receiverName}' already has a handler for message '{messageName}'");
                return;
            }

            handlers.Add(receiverName, message => {
                try
                {
                    JSON.Load(message).Make(out T decodedMessage);
                    handler(decodedMessage);
                }
                catch(System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"Failed to decode message '{messageName}': {ex}");
                }
            });
        }

        public static void DeregisterListener(string messageName, string receiverName)
        {
            if (_messageHandlers.TryGetValue(messageName, out var handlers))
            {
                handlers.Remove(receiverName);
            }
        }

        public static void Send<T>(string messageName, T message)
        {
            if (_messageHandlers.TryGetValue(messageName, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.Value(JSON.Dump(message, EncodeOptions.NoTypeHints | EncodeOptions.DropNulls));
                }
            }
        }

        public static void RegisterListener(string v, object applyDestroyTerrainRequest)
        {
            throw new NotImplementedException();
        }
    }
}
