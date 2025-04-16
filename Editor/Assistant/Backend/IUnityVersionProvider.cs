using System.Collections.Generic;

namespace Unity.AI.Assistant.Editor.Backend
{
    interface IUnityVersionProvider
    {
        IReadOnlyList<string> Version { get; }
    }
}
