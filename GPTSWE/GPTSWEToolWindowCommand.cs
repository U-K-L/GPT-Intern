using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Task = System.Threading.Tasks.Task;

namespace GPTSWE
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class GPTSWEToolWindowCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 4130;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("12b3bbc3-b17d-40d7-8b41-bb70c0e4cce0");

        /// <summary>
        // VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="GPTSWEToolWindowCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private GPTSWEToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static GPTSWEToolWindowCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in GPTSWEToolWindowCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GPTSWEToolWindowCommand(package, commandService);
        }

        /// <summary>
        /// Shows the tool window when the menu item is clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = this.package.FindToolWindow(typeof(GPTSWEToolWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window");
            }
            // Get the control hosted in the tool window
            GPTSWEToolWindowControl control = (GPTSWEToolWindowControl)window.Content;

            // Subscribe to the event
            control.ResponseReceived += Control_ResponseReceived;

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        // Event handler method
        private void Control_ResponseReceived(object sender, ResponseEventArgs e)
        {
            // Handle the event - trigger other actions
            // For example, write to the Output window
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                //ResponseGenerated(e.UserInput, e.Response);
            });
        }


        public static void WriteToDocument(string filePath, string response)
        {
            ToolWindowPane window = Instance.package.FindToolWindow(typeof(GPTSWEToolWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window.");
            }

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Show the tool window
                IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());

                // Get the DTE (Development Tools Environment) service
                DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "Unable to access DTE service.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Check if GetProjectDirectory() returns a valid path
                string projectDirectory = GetProjectDirectory();
                if (string.IsNullOrEmpty(projectDirectory))
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "Project directory is null or empty.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                string absoluteFilePath = filePath;
                if (!System.IO.File.Exists(absoluteFilePath))
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        $"The file at path '{absoluteFilePath}' does not exist.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Open the specified document or find if it's already opened
                Document openDocument = null;
                Window docWindow = null;
                foreach (Document doc in dte.Documents)
                {
                    if (doc.FullName.Equals(absoluteFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        openDocument = doc;
                        break;
                    }
                }

                // If the document is not already open, open it
                if (openDocument == null)
                {
                    try
                    {
                        docWindow = dte.ItemOperations.OpenFile(absoluteFilePath);
                        openDocument = docWindow.Document;
                    }
                    catch (Exception ex)
                    {
                        VsShellUtilities.ShowMessageBox(
                            Instance.package,
                            $"Error opening file: {ex.Message}",
                            "Error",
                            OLEMSGICON.OLEMSGICON_CRITICAL,
                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        return;
                    }
                }

                if (openDocument == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "Document is still null after trying to open it.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Ensure the document is a text document
                TextDocument textDocument = openDocument.Object("TextDocument") as TextDocument;
                if (textDocument == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "The document is not a text document or could not be cast to TextDocument.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Modify the document's text
                try
                {
                    EditPoint startPoint = textDocument.StartPoint.CreateEditPoint();
                    EditPoint endPoint = textDocument.EndPoint.CreateEditPoint();

                    // Write the unsaved content to a temporary file for comparison
                    string tempFilePath = Path.GetTempFileName();
                    File.WriteAllText(tempFilePath, response);

                    Document activeDocument = dte.ActiveDocument;
                    string activeDocumentFilePath = activeDocument.FullName;

                    // Compare the unsaved content (temp file) with the saved version on disk
                    CompareFilesWithKeyAcceptReject(absoluteFilePath, tempFilePath, activeDocumentFilePath);

                    activeDocument.Close();
                }
                catch (Exception ex)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        $"An error occurred while modifying the document: {ex.Message}",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            });
        }


        public static void CompareUnsavedChanges(string filePath)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Get the DTE (Development Tools Environment) service
                DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "Unable to access DTE service.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Check if the file is already open in the editor
                Document openDocument = null;
                foreach (Document doc in dte.Documents)
                {
                    if (doc.FullName.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        openDocument = doc;
                        break;
                    }
                }

                if (openDocument == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "The file is not currently open.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Get the text document (unsaved content in memory)
                TextDocument textDocument = openDocument.Object("TextDocument") as TextDocument;
                if (textDocument == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "The document is not a text document.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Get the unsaved content (in-memory)
                EditPoint startPoint = textDocument.StartPoint.CreateEditPoint();
                EditPoint endPoint = textDocument.EndPoint.CreateEditPoint();
                string unsavedContent = startPoint.GetText(endPoint);

                // Write the unsaved content to a temporary file for comparison
                string tempFilePath = Path.GetTempFileName();
                File.WriteAllText(tempFilePath, unsavedContent);

                // Compare the unsaved content (temp file) with the saved version on disk
                CompareFilesWithAcceptReject(filePath, tempFilePath);

                // Cleanup the temp file after comparison
                File.Delete(tempFilePath);
            });
        }

        // Function to open the diff window (as explained earlier)
        public static void ShowFileDifference(string originalFilePath, string modifiedFilePath, string windowTitle)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Get the difference service
                IVsDifferenceService diffService = Package.GetGlobalService(typeof(SVsDifferenceService)) as IVsDifferenceService;

                if (diffService == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "Could not get the Difference Service.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                try
                {
                    // Call the difference service to open the diff window
                    diffService.OpenComparisonWindow2(
                        originalFilePath,      // Original file
                        modifiedFilePath,      // Modified file (temp file with unsaved content)
                        windowTitle,           // Title of the diff window
                        null,                  // Tooltip
                        null,                  // Original file label
                        null,                  // Modified file label
                        null,                  // Inline parameter (optional)
                        null,
                        (uint)__VSDIFFSERVICEOPTIONS.VSDIFFOPT_DetectBinaryFiles); // Diff options (can add more flags)
                }
                catch (Exception ex)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        $"An error occurred while opening the diff window: {ex.Message}",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            });
        }

        public static void CompareFilesWithAcceptReject(string originalFilePath, string modifiedFilePath)
        {
            // Open the diff window using IVsDifferenceService
            ShowFileDifference(originalFilePath, modifiedFilePath, "File Differences");

            // Ask the user to accept or reject the changes
            var result = MessageBox.Show("Do you want to accept the changes?", "Accept or Reject", MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                // If the user accepts, overwrite the original file with the modified version
                try
                {
                    File.Copy(modifiedFilePath, originalFilePath, true); // Overwrite the original file with the modified content
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "Changes accepted and applied.",
                        "Success",
                        OLEMSGICON.OLEMSGICON_INFO,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
                catch (Exception ex)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        $"An error occurred while applying changes: {ex.Message}",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
            else
            {
                // User rejected the changes
                VsShellUtilities.ShowMessageBox(
                    Instance.package,
                    "Changes rejected. No modifications applied.",
                    "Rejected",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }


        public static void CompareFilesWithKeyAcceptReject(string originalFilePath, string modifiedFilePath, string activeDocumentFilePath)
        {
            // Open the diff window using IVsDifferenceService
            ShowFileDifference(originalFilePath, modifiedFilePath, "File Differences");

            // Attach key press handlers for accepting or rejecting changes
            AttachKeyHandlers(originalFilePath, modifiedFilePath, activeDocumentFilePath);
        }

        private static void AttachKeyHandlers(string originalFilePath, string modifiedFilePath, string activeDocumentFilePath)
        {

            // Subscribe to key events globally (in the context of Visual Studio)
            System.Windows.Input.Keyboard.AddKeyDownHandler(
                System.Windows.Application.Current.MainWindow, (sender, e) =>
                {
                    // Handle the Enter key (accept changes)
                    if (e.Key == System.Windows.Input.Key.Enter)
                    {
                        AcceptChanges(originalFilePath, modifiedFilePath);
                    }
                    // Handle the Backspace key (reject changes)
                    else if (e.Key == System.Windows.Input.Key.Back)
                    {
                        RejectChanges(modifiedFilePath);
                    }
                });
        }

        private static void AcceptChanges(string originalFilePath, string modifiedFilePath)
        {
            try
            {
                // Overwrite the original file with the modified version
                File.Copy(modifiedFilePath, originalFilePath, true);
                VsShellUtilities.ShowMessageBox(
                    Instance.package,
                    "Changes accepted and applied.",
                    "Success",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                    Instance.package,
                    $"An error occurred while applying changes: {ex.Message}",
                    "Error",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }

            File.Delete(modifiedFilePath);
        }

        private static void RejectChanges(string modifiedFilePath)
        {
            VsShellUtilities.ShowMessageBox(
                Instance.package,
                "Changes rejected. No modifications applied.",
                "Rejected",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            File.Delete(modifiedFilePath);
        }



        public static string GetCurrentDocumentText()
        {
            // Handle the input and response here
            // For example, log them or process them further
            string content = "Code file missing.";

            ToolWindowPane window = Instance.package.FindToolWindow(typeof(GPTSWEToolWindow), 0, true);
            if ((null == window) || (null == window.Frame))
            {
                throw new NotSupportedException("Cannot create tool window.");
            }
            // Ensure we're on the UI thread if interacting with Visual Studio services
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Show the tool window
                IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());


                // Get the DTE (Development Tools Environment) service
                DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "Unable to access DTE service.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Get the active document
                Document activeDocument = dte.ActiveDocument;
                if (activeDocument == null || activeDocument.ReadOnly)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "No writable active document found.",
                        "Information",
                        OLEMSGICON.OLEMSGICON_INFO,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Ensure the document is a text document
                TextDocument textDocument = activeDocument.Object("TextDocument") as TextDocument;
                if (textDocument == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "The active document is not a text document.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                // Modify the document's text
                try
                {
                    // Create an EditPoint at the start of the document
                    EditPoint editPoint = textDocument.StartPoint.CreateEditPoint();

                    // Read the existing content
                    content = editPoint.GetText(textDocument.EndPoint);
                }
                catch (Exception ex)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        $"An error occurred: {ex.Message}",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }


            });

            return content;
        }

        public static string GetAllDocumentsText()
        {
            string content = "";

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null)
                {
                    VsShellUtilities.ShowMessageBox(
                        Instance.package,
                        "Unable to access DTE service.",
                        "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    return;
                }

                Projects projects = dte.Solution.Projects;

                foreach (Project project in projects)
                {
                    content += GetProjectFiles(project.ProjectItems);
                }
            });

            return content;
        }

        private static string GetProjectDirectory()
        {
            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;

            if (dte == null || dte.Solution == null)
            {
                throw new InvalidOperationException("Unable to locate the project directory.");
            }

            return System.IO.Path.GetDirectoryName(dte.Solution.FullName);

        }

        public static string GetFilePathsFromProject()
        {
            string contents = "";

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            Projects projects = dte.Solution.Projects;

            foreach (Project project in projects)
            {
                contents += GetProjectFiles(project.ProjectItems);
            }

            return contents;
        }

        private static string GetProjectFiles(ProjectItems items)
        {
            string content = "";

            foreach (ProjectItem item in items)
            {
                try
                {
                    for (short i = 0; i < item.FileCount; i++)
                    {
                        if (i >= 0 && i < item.FileCount)
                        {
                            string filePath = item.FileNames[i];
                            content += "FILE FULL PATH IS: { " + filePath + "} \n";
                            content += System.IO.File.ReadAllText(filePath) + "\n";
                        }
                    }

                    if (item.ProjectItems != null)
                    {
                        content += GetProjectFiles(item.ProjectItems);
                    }
                }
                catch (Exception ex)
                {

                }
            }

            return content;
        }

        private static string GetFilePaths(ProjectItems items)
        {
            string content = "";

            foreach (ProjectItem item in items)
            {
                try
                {
                    for (short i = 0; i < item.FileCount; i++)
                    {
                        if (i >= 0 && i < item.FileCount)
                        {
                            string filePath = item.FileNames[i];
                            content += "FILE PATH IS: { " + filePath + "} \n";
                        }
                    }

                    if (item.ProjectItems != null)
                    {
                        content += GetProjectFiles(item.ProjectItems);
                    }
                }
                catch (Exception ex)
                {
                }
            }

            return content;
        }
    }
}
