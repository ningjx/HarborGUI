#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
ZIP压缩包路径斜杠检测工具
检测ZIP文件中路径使用的是正斜杠(/)还是反斜杠(\)
"""

import zipfile
import os
import sys
import struct
import subprocess


def detect_slashes_raw(zip_path):
    """
    使用原始方式读取ZIP文件，检测路径中的反斜杠
    不依赖zipfile模块的自动转换
    """
    if not os.path.exists(zip_path):
        print(f"错误: 文件 '{zip_path}' 不存在")
        return None
    
    results = {
        'forward_slash': [],
        'backward_slash': [],
        'mixed': [],
        'no_slash': [],
        'total_entries': 0
    }
    
    try:
        with open(zip_path, 'rb') as f:
            data = f.read()
            
            pos = 0
            while True:
                pos = data.find(b'PK\x03\x04', pos)
                if pos == -1:
                    break
                
                if pos + 30 > len(data):
                    break
                
                filename_len = struct.unpack('<H', data[pos+26:pos+28])[0]
                extra_len = struct.unpack('<H', data[pos+28:pos+30])[0]
                
                filename_start = pos + 30
                filename_end = filename_start + filename_len
                if filename_end > len(data):
                    break
                
                # 尝试多种编码
                try:
                    filename = data[filename_start:filename_end].decode('utf-8')
                except:
                    try:
                        filename = data[filename_start:filename_end].decode('gbk')
                    except:
                        filename = data[filename_start:filename_end].decode('cp437', errors='ignore')
                
                has_forward = '/' in filename
                has_backward = '\\' in filename
                
                entry = {
                    'path': filename,
                    'raw': repr(filename)
                }
                
                if has_forward and has_backward:
                    results['mixed'].append(entry)
                elif has_forward and not has_backward:
                    results['forward_slash'].append(entry)
                elif has_backward and not has_forward:
                    results['backward_slash'].append(entry)
                else:
                    results['no_slash'].append(entry)
                
                results['total_entries'] += 1
                
                pos = filename_end + extra_len
    
    except Exception as e:
        print(f"读取ZIP文件时出错: {e}")
        return None
    
    return results


def detect_slashes_powershell(zip_path):
    """
    使用PowerShell检测（修复编码问题）
    """
    # 使用更简单的PowerShell命令，避免编码问题
    ps_script = f'''
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead('{zip_path}')
    $backslash_paths = @()
    $forward_paths = @()
    $mixed_paths = @()
    
    foreach ($entry in $zip.Entries) {{
        $path = $entry.FullName
        $hasForward = $path -match '/'
        $hasBackward = $path -match '\\\\'
        
        if ($hasForward -and $hasBackward) {{
            $mixed_paths += $path
        }} elseif ($hasForward) {{
            $forward_paths += $path
        }} elseif ($hasBackward) {{
            $backslash_paths += $path
        }}
    }}
    
    $zip.Dispose()
    
    # 输出结果（使用简单格式）
    Write-Host "FORWARD_COUNT:$($forward_paths.Count)"
    Write-Host "BACKSLASH_COUNT:$($backslash_paths.Count)"
    Write-Host "MIXED_COUNT:$($mixed_paths.Count)"
    
    if ($backslash_paths.Count -gt 0) {{
        Write-Host "BACKSLASH_PATHS:"
        foreach ($p in $backslash_paths) {{
            Write-Host $p
        }}
    }}
    '''
    
    try:
        # 使用不同的编码方式
        result = subprocess.run(
            ['powershell', '-NoProfile', '-Command', ps_script],
            capture_output=True,
            text=True,
            encoding='gbk',  # 使用gbk编码
            errors='ignore'
        )
        return result.stdout
    except Exception as e:
        return f"PowerShell调用失败: {e}"


def print_results(results):
    """打印检测结果"""
    if results is None:
        return

    backward_count = len(results['backward_slash'])

    if backward_count == 0:
        print("0")
    else:
        paths = "  ".join(entry['path'] for entry in results['backward_slash'])
        print(f"检测到反斜杠{backward_count}个，路径为:{paths}")


def main():
    """主函数"""
    if len(sys.argv) > 1:
        zip_path = sys.argv[1]
    else:
        zip_path = input("请输入ZIP文件路径: ").strip()
    
    zip_path = zip_path.strip('"').strip("'")
    
    if not os.path.exists(zip_path):
        print(f"错误: 文件 '{zip_path}' 不存在")
        sys.exit(1)
    
    # 使用原始方式检测
    results = detect_slashes_raw(zip_path)
    print_results(results)


if __name__ == "__main__":
    main()