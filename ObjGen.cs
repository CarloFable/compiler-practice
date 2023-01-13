using System;
using System.Collections.Generic;
using System.IO;

namespace Complier
{
    public class ObjGen
    {
        private readonly string[] reg_value;
        private static readonly string[] reg = { "eax", "ecx", "edx" };
        private readonly List<(string op, string addr1, string addr2)> addr3;
        private readonly List<(string id, int type, string attribute)> symbol_table;
        private readonly string end_id;
        private List<string> output;

        public ObjGen(List<(string op, string addr1, string addr2)> addr3, List<(string id, int type, string attribute)> symbol_table)
        {
            this.addr3 = addr3;
            this.symbol_table = symbol_table;
            end_id = symbol_table[symbol_table.Count - 1].id;
            reg_value = new string[3];  // EAX ECX EDX
            output = new List<string>();
        }

        private static bool IsNum(string str)
        {
            foreach(char c in str)
            {
                if(!char.IsDigit(c))
                    return false;
            }
            return true;
        }

        // 常数运算直接用结果替换
        private static int ImmCal(string op, int num1, int num2)
        {
            if (op == "+")
                return num1 + num2;
            else if (op == "-")
                return num1 - num2;
            else if (op == "*")
                return num1 * num2;
            else if (op == "/")
                return num1 / num2;
            else
                return 0;
        }

        private void Assign(string id, string regn)
        {
            foreach(var symbol in symbol_table)
            {
                if(symbol.id == id && symbol.type == 22)
                {
                    int idx = Array.IndexOf(reg_value, id);
                    if (idx != -1)
                        reg_value[idx] = null;
                    output.Add($"mov dword ptr [{id}],{regn}");
                    return;
                }
            }
        }
        private void Assign(string id, int num)
        {
            foreach (var symbol in symbol_table)
            {
                if (symbol.id == id && symbol.type == 22)
                {
                    int idx = Array.IndexOf(reg_value, id);
                    if (idx != -1)
                        reg_value[idx] = null;
                    output.Add($"mov dword ptr [{id}],{num}");
                    return;
                }
            }
        }

        private int RegChoose(int cur, int idx1, int idx2)
        { 
            int len = reg_value.Length;
            for(int i = 0; i < len; ++i)
                if (reg_value[i] is null)
                    return i;

            // 淘汰未来最远使用的寄存器值,过时的寄存器值被使用视为未被使用
            len = addr3.Count;
            bool[] flag = { false, false, false };
            bool[] valid = { true, true, true };
            int cnt = 0;
            for (int i = cur; i < len; ++i)
            {
                int idx = Array.IndexOf(reg_value, addr3[i].addr1);
                if (idx != -1 && !flag[idx] && addr3[i].op != "=" && valid[idx])
                {
                    flag[idx] = true;
                    ++cnt;
                }
                else if (idx != -1 && addr3[i].op == "=")
                    valid[idx] = false;

                if (cnt == 2)
                {
                    idx = Array.IndexOf(flag, false);
                    if (reg_value[idx][0] == '(')
                    {
                        for(int j = i; j < len; ++j)
                        {
                            if (addr3[j].op == "=")
                                break;
                            if (addr3[j].addr1 == reg_value[idx] || addr3[j].addr2 == reg_value[idx])
                            {
                                output.Add($"mov dword ptr [{end_id + $"+{(GetIdx(reg_value[idx]) + 1) * 4}"}],{reg[idx]}");
                                break;
                            }
                        }  
                    }
                    return idx;
                }

                idx = Array.IndexOf(reg_value, addr3[i].addr2);
                if (idx != -1 && !flag[idx] && valid[idx])
                {
                    flag[idx] = true;
                    ++cnt;
                }

                if (cnt == 2)
                {
                    idx = Array.IndexOf(flag, false);
                    if (reg_value[idx][0] == '(')
                    {
                        for (int j = i; j < len; ++j)
                        {
                            if (addr3[j].op == "=")
                                break;
                            if (addr3[j].addr1 == reg_value[idx] || addr3[j].addr2 == reg_value[idx])
                            {
                                output.Add($"mov dword ptr [{end_id + $"+{(GetIdx(reg_value[idx]) + 1) * 4}"}],{reg[idx]}");
                                break;
                            }
                        }
                    }
                    return idx;
                }
            }
            if (!flag[idx1])
                return idx1;
            if (!flag[idx2])
                return idx2;
            return Array.IndexOf(flag, false);
        }

        private int GetIdx(string addr) => Convert.ToInt32(addr[1..(addr.Length - 1)]);

