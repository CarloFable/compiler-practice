using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Complier
{
    public class GrammarAnalyzer
    {
        private readonly static string lr_newpath = "../../../LR状态分析表.txt";
        private readonly static string[] expression = { "S' -> E'", "E' -> S", "S -> int T", "S -> S , T", "T -> id",
            "T -> E", "E' -> E", "E -> id = A", "A -> A + B", "A -> A - B", "A -> B", "B -> B * C", "B -> B / C",
            "B -> C", "C -> ( A )", "C -> id", "C -> num" };    // 产生式
        // 终结符，与编码表有关，顺序与分析表有关，分析表中的$为token中的;
        private readonly static (int id, string value)[] end = { (22, "int"), (1, "id"), (38, "+"), (39, "-"), (40, "*"),
            (41, "/"), (60, "="), (74, "("), (75, ")"), (73, ","), (2, "num"), (80, ";") };
        // 当前状态，读入终结符，移进/归约，转移状态/归约产生式
        // act=0移进，act=1归约，当act=2,next=-1时为accept
        private readonly List<(int state, int end, int act, int next)> action;
        // 非终结符，顺序与分析表有关
        private readonly static string[] nend = { "S", "T", "V", "E", "A", "B", "C", "E'" };
        // 当前状态，读入非终结符，转移状态
        private readonly List<(int state, int nend, int next)> go2;
        public List<string> output;
        public List<(string id, int type, string attribute)> symbol_table;
        private static int offset = 0;
        public List<(string op, string addr1, string addr2)> addr3;
        private int cur;
        /*文法支持类型声明语句（int），赋值（无符号整数及其四则运算表达式）语句，声明与赋值的复合语句(可多个变量共同声明和分别赋值）
         *例如 int a, b=1, c=2*5; a=b/c; b=0; c=1+b-a;
         */

        public GrammarAnalyzer(string lr_path, List<(string id, int type, string attribute)> symbol_table)
        {
            File.Copy(lr_path, lr_newpath, true);
            Preprocess(lr_newpath);

            action = new List<(int state, int end, int act, int next)>();
            go2 = new List<(int state, int nend, int next)>();
            output = new List<string>();
            this.symbol_table = symbol_table;
            addr3 = new();
            cur = 0;
        }

        // 预处理LR状态分析表
        private static void Preprocess(string path)
        {
            string lr = File.ReadAllText(path);

            int len = expression.Length;
            for (int i = 0; i < len; ++i)
                lr = lr.Replace(expression[i], i.ToString());   // 如果产生式的子式在产生式中将会被错误地替换

            lr = lr.Replace("shift", "s");
            lr = lr.Replace("reduce", "r");

            File.WriteAllText(path, lr);
        }
        // 初始化action和goto表
        private static void InitTable(string path, List<(int, int, int, int)> action, List<(int, int, int)> go2)
        {
            string[] lr = File.ReadAllLines(path);

            int num = lr.Length;
            int end_num = end.Length;

            for(int i = 2; i < num; ++i)
            {
                lr[i] = lr[i].Trim();
                string[] r = lr[i].Split(",");
                int len = r.Length;

                for(int j = 1; j < len; ++j)
                {
                    if (string.IsNullOrEmpty(r[j]))
                        continue;
                    else if (r[j][0] == 's' || (r[j][0] == '"' && r[j][1] == 's'))
                        action.Add((i - 2, end[j - 1].id, 0, Convert.ToInt32(Regex.Match(r[j], "[0-9]+").Value)));
                    else if (r[j][0] == 'r' || (r[j][0] == '"' && r[j][1] == 'r'))
                        action.Add((i - 2, end[j - 1].id, 1, Convert.ToInt32(Regex.Match(r[j], "[0-9]+").Value)));
                    else if (char.IsDigit(r[j][0]))
                        go2.Add((i - 2, j - end_num - 1, Convert.ToInt32(r[j])));
                    else if (r[j] == "accept")
                        action.Add((i - 2, end[j - 1].id, 2, -1));
                }
            }
        }

        private static (int, int) SearchAction(int state, int end, List<(int state, int end, int act, int next)> action)
        {
            foreach (var item in action)
                if (item.state == state && item.end == end)
                    return (item.act, item.next);
            return (-1, -1);    // error
        }

        private static int SearchGo2(int state, int nend, List<(int state, int nend, int next)> go2)
        {
            foreach (var item in go2)
                if (item.state == state && item.nend == nend)
                    return item.next;
            return -1;      // error
        }

        private static List<Queue<(int, string)>> InputPre(List<(int id, string value)> tokens)
        {
            List<Queue<(int, string)>> inputs = new();
            Queue<(int, string)> input = new();

            foreach (var token in tokens)
            {
                input.Enqueue(token);
                // ;
                if(token.id == 80)
                {
                    inputs.Add(input);
                    input = new();
                }
            }

            return inputs;
        }

        // 语义分析，依赖文法
        private string SemAnalyze(int exp, List<string> sem, int cur)
        {
            int len = sem.Count;
            if (exp == 2 || exp == 3)
            {
                int stlen = symbol_table.Count;
                for (int i = 0; i < stlen; ++i)
                {
                    string id = symbol_table[i].id;
                    if (id == sem[0])
                    {
                        string attribute = "?";
                        int type = Array.IndexOf(LexicalAnalyzer.keyword, sem[len - 1]) + 6;    // idx + 6, 与编码表有关
                        // int
                        if (type == 22)
                        {
                            attribute = offset + ",变量,NULL";
                            offset += 4;
                        }
                        symbol_table[i] = (id, type, attribute);
                        return sem[len - 1];
                    }
                }
            }
            else if (exp == 7)
            {
                addr3.Add(("=", sem[len - 1], sem[0]));
                return sem[len - 1];
            }
            else if (exp == 8)
            {
                int idx = addr3.IndexOf(("+", sem[len - 1], sem[0]), cur);
                if (idx != -1)
                    return $"({idx})";
                addr3.Add(("+", sem[len - 1], sem[0]));
                return $"({addr3.Count - 1})";
            }
            else if (exp == 9)
            {
                int idx = addr3.IndexOf(("-", sem[len - 1], sem[0]), cur);
                if (idx != -1)
                    return $"({idx})";
                addr3.Add(("-", sem[len - 1], sem[0]));
                return $"({addr3.Count - 1})";
            }
            else if (exp == 11)
            {
                int idx = addr3.IndexOf(("*", sem[len - 1], sem[0]), cur);
                if (idx != -1)
                    return $"({idx})";
                addr3.Add(("*", sem[len - 1], sem[0]));
                return $"({addr3.Count - 1})";
            }
            else if (exp == 12)
            {
                int idx = addr3.IndexOf(("/", sem[len - 1], sem[0]), cur);
                if (idx != -1)
                    return $"({idx})";
                addr3.Add(("/", sem[len - 1], sem[0]));
                return $"({addr3.Count - 1})";
            }
            else if (exp == 14)
                return sem[1];
            else
                return sem[len - 1];
            return string.Empty;
        }

        private void Analyze(Queue<(int, string)> tokens, List<(int, int, int, int)> action, List<(int, int, int)> go2, List<string> output)
        {
            Stack<(int state, int id, string sem)> analy = new();
            analy.Push((0, 80, ";"));


            while(true)
            {
                (int token, string name) = tokens.Peek();

                (int act, int next) = SearchAction(analy.Peek().state, token, action);
                // shift
                if (act == 0)
                {
                    tokens.Dequeue();
                    analy.Push((next, token, name));
                }
                // reduce
                else if(act == 1)
                {
                    string reduce_exp = expression[next];
                    string[] exp = reduce_exp.Split(" ");
                    int len = exp.Length;
                    List<string> typename = new();
                    for (int i = Array.IndexOf(exp, "->") + 1; i < len; ++i)
                        typename.Add(analy.Pop().sem);
                    int nend_id = Array.IndexOf(nend, exp[0]);
                    analy.Push((SearchGo2(analy.Peek().state, nend_id, go2), -nend_id, SemAnalyze(next, typename, cur)));
                    output.Add(reduce_exp);
                }
                // accept
                else if(act == 2)
                {
                    cur = addr3.Count;
                    output.Add("Accept a sentence.");
                    return;
                }
                // error
                else
                {
                    output.Add("Error!");
                    return;
                }
            }
        }

        public void Output(string gramout_path, string table_path, string addr_path, List<(int, string)> tokens)
        {
            InitTable(lr_newpath, action, go2);

            var inputs = InputPre(tokens);
            foreach(var input in inputs)
                Analyze(input, action, go2, output);
            File.WriteAllLines(gramout_path, output);

            int stc = symbol_table.Count;
            string[] table = new string[stc];
            for (int i = 0; i < stc; ++i)
                table[i] = "(" + symbol_table[i].id + "," + symbol_table[i].type + "," + symbol_table[i].attribute + ")";
            File.WriteAllLines(table_path, table);

            int addrc = addr3.Count;
            string[] addr = new string[addrc];
            for (int i = 0; i < addrc; ++i)
                addr[i] = "(" + addr3[i].op + "," + addr3[i].addr1 + "," + addr3[i].addr2 + ")";
            File.WriteAllLines(addr_path, addr);
        }
    }
}
