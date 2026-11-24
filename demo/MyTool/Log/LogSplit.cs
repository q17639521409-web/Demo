using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Log
{
    class LogSplit
    {
        public static void RunLogSplit(string args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("用法: dotnet run -- <日志文件>");
                Console.WriteLine("示例: dotnet run -- log.txt");
                return;
            }

            string inputFile = args;
            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"❌ 错误: 文件不存在 - {inputFile}");
                return;
            }

            string outputFile = Path.GetFileNameWithoutExtension(inputFile) + "_output.sql";
            
            Console.WriteLine($"📖 正在读取: {inputFile}");

            var sqlLines = new List<string>();
            sqlLines.Add("CREATE TABLE IF NOT EXISTS user_operation_log (");
            sqlLines.Add("    id INTEGER PRIMARY KEY AUTOINCREMENT,");
            sqlLines.Add("    time TEXT NOT NULL,");
            sqlLines.Add("    userid TEXT");
            sqlLines.Add(");");
            sqlLines.Add("");
            sqlLines.Add("INSERT INTO user_operation_log (time, userid) VALUES");

            var values = new List<string>();
            int count = 0;
            int skipped = 0;
            int lineNum = 0;

            string[] lines = File.ReadAllLines(inputFile, Encoding.UTF8);
            
            foreach (string line in lines)
            {
                lineNum++;
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;
                
                if (!Regex.IsMatch(trimmedLine, @"[Ss]uccess:")) continue;

                var timeMatch = Regex.Match(trimmedLine, @"(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2},\d{3})");
                if (!timeMatch.Success)
                {
                    Console.WriteLine($"⚠️  第 {lineNum} 行: 无法提取时间，已跳过");
                    skipped++;
                    continue;
                }
                string timeStr = timeMatch.Groups[1].Value;

                string userId = "";
                var userMatch = Regex.Match(trimmedLine, @"[Ss]uccess:\s*(\d+)");
                if (userMatch.Success)
                {
                    userId = userMatch.Groups[1].Value;
                }

                values.Add($"('{timeStr}', '{userId}')");
                count++;
            }

            if (count == 0)
            {
                Console.WriteLine("❌ 未提取到任何有效数据");
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (i == values.Count - 1)
                    sqlLines.Add(values[i] + ";");
                else
                    sqlLines.Add(values[i] + ",");
            }

            File.WriteAllLines(outputFile, sqlLines, Encoding.UTF8);
            
            Console.WriteLine($"✅ 解析完成！");
            Console.WriteLine($"   📊 提取记录数: {count}");
            Console.WriteLine($"   ⚠️  跳过行数: {skipped}");
            Console.WriteLine($"   📄 SQL 文件: {outputFile}");
        }
    }
}