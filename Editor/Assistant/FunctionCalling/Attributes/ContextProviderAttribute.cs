using System;
using System.ComponentModel;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    /// <summary>
    ///     Marks a static method returning some context for Muse Chat as a <see cref="string"/>.
    ///     Each method parameter must have a <see cref="ParameterAttribute"/> attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false), EditorBrowsable(EditorBrowsableState.Never)]
    public class ContextProviderAttribute : Attribute
    {
        /// <summary>
        ///     A description of the piece of context returned by the method.
        /// </summary>
        public readonly string Description;

        /// <summary>
        ///     Marks a static method returning some context for Muse Chat as a <see cref="string"/>.
        /// </summary>
        /// <param name="description">
        ///     A description of the piece of context returned by the method.
        /// </param>
        /// <exception cref="ArgumentException">
        ///     Thrown if description is null or empty. A description must be provided for the LLM to understand how to
        ///     use the tool.
        /// </exception>
        public ContextProviderAttribute(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Cannot be empty", nameof(description));

            Description = description;
        }
    }
}
