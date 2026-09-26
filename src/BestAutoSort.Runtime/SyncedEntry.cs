using System;
using BepInEx.Configuration;

namespace BestAutoSort.Runtime;

internal interface ISyncedEntry
{
	string Key { get; }

	ConfigEntryBase Base { get; }

	bool IsOverridden { get; }

	string LocalSerialized();

	bool TryApplyRemote(string serialized);

	void ClearRemote();

	void OnChanged(Action handler);
}

/// <summary>
/// Config entry whose effective value comes from the server while connected.
/// The server value lives in memory only: the player's own .cfg is never
/// rewritten, and the local value returns on disconnect. Same <c>.Value</c>
/// surface as ConfigEntry, so call sites stay unchanged.
/// </summary>
internal sealed class SyncedEntry<T> : ISyncedEntry
{
	private T _remote;

	private bool _hasRemote;

	internal ConfigEntry<T> Entry { get; }

	internal SyncedEntry(ConfigEntry<T> entry)
	{
		Entry = entry;
		ConfigSync.Register(this);
	}

	internal T Value
	{
		get => _hasRemote ? _remote : Entry.Value;
		set => Entry.Value = value;
	}

	public string Key => Entry.Definition.Section + "/" + Entry.Definition.Key;

	public ConfigEntryBase Base => Entry;

	public bool IsOverridden => _hasRemote;

	public string LocalSerialized()
	{
		return Entry.GetSerializedValue();
	}

	public bool TryApplyRemote(string serialized)
	{
		try
		{
			object value = TomlTypeConverter.ConvertToValue(serialized, typeof(T));
			if (Entry.Description.AcceptableValues != null)
			{
				value = Entry.Description.AcceptableValues.Clamp(value);
			}
			_remote = (T)value;
			_hasRemote = true;
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public void OnChanged(Action handler)
	{
		Entry.SettingChanged += delegate
		{
			handler();
		};
	}

	public void ClearRemote()
	{
		_hasRemote = false;
		_remote = default;
	}
}
