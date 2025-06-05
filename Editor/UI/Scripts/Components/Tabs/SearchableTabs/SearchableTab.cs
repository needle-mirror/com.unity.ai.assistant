using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Search;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class SearchableTab : SelectionPopupTab
    {
        static readonly Dictionary<string, SearchContextWrapper> s_SearchContextWrapperCache = new();
        static string s_CurrentQuery;

        public abstract SearchContextWrapper[]
            SearchProviders { get; } // Should be overriden to return search providers using GetSearchContextWrapper()

        public abstract int Order { get; }

        public override string Instruction1Message =>
            "Search sources inside of Unity and attach them your prompt for additional context.";

        public override string Instruction2Message => "Or drag and drop them directly below.";

        public bool IsLoading =>
            SearchProviders is { Length: > 0 } && SearchProviders.Any(p => p is { IsLoading: true });

        protected static SearchContextWrapper GetSearchContextWrapper(string providerId)
        {
            if (!s_SearchContextWrapperCache.TryGetValue(providerId, out var wrapper))
            {
                var context = SearchService.CreateContext(providerId, s_CurrentQuery);
                wrapper = new SearchContextWrapper(context);
                s_SearchContextWrapperCache[providerId] = wrapper;
            }

            return wrapper;
        }

        protected SearchableTab(string label) : base(label)
        {
        }

        public static void SetupSearchProviders(string query)
        {
            s_CurrentQuery = query;

            foreach (var searchContextWrapper in s_SearchContextWrapperCache.Values)
            {
                searchContextWrapper.Stop();
            }

            s_SearchContextWrapperCache.Clear();
        }

        public static void StartSearchers()
        {
            foreach (var searchContextWrapper in s_SearchContextWrapperCache.Values)
            {
                searchContextWrapper.Start();
            }
        }

        internal class SearchContextWrapper
        {
            SearchContext Context;
            public List<Action<IList<UnityEngine.Object>>> Callbacks = new();

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
                Context.asyncItemReceived += (_, items) =>
                {
                    if (!m_Active)
                        return;

                    var itemsAsList = GetItemsAsList(items);

                    foreach (var callback in Callbacks)
                        callback.Invoke(itemsAsList);
                };

                // Needed so we get out of loading state when the search is finished but no results were returned:
                Context.sessionEnded += _ =>
                {
                    if (!m_Active)
                        return;

                    foreach (var callback in Callbacks)
                        callback.Invoke(null);
                };

                var initialResults = SearchService.GetItems(Context, SearchFlags.FirstBatchAsync);

                var itemsAsList = GetItemsAsList(initialResults);

                if (itemsAsList.Count > 0)
                {
                    foreach (var callback in Callbacks)
                        callback.Invoke(itemsAsList);
                }

                return;

                List<UnityEngine.Object> GetItemsAsList(IEnumerable<SearchItem> items)
                {
                    return items.Select(item => item.ToObject()).Where(obj => obj).ToList();
                }
            }
        }
    }
}
