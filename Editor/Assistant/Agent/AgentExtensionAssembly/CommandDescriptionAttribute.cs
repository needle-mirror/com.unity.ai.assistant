using System;

namespace Unity.AI.Assistant.Agent.Dynamic.Extension.Editor
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
#if CODE_LIBRARY_INSTALLED
    public
#else
    internal
#endif
    class CommandDescriptionAttribute : Attribute
    {
        public string Description { get; }

        public CommandDescriptionAttribute(string description)
        {
            Description = description;
        }
    }
}
