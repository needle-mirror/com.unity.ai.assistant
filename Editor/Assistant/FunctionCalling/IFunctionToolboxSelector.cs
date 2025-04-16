using System.Collections.Generic;
using Unity.AI.Assistant.Editor.ApplicationModels;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    internal interface IFunctionToolboxSelector
    {
        FunctionDefinition FunctionDefinition { get; }
        string Name { get; }
        List<ParameterDefinition> Parameters { get; }
    }
}