        // 满足交换律的二元运算：+ *
        private void OpCalGen(int i, string op)
        {
            int imm = 0;
            int idx1 = Array.IndexOf(reg_value, addr3[i].addr1);
            int idx2 = Array.IndexOf(reg_value, addr3[i].addr2);

            if (idx1 == -1)
            {
                if (IsNum(addr3[i].addr1))
                    imm = Convert.ToInt32(addr3[i].addr1);
                else if (addr3[i].addr1[0] == '(')
                {
                    int idx = GetIdx(addr3[i].addr1);
                    int regn = RegChoose(i, 0, 0);
                    reg_value[regn] = addr3[i].addr1;
                    output.Add($"mov {reg[regn]},dword ptr [{end_id + $"+{(idx + 1) * 4}"}]");
                }
                else
                {
                    int regn = RegChoose(i, 0, 0);
                    reg_value[regn] = addr3[i].addr1;
                    foreach (var (id, type, _) in symbol_table)
                    {
                        if (id == addr3[i].addr1 && type == 22)
                        {
                            output.Add($"mov {reg[regn]},dword ptr [{addr3[i].addr1}]");
                            break;
                        }
                    }
                }
            }

            if (idx2 == -1)
            {
                if (IsNum(addr3[i].addr2))
                    imm = Convert.ToInt32(addr3[i].addr2);
                else if (addr3[i].addr2[0] == '(')
                {
                    int idx = GetIdx(addr3[i].addr2);
                    int regn = RegChoose(i, 0, 0);
                    reg_value[regn] = addr3[i].addr2;
                    output.Add($"mov {reg[regn]},dword ptr [{end_id + $"+{(idx + 1) * 4}"}]");
                }
                else
                {
                    int regn = RegChoose(i, 0, 0);
                    reg_value[regn] = addr3[i].addr2;
                    foreach (var (id, type, _) in symbol_table)
                    {
                        if (id == addr3[i].addr2 && type == 22)
                        {
                            output.Add($"mov {reg[regn]},dword ptr [{addr3[i].addr2}]");
                            break;
                        }
                    }
                }
            }

            idx1 = Array.IndexOf(reg_value, addr3[i].addr1);
            idx2 = Array.IndexOf(reg_value, addr3[i].addr2);

            if (idx1 == -1 || idx2 == -1)
            {
                int result = 0;
                if (idx1 != -1)
                    result = idx1;
                else if (idx2 != -1)
                    result = idx2;
                result = RegChoose(i + 1, result, result);
                reg_value[result] = $"({i})";
                if(idx1 != -1 && result != idx1)
                    output.Add($"mov {reg[result]},{reg[idx1]}");
                else if (idx2 != -1 && result != idx2)
                    output.Add($"mov {reg[result]},{reg[idx2]}");
                output.Add($"{op} {reg[result]},{imm}");
            }
            else
            {
                int result = RegChoose(i + 1, idx1, idx2);
                reg_value[result] = $"({i})";
                if (result == idx1)
                    output.Add($"{op} {reg[result]},{reg[idx2]}");
                else if (result == idx2)
                    output.Add($"{op} {reg[result]},{reg[idx1]}");
                else
                {
                    output.Add($"mov {reg[result]},{reg[idx1]}");
                    output.Add($"{op} {reg[result]},{reg[idx2]}");
                }
            }
        }
        // 不满足交换律的二元运算：- /
        private void OpCalGen1(int i, string op)
        {
            int imm = 0;
            int idx1 = Array.IndexOf(reg_value, addr3[i].addr1);
            int idx2 = Array.IndexOf(reg_value, addr3[i].addr2);

            if (idx1 == -1)
            {
                if (IsNum(addr3[i].addr1))
                    imm = Convert.ToInt32(addr3[i].addr1);
                else if (addr3[i].addr1[0] == '(')
                {
                    int idx = GetIdx(addr3[i].addr1);
                    int regn = RegChoose(i, 0, 0);
                    reg_value[regn] = addr3[i].addr1;
                    output.Add($"mov {reg[regn]},dword ptr [{end_id + $"+{(idx + 1) * 4}"}]");
                }
                else
                {
                    int regn = RegChoose(i, 0, 0);
                    reg_value[regn] = addr3[i].addr1;
                    foreach (var (id, type, _) in symbol_table)
                    {
                        if (id == addr3[i].addr1 && type == 22)
                        {
                            output.Add($"mov {reg[regn]},dword ptr [{addr3[i].addr1}]");
                            break;
                        }
                    }
                }
            }

            if (idx2 == -1)
            {
                if (IsNum(addr3[i].addr2))
                    imm = Convert.ToInt32(addr3[i].addr2);
                else if (addr3[i].addr2[0] == '(')
                {
                    int idx = GetIdx(addr3[i].addr2);
                    int regn = RegChoose(i, 0, 0);
                    reg_value[regn] = addr3[i].addr2;
                    output.Add($"mov {reg[regn]},dword ptr [{end_id + $"+{(idx + 1) * 4}"}]");
                }
                else
                {
                    int regn = RegChoose(i, 0, 0);
                    reg_value[regn] = addr3[i].addr2;
                    foreach (var (id, type, _) in symbol_table)
                    {
                        if (id == addr3[i].addr2 && type == 22)
                        {
                            output.Add($"mov {reg[regn]},dword ptr [{addr3[i].addr2}]");
                            break;
                        }
                    }
                }
            }

            idx1 = Array.IndexOf(reg_value, addr3[i].addr1);
            idx2 = Array.IndexOf(reg_value, addr3[i].addr2);

            if(idx1 == -1)
            {
                int immreg = RegChoose(i, 0, 0);
                output.Add($"mov {reg[immreg]},{imm}");
                reg_value[immreg] = $"({i})";
                output.Add($"{op} {reg[immreg]},{reg[idx2]}");
            }
            else if (idx2 == -1)
            {
                int result = RegChoose(i + 1, idx1, idx1);
                reg_value[result] = $"({i})";
                if(result != idx1)
                    output.Add($"mov {reg[result]},{reg[idx1]}");
                output.Add($"{op} {reg[result]},{imm}");
            }
            else
            {
                int result = RegChoose(i + 1, idx1, idx2);

                if (result == idx1)
                {
                    reg_value[result] = $"({i})";
                    output.Add($"{op} {reg[result]},{reg[idx2]}");
                }
                else if(result == idx2)
                {
                    if (output[^1] == $"mov dword ptr [{end_id + $"+{(GetIdx(reg_value[idx2]) + 1) * 4}"}],{reg[idx2]}")
                        output.RemoveAt(output.Count - 1);
                   
                    if (reg_value[idx1][0] == '(')
                    {
                        int len = addr3.Count;
                        for (int j = i; j < len; ++j)
                        {
                            if (addr3[j].op == "=")
                                break;
                            if (addr3[j].addr1 == reg_value[idx1] || addr3[j].addr2 == reg_value[idx1])
                            {
                                output.Add($"mov dword ptr [{end_id + $"+{(GetIdx(reg_value[idx1]) + 1) * 4}"}],{reg[idx1]}");
                                break;
                            }
                        }
                    }
                    reg_value[idx1] = $"({i})";
                    output.Add($"{op} {reg[idx1]},{reg[idx2]}");
                }
                else
                {
                    reg_value[result] = $"({i})";
                    output.Add($"mov {reg[result]},{reg[idx1]}");
                    output.Add($"{op} {reg[result]},{reg[idx2]}");
                }
            }
        }

