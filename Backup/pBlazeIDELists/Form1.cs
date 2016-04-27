using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;

/***************************************************************************************************************************
 * Feture request:
 *      1. Load a test file at startup, if no file has been loaded before. 
 *      2. When user selects a sub, find it in the program and highligth it.
 *      3. App operating state user feedback
 *      4. Remove view buttons, app will have a file loaded, unless the file is changing!
 *      5. Add file changing detection and response
 *      6. 
 *      
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 **************************************************************************************************************************/

namespace pBlazeIDELists
{
    public partial class Form1 : Form
    {

        bool Success = false;
        bool Fail = true;

        //private string strPSMFileName;
        //private FileStream psmFileStream;

        // Will hold the user settable items including:
        //  1. Application's path
        //  2. Open last used file?
        //  3. 
        private pBlazeIDELists.Properties.Settings AppSettings = new pBlazeIDELists.Properties.Settings();

        // Data containers
        // All the strings are stored in this collection, the keys become the line number, and the string contains the 
        // code and comments
        private Dictionary<int, string> strItems = new Dictionary<int, string>();               // All strings found in input file
        private Dictionary<int, string> strSLCItems = new Dictionary<int, string>();            // Single Line Comments 
        private Dictionary<int, string> strCODEItems = new Dictionary<int, string>();           // Code Items other than sub + code items
        private Dictionary<int, string> strDWECItems = new Dictionary<int, string>();           // Directive with embedded comment
        private Dictionary<int, string> strOpcodeItems = new Dictionary<int, string>();         // Opcode with no embedded comment
        private Dictionary<int, string> srtOWECItems = new Dictionary<int, string>();           // Opcode with embedded comment
        private Dictionary<int, string> strSubrutineItems = new Dictionary<int, string>();      // All the subrutines found

        public Form1()
        {
            InitializeComponent();

            // This application executable path, we'll use this to find the test data file.
            this.AppSettings.AppExecPath = Application.ExecutablePath;

            // Make sure we start with empty UI items
            // Code file UI items
            this.txtBox_FileContents.Enabled = false;
            this.txtBox_FileContents.Visible = false;

            // Subs UI Items
            this.dataGridView1.Enabled = false;
            this.dataGridView1.Visible = false;


        }

        private void openFileMenuItem_Click(object sender, EventArgs e)
        {
            // show the dialog
            this.openPSMFileDlg.ShowDialog();
        }



        private void openPSMFileDlg_FileOk(object sender, CancelEventArgs e)
        {

            this.label1.Text = "psm file selected: " + this.openPSMFileDlg.FileName;
            
            // Empty dictionaries, clear data, prepare to recieve new data
            this.strItems.Clear();
            this.strSubrutineItems.Clear();
            this.txtBox_FileContents.Visible = false;
            this.dataGridView1.Visible = false;

            // if the file is a valid psm file TODO: Find a way to vailidate file?
                            // If we can read data from the input file, we update the UI with it
            if (ReadCodeFile(this.openPSMFileDlg.FileName, ref this.strItems) != Success)
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

                    UpdateCodeFileView();

                    this.txtBox_FileContents.Visible = true;

                    // 
                    this.dataGridView1.Enabled = true;

                    UpdateCodeSubsView();

                    this.dataGridView1.Visible = true;
                }
                else
                {
                    MessageBox.Show("The code file is empty, nothing to display!", "Empty Code File", MessageBoxButtons.OK);
                }
            }
            
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("pBalze IDE Lists - Show info that we need!", "pBlazeIDEList About", MessageBoxButtons.OK);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // User has selected to terminate the app
            //this.psmFileStream.Close();

            this.Close();
        }

        /// <summary>
        /// Updates the UI when data is ready for displaying to the user.
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

        private void UpdateCodeSubsView()
        {
            // Scan data and find all sub-rutines then update UI 
            this.label1.Text = "Searching code file for sub-rutines...";

            BindingSource bs = new BindingSource();
            
            //Process data and extract sub-rutine lines
            this.strSubrutineItems = GetCodeItems(strItems);

            if (this.strSubrutineItems.Keys.Count == 0)
            {
                MessageBox.Show("No subrutines found in selected file.", "Not data found!", MessageBoxButtons.OK);
            }
            else
            {
                
                bs.DataSource = this.strSubrutineItems;
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
        /// <returns></returns>
        private Dictionary<int, string> GetCodeItems(Dictionary<int, string> data)
        {
            // Find all the pure comment lines, we'll use this collection to find all the code lines
            Regex rxComment = new Regex(@";");

            foreach (int key in strItems.Keys)
            {
                Match m = rxComment.Match(strItems[key]);
                if (m.Success)
                {
                    // If the comment line marker ";" is the first char in the string, then the whole line is a comment
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
                    // no match, the line does not start with ";" character AND does not have an embedded ";" character,
                    // so we add it to our CODE collection
                    strCODEItems.Add(key, strItems[key]);
                }
            }

            // Now we need to find all the rutines using the <chars>: pattern
            Regex rxCODE = new Regex("[a-zA-Z]");
            Regex rxSubrutine = new Regex(":");

            string[] subrName;
            int lineNumber = 0;

            foreach (int key in strCODEItems.Keys)
            {
                // Find all the code lines, then all the sub-rutines line and load the items into their
                // respective collections
                Match m = rxCODE.Match(strCODEItems[key]);

                if (m.Success)
                {
                    // if the first char is member of the allowed chars for label, directive or opcode then the line is code
                    if (m.Index == 0)
                    {
                        // If the current line has a ";" sub-rutine char in it, then it may be a sub-rutine...
                        Match m1 = rxSubrutine.Match(strCODEItems[key]);

                        if (m1.Success)
                        {
                            // If the index of the ";" char is greater than 0, then it is a sub-rutine, so we add it to 
                            // the collection
                            if (m1.Index > 0)
                            {
                                subrName = rxSubrutine.Split(strCODEItems[key], 1);
                                strSubrutineItems.Add(key, subrName[0]);
                                lineNumber = key + 1;
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
            this.label1.Text = "Done searching for sub-rutines! Found " + strSubrutineItems.Keys.Count.ToString() + " sub-rutines";

            return strSubrutineItems;

        }

        private void form1BindingSource_CurrentChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Form has loaded: 
            // 1. If there is file history, test if file is available, then ask user if we should open it, if 
            //      setting "open last file" is set to false
            // 2. 

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

                        // 
                        this.dataGridView1.Enabled = true;
                        this.dataGridView1.Visible = true;
                        UpdateCodeSubsView();
                    }
                    else
                    {
                        MessageBox.Show("The code file is empty, nothing to display!", "Empty Code File", MessageBoxButtons.OK);
                    }

                }
            }

        }

        private bool ReadCodeFile(string filePath, ref Dictionary<int, string> data)
        {
            // Opens then reads a code data file, then if successful, returns a Dictionary that contains all the 
            // code lines found in the file.

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
                    }

                    // Done reading, close the file
                    sr.Close();

                    // Since we were able to load a file, update the AppSettings
                    this.AppSettings.LastFileUsedPath = filePath;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error Openning File" + filePath + "the file may be invalid", "File Open Error! " + ex.Message, MessageBoxButtons.OK);
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
