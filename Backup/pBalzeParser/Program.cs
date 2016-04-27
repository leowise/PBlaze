using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;


// The file type we are parsing, can contain the following types of data
// a. Comments: ;<comment text> comments always start with a semi-colon!
// b. Empty lines?? This can be considered an error!
// c. Directives:
//      1. File generation and input files: i.e. VHDL "ROM_form.vhd", "uclock.vhd", "uclock"
//      2. Constants: <name> <spaces> (EQU, DSIN, DSOUT, etc.) <space> $<hex value> <decimal value> <register name>
//      3. Rutines: first byte should be a char, the string can include letters and the "_" char then may include a comment
//      4. Opcodes: The opcodes are usually part of a sub-rutine in the form <sub-rutine name>: <opcode>
//                                                                                              <opcode>
//                                                                                                  .
//              (other flow control opcodes and possibly more RET!!!
//                                                                                                  .
//                                                                                              RET (the return from the rutine)
// 

// Patterns: 
/*  Knowns: 
 *          All files are stored as bytes? 
 *          All files have a minimu / maximum size
 *          All files end in null
 *          All files have a <file>.<extension>.<extension> format?
 *  All PSM file:
 *          Have single line comments: ;<comment>
 *          Have embedded comments <directive, opcode, sub-rutine, etc><[space]>;<comment>
 *          Have no BLANK lines, this will be considered as an error
 *          
 * 
 * Regex: Key string patterns
 *          SLC - Single line comment Char[0) = ; the semi-colon is the first charcter
 *          EC  - Embedded comment Chars...Char ; Comment, comment can contain more embedded ; characters!
 *          OC  - Opcodes
 *          SR  - Sub-rutines pattern: <ASCI char>: Opcodes ... RETurns 
 *          
 *          
 * 
 * 
 */

// Create a string collection and populate it with text strings from the file's data

namespace pBalzeParser
{
    class Program
    {
        // This helper will read a psm file (in the local directory) store it in memory. Then it
        // will parse the file and extract desired text strings. Then display a list of these strings.
        // It will also monitor the psm file and when it changes (its modified, deleted, etc.) it will:
        // If file was modified, it will re-scan the file and re-parse it and update the list
        // it the file wasa deleted, then it will ask the user if done with the file and extit?

        // PSM file operation flow:
        //              1. Find and open file. 
        //              2. Read file into memory (a string dictionary)
        //              3. Parse the file and extract the desired text using Regex
        //              4. If found text then create a list and update display

        // 
        static void Main(string[] args)
        {
            // Data containers
            // All the strings are stored in this collection, the keys become the line number, and the string contains the 
            // code and comments
            Dictionary<int, string> strItems = new Dictionary<int, string>();
            Dictionary<int, string> strSLCItems = new Dictionary<int, string>();    // Single Line Comments
            Dictionary<int, string> strCODEItems = new Dictionary<int, string>();
            Dictionary<int, string> strDWECItems = new Dictionary<int, string>();   // Directive with embedded comment
            Dictionary<int, string> strOpcodeItems = new Dictionary<int, string>();     // Opcode with no embedded comment
            Dictionary<int, string> srtOWECItems = new Dictionary<int,string>();        // Opcode with embedded comment
            Dictionary<int, string> strSubrutineItems = new Dictionary<int, string>();  // 

            Console.WriteLine(".psn file operations:");

            string FILE_NAME;
            int lineNum = 0;

            FILE_NAME = Console.ReadLine();

            //FILE_NAME = "test_uclock.psm";

            if (!File.Exists(FILE_NAME))
            {
                Console.WriteLine("{0} does not exist.", FILE_NAME);
                return;
            }
            using (StreamReader sr = File.OpenText(FILE_NAME))
            {
                string input;
                while ((input = sr.ReadLine()) != null)
                {
                    strItems.Add(lineNum++, input);

                }
                Console.WriteLine("The end of the file has been reached.");
                sr.Close();

                // File processing info
                Console.WriteLine("Total file lines: {0}", strItems.Keys.Count.ToString());
                //Console.ReadLine();
            }

            /*  Important Regex chars:
             *      Escapes:
             *      \r - carriage return
             *      \n - new line
             *      \x20 - ASCII chars using hex representation (exactly two digits)
             *      
             *      
             *      
             * 
             */

            // Parsing plan is:
            // Pass one: Find all the single line comments, mark such lines as SLC
            // Pass two: Find all directives, mark them
            // Pass three: Find all subrutines, mark them 
            // Pass four: Find all Opcodes, mark them
            // 

            // Test the Regex class!

            // Matches all the Single Line Comment items, these are all the comment lines that
            // don't contain code
            Regex rxComment = new Regex(@";");

            foreach (int key in strItems.Keys)
            {
                Match m = rxComment.Match(strItems[key]);
                if (m.Success)
                {
                    // If the comment marker ; is the first char in the string, then the whole line is a comment
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
                // Show work
                //Console.WriteLine("matching... {0}", strCODEItems[key]);

                Match m = rxCODE.Match(strCODEItems[key]);
                if (m.Success)
                {
                    // if the first char is member of the allowed chars for label, directive or opcode then the line is code
                    if (m.Index == 0)
                    {
                        //Console.WriteLine("found code in line -> {0} code: {1}", key.ToString(), strCODEItems[key]);

                        // lets find out if the current line is a sub-rutine
                        Match m1 = rxSubrutine.Match(strCODEItems[key]);

                        if (m1.Success)
                        {
                            if (m1.Index > 0)
                            {
                                subrName = rxSubrutine.Split(strCODEItems[key], 1);
                                strSubrutineItems.Add(key, subrName[0]);
                                lineNumber = key + 1;
                                Console.WriteLine("Sub-rutine: Line # {0} Name: {1}", lineNumber.ToString(), subrName[0]);
                            }
                        }
                    }
                    else if (m.Index > 0) // TODO: Check this!
                    {
                        //Console.WriteLine("found code with leading spaces -> {0} code: {1}", key.ToString(), strCODEItems[key]);
                    }
                }
            }

        }
    }
}
