#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import re
import sys
import os


def parse_log_to_sql(input_file):
    if not os.path.exists(input_file):
        print(f"❌ 错误: 文件不存在 - {input_file}")
        return False
    
    base_name = os.path.splitext(input_file)[0]
    output_file = f"{base_name}_output.sql"
    
    print(f"📖 正在读取: {input_file}")
    
    sql_lines = []
    sql_lines.append("CREATE TABLE IF NOT EXISTS user_operation_log (")
    sql_lines.append("    id INTEGER PRIMARY KEY AUTOINCREMENT,")
    sql_lines.append("    time TEXT NOT NULL,")
    sql_lines.append("    userid TEXT")
    sql_lines.append(");")
    sql_lines.append("")
    sql_lines.append("INSERT INTO user_operation_log (time, userid) VALUES")
    
    values = []
    count = 0
    skipped = 0
    
    with open(input_file, 'r', encoding='utf-8') as f:
        for line_num, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            if not re.search(r'[Ss]uccess:', line):
                continue
            
            time_match = re.search(r'(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2},\d{3})', line)
            if not time_match:
                print(f"⚠️  第 {line_num} 行: 无法提取时间，已跳过")
                skipped += 1
                continue
            time_str = time_match.group(1)
            
            userid = ''
            match = re.search(r'[Ss]uccess:\s*(\d+)', line)
            if match:
                userid = match.group(1)
            
            values.append(f"('{time_str}', '{userid}')")
            count += 1
    
    if count == 0:
        print("❌ 未提取到任何有效数据")
        return False
    
    for i, val in enumerate(values):
        if i == len(values) - 1:
            sql_lines.append(f"{val};")
        else:
            sql_lines.append(f"{val},")
    
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write('\n'.join(sql_lines))
    
    print(f"✅ 解析完成！")
    print(f"   📊 提取记录数: {count}")
    print(f"   📄 SQL 文件: {output_file}")
    
    return True


def main():
    if len(sys.argv) < 2:
        print("用法: python parse_log.py <日志文件>")
        print("示例: python parse_log.py log.txt")
        sys.exit(1)
    
    parse_log_to_sql(sys.argv[1])


if __name__ == "__main__":
    main()