using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Linq;


/***************************************************************************************************************************
 * Author:  Oscar Mendoza / IITS
 * Date:    Oct. 2011
 *
 * Description:
 * A basic PSM file parser that shows a .PSM file contents and the subroutines found within.
 * This program demonstrate: 1. Basic use of RegEX, parsing a text file (pBlaze FPGA IDE assembler) to find
 * code subroutines, display the file's content and the subroutines found.
 *
 * Features:
 *      1. Load a test file at startup to test the app's functionality.
 *      2. When user selects a sub, find it in the program and highlight it.
 *      3. File edit detection: Ask user approval to change file, then open new / changed file.
 *
 * Improvements:
 *      a. improve program's responsiveness, UI stops responding when user re-loads file. Use background workers?
 *      b.
 *      
 * Dev Track:   Date: July 08, 2013 - App feature changes and enhancements
 * 
 *      a. Add sub-routine sorting capabilities - enable to sort the sub-routine -
 *          12/23/2014 - added code to sort the subs before displaying to UI - used Linq 
 *      b. Once sub-routine is found, scroll sub-routine highlight to top
 *      c. 
 *
 **************************************************************************************************************************/

namespace pBlazeIDELists
{
    public partial class Form1 : Form
    {
        bool Success = false;
        bool Fail = true;

        // source file changing flag - indicates some change with the source file, some methods may
        // be affected!
        bool sourceFileChanging = false;
        bool sourceFileChanged = false;
        
        // needed to make thread safe calls!
        private BackgroundWorker backgroundWorker1;

        // Will hold the user settable items including:
        //  1. Application's path
        //  2. Open last used file?
        //  3.
        private pBlazeIDELists.Properties.Settings AppSettings = new pBlazeIDELists.Properties.Settings();

        // The current and previous work file, used to track file changes
        private string currentFile;
        private string currentDir;
        private string previousFile;
        private string previousDir;
        private string filePath;

        // tracks the work file status
        private Boolean fileDataCurrent;

        // enables tracking the file's change status
        private FileSystemWatcher fsw = new FileSystemWatcher();

        // Data containers
        // All the strings are stored in this collection, the keys become the line number, and the string contains the
        // code and comments
        private Dictionary<int, string> strItems = new Dictionary<int, string>();               // All text strings found in input file
        private Dictionary<int, string> strSLCItems = new Dictionary<int, string>();            // Single Line Comments
        private Dictionary<int, string> strCODEItems = new Dictionary<int, string>();           // Code Items other than sub + code items
        private Dictionary<int, string> strDWECItems = new Dictionary<int, string>();           // Directive with embedded comment
        private Dictionary<int, string> strOpcodeItems = new Dictionary<int, string>();         // Opcode with no embedded comment
        private Dictionary<int, string> srtOWECItems = new Dictionary<int, string>();           // Opcode with embedded comment
        private Dictionary<int, string> strSubrutineItems = new Dictionary<int, string>();      // All the subroutines found

        public Form1()
        {
            InitializeComponent();

            // This application executable path, we'll use this to find the test data file.
            this.AppSettings.AppExecPath = Application.ExecutablePath;

            // Subs UI Items
            this.dataGridView1.Enabled = false;
            this.dataGridView1.Visible = false;

            this.currentFile = "n/a";
            this.currentDir = "n/a";
            this.previousFile = "n/a";
            this.previousDir = "n/a";
            this.filePath = "n/a";

            this.fileDataCurrent = false;

            // wire the FileSystemsWatcher event handler
            fsw.Changed += new FileSystemEventHandler(PSMFileWatcher_Changed);

            // don't enable watcher until the file is selected
            fsw.EnableRaisingEvents = false;

            this.dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            

            // wire an event handler to the GridView row selected event
            //this.dataGridView1.SelectionChanged += new EventHandler(dataGridView1_SelectionChanged);
        }
        /// <summary>
        /// Handles the DataGrid Selection Changed event. When user selects a row, the
        /// code data TextBox is updated to show the selected sub-routine.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            // subroutine row selection has changed, need to get the newly selected line index
            // use this index to find the sub-routine in the code file:
            // do this only if we are NOT changing source files!

            if (!sourceFileChanging)
            {
                // switch focus to text box control
                this.txtBox_FileContents.Focus();
                // get the selected row text, then find the text's index, then select the text
                // and finally scroll to the caret to update the displayed text.
                string text = this.dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
                int strIndex = this.txtBox_FileContents.Text.IndexOf(text);
                this.txtBox_FileContents.Select(strIndex, text.Length);
                this.txtBox_FileContents.ScrollToCaret();
            }
        }

        /// <summary>
        /// Handles the File > Open click event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openFileMenuItem_Click(object sender, EventArgs e)
        {
            // show the dialog
            this.openPSMFileDlg.ShowDialog();
        }

