using HarborGUI.Models;

namespace HarborGUI.Services.Interfaces;

/// <summary>
/// 配置服务：读取/写入应用主配置和质检规则配置
/// </summary>
public interface IConfigService
{
    // ==================== 质检规则配置 ====================

    /// <summary>从指定路径加载质检规则配置</summary>
    VerifyRulesConfig LoadVerifyRules(string configPath);

    /// <summary>保存质检规则配置到指定路径</summary>
    void SaveVerifyRules(string configPath, VerifyRulesConfig config);

    /// <summary>获取默认质检规则配置文件路径</summary>
    string GetDefaultVerifyRulesPath();

    // ==================== 应用主配置 ====================

    /// <summary>加载应用主配置（窗口状态、环境变量等）</summary>
    AppConfig LoadAppConfig();

    /// <summary>保存应用主配置</summary>
    void SaveAppConfig(AppConfig config);

    /// <summary>获取默认主配置文件路径</summary>
    string GetDefaultAppConfigPath();
}
