using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using HarborGUI.Models;
using HarborGUI.Services.Interfaces;

namespace HarborGUI.Services;

/// <summary>
/// 配置服务实现：JSON 文件读写（主配置 + 质检规则配置）
/// </summary>
public class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _configDir;
    private readonly ILogService? _log;

    public ConfigService(ILogService? log = null)
    {
        _configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        _log = log;
    }

    // ==================== 质检规则配置 ====================

    public VerifyRulesConfig LoadVerifyRules(string configPath)
    {
        if (!File.Exists(configPath))
            return new VerifyRulesConfig();

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<VerifyRulesConfig>(json, JsonOptions) ?? new VerifyRulesConfig();
    }

    public void SaveVerifyRules(string configPath, VerifyRulesConfig config)
    {
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, json);
    }

    public string GetDefaultVerifyRulesPath()
        => Path.Combine(_configDir, "verify_rules.json");

    // ==================== 应用主配置 ====================

    public AppConfig LoadAppConfig()
    {
        var configPath = GetDefaultAppConfigPath();
        _log?.Info($"加载主配置: {configPath}");

        if (!File.Exists(configPath))
        {
            _log?.Info("配置文件不存在，创建默认配置");
            return CreateDefaultAppConfig();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            _log?.Debug($"配置内容长度: {json.Length} 字符");
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config == null)
            {
                _log?.Warning("配置反序列化返回 null，使用默认配置");
                return CreateDefaultAppConfig();
            }
            _log?.Info($"加载成功: {config.EnvironmentVariables.Count} 个环境变量");
            return config;
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "加载配置文件失败，使用默认配置");
            return CreateDefaultAppConfig();
        }
    }

    public void SaveAppConfig(AppConfig config)
    {
        if (!Directory.Exists(_configDir))
            Directory.CreateDirectory(_configDir);

        var configPath = GetDefaultAppConfigPath();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        _log?.Info($"保存主配置: {configPath}, {config.EnvironmentVariables.Count} 个环境变量, JSON长度={json.Length}");
        
        // 记录每个环境变量的 key（不记录值，保护隐私）
        foreach (var kv in config.EnvironmentVariables)
            _log?.Debug($"  环境变量: {kv.Key} = {(string.IsNullOrEmpty(kv.Value) ? "(空)" : "(已设置)")}");

        File.WriteAllText(configPath, json);
    }

    public string GetDefaultAppConfigPath()
        => Path.Combine(_configDir, "config.json");

    private static AppConfig CreateDefaultAppConfig() => new()
    {
        WindowState = new WindowStateConfig
        {
            Width = 1100,
            Height = 700,
            Left = -1,
            Top = -1,
            Maximized = false
        },
        EnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["API_KEY"] = "",
            ["Base_URL"] = "",
            ["Model_Name"] = ""
        },
        CustomButton = new CustomButtonConfig
        {
            Label = "自定义",
            Script = ""
        }
    };
}
