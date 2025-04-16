using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

namespace Unity.AI.Assistant.Editor.ApplicationModels
{
    class FunctionCall
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NonGeneratedPartials.FunctionCall" /> class.
        /// </summary>
        [JsonConstructor]
        public FunctionCall() { }
        public FunctionCall(string function, List<string> parameters)
        {
            Function = function;
            Parameters = parameters;
        }

        /// <summary>
        /// Gets or Sets Function
        /// </summary>
        [DataMember(Name = "function", IsRequired = true, EmitDefaultValue = true)]
        public string Function { get; set; }

        /// <summary>
        /// Gets or Sets Parameters
        /// </summary>
        [DataMember(Name = "parameters", IsRequired = true, EmitDefaultValue = true)]
        public List<string> Parameters { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("class FunctionCall {\n");
            sb.Append("  Function: ").Append(Function).Append("\n");
            sb.Append("  Parameters: ").Append(Parameters).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }

        public static IEnumerable<FunctionCall> Deduplicate(IEnumerable<FunctionCall> calls)
            => calls.Distinct();

        protected bool Equals(FunctionCall other)
        {
            if (Parameters.Count != other.Parameters.Count)
                return false;

            if (Function != other.Function)
                return false;

            for (var i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i] != other.Parameters[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FunctionCall)obj);
        }

        public override int GetHashCode()
        {
            int code = Function.GetHashCode();
            foreach (var parameter in Parameters)
                code = HashCode.Combine<int, string>(code, parameter);

            return code;
        }
    }
}
