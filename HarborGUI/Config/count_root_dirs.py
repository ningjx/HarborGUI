"""检测压缩包根目录下的文件夹数量。
用法: py count_root_dirs.py <压缩包路径>
输出: 一个整数，表示根目录下文件夹的个数
"""
import sys
import zipfile
from pathlib import Path


def count_root_dirs(zip_path: str) -> int:
    """返回 zip 根目录下文件夹的数量（只统计根目录级别）。"""
    dirs = set()
    with zipfile.ZipFile(zip_path, 'r') as zf:
        for name in zf.namelist():
            # 跨平台：统一用 / 分隔
            parts = name.replace('\\', '/').split('/')
            if len(parts) >= 2 and parts[0]:
                dirs.add(parts[0])
    return len(dirs)


def main():
    if len(sys.argv) != 2:
        print("Usage: py count_root_dirs.py <zip_path>", file=sys.stderr)
        sys.exit(1)

    zip_path = sys.argv[1]
    if not Path(zip_path).exists():
        print(f"File not found: {zip_path}", file=sys.stderr)
        sys.exit(1)

    count = count_root_dirs(zip_path)
    print(count)


if __name__ == "__main__":
    main()