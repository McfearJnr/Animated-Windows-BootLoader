using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

/**
 * A class for handling EFI boot entries. Lazy access with cache.
 * Notice: Data is not updated after the first read.
 */
public class EfiBootEntries {
	/**
	 * Information about an EFI boot entry.
	 */
	public class BootEntryData {
		public UInt32 Attributes;
		public string Label;
		public class DevicePathNode {
			public byte Type, SubType;
			public byte[] Data;
			public DevicePathNode(byte[] data) {
				Type = data[0];
				SubType = data[1];
				Data = data.Skip(4).ToArray();
			}
			public byte[] ToBytes() {
				var len = Data.Length + 4;
				return new byte[] { Type, SubType, (byte)(len & 0xff), (byte)(len >> 8) }.Concat(Data).ToArray();
			}
		}
		public List<DevicePathNode> DevicePathNodes;
		public byte[] Arguments;

		public BootEntryData(byte[] data) {
			Attributes = BitConverter.ToUInt32(data, 0);
			var pathNodesLength = BitConverter.ToUInt16(data, 4);
			Label = new string(Efi.BytesToUInt16s(data).Skip(3).TakeWhile(i => i != 0).Select(i => (char)i).ToArray());
			var pos = 6 + 2 * (Label.Length + 1);
			var pathNodesEnd = pos + pathNodesLength;
			DevicePathNodes = new List<DevicePathNode>();
			Arguments = new byte[0];
			while (pos + 4 <= pathNodesEnd) {
				var len = BitConverter.ToUInt16(data, pos + 2);
				if (len < 4 || pos + len > pathNodesEnd) {
					return; // throw new Exception("Bad entry.");
				}
				var node = new DevicePathNode(data.Skip(pos).Take(len).ToArray());
				DevicePathNodes.Add(node);
				if (node.Type == 0x7f && node.SubType == 0xff) {
					// End of entire device path.
					// Apparently some firmwares produce paths with unused nodes at the end.
					break;
				}
				pos += len;
			}
			Arguments = data.Skip(pathNodesEnd).ToArray();
		}
		public byte[] ToBytes() {
			return new byte[0]
				.Concat(BitConverter.GetBytes((UInt32) Attributes))
				.Concat(BitConverter.GetBytes((UInt16) DevicePathNodes.Sum(n => n.Data.Length + 4)))
				.Concat(Encoding.Unicode.GetBytes(Label + "\0"))
				.Concat(DevicePathNodes.SelectMany(n => n.ToBytes()))
				.Concat(Arguments)
				.ToArray();
		}
		public DevicePathNode FileNameNode {
			get {
				var d = DevicePathNodes;
				return d.Count > 1 && d[d.Count - 1].Type == 0x7F && d[d.Count - 2].Type == 0x04 ? d[d.Count - 2] : null;
			}
		}
		public bool HasFileName {
			get {
				return FileNameNode != null;
			}
		}
		public string FileName {
			get {
				if (!HasFileName) {
					return "";
				}
				return new string(Encoding.Unicode.GetChars(FileNameNode.Data).TakeWhile(c => c != '\0').ToArray());
			}
			set {
				if (!HasFileName) {
					throw new Exception("Logic error: Setting FileName on a bad boot entry.");
				}
				FileNameNode.Data = Encoding.Unicode.GetBytes(value + "\0");
			}
		}
	}

	/**
	 * Status of the own boot entry.
	 */
	public enum OwnEntryStatus {
		NotFound,
		Disabled,
		EnabledAfterWindows,
		Enabled
	}

	/**
	 * Path to the Windows boot loader.
	 */
	public const string WindowsLoaderPath = "\\EFI\\Microsoft\\Boot\\bootmgfw.efi";

	/**
	 * Path to the HackBGRT loader.
	 */
	public const string OwnLoaderPath = "\\EFI\\HackBGRT\\loader.efi";

	private readonly Dictionary<UInt16, (Efi.Variable, BootEntryData)> cache;
	private readonly Efi.Variable BootOrder;
	private readonly Efi.Variable BootCurrent;
	private readonly List<UInt16> BootOrderInts;
	private readonly List<UInt16> BootCurrentInts;

	/**
	 * Constructor. Reads BootOrder and BootCurrent.
	 */
	public EfiBootEntries() {
		cache = new Dictionary<UInt16, (Efi.Variable, BootEntryData)>();
		BootOrder = Efi.GetVariable("BootOrder");
		BootCurrent = Efi.GetVariable("BootCurrent");
		if (BootOrder.Data == null) {
			throw new Exception("Could not read BootOrder.");
		}
		BootCurrentInts = new List<UInt16>(Efi.BytesToUInt16s(BootCurrent.Data ?? new byte[0]));
		BootOrderInts = new List<UInt16>(Efi.BytesToUInt16s(BootOrder.Data));
	}

