#!/usr/bin/env python3
"""
一键打包脚本：查找金标轨迹 → 复制到 golden-trajectory → 打包发布
在任务根目录下执行，自动从最新的 jobs 目录中查找符合条件的金标轨迹数据。
"""
import json
import os
import re
import shutil
import zipfile
from pathlib import Path


def main():
    base_dir = Path.cwd()
    task_label = base_dir.name  # 当前目录名，用于区分批量执行时的每条任务
    jobs_dir = base_dir / "jobs"

    # ==================== Step 1: 查找金标轨迹 ====================
    golden_source = find_golden_trajectory(jobs_dir)
    if golden_source is None:
        print(f"[{task_label}] 找不到金标轨迹")
        return 1

    # 复制到 golden-trajectory
    golden_target = base_dir / "golden-trajectory"
    if golden_target.exists():
        shutil.rmtree(golden_target)
    shutil.copytree(golden_source, golden_target)

    # ==================== Step 2: 读取 task.toml 获取打包名 ====================
    task_toml_path = base_dir / "task.toml"
    if not task_toml_path.exists():
        print(f"[{task_label}] task.toml 不存在")
        return 1

    task_name = extract_task_name(task_toml_path)
    if task_name is None:
        print(f"[{task_label}] 无法从 task.toml 中解析任务名")
        return 1

    # 去掉 terminal-bench/ 前缀
    zip_name = re.sub(r'^terminal-bench/', '', task_name)
    zip_path = base_dir / f"{zip_name}.zip"

    # 移除已存在的同名压缩包
    if zip_path.exists():
        zip_path.unlink()

    # ==================== Step 3: 打包 ====================
    items_to_zip = [
        "environment",
        "golden-trajectory",
        "solution",
        "tests",
        "instruction.md",
        "README.md",
        "task.toml",
    ]

    try:
        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zf:
            for item in items_to_zip:
                item_path = base_dir / item
                if not item_path.exists():
                    continue
                if item_path.is_dir():
                    for file_path in sorted(item_path.rglob('*')):
                        zf.write(file_path, file_path.relative_to(base_dir).as_posix())
                else:
                    zf.write(item_path, item)
    except Exception as e:
        print(f"[{task_label}] 打包失败: {e}")
        return 1

    print(f"[{task_label}] 打包完成：{zip_name}.zip")
    return 0


def find_golden_trajectory(jobs_dir: Path) -> Path | None:
    """
    从 jobs/ 目录中按时间倒序查找金标轨迹。

    条件（全部满足）：
      - agent/ 下有 claude-code.txt, trajectory.json
      - verifier/ 下有 ctrf.json, reward.txt
      - 根目录下有 result.json, config.json
      - verifier/reward.txt 内容转数字 == 1
    """
    if not jobs_dir.is_dir():
        return None

    # 按目录名倒序（最新日期优先）
    job_dirs = sorted(
        [d for d in jobs_dir.iterdir() if d.is_dir()],
        key=lambda d: d.name,
        reverse=True,
    )

    for job_dir in job_dirs:
        for task_dir in sorted(job_dir.iterdir()):
            if not task_dir.is_dir():
                continue

            agent_dir = task_dir / "agent"
            verifier_dir = task_dir / "verifier"

            # 快速路径检查
            if not agent_dir.is_dir() or not verifier_dir.is_dir():
                continue
            if not (agent_dir / "claude-code.txt").is_file():
                continue
            if not (agent_dir / "trajectory.json").is_file():
                continue
            if not (verifier_dir / "ctrf.json").is_file():
                continue
            if not (task_dir / "result.json").is_file():
                continue
            if not (task_dir / "config.json").is_file():
                continue

            # reward.txt 内容 == 1
            reward_path = verifier_dir / "reward.txt"
            if not reward_path.is_file():
                continue
            try:
                reward_text = reward_path.read_text(encoding="utf-8").strip()
                if float(reward_text) != 1.0:
                    continue
            except (ValueError, OSError):
                continue

            return task_dir

    return None


def extract_task_name(toml_path: Path) -> str | None:
    """从 task.toml 的 [task] 段中提取 name 字段值。"""
    content = toml_path.read_text(encoding="utf-8")

    # 匹配 [task] 段的内容（直到下一个段或文件结尾）
    m = re.search(r'\[task\]\s*(.*?)(?=\[|\Z)', content, re.DOTALL)
    if not m:
        return None

    section = m.group(1)
    # 匹配 name = "..." 或 name = '...'
    for pattern in [r'name\s*=\s*"([^"]*)"', r"name\s*=\s*'([^']*)'"]:
        nm = re.search(pattern, section)
        if nm:
            return nm.group(1)
    return None


if __name__ == "__main__":
    raise SystemExit(main())
