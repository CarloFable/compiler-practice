C#
.NET 6.0
Visual Studio 2022

整个编译程序可翻译int类型声明和复合声明，赋值，四则运算（无符号整数）

词法分析：LexicalAnalyzer.cs
输入：源代码lex-input-code.txt
输出：单词序列lex-output-token.txt（依赖编码表）
符号表lex-output-tableWords.txt

语法分析、语义分析和中间代码生成：GrammarAnalyzer.cs
输入：LR状态分析表_原表.csv
（转换为LR状态分析表.txt，转换依赖文法编码）
（原表生成依赖于文法和编译工作台导出xlsx文件，然后在excel中另存为csv文件）
单词序列lex-output-token.txt
输出：产生式序列gram-output.txt
符号表sem-output-tableWords.txt
三地址码sem-output-3Addr.txt

目标代码生成：ObjGen.cs
输入：符号表sem-output-tableWords.txt
三地址码sem-output-3Addr.txt
输出：目标代码obj-output.txt（x86汇编 代码段）
源代码中变量应先声明后使用，否则目标代码会缺失访存取值到寄存器的指令
非常数的除法运算，生成的x86汇编代码有误