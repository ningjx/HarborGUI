# Changelog

## v0.2.0 (2026-07-07)

- 重构变量解析为 Provider 模式，支持可扩展变量来源
- 新增运行时变量 `${Zip_Path}`，压缩包任务可引用原始压缩包路径
- `VerifyScript` 支持参数输入和变量解析（如 `./script.py ${Zip_Path}`）
- 消除 MainViewModel 对 VariableResolver 具体类型的强制转换依赖
- 新增反斜杠检测质检规则

## v0.1.0 (2026-07-05)

- 初始化项目结构和 WPF MVVM 框架
- 任务自动发现（ZIP / 目录）
- 质检规则链引擎：命令执行 + Python 验证脚本
- 自定义输出格式 `ResultFormat`
- 环境变量注入 `${KEY}`
- 并行任务执行与进度显示
- CSV 报告生成
- 任务栏进度同步
- GitHub Actions 发布工作流
