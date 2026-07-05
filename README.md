# HarborGUI

批量质检工具 — 对批量任务执行自定义规则链式质检，生成结构化报告。

## 功能

- **自动发现任务** — 扫描工作目录下的 ZIP 文件和子文件夹作为待检任务
- **规则链质检** — 每个规则按顺序执行命令 + 可选 Python 验证脚本
- **自定义输出格式** — 通过 `ResultFormat` 模板控制质检结果展示
- **环境变量注入** — 命令和脚本中可使用 `${KEY}` 占位符，在界面中统一配置
- **并行执行** — 可配置最大并行任务数
- **CSV 报告** — 每次质检自动生成 CSV 格式报告
- **任务栏进度** — 质检进度实时显示在任务栏图标

## 使用方法

1. 打开 HarborGUI，选择工作目录（包含 ZIP 或子文件夹）
2. 选择质检规则配置文件（.json）
3. 选中需要质检的任务项
4. 点击"开始质检"
5. 查看质检结果和 CSV 报告

## 配置

### 质检规则 (`Config/verify_rules.json`)

每条规则定义：

| 字段 | 说明 |
|------|------|
| `RuleName` | 规则名称 |
| `Commands` | 要执行的命令列表（按顺序） |
| `ExitCode` | 期望的退出码 |
| `VerifyScript` | 可选 Python 验证脚本路径（相对于 Config 目录） |
| `MatchResult` | 验证脚本的期望输出（支持数值范围如 `(0,0.5]`） |
| `FailContinue` | 失败后是否继续后续规则 |
| `ResultFormat` | 自定义输出格式模板 |

### 输出格式模板

`ResultFormat` 支持变量：

- `${Valid_Result}` / `${Vaild_Result}` — "通过" / "失败"
- `${Script_Output}` — 脚本输出内容

### 主配置 (`Config/config.json`)

UI 中自动管理，包含窗口状态、环境变量、并行数等。

## 构建

```bash
dotnet publish HarborGUI/HarborGUI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 许可

[MIT](LICENSE.txt)
