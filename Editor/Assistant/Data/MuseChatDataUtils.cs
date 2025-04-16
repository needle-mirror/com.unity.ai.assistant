using System;
using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.Editor.ApplicationModels;

namespace Unity.AI.Assistant.Editor.Data
{
    internal static class MuseChatDataUtils
    {
        public static AssistantInspiration ToInternal(this Inspiration apiData)
        {
            var result = new AssistantInspiration
            {
                Id = new AssistantInspirationId(apiData.Id),
                Description = apiData.Description,
                Value = apiData.Value
            };

            var commandString = apiData.Mode.ToString().ToLower();
            if (ChatCommands.TryGetCommandHandler(commandString, out _))
            {
                result.Command = commandString;
            }
            else
            {
                throw new InvalidOperationException();
            }

            return result;
        }

        public static Inspiration ToExternal(this AssistantInspiration data)
        {
            Inspiration apiData = new Inspiration(Inspiration.ModeEnum.Ask, data.Value)
            {
                Id = data.Id.IsValid ? data.Id.Value : default,
                Description = data.Description
            };
            if (Enum.TryParse<Inspiration.ModeEnum>(data.Command, true, out var result))
            {
                apiData.Mode = result;
            }
            return apiData;
        }
    }
}
