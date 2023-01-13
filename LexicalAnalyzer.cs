using System;
using System.Collections.Generic;
using System.IO;

namespace Complier
{
    // 关联编码表
    public class LexicalAnalyzer
    {
        public readonly static string[] keyword = {"auto", "break", "case", "char", "const", "continue",
            "default", "do", "double", "else", "enum", "extern", "float", "for", "goto", "if", "int", "long",
            "register", "return", "short", "signed", "sizeof", "static", "struct", "switch", "typedef",
            "union", "unsigned", "void", "volatile", "while"};
        private readonly static string[] symbol = {"+", "-", "*", "/", "%", "++", "--", ">", "<", "==", ">=",
            "<=", "!=", "&&", "||", "!", "&", "|", "~", "^", "<<", ">>", "=", "+=", "-=", "*=", "/=", "%=", "&=",
            "|=", "^=", "<<=", ">>=", "?", ":", ",", "(", ")", "[", "]", "->", ".", ";", "{", "}", "\\", "#",
            "'", "\"", "未定义单词"};
        /*此处注意，由于算法缘故，词法分析会认为p+++q为p ++ + q，而不是p + ++ q，诸如此类的情况最好是代码加括号
         *此处注意，由于算法缘故，词法分析会认为p++3为p ++ 3，而不是p + + 3，诸如此类的情况最好是代码加括号
         */
        private readonly string code;
        private readonly int code_len;
        public List<(int key, string value)> tokens;
        // attribute: 符号种类,地址,获取扩展属性的方法
        public List<(string id, int type, string attribute)> symbol_table;

        public LexicalAnalyzer(string path)
        {
            code = File.ReadAllText(path);
            code_len = code.Length;
            tokens = new List<(int key, string value)>();
            symbol_table = new List<(string id, int type, string attribute)>();
        }

        private delegate bool tokenMatchCondition(char c);
        // 单词匹配(标识符、整数、浮点数）
        private static string TokenMatch(ref int i, string code, int len, tokenMatchCondition cond)
        {
            string token = code[i].ToString();

            ++i;
            while (i < len && cond(code[i]))
            {
                token = string.Concat(token, code[i].ToString());
                ++i;
            }
            --i;

            return token;
        }
        // 字符、字符串常数分析
        private static void StrAnalyze(ref int i, string code, int len, List<(int, string)> tokens, char c, int type_c, int type)
        {
            int start = i;
            int end = i;

            ++i;
            while (i < len && code[i] != '\n')
            {
                if (code[i - 1] != '\\' && code[i] == c)
                {
                    end = i;
                    break;
                }
                ++i;
            }
            i = end;

            // 一行中仅出现一次c（或者另一个c前有\；c可以是'/"）
            if (start == end)
                tokens.Add((type_c, code[end].ToString()));     // c
            // 一行中c成对
            else
            {
                string token = code[start].ToString();
                for (int j = start + 1; j <= end; ++j)
                    token = string.Concat(token, code[j].ToString());
                tokens.Add((type, token));      // 字符/字符串常数
            }
        }

