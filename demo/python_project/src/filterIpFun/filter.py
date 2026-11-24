#当前文件解析  匹配包含 /SanDisklightsoffwebservice/ 和 .asmx 的行 以及对应的IP地址 以及对应的调用次数 以及对应的hostname
#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import re
import sys
import glob
import os
import argparse
import socket
import threading
import subprocess
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed

# ========== 配置区域（请修改这里） ==========
SEARCH_PATTERN = r'^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\s+(\d+\.\d+\.\d+\.\d+)\s+(\w+)\s+(/SanDisklightsoffwebservice/[^\s]+\.asmx).*?(\d+\.\d+\.\d+\.\d+)\s*$'
# ===========================================

# DNS缓存
dns_cache = {}
dns_cache_lock = threading.Lock()

def get_hostname(ip, timeout=5):
    """
    通过多种方式获取IP对应的hostname
    优先使用nslookup命令（和命令行结果一致）
    """
    with dns_cache_lock:
        if ip in dns_cache:
            return dns_cache[ip]
    
    hostname = ip  # 默认返回IP
    
    # 方法1: 使用nslookup命令（Windows/Linux都支持）
    try:
        # 使用nslookup命令
        result = subprocess.run(
            ['nslookup', ip],
            capture_output=True,
            text=True,
            timeout=timeout
        )
        
        # 解析nslookup输出
        for line in result.stdout.split('\n'):
            # 查找 "名称:    cvpwsip05.ad.shared" 或 "Name:    cvpwsip05.ad.shared"
            if '名称:' in line or 'Name:' in line:
                # 提取主机名
                parts = re.split(r'名称:|Name:', line)
                if len(parts) > 1:
                    full_hostname = parts[1].strip()
                    # 如果包含点号，取第一部分作为简短主机名
                    if '.' in full_hostname:
                        short_name = full_hostname.split('.')[0]
                        hostname = short_name
                    else:
                        hostname = full_hostname
                    break
    except subprocess.TimeoutExpired:
        pass
    except Exception as e:
        pass
    
    # 方法2: 如果nslookup失败，尝试socket反向解析
    if hostname == ip:
        try:
            socket.setdefaulttimeout(timeout)
            full_hostname = socket.gethostbyaddr(ip)[0]
            if '.' in full_hostname:
                short_name = full_hostname.split('.')[0]
                hostname = short_name
            else:
                hostname = full_hostname
        except:
            pass
    
    # 缓存结果
    with dns_cache_lock:
        dns_cache[ip] = hostname
    
    return hostname

def parse_log_line(line):
    """
    解析日志行，提取所需字段
    返回: (源IP, 请求方法, URI, 目标IP) 或 None
    """
    pattern = SEARCH_PATTERN
    
    match = re.search(pattern, line, re.IGNORECASE)
    if match:
        server_ip = match.group(2)
        method = match.group(3)
        uri = match.group(4)
        target_ip = match.group(5)
        return (server_ip, method, uri, target_ip)
    return None

def normalize_uri(uri):
    """规范化URI，转为小写"""
    return uri.strip().lower()

def process_file(filepath, stats=None, first_seen=None):
    """处理单个文件"""
    if stats is None:
        stats = defaultdict(int)
    
    if first_seen is None:
        first_seen = {}
    
    try:
        with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                
                result = parse_log_line(line)
                if result:
                    server_ip, method, uri, target_ip = result
                    
                    normalized_uri = normalize_uri(uri)
                    dedup_key = f"{normalized_uri}|{target_ip}"
                    
                    stats[dedup_key] += 1
                    
                    if dedup_key not in first_seen:
                        first_seen[dedup_key] = {
                            'original_uri': uri,
                            'server_ip': server_ip,
                            'method': method,
                            'target_ip': target_ip
                        }
        
        return len(stats)
    
    except Exception as e:
        print(f"处理文件 {filepath} 出错: {e}", file=sys.stderr)
        return 0

def resolve_hostnames(ips, max_workers=5):
    """
    批量解析IP对应的hostname
    使用线程池提高效率
    """
    print("\n正在解析DNS... (使用nslookup命令)", file=sys.stderr)
    print("", file=sys.stderr)
    
    ip_list = list(ips)
    results = {}
    success_count = 0
    
    # 使用较少的线程数，避免nslookup并发问题
    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        future_to_ip = {executor.submit(get_hostname, ip): ip for ip in ip_list}
        
        for future in as_completed(future_to_ip):
            ip = future_to_ip[future]
            try:
                hostname = future.result()
                results[ip] = hostname
                if hostname != ip:
                    success_count += 1
                    print(f"  ✓ {ip} -> {hostname}", file=sys.stderr)
                else:
                    print(f"  ✗ {ip} -> 解析失败", file=sys.stderr)
            except Exception as e:
                results[ip] = ip
                print(f"  ✗ {ip} -> 解析异常: {e}", file=sys.stderr)
    
    print(f"\nDNS解析完成: 成功 {success_count}/{len(ip_list)} 个", file=sys.stderr)
    return results

