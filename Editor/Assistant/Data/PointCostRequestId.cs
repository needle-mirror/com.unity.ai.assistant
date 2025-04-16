using System;

namespace Unity.AI.Assistant.Editor.Data
{
    struct PointCostRequestId
    {
        private static int k_NextId = 1;

        internal static readonly PointCostRequestId Invalid = new PointCostRequestId(AssistantConversationId.Invalid, 0);

        // -------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------
        public PointCostRequestId(AssistantConversationId conversationId, int id)
        {
            ConversationId = conversationId;
            Value = id;
        }

        // -------------------------------------------------------------------
        // Public
        // -------------------------------------------------------------------
        public readonly AssistantConversationId ConversationId;
        public readonly int Value;

        public static PointCostRequestId GetNext(AssistantConversationId conversationId)
        {
            return new PointCostRequestId(conversationId, k_NextId++);
        }

        public static bool operator ==(PointCostRequestId value1, PointCostRequestId value2)
        {
            return value1.Equals(value2);
        }

        public static bool operator !=(PointCostRequestId value1, PointCostRequestId value2)
        {
            return !(value1 == value2);
        }

        public override bool Equals(object obj)
        {
            return obj is PointCostRequestId other && Equals(other);
        }

        public bool Equals(PointCostRequestId other)
        {
            return ConversationId == other.ConversationId && Value == other.Value ;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ConversationId, Value);
        }

        public override string ToString()
        {
            return $"{ConversationId}.{Value}";
        }
    }
}
