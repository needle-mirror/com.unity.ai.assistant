using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.WhatsNew
{
    abstract class WhatsNewContent : ManagedTemplate
    {
        const string k_BackToStartText = "Back to start";

        readonly IList<VisualElement> k_Pages = new List<VisualElement>();
        readonly IDictionary<VisualElement, string> k_PageVideos = new Dictionary<VisualElement, string>();

        Button m_BackButton;
        Button m_NextButton;
        Label m_PageControlLabel;

        int m_CurrentPageIndex;

        WhatsNewView m_ParentView;

        protected WhatsNewContent()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public abstract string Title { get; }
        public abstract string Description { get; }

        public void SetParent(WhatsNewView newParent)
        {
            m_ParentView = newParent;
        }

        public void BrowseTo(int page)
        {
            m_CurrentPageIndex = Mathf.Clamp(page, 0, k_Pages.Count - 1);
            RefreshPageDisplay();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            view.style.flexGrow = 1;

            m_BackButton = view.SetupButton("pageControlBack", OnBackPressed);
            m_NextButton = view.SetupButton("pageControlNext", OnNextPressed);

            m_PageControlLabel = view.Q<Label>("pageControlLabel");
        }

        protected void RegisterPage(VisualElement page, string videoFile = null)
        {
            k_Pages.Add(page);

            if (!string.IsNullOrEmpty(videoFile))
            {
                k_PageVideos.Add(page, videoFile);
            }
        }

        void ExitContent()
        {
            m_ParentView.ActivateLandingPage();
        }

        void OnNextPressed(PointerUpEvent evt)
        {
            if (m_CurrentPageIndex < k_Pages.Count - 1)
            {
                m_CurrentPageIndex++;
            }
            else
            {
                ExitContent();
                return;
            }

            RefreshPageDisplay();
        }

        void OnBackPressed(PointerUpEvent evt)
        {
            if (m_CurrentPageIndex > 0)
            {
                m_CurrentPageIndex--;
            }
            else
            {
                ExitContent();
                return;
            }

            RefreshPageDisplay();
        }

        void RefreshPageDisplay()
        {
            PlayVideo();

            for(var i = 0; i < k_Pages.Count; i++)
            {
                k_Pages[i].SetDisplay(i == m_CurrentPageIndex);
            }

            m_NextButton.text = m_CurrentPageIndex == k_Pages.Count - 1 ? k_BackToStartText : "Next";
            m_BackButton.text = m_CurrentPageIndex == 0 ? k_BackToStartText : "Back";
            m_PageControlLabel.text = $"{m_CurrentPageIndex + 1} / {k_Pages.Count}";
        }

        void PlayVideo()
        {
            if (m_ParentView == null)
            {
                return;
            }
            
            var targetPage = k_Pages[m_CurrentPageIndex];

            if (!k_PageVideos.TryGetValue(targetPage, out var videoFileName))
            {
                m_ParentView.StopVideo();
                return;
            }

            var targetElement = targetPage.Q<VisualElement>($"page{m_CurrentPageIndex + 1}Video");
            if (targetElement == null)
            {
                m_ParentView.StopVideo();
                return;
            }

            m_ParentView.PlayVideoInto(targetElement, videoFileName);
        }
    }
}