	/**
	 * Get the boot entry with the given number.
	 *
	 * @param num Number of the boot entry.
	 * @return The boot entry.
	 */
	public (Efi.Variable, BootEntryData) GetEntry(UInt16 num) {
		if (!cache.ContainsKey(num)) {
			var v = Efi.GetVariable(String.Format("Boot{0:X04}", num));
			cache[num] = (v, v.Data == null ? null : new BootEntryData(v.Data));
		}
		return cache[num];
	}

	/**
	 * Find entry by file name.
	 *
	 * @param fileName File name of the boot entry.
	 * @return The boot entry.
	 */
	public (UInt16, Efi.Variable, BootEntryData) FindEntry(string fileName) {
		var rest = Enumerable.Range(0, 0xff).Select(i => (UInt16) i);
		var entryAccessOrder = BootCurrentInts.Concat(BootOrderInts).Concat(rest);
		foreach (var num in entryAccessOrder) {
			var (v, e) = GetEntry(num);
			if (fileName == null ? e == null : (e != null && e.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))) {
				return (num, v, e);
			}
		}
		return (0xffff, null, null);
	}

	/**
	 * Get the Windows boot entry.
	 */
	public (UInt16, Efi.Variable, BootEntryData) WindowsEntry {
		get { return FindEntry(WindowsLoaderPath); }
	}

	/**
	 * Get the HackBGRT boot entry.
	 */
	public (UInt16, Efi.Variable, BootEntryData) OwnEntry {
		get { return FindEntry(OwnLoaderPath); }
	}

	/**
	 * Get an entry by file path.
	 */
	public (UInt16, Efi.Variable, BootEntryData) EntryByPath(string fileName) {
		return FindEntry(fileName);
	}

	/**
	 * Find entry by label.
	 */
	public (UInt16, Efi.Variable, BootEntryData) FindEntryByLabel(string label) {
		var rest = Enumerable.Range(0, 0xff).Select(i => (UInt16)i);
		var entryAccessOrder = BootCurrentInts.Concat(BootOrderInts).Concat(rest);
		foreach (var num in entryAccessOrder) {
			var (v, e) = GetEntry(num);
			if (e != null && String.Equals(e.Label, label, StringComparison.OrdinalIgnoreCase)) {
				return (num, v, e);
			}
		}
		return (0xffff, null, null);
	}

	/**
	 * Get a free boot entry.
	 */
	public (UInt16, Efi.Variable, BootEntryData) FreeEntry {
		get { return FindEntry(null); }
	}

	/**
	 * Check if the own entry is enabled.
	 */
	public OwnEntryStatus GetOwnEntryStatus() {
		return GetEntryStatus(OwnLoaderPath);
	}

	/**
	 * Check if a given entry path is enabled.
	 */
	public OwnEntryStatus GetEntryStatus(string fileName) {
		var (ownNum, ownVar, _) = FindEntry(fileName);
		if (ownVar == null) {
			return OwnEntryStatus.NotFound;
		}
		var (msNum, _, _) = WindowsEntry;
		var msPos = BootOrderInts.IndexOf(msNum);
		var ownPos = BootOrderInts.IndexOf(ownNum);
		if (ownPos < 0) {
			return OwnEntryStatus.Disabled;
		}
		if (ownPos < msPos || msPos < 0) {
			return OwnEntryStatus.Enabled;
		}
		return OwnEntryStatus.EnabledAfterWindows;
	}

	/**
	 * List known entries (for display / diagnostics).
	 */
	public IEnumerable<(UInt16 Number, string Label, string FileName)> EnumerateKnownEntries() {
		var seen = new HashSet<UInt16>();
		foreach (var num in BootOrderInts.Concat(BootCurrentInts).Concat(Enumerable.Range(0, 0xff).Select(i => (UInt16) i))) {
			if (!seen.Add(num)) {
				continue;
			}
			var (_, e) = GetEntry(num);
			if (e != null) {
				yield return (num, e.Label, e.FileName);
			}
		}
	}

	/**
	 * Disable the said boot entry from BootOrder.
	 *
	 * @param dryRun Don't actually write to NVRAM.
	 * @return True, if the entry was found in BootOrder.
	 */
	public bool DisableOwnEntry(bool dryRun = false) {
		return DisableEntry(OwnLoaderPath, dryRun);
	}

	/**
	 * Disable a boot entry from BootOrder by file path.
	 *
	 * @param fileName File path of the entry.
	 * @param dryRun Don't actually write to NVRAM.
	 * @return True, if the entry was found in BootOrder.
	 */
	public bool DisableEntry(string fileName, bool dryRun = false) {
		var (ownNum, ownVar, _) = FindEntry(fileName);
		if (ownVar == null) {
			Setup.Log($"Entry not found for path {fileName}.");
			return false;
		}
		Setup.Log($"Old boot order: {BootOrder}");
		if (!BootOrderInts.Contains(ownNum)) {
			Setup.Log("Own entry not in BootOrder.");
		} else {
			Setup.Log($"Disabling own entry: {ownNum:X04}");
			BootOrderInts.Remove(ownNum);
			BootOrder.Data = BootOrderInts.SelectMany(num => new byte[] { (byte)(num & 0xff), (byte)(num >> 8) }).ToArray();
			Efi.SetVariable(BootOrder, dryRun);
			return true;
		}
		return false;
	}

	/**
	 * Create the boot entry.
	 *
	 * @param alwaysCopyFromMS If true, do not preserve any existing data.
	 * @param dryRun Don't actually write to NVRAM.
	 */
	public void MakeOwnEntry(bool alwaysCopyFromMS, bool dryRun = false) {
		MakeEntry(OwnLoaderPath, "HackBGRT", alwaysCopyFromMS, dryRun);
	}

	/**
	 * Create or update the boot entry for a given path.
	 *
	 * @param fileName EFI file path for this entry.
	 * @param label Label of the entry.
	 * @param alwaysCopyFromMS If true, do not preserve existing data.
	 * @param dryRun Don't actually write to NVRAM.
	 */
	public void MakeEntry(string fileName, string label, bool alwaysCopyFromMS, bool dryRun = false) {
		var (msNum, msVar, msEntry) = WindowsEntry;
		var (ownNum, ownVar, ownEntry) = FindEntry(fileName);
		if (ownVar == null) {
			(ownNum, ownVar, ownEntry) = FreeEntry;
			if (ownVar == null) {
				throw new Exception("MakeEntry: No free entry.");
			}
			Setup.Log($"Creating entry {ownNum:X4} for path {fileName}.");
		} else {
			Setup.Log($"Updating entry {ownNum:X4} for path {fileName}.");
		}

		Setup.Log($"Read EFI variable: {msVar}");
		Setup.Log($"Read EFI variable: {ownVar}");
		// Make a new boot entry using the MS entry as a starting point.
		if (!alwaysCopyFromMS && ownEntry != null) {
			// Shim expects the arguments to be a filename or nothing.
			// But BCDEdit expects some Microsoft-specific data.
			// Modify the entry so that BCDEdit still recognises it
			// but the data becomes a valid UCS-2 / UTF-16LE file name.
			var str = new string(ownEntry.Arguments.Take(12).Select(c => (char) c).ToArray());
			if (str == "WINDOWS\0\x01\0\0\0") {
				ownEntry.Arguments[8] = (byte) 'X';
			} else if (str != "WINDOWS\0\x58\0\0\0") {
				// Not recognized. Clear the arguments.
				ownEntry.Arguments = new byte[0];
			}
		} else {
			if (msEntry == null) {
				throw new Exception("MakeEntry: Windows Boot Manager not found.");
			}
			ownEntry = msEntry;
			ownEntry.Arguments = new byte[0];
			ownEntry.Label = label;
			ownEntry.FileName = fileName;
		}
		ownEntry.Attributes = 1; // LOAD_OPTION_ACTIVE
		ownVar.Attributes = 7; // EFI_VARIABLE_NON_VOLATILE | EFI_VARIABLE_BOOTSERVICE_ACCESS | EFI_VARIABLE_RUNTIME_ACCESS
		ownVar.Data = ownEntry.ToBytes();
		Efi.SetVariable(ownVar, dryRun);
	}

	/**
	 * Enable the own boot entry.
	 *
	 * @param dryRun Don't actually write to NVRAM.
	 */
	public void EnableOwnEntry(bool dryRun = false) {
		EnableEntry(OwnLoaderPath, dryRun, true);
	}

	/**
	 * Enable a boot entry and optionally make it default.
	 *
	 * @param fileName EFI file path for this entry.
	 * @param dryRun Don't actually write to NVRAM.
	 * @param makeDefault True = move before Windows; False = only ensure it's present (after Windows).
	 */
	public void EnableEntry(string fileName, bool dryRun = false, bool makeDefault = true) {
		var (ownNum, ownVar, _) = FindEntry(fileName);
		if (ownVar == null) {
			Setup.Log($"Entry not found for path {fileName}.");
			return;
		}
		var (msNum, _, _) = WindowsEntry;
		var msPos = BootOrderInts.IndexOf(msNum);
		var ownPos = BootOrderInts.IndexOf(ownNum);
		Setup.Log($"Old boot order: {BootOrder}");

		if (makeDefault) {
			var mustAdd = ownPos == -1;
			var mustMove = 0 <= msPos && msPos <= ownPos;
			if (mustAdd || mustMove) {
				Setup.Log($"Enabling entry as default: {ownNum:X04}");
				if (mustMove) {
					BootOrderInts.RemoveAt(ownPos);
				}
				BootOrderInts.Insert(msPos < 0 ? 0 : msPos, ownNum);
				BootOrder.Data = BootOrderInts.SelectMany(num => new byte[] { (byte)(num & 0xff), (byte)(num >> 8) }).ToArray();
				Efi.SetVariable(BootOrder, dryRun);
			}
			return;
		}

		if (ownPos >= 0) {
			Setup.Log($"Entry already present in BootOrder: {ownNum:X04}");
			return;
		}
		Setup.Log($"Adding entry without making default: {ownNum:X04}");
		var insertPos = msPos < 0 ? BootOrderInts.Count : msPos + 1;
		BootOrderInts.Insert(insertPos, ownNum);
		BootOrder.Data = BootOrderInts.SelectMany(num => new byte[] { (byte)(num & 0xff), (byte)(num >> 8) }).ToArray();
		Efi.SetVariable(BootOrder, dryRun);
	}

	/**
	 * Delete an entry variable by path.
	 *
	 * @param fileName EFI file path for this entry.
	 * @param dryRun Don't actually write to NVRAM.
	 * @return True if found and deleted.
	 */
	public bool DeleteEntry(string fileName, bool dryRun = false) {
		var (num, entryVar, _) = FindEntry(fileName);
		if (entryVar == null) {
			Setup.Log($"No entry to delete for path {fileName}.");
			return false;
		}
		Setup.Log($"Deleting entry Boot{num:X04} ({fileName}).");
			DisableEntry(fileName, dryRun);
			entryVar.Data = new byte[0];
			Efi.SetVariable(entryVar, dryRun);
			return true;
		}

	/**
	 * Delete an entry by label if present.
	 */
	public bool DeleteEntryByLabel(string label, bool dryRun = false) {
		var (num, entryVar, entryData) = FindEntryByLabel(label);
		if (entryVar == null || entryData == null) {
			return false;
		}
		Setup.Log($"Deleting entry Boot{num:X04} by label ({label}).");
		DisableEntry(entryData.FileName, dryRun);
		entryVar.Data = new byte[0];
		Efi.SetVariable(entryVar, dryRun);
		return true;
	}

	/**
	 * Move an existing entry to the front of BootOrder.
	 */
	public void MakeEntryDefault(string fileName, bool dryRun = false) {
		var (num, v, _) = FindEntry(fileName);
		if (v == null) {
			Setup.Log($"Entry not found for default operation: {fileName}");
			return;
		}
		var pos = BootOrderInts.IndexOf(num);
		if (pos >= 0) {
			BootOrderInts.RemoveAt(pos);
		}
		BootOrderInts.Insert(0, num);
		BootOrder.Data = BootOrderInts.SelectMany(n => new byte[] { (byte)(n & 0xff), (byte)(n >> 8) }).ToArray();
		Efi.SetVariable(BootOrder, dryRun);
	}

	/**
	 * Ensure an entry is present in BootOrder without moving it to first place.
	 */
	public void AddEntryWithoutDefault(string fileName, bool dryRun = false) {
		EnableEntry(fileName, dryRun, false);
	}

	/**
	 * Log the boot entries.
	 */
	public void LogEntries() {
		Setup.Log($"LogEntries: {BootOrder}");
		Setup.Log($"LogEntries: {BootCurrent}");
		// Windows can't enumerate EFI variables, and trying them all is too slow.
		// BootOrder + BootCurrent + the first 0xff entries should be enough.
		var seen = new HashSet<UInt16>();
		foreach (var num in BootOrderInts.Concat(BootCurrentInts).Concat(Enumerable.Range(0, 0xff).Select(i => (UInt16) i))) {
			if (seen.Contains(num)) {
				continue;
			}
			seen.Add(num);
			var (v, e) = GetEntry(num);
			if (e != null) {
				Setup.Log($"LogEntries: {v}");
			}
		}
	}
}
