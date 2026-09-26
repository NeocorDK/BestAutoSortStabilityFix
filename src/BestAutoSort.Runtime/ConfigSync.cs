using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace BestAutoSort.Runtime;

/// <summary>
/// Server-authoritative values for gameplay settings (ranges, chest sharing,
/// feeding, production). Client asks the server once connected; the server
/// answers and re-broadcasts whenever its values change — including edits of
/// its .cfg on disk, picked up live. Personal settings (keys, UI, sorting,
/// visuals, debug) stay local. Server without the mod: local values apply.
/// </summary>
internal static class ConfigSync
{
	private const string RequestRpc = "BestAutoSort_ConfigRequest";

	private const string SyncRpc = "BestAutoSort_ConfigSync";

	private const int Format = 1;

	private const float RequestInterval = 10f;

	private const int MaxRequests = 12;

	private static readonly List<ISyncedEntry> Entries = new List<ISyncedEntry>();

	private static ZRoutedRpc? _registered;

	private static bool _received;

	private static int _requests;

	private static float _nextRequestAt;

	private static bool _serverDirty;

	private static FileSystemWatcher? _watcher;

	private static volatile bool _fileChanged;

	private static float _reloadAt;

	private static bool _reloadPending;

	internal static bool IsSyncedFromServer => _received;

	internal static void Register(ISyncedEntry entry)
	{
		Entries.Add(entry);
		entry.OnChanged(delegate
		{
			_serverDirty = true;
		});
	}

	internal static void Pump()
	{
		ZRoutedRpc rpc = ZRoutedRpc.instance;
		ZNet net = ZNet.instance;
		if (rpc == null || (Object)(object)net == (Object)null)
		{
			if (_registered != null)
			{
				ResetSession();
				_registered = null;
			}
			return;
		}
		if (rpc != _registered)
		{
			ResetSession();
			_registered = rpc;
			rpc.Register(RequestRpc, OnRequest);
			rpc.Register<ZPackage>(SyncRpc, OnSync);
		}
		if (net.IsServer())
		{
			PumpServer(rpc);
			return;
		}
		if (_received || _requests >= MaxRequests || ZNet.GetConnectionStatus() != ZNet.ConnectionStatus.Connected)
		{
			return;
		}
		float now = Time.realtimeSinceStartup;
		if (now < _nextRequestAt)
		{
			return;
		}
		long server = ServerPeerId(net);
		if (server == 0L)
		{
			return;
		}
		_nextRequestAt = now + RequestInterval;
		_requests++;
		rpc.InvokeRoutedRPC(server, RequestRpc);
		if (_requests == MaxRequests)
		{
			Plugin.LogInstance.LogWarning((object)"[ConfigSync] server did not send settings (server without BestAutoSort?); using local config.");
		}
	}

	private static void PumpServer(ZRoutedRpc rpc)
	{
		EnsureWatcher();
		float now = Time.realtimeSinceStartup;
		if (_fileChanged)
		{
			// Debounce: editors write in several steps; each write pushes the reload back.
			_fileChanged = false;
			_reloadPending = true;
			_reloadAt = now + 1f;
		}
		if (_reloadPending && now >= _reloadAt)
		{
			_reloadPending = false;
			try
			{
				// Reload fires SettingChanged for changed entries -> broadcast below.
				ModConfig.File?.Reload();
				Plugin.LogInstance.LogInfo((object)"[ConfigSync] config file changed on disk, reloaded.");
			}
			catch (Exception ex)
			{
				Plugin.LogInstance.LogWarning((object)("[ConfigSync] config reload failed: " + ex.Message));
			}
		}
		if (_serverDirty)
		{
			_serverDirty = false;
			rpc.InvokeRoutedRPC(ZRoutedRpc.Everybody, SyncRpc, BuildPackage());
			Plugin.LogInstance.LogInfo((object)"[ConfigSync] settings changed, pushed to all players.");
		}
	}

