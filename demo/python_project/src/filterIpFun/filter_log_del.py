#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import re
import sys
import glob
import os
import argparse

def parse_log_line(line):
    """
    解析日志行，提取所需字段
    返回: (时间, 源IP, 请求方法, URI, 目标IP) 或 None
    """
    # 匹配包含 /SanDisklightsoffwebservice/ 和 .asmx 的行
    pattern = r'^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\s+(\d+\.\d+\.\d+\.\d+)\s+(\w+)\s+(/SanDisklightsoffwebservice/[^\s]+\.asmx).*?(\d+\.\d+\.\d+\.\d+)\s*$'
    
    match = re.search(pattern, line, re.IGNORECASE)
    if match:
        timestamp = match.group(1)      # 时间
        server_ip = match.group(2)      # 服务器IP
        method = match.group(3)         # 请求方法（POST/GET/HEAD等）
        uri = match.group(4)            # 请求URI（包含.asmx）
        target_ip = match.group(5)      # 目标IP
        
        # 不再限制目标IP，所有IP都保留
        return (timestamp, server_ip, method, uri, target_ip)
    
    return None

def process_file(filepath, output_file=None, show_filename=False, seen=None):
    """
    处理单个文件
    """
    if seen is None:
        seen = set()
    
    results = []
    filename = os.path.basename(filepath)
    
    try:
        with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                
                result = parse_log_line(line)
                if result:
                    timestamp, server_ip, method, uri, target_ip = result
                    
                    # 构建去重键：URI + 目标IP
                    dedup_key = f"{uri}|{target_ip}"
                    
                    # 如果已经存在，跳过
                    if dedup_key in seen:
                        continue
                    
                    seen.add(dedup_key)
                    
                    if show_filename:
                        # 输出包含文件名
                        results.append(f"[{filename}] {timestamp} {server_ip} {method} {uri} {target_ip}")
                    else:
                        results.append(f"{timestamp} {server_ip} {method} {uri} {target_ip}")
        
        # 如果有输出文件，写入文件
        if output_file and results:
            with open(output_file, 'a', encoding='utf-8') as f:
                for line in results:
                    f.write(line + '\n')
        elif results:
            # 直接打印到控制台
            for line in results:
                print(line)
        
        return len(results)
    
    except Exception as e:
        print(f"处理文件 {filepath} 出错: {e}", file=sys.stderr)
        return 0

def main():
    parser = argparse.ArgumentParser(description='过滤并去重 SanDiskLightsOff 请求')
    parser.add_argument('files', nargs='+', help='要处理的日志文件（支持绝对路径和通配符）')
    parser.add_argument('-o', '--output', help='输出文件（如果不指定，则输出到控制台）')
    parser.add_argument('-s', '--show-filename', action='store_true', help='在输出中包含文件名')
    parser.add_argument('--stats', action='store_true', help='显示统计信息（总共找到多少条去重记录）')
    
    args = parser.parse_args()
    
    # 扩展通配符
    file_list = []
    for pattern in args.files:
        if not os.path.isabs(pattern):
            if '*' in pattern or '?' in pattern:
                matched_files = glob.glob(pattern, recursive=True)
                for f in matched_files:
                    file_list.append(os.path.abspath(f))
            else:
                if os.path.exists(pattern):
                    file_list.append(os.path.abspath(pattern))
                else:
                    print(f"警告: 文件不存在 {pattern}", file=sys.stderr)
        else:
            if '*' in pattern or '?' in pattern:
                matched_files = glob.glob(pattern, recursive=True)
                file_list.extend(matched_files)
            else:
                if os.path.exists(pattern):
                    file_list.append(pattern)
                else:
                    print(f"警告: 文件不存在 {pattern}", file=sys.stderr)
    
    # 去重并排序
    file_list = sorted(set(file_list))
    
    if not file_list:
        print("错误: 没有找到任何文件", file=sys.stderr)
        sys.exit(1)
    
    # 显示找到的文件
    print(f"找到 {len(file_list)} 个文件:", file=sys.stderr)
    for f in file_list:
        print(f"  {f}", file=sys.stderr)
    print("", file=sys.stderr)
    
    # 如果指定了输出文件，转换为绝对路径
    output_path = None
    if args.output:
        output_path = os.path.abspath(args.output)
        # 清空或创建输出文件
        with open(output_path, 'w', encoding='utf-8') as f:
            pass
        print(f"输出文件: {output_path}", file=sys.stderr)
        print("", file=sys.stderr)
    
    # 全局去重集合（跨文件去重）
    global_seen = set()
    total_count = 0
    
    for filepath in file_list:
        count = process_file(filepath, output_path, args.show_filename, global_seen)
        total_count += count
        print(f"处理完成: {filepath} (新增 {count} 条去重记录)", file=sys.stderr)
    
    print(f"\n总共处理 {len(file_list)} 个文件", file=sys.stderr)
    print(f"去重后共找到 {total_count} 条符合条件的记录", file=sys.stderr)
    if output_path:
        print(f"结果已保存到: {output_path}", file=sys.stderr)

if __name__ == "__main__":
    main()