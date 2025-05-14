---
uid: code-overview
---

# Use /code mode

The **/code** mode in Assistant generates, validates, and compiles C# scripts based on natural language prompts. It also performs syntax checks and ensures that the generated code follows Unity API best practices.

## Features of /code mode

Use the **/code** mode to do the following:

* Generate C# scripts based on natural language queries.
* Compile and test the generated code for syntax errors.
* Edit the script in an Integrated Development Environment (IDE) like Visual Studio or Rider.
* Save, copy, or run the generated script for further testing and refinement.

## How /code mode works

When you enter a request in **/code** mode, Assistant performs the following tasks:

* Processes the input prompt and identifies it as a coding request.
* Generates a C# script.
* Compiles and validates the script to detect syntax.

## Query with /code mode

To interact with Assistant in **/code** mode, follow these steps:

1. To begin a new conversation, select **+ Chat**.
1. Switch to the **/code** mode using one of the following methods in the text field:

   * Type `/code`.
   * Select **Shortcuts** > **/code**.
1. Enter your request in the text field. For example, `Write the player controller for a side-scroller video game`.
1. Press **Enter** on your keyboard or select the send icon.

   You can also [Enable and use the Ctrl+Enter (macOS: ⌘Return) Preferences option](xref:preferences) to send your prompt to Assistant.

   Assistant generates the script, complies it, and displays the result.

## Understand the code review and validation process

If the compilation is successful, Assistant displays the script without warnings. If the compilation fails, an error message appears.

If compilation fails, you can do the following:

* Modify the prompt to refine the request.
* Edit the script manually to fix errors before use.

## Save and Copy the generated code

You can handle the generated scripts using the following options:

* **Copy**: Select **Copy** to copy the script and paste it into an IDE.
* **Save**: Select **Save** to save the script locally in your Unity project.
* Open the script directly in an IDE like Visual Studio or Rider to edit it.

## Additional resources

* [Assistant interface](xref:assistant-interface)
* [Best practices for using Assistant](xref:assistant-best)
* [Enable and use the Ctrl+Enter (macOS: ⌘Return) Preferences option](xref:preferences)