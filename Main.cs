using Complier;
using System;
using System.IO;

LexicalAnalyzer la = new("../../../lex-input-code.txt");
la.Output("../../../lex-output-token.txt", "../../../lex-output-tableWords.txt");

GrammarAnalyzer ga = new("../../../LR状态分析表_原表.csv", la.symbol_table);
ga.Output("../../../gram-output.txt", "../../../sem-output-tableWords.txt", "../../../sem-output-3Addr.txt", la.tokens);

ObjGen og = new(ga.addr3, ga.symbol_table);
og.Output("../../../obj-output.txt");

string readme = File.ReadAllText("../../../readme.txt");
Console.WriteLine(readme);

namespace Complier
{
    class Main
    {

    }
}
