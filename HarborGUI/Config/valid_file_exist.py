import os
import sys

# 强制 UTF-8，解决 Windows 终端中文乱码
sys.stdout.reconfigure(encoding="utf-8")

cwd = os.getcwd()

required = [
    "task.toml",
    "README.md",
    "instruction.md",
    os.path.join("tests", "test_outputs.py"),
    os.path.join("tests", "test.sh"),
    os.path.join("solution", "solve.sh"),
    os.path.join("environment", "Dockerfile"),
    os.path.join("golden-trajectory", "result.json"),
    os.path.join("golden-trajectory", "config.json"),
    os.path.join("golden-trajectory", "agent", "trajectory.json"),
    os.path.join("golden-trajectory", "verifier", "reward.txt"),
    os.path.join("golden-trajectory", "verifier", "ctrf.json"),
]

missing = [p for p in required if not os.path.exists(os.path.join(cwd, p))]

if missing:
    print(f"以下文件不存在:")
    for p in missing:
        print(f" {p}")
    sys.exit(0)
else:
    print(0)