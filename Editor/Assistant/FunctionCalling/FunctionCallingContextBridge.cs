using System;
using System.Collections.Generic;

namespace Unity.AI.Assistant.Editor.FunctionCalling
{
    class FunctionCallingContextBridge
    {
        public static object LastPostedContext { get; private set; }

        public static IDisposable GetCallContext(object context)
        {
            return new CallContext(context);
        }

        public class CallContext : IDisposable
        {
            public CallContext(object context)
            {
                LastPostedContext = context;
            }

            public void Dispose()
            {
                LastPostedContext = null;
            }
        }
    }
}
