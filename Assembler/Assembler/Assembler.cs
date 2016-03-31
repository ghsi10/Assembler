using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assembler
{
    public class Assembler
    {
        private const int WORD_SIZE = 16;

        private Dictionary<string, int[]> m_dControl, m_dJmp; //these dictionaries map command mnemonics to machine code - they are initialized at the bottom of the class

        //more data structures here (symbol map, ...)
        private Dictionary<string, int[]> m_DestDictionary;
        private Dictionary<string, int> m_SymbolDictionary;

        public Assembler()
        {
            InitCommandDictionaries();
        }

        //this method is called from the outside to run the assembler translation
        public void TranslateAssemblyFile(string sInputAssemblyFile, string sOutputMachineCodeFile)
        {
            //read the raw input, including comments, errors, ...
            StreamReader sr = new StreamReader(sInputAssemblyFile);
            List<string> lLines = new List<string>();
            while (!sr.EndOfStream)
            {
                lLines.Add(sr.ReadLine());
            }
            sr.Close();
            //translate to machine code
            List<string> lTranslated = TranslateAssemblyFile(lLines);
            //write the output to the machine code file
            StreamWriter sw = new StreamWriter(sOutputMachineCodeFile);
            foreach (string sLine in lTranslated)
                sw.WriteLine(sLine);
            sw.Close();
        }

        //translate assembly into machine code
        private List<string> TranslateAssemblyFile(List<string> lLines)
        {
            //init data structures here 
            m_SymbolDictionary = new Dictionary<string, int>();
            m_SymbolDictionary["SCREEN"] = 16384;
            m_SymbolDictionary["KEYBOARD"] = 24576;
            m_SymbolDictionary["R0"] = 0;
            m_SymbolDictionary["R1"] = 1;
            m_SymbolDictionary["R2"] = 2;
            m_SymbolDictionary["R3"] = 3;
            m_SymbolDictionary["R4"] = 4;
            m_SymbolDictionary["R5"] = 5;
            m_SymbolDictionary["R6"] = 6;
            m_SymbolDictionary["R7"] = 7;
            m_SymbolDictionary["R8"] = 8;
            m_SymbolDictionary["R9"] = 9;
            m_SymbolDictionary["R10"] = 10;
            m_SymbolDictionary["R11"] = 11;
            m_SymbolDictionary["R12"] = 12;
            m_SymbolDictionary["R13"] = 13;
            m_SymbolDictionary["R14"] = 14;
            m_SymbolDictionary["R15"] = 15;

            //expand the macros
            List<string> lAfterMacroExpansion = ExpendMacros(lLines);

            //first pass - create symbol table and remove lable lines
            List<string> lAfterFirstPass = FirstPass(lAfterMacroExpansion);

            //second pass - replace symbols with numbers, and translate to machine code
            List<string> lAfterSecondPass = SecondPass(lAfterFirstPass);
            return lAfterSecondPass;
        }


        //expand all macros
        private List<string> ExpendMacros(List<string> lLines)
        {
            List<string> lAfterExpansion = new List<string>();
            for (int i = 0; i < lLines.Count; i++)
            {
                //remove all redudant characters
                string sLine = CleanWhiteSpacesAndComments(lLines[i]);
                if (sLine == "")
                    continue;
                //if the line contains a macro, expand it, otherwise the line remains the same
                List<string> lExpanded = ExapndMacro(sLine);
                //we may get multiple lines from a macro expansion
                foreach (string sExpanded in lExpanded)
                {
                    lAfterExpansion.Add(sExpanded);
                }
            }
            return lAfterExpansion;
        }

        //expand a single macro line
        private List<string> ExapndMacro(string sLine)
        {
            List<string> lExpanded = new List<string>();

            if (IsCCommand(sLine))
            {
                string sDest, sCompute, sJmp;
                GetCommandParts(sLine, out sDest, out sCompute, out sJmp);
                //your code here - check for indirect addessing and for jmp shortcuts
                if (sLine.Contains("["))
                {
                    int startIndex = sLine.IndexOf("[") + 1;
                    int endIndex = sLine.IndexOf("]") - 1;
                    string x = sLine.Substring(startIndex, endIndex - startIndex + 1);
                    if (sDest.Contains("[") && sCompute.Contains("["))
                    {
                        //m[x]=m[x]+1
                        //@X
                        //M=M+1
                        string new1 = "@" + x;
                        string comp = sCompute.Substring(sCompute.IndexOf("]") + 1);
                        string new2 = "M=M" + comp;
                        lExpanded.Add(new1);
                        lExpanded.Add(new2);
                    }
                    else if (sDest.Contains("["))
                    {
                        //M[x]=D:
                        // @x 
                        // M=D
                        String new1 = "@" + x;
                        String new2 = "M=" + sCompute;
                        lExpanded.Add(new1);
                        lExpanded.Add(new2);
                    }
                    else if (sCompute.Contains("["))
                    {
                        //D=m[x]
                        //@x
                        //D=M
                        String new1 = "@" + x;
                        String new2 = sDest + "=M";
                        lExpanded.Add(new1);
                        lExpanded.Add(new2);
                    }
                }
                if (sJmp.Contains(":"))
                {
                    //0;jmp:loop 
                    // @loop
                    // 0;jmp
                    string new1 = "@" + sLine.Substring(sLine.IndexOf(":") + 1);
                    int locationOfDot = sLine.IndexOf(':');
                    String new2 = sLine.Substring(0, locationOfDot);
                    lExpanded.Add(new1);
                    lExpanded.Add(new2);
                }
            }
            if (lExpanded.Count == 0)
                lExpanded.Add(sLine);
            return lExpanded;
        }

        //first pass - record all symbols - labels and variables
        private List<string> FirstPass(List<string> lLines)
        {
            int cRealLines = 0;
            string sLine = "";
            int location = 16;
            List<string> lAfterPass = new List<string>();
            for (int i = 0; i < lLines.Count; i++)
            {
                sLine = lLines[i];
                if (IsLabelLine(sLine))
                    m_SymbolDictionary[sLine.Substring(1, sLine.Length - 2)] = cRealLines;
                else cRealLines = cRealLines + 1;
            }

            for (int i = 0; i < lLines.Count; i++)
            {
                sLine = lLines[i];
                if (IsLabelLine(sLine))
                {
                    continue;
                }
                else if (IsACommand(sLine))
                {
                    //may contain a variable
                    string stringToNum = sLine.Substring(1); // cleaning the @ from A command
                    int number;
                    bool isNumber = int.TryParse(stringToNum, out number);
                    if (!isNumber) // if is symbol
                    {
                        if (!m_SymbolDictionary.Keys.Contains(stringToNum))//if the symbol isn't in dicationary yet
                        {
                            m_SymbolDictionary[stringToNum] = location;// from 16 
                            location++;
                        }
                    }
                }
                else if (IsCCommand(sLine))
                {
                    //do nothing here
                }
                else
                    throw new FormatException("Cannot parse line " + i + ": " + lLines[i]);
                lAfterPass.Add(sLine);
                cRealLines++;
            }
            return lAfterPass;
        }

        //second pass - translate lines into machine code, replaicng symbols with numbers
        private List<string> SecondPass(List<string> lLines)
        {
            string sLine = "";
            List<string> lAfterPass = new List<string>();
            for (int i = 0; i < lLines.Count; i++)
            {
                sLine = lLines[i];
                if (IsACommand(sLine))
                {
                    //translate an A command into a sequence of bits
                    string answer = "";
                    string ConvertToNumber = sLine.Substring(1);
                    int number;
                    if (int.TryParse(ConvertToNumber, out number)) //check if it is number
                    {
                        int num1 = System.Convert.ToInt32(ConvertToNumber);
                        answer = ToBinary(num1);
                    }
                    else
                    {
                        answer = ToBinary(m_SymbolDictionary[ConvertToNumber]);//otherwise it is word from smbol dictionary
                    }
                    lAfterPass.Add(answer);
                }
                else if (IsCCommand(sLine))
                {
                    string sDest, sControl, sJmp;
                    GetCommandParts(sLine, out sDest, out sControl, out sJmp);
                    //translate an C command into a sequence of bits
                    string answer = "111";
                    InitCommandDictionaries();
                    answer = answer + ToString(m_dControl[sControl]) + ToString(m_DestDictionary[sDest]) + ToString(m_dJmp[sJmp]);
                    lAfterPass.Add(answer);
                }
                else
                    throw new FormatException("Cannot parse line " + i + ": " + lLines[i]);
            }
            return lAfterPass;
        }

        //helper functions for translating numbers or bits into strings
        private string ToString(int[] aBits)
        {
            string sBinary = "";
            for (int i = 0; i < aBits.Length; i++)
                sBinary += aBits[i];
            return sBinary;
        }

        private string ToBinary(int x)
        {
            string sBinary = "";
            for (int i = 0; i < WORD_SIZE; i++)
            {
                sBinary = (x % 2) + sBinary;
                x = x / 2;
            }
            return sBinary;
        }


        //helper function for splitting the various fields of a C command
        private void GetCommandParts(string sLine, out string sDest, out string sControl, out string sJmp)
        {
            if (sLine.Contains('='))
            {
                int idx = sLine.IndexOf('=');
                sDest = sLine.Substring(0, idx);
                sLine = sLine.Substring(idx + 1);
            }
            else
                sDest = "";
            if (sLine.Contains(';'))
            {
                int idx = sLine.IndexOf(';');
                sControl = sLine.Substring(0, idx);
                sJmp = sLine.Substring(idx + 1);

            }
            else
            {
                sControl = sLine;
                sJmp = "";
            }
        }

        private bool IsCCommand(string sLine)
        {
            return !IsLabelLine(sLine) && sLine[0] != '@';
        }

        private bool IsACommand(string sLine)
        {
            return sLine[0] == '@';
        }

        private bool IsLabelLine(string sLine)
        {
            if (sLine.StartsWith("(") && sLine.EndsWith(")"))
                return true;
            return false;
        }

        private string CleanWhiteSpacesAndComments(string sDirty)
        {
            string sClean = "";
            for (int i = 0; i < sDirty.Length; i++)
            {
                char c = sDirty[i];
                if (c == '/' && i < sDirty.Length - 1 && sDirty[i + 1] == '/') // this is a comment
                    return sClean;
                if (c > ' ' && c <= '~')//ignore white spaces
                    sClean += c;
            }
            return sClean;
        }


        private void InitCommandDictionaries()
        {

            m_dControl = new Dictionary<string, int[]>();

            m_dControl["0"] = new int[] { 0, 1, 0, 1, 0, 1, 0 };
            m_dControl["1"] = new int[] { 0, 1, 1, 1, 1, 1, 1 };
            m_dControl["-1"] = new int[] { 0, 1, 1, 1, 0, 1, 0 };
            m_dControl["D"] = new int[] { 0, 0, 0, 1, 1, 0, 0 };
            m_dControl["A"] = new int[] { 0, 1, 1, 0, 0, 0, 0 };
            m_dControl["!D"] = new int[] { 0, 0, 0, 1, 1, 0, 1 };
            m_dControl["!A"] = new int[] { 0, 1, 1, 0, 0, 0, 1 };
            m_dControl["-D"] = new int[] { 0, 0, 0, 1, 1, 1, 1 };
            m_dControl["-A"] = new int[] { 0, 1, 1, 0, 0, 1, 1 };
            m_dControl["D+1"] = new int[] { 0, 0, 1, 1, 1, 1, 1 };
            m_dControl["A+1"] = new int[] { 0, 1, 1, 0, 1, 1, 1 };
            m_dControl["D-1"] = new int[] { 0, 0, 0, 1, 1, 1, 0 };
            m_dControl["A-1"] = new int[] { 1, 1, 0, 0, 0, 1, 0 };
            m_dControl["D+A"] = new int[] { 0, 0, 0, 0, 0, 1, 0 };
            m_dControl["D-A"] = new int[] { 0, 0, 1, 0, 0, 1, 1 };
            m_dControl["A-D"] = new int[] { 0, 0, 0, 0, 1, 1, 1 };
            m_dControl["D&A"] = new int[] { 0, 0, 0, 0, 0, 0, 0 };
            m_dControl["D|A"] = new int[] { 0, 0, 1, 0, 1, 0, 1 };

            m_dControl["M"] = new int[] { 1, 1, 1, 0, 0, 0, 0 };
            m_dControl["!M"] = new int[] { 1, 1, 1, 0, 0, 0, 1 };
            m_dControl["-M"] = new int[] { 1, 1, 1, 0, 0, 1, 1 };
            m_dControl["M+1"] = new int[] { 1, 1, 1, 0, 1, 1, 1 };
            m_dControl["M-1"] = new int[] { 1, 1, 1, 0, 0, 1, 0 };
            m_dControl["D+M"] = new int[] { 1, 0, 0, 0, 0, 1, 0 };
            m_dControl["D-M"] = new int[] { 1, 0, 1, 0, 0, 1, 1 };
            m_dControl["M-D"] = new int[] { 1, 0, 0, 0, 1, 1, 1 };
            m_dControl["D&M"] = new int[] { 1, 0, 0, 0, 0, 0, 0 };
            m_dControl["D|M"] = new int[] { 1, 0, 1, 0, 1, 0, 1 };


            m_dJmp = new Dictionary<string, int[]>();

            m_dJmp[""] = new int[] { 0, 0, 0 };
            m_dJmp["JGT"] = new int[] { 0, 0, 1 };
            m_dJmp["JEQ"] = new int[] { 0, 1, 0 };
            m_dJmp["JGE"] = new int[] { 0, 1, 1 };
            m_dJmp["JLT"] = new int[] { 1, 0, 0 };
            m_dJmp["JNE"] = new int[] { 1, 0, 1 };
            m_dJmp["JLE"] = new int[] { 1, 1, 0 };
            m_dJmp["JMP"] = new int[] { 1, 1, 1 };

            m_DestDictionary = new Dictionary<string, int[]>();
            m_DestDictionary[""] = new int[] { 0, 0, 0 };
            m_DestDictionary["M"] = new int[] { 0, 0, 1 };
            m_DestDictionary["D"] = new int[] { 0, 1, 0 };
            m_DestDictionary["MD"] = new int[] { 0, 1, 1 };
            m_DestDictionary["A"] = new int[] { 1, 0, 0 };
            m_DestDictionary["AM"] = new int[] { 1, 0, 1 };
            m_DestDictionary["AD"] = new int[] { 1, 1, 0 };
            m_DestDictionary["AMD"] = new int[] { 1, 1, 1 };
        }
    }
}
