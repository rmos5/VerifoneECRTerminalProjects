namespace Verifone.ECRTerminal
{
    /// <summary>
    /// Provides UI callbacks for prompting the user during transaction flows.
    /// </summary>
    public interface IUserPromptHandler
    {
        /// <summary>
        /// Shows a modal dialog to the user with a prompt message and returns whether the user accepted.
        /// </summary>
        /// <param name="promptMessage">The message to display to the user.</param>
        /// <returns><c>true</c> if the user accepted; otherwise, <c>false</c>.</returns>
        bool ShowUserPromptDialog(string promptMessage);

        /// <summary>
        /// Shows a modal dialog to the user with a prompt message and returns whether the user accepted,
        /// also returning any free-form input the user entered.
        /// </summary>
        /// <param name="promptMessage">The message to display to the user.</param>
        /// <param name="userInput">On success, receives user-entered text (may be empty).</param>
        /// <returns><c>true</c> if the user accepted; otherwise, <c>false</c>.</returns>
        bool ShowUserPromptDialog(string promptMessage, out string userInput);
    }
}
