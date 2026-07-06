import glob
import os
import sys

# CWD = 任务根目录
root = os.getcwd()
jobs_dir = os.path.join(root, "jobs")

if not os.path.isdir(jobs_dir):
    print(0.0)
    sys.exit(0)

# jobs/ 下按名称排序，取最新的日期批次
batches = sorted(d for d in glob.glob(os.path.join(jobs_dir, "*")) if os.path.isdir(d))
if not batches:
    print(0.0)
    sys.exit(0)

latest_batch = batches[-1]

# 遍历批次下每个任务目录的 verifier/reward.txt
rewards = []
task_count = 0
for task_dir in glob.glob(os.path.join(latest_batch, "*")):
    if not os.path.isdir(task_dir):
        continue
    task_count += 1
    reward_file = os.path.join(task_dir, "verifier", "reward.txt")
    if os.path.isfile(reward_file):
        try:
            with open(reward_file, "rb") as f:
                val = f.read().strip()
            rewards.append(float(val) if val else 0.0)
        except (ValueError, OSError):
            continue

if rewards:
    print(round(sum(rewards) / task_count, 2))
else:
    print(0.0)
