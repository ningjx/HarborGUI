#!/usr/bin/env python3
"""
统计 Shell 脚本中静态写入文件/stdout 的字符总数。
覆盖：echo、printf、heredoc、Python heredoc 内的 print/open().write() 等。
用法：python count_write_chars.py <script_path>
"""

import re
import sys


def find_heredoc_ranges(lines):
    """找出所有 heredoc 区域，返回 [(start, end, delim, is_python), ...]"""
    ranges = []
    i = 0
    while i < len(lines):
        m = re.search(r'<<-?\s*["\']?(\w+)["\']?', lines[i])
        if not m:
            i += 1
            continue
        delim = m.group(1)
        is_python = bool(re.search(r'python3?\s', lines[i]))
        start = i + 1
        i += 1
        while i < len(lines):
            if re.match(rf'^\s*{re.escape(delim)}\s*$', lines[i]):
                ranges.append((start, i, delim, is_python))
                i += 1
                break
            i += 1
    return ranges


def is_inside_heredoc(idx, heredoc_ranges):
    """判断某行是否在 heredoc 内部"""
    for s, e, *_ in heredoc_ranges:
        if s <= idx < e:
            return True
    return False


def count_shell_writes(lines, heredoc_ranges):
    """统计 shell 层面 echo/printf + 重定向 的写入字符数"""
    total = 0
    for idx, line in enumerate(lines):
        if is_inside_heredoc(idx, heredoc_ranges):
            continue
        # 必须有重定向才可能是写入文件
        if not re.search(r'[>]{1,2}', line):
            continue

        # echo "..." 或 echo '...' 或 echo 裸字符串
        for m in re.finditer(r'echo\s+(?:"([^"]*)"|\'([^\']*)\'|([^\s|;&><]+))', line):
            s = m.group(1) or m.group(2) or m.group(3) or ""
            total += len(s)
            if '-n' not in line[:m.start()]:
                total += 1  # 换行符

        # printf "format" ...
        for m in re.finditer(r'printf\s+(?:"([^"]*)"|\'([^\']*)\'|([^\s|;&><]+))', line):
            total += len(m.group(1) or m.group(2) or m.group(3) or "")

    return total


def count_heredoc_writes(lines, heredoc_ranges):
    """统计 heredoc body 字符数 + Python heredoc 内的 print/write"""
    total = 0
    for start, end, delim, is_python in heredoc_ranges:
        body = ''.join(lines[start:end])
        total += len(body)
        if is_python:
            total += count_python_writes(body)
    return total


def count_python_writes(text):
    """统计 Python 代码中 print() 和 .write() 的静态字符串字符数"""
    total = 0

    # print("...")
    for m in re.finditer(r'print\("([^"]*)"\)', text):
        total += len(m.group(1)) + 1
    # print('...')
    for m in re.finditer(r"print\('([^']*)'\)", text):
        total += len(m.group(1)) + 1
    # print(f"...")  -- 去掉 {var} 只算静态部分
    for m in re.finditer(r'print\(f"([^"]*)"\)', text):
        static = re.sub(r'\{[^}]*\}', '', m.group(1))
        total += len(static) + 1
    for m in re.finditer(r"print\(f'([^']*)'\)", text):
        static = re.sub(r'\{[^}]*\}', '', m.group(1))
        total += len(static) + 1

    # .write("...")
    for m in re.finditer(r'\.write\("([^"]*)"\)', text):
        total += len(m.group(1))
    for m in re.finditer(r"\.write\('([^']*)'\)", text):
        total += len(m.group(1))
    # .write(f"...")
    for m in re.finditer(r'\.write\(f"([^"]*)"\)', text):
        total += len(re.sub(r'\{[^}]*\}', '', m.group(1)))
    # .write( ... + "..." + ...)  -- 拼接中的字符串
    for m in re.finditer(r'\.write\([^)]*"([^"]*)"', text):
        total += len(m.group(1))

    return total


def main():
    #if len(sys.argv) < 2:
    #    print(f"Usage: python {sys.argv[0]} <script_path>", file=sys.stderr)
    #    sys.exit(1)

    with open(r".\tests\test.sh", 'r', encoding='utf-8', errors='replace') as f:
        lines = f.readlines()

    heredoc_ranges = find_heredoc_ranges(lines)
    total = count_shell_writes(lines, heredoc_ranges)
    total += count_heredoc_writes(lines, heredoc_ranges)

    print(total)


if __name__ == '__main__':
    main()