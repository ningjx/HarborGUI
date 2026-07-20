#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
任务指令查重工具
上传单个任务的 ZIP 压缩包到查重服务，检查重复并强制入库（同名任务覆盖更新）。

用法:
    python check_instruction_dedup.py <task.zip> [--ignore-same-name]

参数:
    task.zip          任务压缩包路径
    --ignore-same-name  忽略同名任务的匹配结果（仅影响输出显示）

行为:
    - 调用 /add-force 接口，match_task_name=true
    - 从 task.toml 的 task.name 读取任务名进行匹配
    - 有同名任务则覆盖更新，无同名任务则新增
    - 无论是否重复都强制入库

输出:
    - 无重复匹配 → 0
    - 有重复匹配 → 与哪个已存在任务重复及其相似度

API 文档参考: instruction-dedup/docs/how-to/run-web-api.md
"""

import sys
from pathlib import Path

import requests

API_BASE = "http://192.168.0.60:4320/api/v1"


def check_archive(zip_path: str) -> dict:
    """上传 ZIP，检查重复并强制入库（同名覆盖）"""
    url = f"{API_BASE}/archives/add-force"
    with open(zip_path, "rb") as f:
        files = {"file": (Path(zip_path).name, f, "application/zip")}
        data = {"match_task_name": "true"}
        resp = requests.post(url, files=files, data=data, timeout=120)
    resp.raise_for_status()
    return resp.json()


def format_matches(matches: list[dict], ignore_same_name: bool = False,
                   current_task_full_name: str | None = None) -> str:
    """格式化匹配结果"""
    if ignore_same_name and current_task_full_name:
        matches = [
            m for m in matches
            if m.get("task_full_name") != current_task_full_name
        ]
        if not matches:
            return "0"

    parts = []
    for m in matches:
        task_name = m.get("task_full_name", m.get("task_name", "unknown"))
        jaccard = m.get("jaccard", 0.0)
        similarity_pct = round(jaccard * 100, 2)
        parts.append(f"{task_name}(相似度:{similarity_pct}%)")
    return " | ".join(parts)


def main() -> None:
    if len(sys.argv) < 2:
        print("用法: python check_instruction_dedup.py <task.zip> [--ignore-same-name]")
        sys.exit(1)

    # 解析 --ignore-same-name 参数（暂未使用）
    ignore_same_name = "--ignore-same-name" in sys.argv
    zip_path = next((a for a in sys.argv[1:] if not a.startswith("--")), None)

    if not zip_path:
        print("用法: python check_instruction_dedup.py <task.zip> [--ignore-same-name]")
        sys.exit(1)

    zip_path = zip_path.strip("\"'")

    if not Path(zip_path).is_file():
        print(f"错误: 文件 '{zip_path}' 不存在")
        sys.exit(1)

    try:
        data = check_archive(zip_path)
    except requests.RequestException as e:
        print(f"错误: 查重服务请求失败 - {e}", file=sys.stderr)
        sys.exit(1)

    if not data.get("duplicate"):
        print(0)
    else:
        matches = data.get("matches", [])
        # ignore_same_name 启用：从 API 返回中取当前任务名进行过滤
        current_task = data.get("task_full_name")
        print(format_matches(matches, ignore_same_name=ignore_same_name,
                             current_task_full_name=current_task))

    sys.exit(0)


if __name__ == "__main__":
    main()