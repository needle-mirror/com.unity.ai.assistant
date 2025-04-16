namespace Unity.AI.Assistant.Editor.Backend
{
    interface IOrganizationIdProvider
    {
        bool GetOrganizationId(out string organizationId);
    }
}
