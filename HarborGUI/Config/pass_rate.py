import glob
import os
import sys

# ============================================================
# 用法: python pass_rate.py <target>
#   target = oracle  → 找最新日期批次: 仅有 1 个任务且 agent/ 含 oracle.txt
#   target = models  → 找最新日期批次: 有 4 个任务且所有 agent/ 均不含 oracle.txt
# ============================================================

if len(sys.argv) < 2:
    print("用法: python pass_rate.py <oracle|models>")
    sys.exit(1)

target = sys.argv[1].strip().lower()

ROOT = os.getcwd()
JOBS_DIR = os.path.join(ROOT, "jobs")

if not os.path.isdir(JOBS_DIR):
    print("没有找到对应的任务集")
    sys.exit(0)

# 按日期从新到旧排序
batches = sorted(
    (d for d in glob.glob(os.path.join(JOBS_DIR, "*")) if os.path.isdir(d)),
    reverse=True,
)

if not batches:
    print("没有找到对应的任务集")
    sys.exit(0)


def get_tasks_in_batch(batch_dir):
    """返回 batch 下所有子目录（任务目录）的路径列表"""
    return sorted(
        d for d in glob.glob(os.path.join(batch_dir, "*")) if os.path.isdir(d)
    )


def has_oracle_txt(task_dir):
    """检查任务目录的 agent/ 下是否有 oracle.txt"""
    return os.path.isfile(os.path.join(task_dir, "agent", "oracle.txt"))


def calc_pass_rate(task_dirs, task_count):
    """计算给定任务目录列表的通过率"""
    rewards = []
    for td in task_dirs:
        reward_file = os.path.join(td, "verifier", "reward.txt")
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


# 从最新到最旧遍历所有批次，找到第一个满足条件的
for batch in batches:
    tasks = get_tasks_in_batch(batch)
    n_tasks = len(tasks)

    if target == "oracle":
        # 条件: 恰好 1 个任务 且 该任务 agent/ 含 oracle.txt
        if n_tasks == 1 and has_oracle_txt(tasks[0]):
            calc_pass_rate(tasks, 1)
            sys.exit(0)

    elif target == "models":
        # 条件: 恰好 4 个任务 且 全部没有 oracle.txt
        if n_tasks == 4 and not any(has_oracle_txt(t) for t in tasks):
            calc_pass_rate(tasks, 4)
            sys.exit(0)

    else:
        print(f"未知 target: {target}，请输入 oracle 或 models")
        sys.exit(1)

# 遍历完所有批次都没找到
print("没有找到对应的任务集")
