#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
agent-name-detect.py
检测 golden-trajectory 和 jobs 目录下 trajectory.json 中的 Agent 名称和 Model 名称是否正确。
用法: python agent-name-detect.py
      在 TerminalBench 任务根目录下运行（与 golden-trajectory/、jobs/ 同级）。
"""

import glob
import os
import re
import sys

# ============================================================
# 配置：期望的 Agent 名称 & Model 名称包含的字符串
# ============================================================
EXPECTED_AGENT = "terminus-2"
EXPECTED_MODEL_PATTERN = "deepseek-v4-pro"

# ============================================================
# 公共方法：检测单个 trajectory.json
# ============================================================
def check_trajectory(filepath, expected_agent, expected_model_pattern):
    """
    读取 trajectory.json 文件，用正则匹配检测 agent.name 和 agent.model_name。
    参数:
        filepath: trajectory.json 的路径
        expected_agent: 期望的 agent 名称（精确匹配）
        expected_model_pattern: 期望的 model_name 中包含的字符串
    返回:
        list[str]: 错误信息列表，空列表表示一切正常
    """
    errors = []

    if not os.path.isfile(filepath):
        return [f"文件不存在: {filepath}"]

    try:
        with open(filepath, "r", encoding="utf-8") as f:
            # agent 块总是在文件开头，读取前 5000 字符足够
            content = f.read(5000)
    except Exception as e:
        return [f"无法读取文件: {filepath}, 错误: {e}"]

    # ---- 检测 agent.name ----
    # 匹配文件中第一个 "name": "xxx"（agent 块在文件最前面）
    name_match = re.search(r'"name"\s*:\s*"([^"]+)"', content)
    if name_match:
        actual_name = name_match.group(1)
        if actual_name != expected_agent:
            errors.append(
                f"使用了错误的Agent : {actual_name} , 文件路径 : {filepath}"
            )
    else:
        errors.append(f"无法解析Agent名称 , 文件路径 : {filepath}")

    # ---- 检测 agent.model_name ----
    model_match = re.search(r'"model_name"\s*:\s*"([^"]+)"', content)
    if model_match:
        actual_model = model_match.group(1)
        if expected_model_pattern not in actual_model:
            errors.append(
                f"使用了错误的Model : {actual_model} , 文件路径 : {filepath}"
            )
    else:
        errors.append(f"无法解析Model名称 , 文件路径 : {filepath}")

    return errors


# ============================================================
# 辅助方法：查找符合 models 条件的任务批次
# ============================================================
def has_oracle_txt(task_dir):
    """检查任务目录的 agent/ 下是否有 oracle.txt"""
    return os.path.isfile(os.path.join(task_dir, "agent", "oracle.txt"))


def find_models_batch(jobs_dir):
    """
    从最新到最旧遍历 jobs 下的批次，
    找到第一个满足条件的：恰好 4 个任务，且全部没有 oracle.txt。
    返回该批次下的任务目录列表，找不到返回 None。
    """
    if not os.path.isdir(jobs_dir):
        return None

    batches = sorted(
        (d for d in glob.glob(os.path.join(jobs_dir, "*")) if os.path.isdir(d)),
        reverse=True,
    )

    for batch in batches:
        tasks = sorted(
            d for d in glob.glob(os.path.join(batch, "*")) if os.path.isdir(d)
        )
        if len(tasks) == 4 and not any(has_oracle_txt(t) for t in tasks):
            return tasks

    return None


# ============================================================
# 主流程
# ============================================================
def main():
    base_dir = os.getcwd()  # 脚本应在任务根目录运行
    all_errors = []

    # ----- 1. 检测 golden-trajectory -----
    golden_path = os.path.join(base_dir, "golden-trajectory", "agent", "trajectory.json")
    all_errors.extend(
        check_trajectory(golden_path, EXPECTED_AGENT, EXPECTED_MODEL_PATTERN)
    )

    # ----- 2. 检测 jobs 下符合 models 条件的批次 -----
    jobs_dir = os.path.join(base_dir, "jobs")
    tasks = find_models_batch(jobs_dir)

    if tasks is None:
        all_errors.append("没有找到对应的任务集")
    else:
        for task_dir in tasks:
            traj_path = os.path.join(task_dir, "agent", "trajectory.json")
            all_errors.extend(
                check_trajectory(traj_path, EXPECTED_AGENT, EXPECTED_MODEL_PATTERN)
            )

    # ----- 3. 输出结果 -----
    if not all_errors:
        print("0")
        return 0
    else:
        for err in all_errors:
            print(err)
        return 0


if __name__ == "__main__":
    sys.exit(main())