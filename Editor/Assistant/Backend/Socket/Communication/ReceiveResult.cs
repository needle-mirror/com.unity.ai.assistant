using System;
using Unity.AI.Assistant.Editor.Backend.Socket.Protocol.Models;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Communication
{
    class ReceiveResult
    {
        public bool IsDeserializedSuccessfully;
        public IModel DeserializedData;
        public string RawData;
        public Exception Exception;
    }
}
