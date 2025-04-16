using Unity.AI.Assistant.Editor.ApplicationModels;

namespace Unity.AI.Assistant.Editor.Data
{
    struct FeedbackData
    {
        public FeedbackData(Sentiment sentiment, string details)
        {
            Sentiment = sentiment;
            Details = details;
        }

        public readonly Sentiment Sentiment;
        public readonly string Details;
    }
}
