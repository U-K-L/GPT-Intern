using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using System.Windows.Forms;

namespace GPTSWE
{
    public class GPTProgramming
    {
        [KernelFunction("read_all_files")]
        [Description("This reads all files in the project. Use to check if multiple files need to be read.")]
        [return: Description("Reads all files and provides information about them.")]
        public async Task<string> ReadAllFilesAsync()
        {
            return GPTSWEToolWindowCommand.GetAllDocumentsText();
        }

        [KernelFunction("read_file")]
        [Description("When the user asks you to program. You begin to read the current file opened. Regardless of what file specified. You take that file then begin to modify it.")]
        [return: Description("Reads the currently opened file. Use this to gain context of what to change.")]
        public async Task<string> ReadFileAsync()
        {
            return GPTSWEToolWindowCommand.GetCurrentDocumentText();
        }


        [KernelFunction("get_file_path")]
        [Description("This gets you the path of the files. You know the file name, but need the path to open its contents.")]
        [return: Description("Sends back the full file path.")]
        public async Task<string> GetFilePaths()
        {

            string paths = GPTSWEToolWindowCommand.GetFilePathsFromProject();
            return paths;
        }

        [KernelFunction("modify_file")]
        [Description("When the user asks you to program. Rewrite the entire file with the answer and then replace that file entirely. You may need to modify multiple files to reach a goal.")]
        [return: Description("All done when finished!")]
        public async Task<string> ModifyFileAsync([Description("Filepath of the file to modify. Might need to use get_file_path first.")] string filepath, [Description("The code (string) to modify the file with, your code answer ONLY")] string code)
        {

            GPTSWEToolWindowCommand.WriteToDocument(filepath, code);
            return "All done \n//";
        }

    }
}