        /// <summary>
        /// Handles the open PSM file Dialog OK click event that selects an input file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openPSMFileDlg_FileOk(object sender, CancelEventArgs e)
        {
            // TODO: Add code to check user has permission to open file?
            //       
            // store the previous source file info
            this.previousDir = this.currentDir;
            this.previousFile = this.currentFile;

            // get the "new" source file info
            this.currentFile = this.openPSMFileDlg.FileName;
            this.currentDir = Path.GetDirectoryName(this.openPSMFileDlg.FileName);

            // clear PSM data only if the input file is different than the one currently open
            if (this.previousFile != currentFile)
            {
                // update flag, some methods depend on this flag to work correctly!
                sourceFileChanging = true;

                // Empty dictionaries, clear data, prepare to receive new data
                this.strItems.Clear();
                this.strSubrutineItems.Clear();
                this.txtBox_FileContents.Visible = false;
                this.dataGridView1.Visible = false;

                // update UI
                this.label1.Text = "PSM data cleared...";

                try
                {
                    OpenPSMFile(this.currentFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error OpenPSMFile occurred!" + ex.Message, "File Open Exception!", MessageBoxButtons.OK);
                    //throw;
                }
            }
            else if (this.previousFile == this.currentFile && this.previousDir == this.currentDir)
            {
                //using our lame file equality test, this file is the same!
                // we do nothing and reset flags
                this.sourceFileChanging = false;
                this.sourceFileChanged = false;
            }
        }

        /// <summary>
        /// Clears the current code data stored in the dictionaries.
        /// </summary>
        private void ClearData()
        {
            // Empty dictionaries, clear data, prepare to receive new data
            this.strItems.Clear();
            this.strSubrutineItems.Clear();

            //TODO: Make thread safe call...
            this.txtBox_FileContents.Visible = false;

            // TODO: make a thread safe call
            //this.backgroundWorker1.RunWorkerAsync();

            this.dataGridView1.Visible = false;

            // update UI
            this.label1.Text = "Data cleared...";
        }

        private void backgroundWorker1_RunWorkerCompelted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.txtBox_FileContents.Visible = false;
        }

        /// <summary>
        /// Starts the work file monitoring process. TODO: dir should be path!!!
        /// </summary>
        /// <param name="dir"></param>
        private void MonitorWorkFile(string dir)
        {
            try
            {
                this.fsw.Path = dir;
                this.fsw.Filter = "*.psm";
                this.fsw.EnableRaisingEvents = true;

                // update the UI once monitoring is set
                this.txtBox_PSMFile.Text = this.txtBox_PSMFile.Text + " -monitored";
            }
            catch (Exception e)
            {
                MessageBox.Show("File watcher setup failed " + e.Message, "FileWatcher Exception", MessageBoxButtons.OKCancel);
                this.txtBox_PSMFile.Text = "work file not monitored!";
            }
        }

        /// <summary>
        /// Opens the user selected work file, it also performs some BASIC file
        /// comparison to avoid opening the same file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private bool OpenPSMFile(string fileName)
        {
            // TODO: Find a better way to validate file?
            // If we can read data from the input file, we update the UI with it
            if (ReadCodeFile(fileName, ref this.strItems) != Success)
            {
                //If not we throw an  exception and...
                this.label1.Text = "Error reading from the test.psm file, please notify the app developer!";
                this.sourceFileChanged = false;
                this.sourceFileChanging = true;
            }
            else
            {
                // If we have code lines, update UI. else if the file is empty, notify user and wait.
                if (this.strItems.Keys.Count > 0)
                {
                    this.txtBox_FileContents.Enabled = true;

                    UpdateCodeFileView();

                    this.txtBox_FileContents.Visible = true;

                    this.dataGridView1.Enabled = true;

                    UpdateCodeSubsView(0);

                    this.dataGridView1.Visible = true;

                    this.txtBox_PSMFile.Text = this.openPSMFileDlg.FileName;

                    MonitorWorkFile(Path.GetDirectoryName(this.openPSMFileDlg.FileName));

                    // update flag, since the source file has changed
                    this.sourceFileChanged = true;
                    this.sourceFileChanging = false;

                    return true;
                }
                else
                {
                    MessageBox.Show("The code file is empty, nothing to display!", "Empty Code File", MessageBoxButtons.OK);

                    this.sourceFileChanged = false;
                    this.sourceFileChanging = true;

                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Handles the PSMFile has changed event generated by the file watcher.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PSMFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            // the work file has changed, handle the change as follows:
            // find out what change has occurred, then based on that change, do nothing or reload the file.
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                // update file changing flag, some methods depend on it to work properly
                this.sourceFileChanging = true;

                this.ClearData();
                this.OpenPSMFile(this.currentFile);
                this.fileDataCurrent = true;
                this.UpdateFileDataSatus();

                // file changing is done, update flags.
                this.sourceFileChanging = false;
                this.sourceFileChanged = true;
            }
        }

        /// <summary>
        /// Maintains the file status UI indicator current.
        /// </summary>
        private void UpdateFileDataSatus()
        {
            if (this.fileDataCurrent)
            {
                this.lbl_FileSatus.BackColor = Color.Green;
            }
            else
            {
                this.lbl_FileSatus.BackColor = Color.Red;
            }
        }

        /// <summary>
        /// Handles the About menu click event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("A basic pBalze code file viewer. Release V2 @ 12/23/2014. Oscar Mendoza", "pBlazeIDEList About", MessageBoxButtons.OK);
        }

