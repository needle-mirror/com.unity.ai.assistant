namespace Unity.AI.Assistant.Editor.Data
{
    struct PointCostRequestData
    {
        public PointCostRequestData(string prompt, int contextItemCount = 0)
        {
            Prompt = prompt;
            ContextItemCount = contextItemCount;
        }

        public readonly int ContextItemCount;
        public readonly string Prompt;
    }
}
