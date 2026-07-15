import json, glob, os, sys

ROOT = os.getcwd()
JOBS_DIR = os.path.join(ROOT, "jobs")

if not os.path.isdir(JOBS_DIR):
    print("no_jobs_dir")
    sys.exit(0)

# 按日期从新到旧排序
batches = sorted(
    (d for d in glob.glob(os.path.join(JOBS_DIR, "*")) if os.path.isdir(d)),
    reverse=True,
)

if not batches:
    print("no_batches_found")
    sys.exit(0)

# 从最新批次开始，找到第一个 n_total_trials==4 的批次
target_batch = None
for batch in batches:
    result_file = os.path.join(batch, "result.json")
    if not os.path.isfile(result_file):
        continue
    try:
        with open(result_file, "r", encoding="utf-8") as f:
            data = json.load(f)
    except Exception:
        continue
    if data.get("n_total_trials") == 4:
        target_batch = (batch, data)
        break

if target_batch is None:
    print("no_valid_batch:no_batch_with_4_trials_found")
    sys.exit(0)

batch_dir, data = target_batch
stats = data.get("stats", {})
errors = []

n_total = data.get("n_total_trials", 0)
n_completed = stats.get("n_completed_trials", 0)
n_errored = stats.get("n_errored_trials", 0)
n_running = stats.get("n_running_trials", 0)
n_pending = stats.get("n_pending_trials", 0)
n_cancelled = stats.get("n_cancelled_trials", 0)

if n_total != 4:
    errors.append("total_trials=" + str(n_total) + "!=4")
if n_completed != 4:
    errors.append("completed=" + str(n_completed) + "!=4")
if n_errored != 0:
    errors.append("errored=" + str(n_errored))
if n_running != 0:
    errors.append("running=" + str(n_running))
if n_pending != 0:
    errors.append("pending=" + str(n_pending))
if n_cancelled != 0:
    errors.append("cancelled=" + str(n_cancelled))

# 校验 evals key 必须包含 terminus-2 和 deepseek-v4-pro
evals = stats.get("evals", {})
if not evals:
    errors.append("no_evals_found")
else:
    for eval_key in evals:
        if "terminus-2" not in eval_key or "deepseek-v4-pro" not in eval_key:
            errors.append("wrong_agent:" + eval_key)

found_mean = False
for eval_key, eval_data in evals.items():
    metrics = eval_data.get("metrics", [])
    for metric in metrics:
        mean_val = metric.get("mean")
        if mean_val is not None:
            found_mean = True
            if mean_val <= 0:
                errors.append("mean=" + str(mean_val) + "<=0")
            elif mean_val > 0.5:
                errors.append("mean=" + str(mean_val) + ">0.5")

if not found_mean:
    errors.append("no_mean_found")

if errors:
    print("|".join(errors))
else:
    print(0)

sys.exit(0)