        /// <summary>
        /// Handles the Exit menu click event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // User has selected to terminate the app
            //this.psmFileStream.Close();

            this.Close();
        }

        /// <summary>
        /// Updates the UI's txtBox_FileContents control.
        /// </summary>
        private void UpdateCodeFileView()
        {
            //Clear text, then load new text
            this.txtBox_FileContents.Text = "";

            StringBuilder sb = new StringBuilder();

            foreach (int key in strItems.Keys)
            {
                sb.AppendLine(key.ToString() + ":  " + strItems[key]);
            }

            this.txtBox_FileContents.Text = sb.ToString();
        }

        /// <summary>
        /// Updates the Code View control when the data has been loaded and is ready for
        /// display.
        /// </summary>
        private void UpdateCodeSubsView(int sortCol)
        {
            // Scan data and find all sub-routines then update UI
            this.label1.Text = "Searching code file for sub-rutines...";

            BindingSource bs = new BindingSource();
            
            
            //Process data and extract sub-routine lines
            this.strSubrutineItems = GetCodeItems(strItems);

            // TODO: remove once tested! can we sort the dictionary?
            // from StackOverflow answer...
            //List<KeyValuePair<string, string>> myList = strSubrutineItems.
            var sortedDic = from entry in strSubrutineItems orderby entry.Value ascending select entry;

            if (sortedDic.Count() == 0 /*this.strSubrutineItems.Keys.Count == 0 */)
            {
                MessageBox.Show("No subroutines found in selected file.", "Not data found!", MessageBoxButtons.OK);
            }
            else
            {
                //bs.DataSource = this.strSubrutineItems;  
                bs.DataSource = sortedDic;
                this.dataGridView1.Enabled = true;
                this.dataGridView1.DataSource = bs;
                this.dataGridView1.Visible = true;
                this.dataGridView1.Update();
            }
        }

        /// <summary>
        /// Parses the strItems collections and finds all the code lines.
        /// </summary>
        /// <param name="data">The collection to be parsed</param>
        /// <returns>A dictionary containing all the code items found.</returns>
        private Dictionary<int, string> GetCodeItems(Dictionary<int, string> data)
        {
            // Find all the pure comment lines, we'll use this collection to find all the code lines
            Regex rxComment = new Regex(@";");

            // Clear code and comment lines lists before loading new lines
            if (this.strSLCItems.Keys.Count > 0)
            {
                this.strSLCItems.Clear();
            }

            if (this.strCODEItems.Keys.Count > 0)
            {
                this.strCODEItems.Clear();
            }

            // Find "pure" comment and potential code lines to process
            foreach (int key in strItems.Keys)
            {
                Match m = rxComment.Match(strItems[key]);
                if (m.Success)
                {
                    // If the comment line marker ";" is the first char in the string,
                    // then the line is a comment, else the line is some kind of code.
                    if (m.Index == 0)
                    {
                        strSLCItems.Add(key, strItems[key]);
                    }
                    else
                    {
                        strCODEItems.Add(key, strItems[key]);
                    }
                }
                else
                {
                    strCODEItems.Add(key, strItems[key]);
                }
            }

            // Now we need to find all the subroutines using the <chars>: pattern
            Regex rxCODE = new Regex("[a-zA-Z]");
            Regex rxSubrutine = new Regex(@":");

            string[] subrName;
            int lineNumber = 0;

            // Find all the code lines, then all the sub-routines line and load the items into their
            // respective collections
            foreach (int key in strCODEItems.Keys)
            {
                Match m = rxCODE.Match(strCODEItems[key]);

                if (m.Success)
                {
                    // if the first char is a member of the allowed chars for label,
                    // directive or opcode then the line is code
                    if (m.Index == 0)
                    {
                        // If the current line has a ":" embedded char in it, then it could be a sub-rutine...
                        Match m1 = rxSubrutine.Match(strCODEItems[key]);

                        // If the line has an embedded ";" char also, we need to determine if it comes before the
                        // ":" char, in which case the line is not a subroutine!
                        Match m2 = rxComment.Match(strCODEItems[key]);

                        // If there is not a ";" match, then it is a subroutine
                        if (m1.Success && !m2.Success)
                        {
                            // If the index of the ":" char is greater than 0, then it it could be
                            // a subroutine, check if the ":" char is not after a ";" char
                            if (m1.Index > 0)
                            {
                                subrName = rxSubrutine.Split(strCODEItems[key], 1);
                                lineNumber = key;
                                strSubrutineItems.Add(lineNumber, subrName[0].Substring(0, m1.Index + 1));
                            }
                        }
                        else if (m1.Success && m2.Success)
                        {
                            int nColonIndex = m1.Index;
                            int nSemicolonIndex = m2.Index;

                            if (nSemicolonIndex > nColonIndex)
                            {
                                // The colon precedes the semicolon, so this is subroutine, else is just a code line
                                subrName = rxSubrutine.Split(strCODEItems[key], 1);
                                lineNumber = key;
                                strSubrutineItems.Add(lineNumber, subrName[0].Substring(0, m1.Index + 1));
                            }
                        }
                    }
                    else if (m.Index > 0) // there are leading spaces, so we also list the lines, but make a note of this!
                    {
                        //Console.WriteLine("found code with leading spaces -> {0} code: {1}", key.ToString(), strCODEItems[key]);
                    }
                }
            }

            // update UI
            this.label1.Text = "Done searching for sub-routines! Found " + strSubrutineItems.Keys.Count.ToString() + " subroutines";

            return strSubrutineItems;
        }


        /// <summary>
        /// When the app's form loads, we load a "test" file that shows the app's functionality,
        /// that is accomplished by opening and reading a test file included with the app. Then this
        /// method calls methods to update the UI views.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            // TODO: until we find a true solution or I have time to mess with this shit!
            Form1.CheckForIllegalCrossThreadCalls = false;

            if (this.AppSettings.LastFileUsedPath == "")
            {
                string strTestFilePath = Path.GetDirectoryName(this.AppSettings.AppExecPath) + "\\test.psm";

                // If we can read data from the input file, we update the UI with it
                if (ReadCodeFile(strTestFilePath, ref this.strItems) != Success)
                {
                    //If not we throw an  exception and...
                    this.label1.Text = "Error reading from the test.psm file, please notify the app developer!";
                }
                else
                {
                    // If we have code lines, update UI. else if the file is empty, notify user and wait.
                    if (this.strItems.Keys.Count > 0)
                    {
                        this.txtBox_FileContents.Enabled = true;
                        this.txtBox_FileContents.Visible = true;
                        UpdateCodeFileView();

                        this.dataGridView1.Enabled = true;
                        this.dataGridView1.Visible = true;
                        UpdateCodeSubsView(0);

                        // update UI
                        this.txtBox_PSMFile.Text = strTestFilePath;

                        // Monitor file changes
                        MonitorWorkFile(Path.GetDirectoryName(this.AppSettings.AppExecPath));

                        this.fileDataCurrent = true;
                        this.UpdateFileDataSatus();

                        // wire an event handler to the GridView row selected event
                        this.dataGridView1.SelectionChanged += new EventHandler(dataGridView1_SelectionChanged);
                    }
                    else
                    {
                        MessageBox.Show("The code file is empty, nothing to display!", "Empty Code File", MessageBoxButtons.OK);
                    }
                }
            }
        }

        /// <summary>
        /// Opens then attempts to read a PSM code file, if successful, returns a Dictionary that contains all the
        /// code lines found in the file, when done closes the file.
        /// </summary>
        /// <param name="filePath">The path to the PSM file</param>
        /// <param name="data">Code lines container</param>
        /// <returns>Success or Fail</returns>
        private bool ReadCodeFile(string filePath, ref Dictionary<int, string> data)
        {
            // test is the file exists, then open and read it
            if (File.Exists(filePath))
            {
                try
                {
                    // Open file for reading
                    StreamReader sr = File.OpenText(filePath);

                    using (sr)
                    {
                        // Read file
                        this.label1.Text = "Reading file...";
                        int lineNumber = 0;
                        string input;

                        while ((input = sr.ReadLine()) != null)
                        {
                            data.Add(lineNumber++, input);
                        }

                        // Done, update UI
                        this.label1.Text = "Reading file done, total lines: " + data.Keys.Count;

                    // Since we were able to load a file, update the AppSettings
                    this.AppSettings.LastFileUsedPath = filePath;

                    // Done reading, close the file
                    sr.Close();
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error Opening File" + filePath + "the file may be invalid", "File Open Error! " + ex.Message, MessageBoxButtons.OK);
                    this.AppSettings.LastFileUsedPath = "none";
                    //throw;
                }

            }
            else
            {
                // file does not exist, handle event here
                return Fail;
            }


            return Success;
        }


    }
}