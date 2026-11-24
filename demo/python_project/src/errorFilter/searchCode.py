#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import os
import re
from datetime import datetime
from collections import defaultdict

# ========== 配置区域（请修改这里） ==========
SEARCH_DIR = r"C:\Users\3000100552\Downloads\err\12222"
SEARCH_STRING = "Exception is Could not connect to net"
OUTPUT_FILE = "exception_report.txt"
# ===========================================

def extract_timestamp_from_filename(filename):
    """从文件名中提取时间戳"""
    pattern = r'^(\d{2}-\d{2})T(\d{2})-(\d{2})-(\d{2})'
    match = re.match(pattern, filename)
    if match:
        date_part = match.group(1)
        hour = match.group(2)
        minute = match.group(3)
        second = match.group(4)
        return f"{date_part} {hour}:{minute}:{second}"
    return None

def extract_hour_from_filename(filename):
    """从文件名中提取小时（用于按小时统计）"""
    pattern = r'^(\d{2}-\d{2})T(\d{2})-'
    match = re.match(pattern, filename)
    if match:
        date_part = match.group(1)
        hour = match.group(2)
        return f"{date_part} {hour}:00"
    return None

def is_text_file(file_path):
    """检测文件是否为文本文件"""
    try:
        with open(file_path, 'rb') as f:
            chunk = f.read(1024)
            if b'\x00' in chunk:
                return False
            chunk.decode('utf-8', errors='ignore')
            return True
    except Exception:
        return False

def search_files(directory, search_string):
    """搜索目录中的所有文本文件"""
    file_data = {}
    hourly_stats = defaultdict(int)
    total_errors = 0
    total_files = 0
    text_file_count = 0
    
    print(f"正在扫描文件夹: {directory}")
    print("-" * 60)
    
    for root, dirs, files in os.walk(directory):
        for file in files:
            file_path = os.path.join(root, file)
            rel_path = os.path.relpath(file_path, directory)
            total_files += 1
            
            # 检查是否为文本文件
            if not is_text_file(file_path):
                continue
            
            text_file_count += 1
            
            try:
                with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                    lines = f.readlines()
                
                # 查找匹配的行
                matched_lines = []
                for line_num, line in enumerate(lines, 1):
                    if search_string in line:
                        matched_lines.append({
                            'line_num': line_num,
                            'content': line.strip()
                        })
                
                if matched_lines:
                    file_data[rel_path] = {
                        'errors': matched_lines
                    }
                    total_errors += len(matched_lines)
                    
                    # 统计每小时错误数
                    hour_key = extract_hour_from_filename(file)
                    if hour_key:
                        hourly_stats[hour_key] += len(matched_lines)
                    
            except Exception as e:
                print(f"  警告: 读取文件失败 {file}: {e}")
    
    print("-" * 60)
    print(f"总共扫描了 {total_files} 个文件")
    print(f"其中文本文件: {text_file_count} 个")
    print(f"发现 {total_errors} 条匹配记录，涉及 {len(file_data)} 个文件")
    
    return file_data, total_errors, text_file_count, hourly_stats

def generate_report(file_data, total_errors, search_dir, search_string, output_file, total_files, hourly_stats):
    """生成统计报告"""
    with open(output_file, 'w', encoding='utf-8') as f:
        # ======== 报告头部 ========
        f.write("=" * 100 + "\n")
        f.write(" " * 35 + "异常记录统计报告\n")
        f.write("=" * 100 + "\n\n")
        f.write(f"生成时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write(f"搜索目录: {search_dir}\n")
        f.write(f"搜索字符串: {search_string}\n")
        f.write("=" * 100 + "\n\n")
        
        # ======== 按小时统计 ========
        f.write("按小时统计\n")
        f.write("-" * 100 + "\n")
        
        if hourly_stats:
            # 按小时排序
            for hour_key in sorted(hourly_stats.keys()):
                # 解析小时，生成时间段
                # hour_key 格式: "08-02 00:00"
                hour_display = hour_key[:8]  # "08-02 00"
                f.write(f"{hour_display}:00 - {hour_display}:59   {hourly_stats[hour_key]}次\n")
        else:
            f.write("未提取到小时信息\n")
        
        f.write("\n" + "=" * 100 + "\n\n")
        
        # ======== 按文件统计汇总 ========
        f.write("文件统计汇总\n")
        f.write("-" * 100 + "\n")
        
        sorted_files = sorted(file_data.items(), key=lambda x: len(x[1]['errors']), reverse=True)
        for idx, (file_path, info) in enumerate(sorted_files, 1):
            f.write(f"{idx}. {file_path} - 错误次数: {len(info['errors'])}\n")
        
        f.write("\n" + "=" * 100 + "\n\n")
        
        # ======== 详细错误信息（按文件分组） ========
        f.write("详细错误信息\n")
        f.write("-" * 100 + "\n\n")
        
        for file_path, info in sorted_files:
            f.write(f"文件路径: {file_path}\n")
            f.write(f"错误次数: {len(info['errors'])}\n")
            
            # 显示该文件的所有错误
            for error in info['errors']:
                f.write(f"行号: {error['line_num']}\n")
                f.write(f"内容: {error['content']}\n")
            
            f.write("\n" + "-" * 100 + "\n\n")
        
        # ======== 报告尾部 ========
        f.write("=" * 100 + "\n")
        f.write(f"总计: {total_errors} 条异常记录\n")
        f.write(f"生成时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write("=" * 100 + "\n")

def main():
    """主函数"""
    if not os.path.exists(SEARCH_DIR):
        print(f"错误: 目录 {SEARCH_DIR} 不存在")
        print("请修改脚本中的 SEARCH_DIR 变量为正确的路径")
        return
    
    print("\n" + "=" * 60)
    print("日志异常扫描工具")
    print("=" * 60)
    print(f"搜索目录: {SEARCH_DIR}")
    print(f"搜索字符串: {SEARCH_STRING}")
    print("=" * 60 + "\n")
    
    file_data, total_errors, total_files, hourly_stats = search_files(SEARCH_DIR, SEARCH_STRING)
    
    if total_errors == 0:
        print("\n未找到包含指定字符串的记录")
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            f.write("未找到包含指定字符串的记录\n")
            f.write(f"搜索目录: {SEARCH_DIR}\n")
            f.write(f"搜索时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        return
    
    generate_report(file_data, total_errors, SEARCH_DIR, SEARCH_STRING, OUTPUT_FILE, total_files, hourly_stats)
    
    print(f"\n✅ 处理完成！报告已生成: {OUTPUT_FILE}")
    print(f"📊 总共发现 {total_errors} 条异常记录，涉及 {len(file_data)} 个文件")

if __name__ == "__main__":
    main()