        // 词法分析
        private static void Analyze(string code, int len, List<(int, string)> tokens, List<(string, int, string)> symbol_table)
        {
            for (int i = 0; i < len; ++i)
            {
                // 空白符跳过
                if (char.IsWhiteSpace(code[i]))
                    continue;
                // 标识符(可能是关键字),字母或下划线开头的字母、数字、下划线混合串，可以是纯字母或纯下划线
                else if (char.IsLetter(code[i]) || code[i] == '_')
                {
                    string token = TokenMatch(ref i, code, len, (c) => char.IsLetterOrDigit(c) || c == '_');   // 匹配标识符（可能是关键字）

                    int idx4keyword = Array.IndexOf(keyword, token);
                    if (idx4keyword > -1)
                        tokens.Add((idx4keyword + 6, token));   // 关键字，idx+6与编码表有关
                    else
                    {
                        tokens.Add((1, token));     // 标识符
                        // 符号表中无同名标识符则加入
                        (string, int, string) word = (token, -1, "?");
                        if (symbol_table.IndexOf(word) == -1)
                            symbol_table.Add(word);
                    }
                }
                // 无符号整数或浮点数（小数点前如果是0，此处不可省略，点后全0可省），与+-结合的正负数情况由语法分析处理
                else if (char.IsDigit(code[i]))
                {
                    bool dot = false;
                    string dot_right = string.Empty;

                    static bool cond(char c) => char.IsDigit(c);
                    string token = TokenMatch(ref i, code, len, cond);   // 匹配整数值

                    ++i;
                    if (i < len && code[i] == '.')      // 匹配浮点数
                    {
                        dot = true;
                        dot_right = TokenMatch(ref i, code, len, cond);     // 匹配.小数值或.
                        ++i;
                    }
                    --i;

                    if (!dot)
                        tokens.Add((2, token));     // 无符号整数
                    else
                        tokens.Add((3, string.Concat(token, dot_right)));   // 无符号浮点数
                }
                // 省略小数点前0的无符号浮点数,点后全0不可省
                else if (i + 1 < len && code[i] == '.' && char.IsDigit(code[i + 1]))
                {
                    static bool cond(char c) => char.IsDigit(c);
                    string token = TokenMatch(ref i, code, len, cond);   // 匹配.小数值
                    tokens.Add((3, token));     // 无符号浮点数
                }
                // 字符常数（''中多个字符情况单词值原样输出，由语法分析处理；不可分行）
                else if (code[i] == '\'')
                    StrAnalyze(ref i, code, len, tokens, '\'', 85, 4);
                // 字符串常数（不可分行）
                else if (code[i] == '"')
                    StrAnalyze(ref i, code, len, tokens, '"', 86, 5);
                // //行注释跳过
                else if (i + 1 < len && code[i] == '/' && code[i + 1] == '/')
                    while (i < len && code[i] != '\n')
                        ++i;
                // /**/注释跳过
                else if (i + 1 < len && code[i] == '/' && code[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < len && !(code[i] == '*' && code[i + 1] == '/'))
                        ++i;
                    ++i;
                }
                // 符号（运算符，分界符等）
                else
                {
                    string token = code[i].ToString();
                    int idx3 = -1;
                    int idx2 = -1;
                    int idx1 = -1;

                    // 三连符
                    if (i + 2 < len)
                        idx3 = Array.IndexOf(symbol, string.Concat(token, code[i + 1].ToString(), code[i + 2].ToString()));
                    // 双符
                    if (idx3 == -1 && i + 1 < len)
                        idx2 = Array.IndexOf(symbol, string.Concat(token, code[i + 1].ToString()));
                    // 单符
                    if (idx2 == -1)
                        idx1 = Array.IndexOf(symbol, token);

                    // 符号，idx+38与编码表有关
                    if (idx3 > -1)
                        tokens.Add((idx3 + 38, symbol[idx3]));
                    else if (idx2 > -1)
                        tokens.Add((idx2 + 38, symbol[idx2]));
                    else if (idx1 > -1)
                        tokens.Add((idx1 + 38, symbol[idx1]));
                    else
                        tokens.Add((87, "未定义单词"));
                }
            }
        }

        // 分析结果输出
        public void Output(string token_path, string table_path)
        {
            Analyze(code, code_len, tokens, symbol_table);

            int tc = tokens.Count;
            int stc = symbol_table.Count;
            string[] token = new string[tc];
            string[] table = new string[stc];
            for (int i = 0; i < tc; ++i)
                token[i] = "(" + tokens[i].key + "," + tokens[i].value + ")";
            for (int i = 0; i < stc; ++i)
                table[i] = "(" + symbol_table[i].id + "," + symbol_table[i].type + "," + symbol_table[i].attribute + ")";

            File.WriteAllLines(token_path, token);      // token串
            File.WriteAllLines(table_path, table);      // 符号表
        }
    }
}