        private void Generate()
        {
            int len = addr3.Count;
            for(int i = 0; i < len; ++i)
            {
                if (IsNum(addr3[i].addr1) && IsNum(addr3[i].addr2))
                {
                    int value = ImmCal(addr3[i].op, Convert.ToInt32(addr3[i].addr1), Convert.ToInt32(addr3[i].addr2));
                    for(int j = i + 1; j < len; ++j)
                    {
                        if(addr3[j].addr1[0] == '(')
                            if(GetIdx(addr3[j].addr1) == i)
                                addr3[j] = (addr3[j].op, value.ToString(), addr3[j].addr2);
                        if (addr3[j].addr2[0] == '(')
                            if (GetIdx(addr3[j].addr2) == i)
                                addr3[j] = (addr3[j].op, addr3[j].addr1, value.ToString());
                    }
                    continue;
                }

                if (addr3[i].op == "=")
                {
                    if (IsNum(addr3[i].addr2))
                        Assign(addr3[i].addr1, Convert.ToInt32(addr3[i].addr2));
                    else
                    {
                        int idx = Array.IndexOf(reg_value, addr3[i].addr2);
                        if(idx == -1)
                        {
                            if (addr3[i].addr2[0] == '(')
                            {
                                int idx1 = GetIdx(addr3[i].addr2);
                                int regn = RegChoose(i, 0, 0);
                                reg_value[regn] = addr3[i].addr2;
                                output.Add($"mov {reg[regn]},dword ptr [{end_id + $"+{(idx1 + 1) * 4}"}]");
                            }
                            else
                            {
                                int regn = RegChoose(i, 0, 0);
                                reg_value[regn] = addr3[i].addr2;
                                foreach (var (id, type, _) in symbol_table)
                                {
                                    if (id == addr3[i].addr2 && type == 22)
                                    {
                                        output.Add($"mov {reg[regn]},dword ptr [{addr3[i].addr2}]");
                                        break;
                                    }
                                }
                            }
                        }
                        Assign(addr3[i].addr1, reg[Array.IndexOf(reg_value, addr3[i].addr2)]);
                    }
                }

                else if (addr3[i].op == "+")
                    OpCalGen(i, "add");
                else if (addr3[i].op == "-")
                    OpCalGen1(i, "sub");
                else if (addr3[i].op == "*")
                    OpCalGen(i, "imul");
                // 此处的除法运算生成的x86汇编代码是错误的
                else if (addr3[i].op == "/")
                    OpCalGen1(i, "idiv");
            }
        }

        public void Output(string Objout_path)
        {
            Generate();
            File.WriteAllLines(Objout_path, output);
        }
    }
}
