---
uid: get-started
---

# Get started with Assistant

Assistant provides three distinct modes of operation ([**/ask**](xref:ask-overview), [**/run**](xref:run-overview), and [**/code**](xref:code-overview)) to streamline workflows within the Unity Editor. Each mode serves a specific purpose: retrieve project-related information, automate tasks, or generate reusable C# scripts.

The following table summarizes the key details of each mode, including their purpose, input requirements, output format, and additional notes to help you identify the appropriate mode for your tasks.

| **Mode** | **Purpose** | **Inputs** | **Output** | **Additional information** |
| -------- | ---------------- | ---------- | ---------- | --------- |
| **Ask mode** | * Provides information about Unity features, documentation, and project settings.<br><br> * Retrieves Unity documentation and provides summarized guidance. | * Natural language queries.<br><br>* Can attach GameObjects, console errors, or components for context-sensitive answers. | * Text-based responses.<br><br>* Step-by-step guidance on workflows.| * Default mode<br><br>* The same query might generate different responses. |
| **Run mode** | * Automates repetitive tasks directly in the Unity Editor.<br><br>* Runs commands through preview and run process.<br><br> * Generates Unity API code for each task. | * Natural language queries.<br><br> * Executes commands and manipulates scene objects automatically. | * Command previews with a list of actions to be performed.<br><br>* C# code snippets that perform the actions.<br><br>*  Performed tasks with a detailed recap log. | * Requires user confirmation before it performs a task.<br><br>* Displays command logic for review but you can't edit it inside Assistant.<br><br>* Integrates with Unity's **Undo History** to revert changes. |
| **Code mode** | * Generates reusable C# scripts for Unity API applications.<br><br> * Automates programming tasks.<br><br>* Validates generated code for compile-time errors. | * User queries phrased as script or coding requests.<br><br> * Can attach components for targeted script creation. | * C# scripts pre-integrated with Unity APIs.<br><br>* Scripts are exportable to an Integrated Development Environment (IDE) for modifications and revisions. | * Allows external IDE integration for script editing.<br><br>* Finalized scripts return to Assistant with updated tags.<br><br>* Focused on programmatic workflows requiring Unity API integration. |

## Additional resources

* [Use /ask mode](xref:ask-overview)
* [Best practices for using Assistant](xref:assistant-best)