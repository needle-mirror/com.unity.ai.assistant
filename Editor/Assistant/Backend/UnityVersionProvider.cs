using System.Collections.Generic;

namespace Unity.AI.Assistant.Editor.Backend
{
    class UnityVersionProvider : IUnityVersionProvider
    {
        static readonly string[] k_UnityVersionField;

        static UnityVersionProvider()
        {
            k_UnityVersionField = new[]
            {
                UnityDataUtils.GetProjectVersion(UnityDataUtils.VersionDetail.Revision)
            };
        }

        public IReadOnlyList<string> Version => k_UnityVersionField;
    }
}