def output_results(stats, first_seen, hostnames=None, output_file=None):
    """输出结果"""
    if not stats:
        print("没有找到任何符合条件的记录", file=sys.stderr)
        return
    
    output_data = []
    for dedup_key, count in stats.items():
        info = first_seen.get(dedup_key, {})
        target_ip = info.get('target_ip', '')
        uri = info.get('original_uri', '')
        
        hostname = hostnames.get(target_ip, target_ip) if hostnames else target_ip
        
        output_data.append({
            'target_ip': target_ip,
            'hostname': hostname,
            'uri': uri,
            'count': count
        })
    
    # 按调用次数降序排序
    output_data.sort(key=lambda x: x['count'], reverse=True)
    
    output_lines = []
    
    # 标题
    header = f"{'目标IP':<20} {'Hostname':<35} {'调用次数':<10} {'URI'}"
    output_lines.append(header)
    output_lines.append("-" * 120)
    
    for item in output_data:
        target_ip = item['target_ip']
        hostname = item['hostname']
        count = item['count']
        uri = item['uri']
        
        if len(uri) > 50:
            uri_display = uri[:47] + "..."
        else:
            uri_display = uri
        
        output_lines.append(f"{target_ip:<20} {hostname:<35} {count:<10} {uri_display}")
    
    output_lines.append("-" * 120)
    output_lines.append(f"总计: {len(stats)} 个不同的URI+IP组合")
    output_lines.append(f"总调用次数: {sum(stats.values())} 次")
    output_lines.append(f"涉及目标IP: {len(set(item['target_ip'] for item in output_data))} 个")
    output_lines.append(f"处理完成时间: {__import__('datetime').datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    
    # 输出
    for line in output_lines:
        print(line)
    
    if output_file:
        with open(output_file, 'w', encoding='utf-8') as f:
            for line in output_lines:
                f.write(line + '\n')
        print(f"\n结果已保存到: {output_file}", file=sys.stderr)

def main():
    parser = argparse.ArgumentParser(description='统计 SanDiskLightsOff 请求的调用次数')
    parser.add_argument('files', nargs='+', help='要处理的日志文件（支持绝对路径和通配符）')
    parser.add_argument('-o', '--output', help='输出文件（如果不指定，则输出到控制台）')
    parser.add_argument('--dns', action='store_true', help='启用DNS反向解析，获取IP对应的hostname')
    parser.add_argument('--dns-workers', type=int, default=5, help='DNS解析并发线程数（默认5）')
    
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
    
    file_list = sorted(set(file_list))
    
    if not file_list:
        print("错误: 没有找到任何文件", file=sys.stderr)
        sys.exit(1)
    
    print(f"找到 {len(file_list)} 个文件:", file=sys.stderr)
    for f in file_list:
        print(f"  {f}", file=sys.stderr)
    print("", file=sys.stderr)
    
    output_path = None
    if args.output:
        output_path = os.path.abspath(args.output)
        with open(output_path, 'w', encoding='utf-8') as f:
            pass
        print(f"输出文件: {output_path}", file=sys.stderr)
        print("", file=sys.stderr)
    
    global_stats = defaultdict(int)
    global_first_seen = {}
    all_target_ips = set()
    
    for filepath in file_list:
        count = process_file(filepath, global_stats, global_first_seen)
        print(f"处理完成: {filepath}", file=sys.stderr)
    
    for info in global_first_seen.values():
        all_target_ips.add(info['target_ip'])
    
    print(f"\n总共处理 {len(file_list)} 个文件", file=sys.stderr)
    print(f"找到 {len(global_stats)} 个不同的URI+IP组合", file=sys.stderr)
    print(f"总调用次数: {sum(global_stats.values())} 次", file=sys.stderr)
    print(f"涉及目标IP: {len(all_target_ips)} 个", file=sys.stderr)
    print("", file=sys.stderr)
    
    hostnames = None
    if args.dns and all_target_ips:
        hostnames = resolve_hostnames(all_target_ips, args.dns_workers)
        print("", file=sys.stderr)
    
    output_results(global_stats, global_first_seen, hostnames, output_path)

if __name__ == "__main__":
    main()