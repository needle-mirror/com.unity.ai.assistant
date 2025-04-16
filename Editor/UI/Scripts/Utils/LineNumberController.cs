using System.Text;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    class LineNumberController
    {
        Label k_SourceLabel;
        Label k_TargetLabel;

        public LineNumberController(Label source, Label target)
        {
            k_SourceLabel = source;
            k_TargetLabel = target;

            source.RegisterValueChangedCallback(_ => RefreshDisplay());
            source.RegisterCallback<GeometryChangedEvent>(_ => RefreshDisplay());
        }

        public void RefreshDisplay()
        {
            var lines = k_SourceLabel.text.Split('\n');
            var builder = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
                builder.AppendLine((i + 1).ToString());

            k_TargetLabel.text = builder.ToString();
        }
    }
}
