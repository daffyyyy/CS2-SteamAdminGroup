using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.Xml;

namespace CS2_SteamAdminGroup;
[MinimumApiVersion(142)]

public class SteamAdminGroupConfig : BasePluginConfig
{
	[JsonPropertyName("Group_ID")] public int Group_ID { get; set; } = 0;
	[JsonPropertyName("Group_Perms")] public string[] Group_Perms { get; set; } = { "#css/admin" };
}

public class CS2_SteamAdminGroup : BasePlugin, IPluginConfig<SteamAdminGroupConfig>
{
	public SteamAdminGroupConfig Config { get; set; } = new SteamAdminGroupConfig();
	public string SteamGroupInfoUrl = "http://steamcommunity.com/gid/10358279GROUP_ID/memberslistxml/?xml=1";
	HashSet<SteamID> AdminsCache = new HashSet<SteamID>();

	public override string ModuleName => "CS2-SteamAdminGroup";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "daffyy";
	public override string ModuleDescription => "A plugin that grants privileges for being a member of a steam group";

	public override void Load(bool hotReload)
	{
		RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);

		if (hotReload)
		{
			OnMapStartHandler(string.Empty);
		}
	}

	public void OnConfigParsed(SteamAdminGroupConfig config)
	{
		if (config.Group_ID == 0)
		{
			throw new Exception($"Invalid value has been set for config value `Group_ID`");
		}

		if (config.Group_Perms.Length == 0)
		{
			throw new Exception($"Invalid value has been set for config value `Group_Perms`");
		}

		SteamGroupInfoUrl = SteamGroupInfoUrl.Replace("GROUP_ID", (1429521408 + config.Group_ID).ToString());
		Config = config;
	}

	private void OnMapStartHandler(string mapName)
	{
		AddTimer(1.0f, () => _ = FetchAdminsFromGroupAsync());
	}

	[ConsoleCommand("css_steamadmingroup_reload")]
	[RequiresPermissions("@css/root")]
	public void OnSteamAdminGroupReloadCommand(CCSPlayerController? caller, CommandInfo command)
	{
		foreach (SteamID steamId in AdminsCache)
		{
			AdminManager.ClearPlayerPermissions(steamId);
			AdminManager.RemovePlayerAdminData(steamId);
			AdminsCache.Remove(steamId);
		}

		AddTimer(1.0f, () => _ = FetchAdminsFromGroupAsync());

		command.ReplyToCommand("Reloaded admins from steam group");
	}

	private async Task FetchAdminsFromGroupAsync()
	{
		try
		{
			using (HttpClient client = new HttpClient())
			{
				HttpResponseMessage response = await client.GetAsync(SteamGroupInfoUrl);

				if (response.IsSuccessStatusCode)
				{
					string groupInfo = await response.Content.ReadAsStringAsync();
					ParseAdmins(groupInfo);
				}
				else
				{
					Logger.LogError("Unable to fetch group info!");
				}
			}
		}
		catch (Exception)
		{
			Logger.LogWarning("Unknown error with parsing group info");
		}
	}

	private void ParseAdmins(string groupInfo)
	{
		try
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(groupInfo);

			// Find all steamID64 elements within the members node
			XmlNodeList? steamIDNodes = xmlDoc.SelectNodes("//members/steamID64");

			if (steamIDNodes != null)
			{
				foreach (XmlNode node in steamIDNodes)
				{
					string steamID64 = node.InnerText;
					if (!string.IsNullOrEmpty(steamID64))
					{
						if (!string.IsNullOrEmpty(steamID64) && SteamID.TryParse(steamID64, out var steamId) && steamId != null)
						{
							foreach (string perm in Config.Group_Perms)
							{
								if (perm.StartsWith("#"))
								{
									// Workaround
									AdminManager.AddPlayerToGroup(steamId, perm);
									AdminManager.AddPlayerToGroup(steamId, perm);
								}
								else if (perm.StartsWith('@'))
								{
									// Workaround
									AdminManager.AddPlayerPermissions(steamId, perm);
									AdminManager.AddPlayerPermissions(steamId, perm);
								}
							}

							if (!AdminsCache.Contains(steamId))
								AdminsCache.Add(steamId);
						}
					}
				}
			}
		}
		catch (Exception)
		{
			Logger.LogWarning("Unable to parse admins from steam group!");
		}
	}
}