	private static void OnRequest(long sender)
	{
		ZNet net = ZNet.instance;
		if ((Object)(object)net == (Object)null || !net.IsServer() || ZRoutedRpc.instance == null)
		{
			return;
		}
		ZRoutedRpc.instance.InvokeRoutedRPC(sender, SyncRpc, BuildPackage());
	}

	private static void OnSync(long sender, ZPackage pkg)
	{
		ZNet net = ZNet.instance;
		ZRoutedRpc rpc = ZRoutedRpc.instance;
		if ((Object)(object)net == (Object)null || net.IsServer() || rpc == null)
		{
			return;
		}
		if (sender != ServerPeerId(net))
		{
			Plugin.LogInstance.LogWarning((object)("[ConfigSync] ignored settings from non-server peer " + sender));
			return;
		}
		try
		{
			int format = pkg.ReadInt();
			if (format != Format)
			{
				Plugin.LogInstance.LogWarning((object)("[ConfigSync] unsupported settings format " + format + "; using local config."));
				return;
			}
			string serverVersion = pkg.ReadString();
			int count = pkg.ReadInt();
			Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
			for (int i = 0; i < count; i++)
			{
				string key = pkg.ReadString();
				values[key] = pkg.ReadString();
			}
			int applied = 0;
			List<string> summary = new List<string>();
			foreach (ISyncedEntry entry in Entries)
			{
				if (values.TryGetValue(entry.Key, out string value) && entry.TryApplyRemote(value))
				{
					applied++;
					summary.Add(entry.Key + "=" + value);
				}
				else
				{
					entry.ClearRemote();
				}
			}
			bool first = !_received;
			_received = true;
			if (!string.Equals(serverVersion, Plugin.PluginVersion, StringComparison.Ordinal))
			{
				Plugin.LogInstance.LogWarning((object)("[ConfigSync] server runs " + serverVersion + ", client " + Plugin.PluginVersion + "."));
			}
			Plugin.LogInstance.LogInfo((object)("[ConfigSync] " + (first ? "received" : "updated") + " " + applied + " server setting(s): " + string.Join(", ", summary.ToArray())));
		}
		catch (Exception ex)
		{
			Plugin.LogInstance.LogWarning((object)("[ConfigSync] bad settings package: " + ex.Message));
		}
	}

	private static long ServerPeerId(ZNet net)
	{
		ZNetPeer peer = net.GetServerPeer();
		return peer != null ? peer.m_uid : 0L;
	}

	private static ZPackage BuildPackage()
	{
		ZPackage pkg = new ZPackage();
		pkg.Write(Format);
		pkg.Write(Plugin.PluginVersion);
		pkg.Write(Entries.Count);
		foreach (ISyncedEntry entry in Entries)
		{
			pkg.Write(entry.Key);
			pkg.Write(entry.LocalSerialized());
		}
		return pkg;
	}

	private static void EnsureWatcher()
	{
		if (_watcher != null || ModConfig.File == null)
		{
			return;
		}
		try
		{
			string path = ModConfig.File.ConfigFilePath;
			_watcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path));
			_watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
			FileSystemEventHandler changed = delegate
			{
				// Watcher thread: only flag; the reload runs on the main thread.
				_fileChanged = true;
			};
			_watcher.Changed += changed;
			_watcher.Created += changed;
			_watcher.Renamed += delegate
			{
				_fileChanged = true;
			};
			_watcher.EnableRaisingEvents = true;
		}
		catch (Exception ex)
		{
			Plugin.LogInstance.LogWarning((object)("[ConfigSync] cannot watch config file: " + ex.Message));
			_watcher = null;
		}
	}

	private static void ResetSession()
	{
		foreach (ISyncedEntry entry in Entries)
		{
			entry.ClearRemote();
		}
		if (_received)
		{
			Plugin.LogInstance.LogInfo((object)"[ConfigSync] disconnected, local config restored.");
		}
		_received = false;
		_requests = 0;
		_nextRequestAt = 0f;
	}

	internal static void Shutdown()
	{
		ResetSession();
		_registered = null;
		if (_watcher != null)
		{
			_watcher.EnableRaisingEvents = false;
			_watcher.Dispose();
			_watcher = null;
		}
	}
}
