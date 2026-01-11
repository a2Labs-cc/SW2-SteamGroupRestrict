using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SteamRestrict.Config;
using SteamRestrict.Events;
using SteamRestrict.Services;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using System.Text.Json;

namespace SteamRestrict;

[PluginMetadata(Id = "SteamRestrict", Version = "1.0.0", Name = "SteamRestrict", Author = "aga", Description = "No description.")]
public partial class SteamRestrict : BasePlugin {
  private SteamRestrictConfig _config = new();
  private readonly HttpClient _httpClient = new();

  private WarningTimerService? _warningTimers;
  private ClientEvents? _clientEvents;

  public SteamRestrict(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void Load(bool hotReload) {
    Core.Configuration
      .InitializeJsonWithModel<SteamRestrictConfig>("config.jsonc", "SteamRestrict")
      .Configure(builder => builder.AddJsonFile(Core.Configuration.GetConfigPath("config.jsonc"), optional: false, reloadOnChange: true));

    LoadConfig();

    var steamApi = new SteamApiService(_httpClient, _config, Core.Logger);
    var restrictionService = new RestrictionService(_config);
    _warningTimers = new WarningTimerService(Core, _config, Core.Logger);

    _clientEvents = new ClientEvents(
      Core,
      Core.Logger,
      _config,
      steamApi,
      restrictionService,
      _warningTimers
    );

    _clientEvents.Register();
  }

  private void LoadConfig()
  {
    try
    {
      var configPath = Core.Configuration.GetConfigPath("config.jsonc");
      Core.Logger.LogWarning("SteamRestrict loading config from {ConfigPath} (section: SteamRestrict)", configPath);

      var steamRestrictCfg = new SteamRestrictConfig();
      Core.Configuration.Manager.GetSection("SteamRestrict").Bind(steamRestrictCfg);
      
      if (string.IsNullOrWhiteSpace(steamRestrictCfg.SteamWebAPI))
      {
        Core.Logger.LogWarning("SteamRestrict config bound but SteamWebAPI is empty; attempting file fallback scan");
        var extractedKey = TryExtractSteamWebApiFromJsonc(configPath);
        if (!string.IsNullOrWhiteSpace(extractedKey))
        {
          steamRestrictCfg.SteamWebAPI = extractedKey;
          Core.Logger.LogWarning("SteamRestrict recovered SteamWebAPI from file scan");
        }
      }

      _config = steamRestrictCfg;
    }
    catch (Exception ex)
    {
      Core.Logger.LogError(ex, "SteamRestrict failed to load config; using defaults");
      _config = new SteamRestrictConfig();
    }

    var keyPreview = string.IsNullOrEmpty(_config.SteamWebAPI)
      ? "<empty>"
      : $"len={_config.SteamWebAPI.Length} prefix={_config.SteamWebAPI.Substring(0, Math.Min(4, _config.SteamWebAPI.Length))}...";
    Core.Logger.LogWarning("SteamRestrict config parsed: SteamWebAPI={KeyPreview}", keyPreview);
  }

  private static string? TryExtractSteamWebApiFromJsonc(string filePath)
  {
    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
    {
      return null;
    }

    var text = File.ReadAllText(filePath);
    if (string.IsNullOrWhiteSpace(text))
    {
      return null;
    }

    using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
    {
      AllowTrailingCommas = true,
      CommentHandling = JsonCommentHandling.Skip
    });

    if (doc.RootElement.ValueKind != JsonValueKind.Object)
    {
      return null;
    }

    if (doc.RootElement.TryGetProperty("SteamWebAPI", out var rootKey) && rootKey.ValueKind == JsonValueKind.String)
    {
      return rootKey.GetString();
    }

    var stack = new Stack<JsonElement>();
    stack.Push(doc.RootElement);
    while (stack.Count > 0)
    {
      var el = stack.Pop();
      if (el.ValueKind == JsonValueKind.Object)
      {
        foreach (var prop in el.EnumerateObject())
        {
          if (prop.NameEquals("SteamWebAPI") && prop.Value.ValueKind == JsonValueKind.String)
          {
            return prop.Value.GetString();
          }
          stack.Push(prop.Value);
        }
      }
      else if (el.ValueKind == JsonValueKind.Array)
      {
        foreach (var item in el.EnumerateArray())
        {
          stack.Push(item);
        }
      }
    }

    return null;
  }

  public override void Unload() {
    try
    {
      _warningTimers?.CancelAll();
      _httpClient.Dispose();
    }
    catch (Exception ex)
    {
      Core.Logger.LogError(ex, "SteamRestrict error during unload");
    }
  }
}