using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Search;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class SearchableTab: SelectionPopupTab
    {
        public abstract SearchContextWrapper[]
            SearchProviders { get; } // Should be overriden to return HierarchySearchContext or ProjectSearchContext

        public abstract int Order { get; }

        public override string Instruction1Message =>
            "Search sources inside of Unity and attach them your prompt for additional context.";

        public override string Instruction2Message => "Or drag and drop them directly below.";

        public bool IsLoading =>
            SearchProviders is { Length: > 0 } && SearchProviders.Any(p => p is { IsLoading: true });

        // Search contexts that can be shared by multiple tabs:
        protected static SearchContextWrapper HierarchySearchContext;
        protected static SearchContextWrapper ProjectSearchContext;

        protected SearchableTab(string label) : base(label)
        {
        }

        public static void SetupSearchProviders(string query)
        {
            HierarchySearchContext?.Stop();
            ProjectSearchContext?.Stop();

            HierarchySearchContext = new SearchContextWrapper(SearchService.CreateContext("scene", query));
            ProjectSearchContext = new SearchContextWrapper(SearchService.CreateContext("asset", query));
        }

        public static void StartSearchers()
        {
            HierarchySearchContext.Start();
            ProjectSearchContext.Start();
        }

        internal class SearchContextWrapper
        {
            SearchContext Context;
            public List<Action<IList<SearchItem>>> Callbacks = new();

            bool m_Active = true;

            public bool IsLoading => m_Active && Context is { searchInProgress: true };

            public SearchContextWrapper(SearchContext context)
            {
                Context = context;
            }

            public void Stop()
            {
                m_Active = false;
                Context.Dispose();
            }

            public void Start()
            {
                SearchService.Request(Context,
                    (_, items) =>
                    {
                        if (!m_Active)
                        {
                            return;
                        }

                        foreach (var callback in Callbacks)
                        {
                            callback.Invoke(items);
                        }
                    });
            }
        }
    }
}
