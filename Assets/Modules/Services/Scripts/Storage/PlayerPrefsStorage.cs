using Ionic.Zlib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using UnityEngine;
using GameModel.Serialization;

namespace Services.Storage
{
    public class PlayerPrefsStorage : IDataStorage
    {
        private long _currentGameId;
        private long _currentVersion;
        private const int _formatId = 3;
        private readonly string _savesDir;

        public PlayerPrefsStorage()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _savesDir = Application.persistentDataPath + "/";
#else
            _savesDir = Application.dataPath + "/../SavesDir/";
#endif
            if (!Directory.Exists(_savesDir)) Directory.CreateDirectory(_savesDir);
        }

        private string GetMainFileName(string mod) => string.IsNullOrEmpty(mod) ? "savegame" : mod;
        private string GetBackupFileName(string mod) => string.IsNullOrEmpty(mod) ? "savegame.bak" : mod + ".bak";
        private string GetErrorFileName(string mod) => string.IsNullOrEmpty(mod) ? "savegame.err" : mod + ".err";

        public bool TryLoad(ISerializableGameData gameData, string mod)
        {
            string mainFile = Path.Combine(_savesDir, GetMainFileName(mod));
            string backupFile = Path.Combine(_savesDir, GetBackupFileName(mod));
            string errorFile = Path.Combine(_savesDir, GetErrorFileName(mod));

            // 1. Try to load main file
            if (TryLoadFile(mainFile, out var mainData))
            {
                if (TryDeserializeUniversal(mainData, gameData, mod)) return true;

                TrySaveFile(errorFile, mainData);
            }

            // 2. Try to load backup file
            if (TryLoadFile(backupFile, out var bakData))
            {
                if (TryDeserializeUniversal(bakData, gameData, mod)) return true;
            }

            // 3. Migrate from PlayerPrefs if no files exist
            string prefsKey = string.IsNullOrEmpty(mod) ? "savegame" : "savegame." + mod;
            string prefsData = PlayerPrefs.GetString(prefsKey);
            if (!string.IsNullOrEmpty(prefsData))
            {
                try
                {
                    byte[] decoded = System.Convert.FromBase64String(prefsData);
                    if (TryDeserializeUniversal(decoded, gameData, mod))
                    {
                        Save(gameData);
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        public void Save(ISerializableGameData gameData)
        {
            try
            {
                if (_currentGameId == gameData.GameId && _currentVersion == gameData.DataVersion) return;

                string mainFile = Path.Combine(_savesDir, GetMainFileName(gameData.ModId));
                string backupFile = Path.Combine(_savesDir, GetBackupFileName(gameData.ModId));

                // Create backup before overwriting
                if (File.Exists(mainFile)) TryCopyFile(mainFile, backupFile);

                var data = new List<byte>();
                data.AddRange(Helpers.Serialize(_formatId));
                data.AddRange(Helpers.Serialize(gameData.GameId));
                data.AddRange(Helpers.Serialize(gameData.TimePlayed));
                data.AddRange(Helpers.Serialize(gameData.DataVersion));
                data.AddRange(Helpers.Serialize(AppConfig.version));
                data.AddRange(ZlibStream.CompressBuffer(gameData.Serialize().ToArray()));

                // Encrypt data before saving
                byte[] finalData = Encrypt(data.ToArray());

                if (TrySaveFile(mainFile, finalData))
                {
                    _currentGameId = gameData.GameId;
                    _currentVersion = gameData.DataVersion;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("File Save Error: " + e.Message);
            }
        }

        public bool TryImportOriginalSave(ISerializableGameData gameData, string mod)
        {
            if (string.IsNullOrEmpty(mod)) return false;

            string mainFile = Path.Combine(_savesDir, GetMainFileName(null));
            string backupFile = Path.Combine(_savesDir, GetBackupFileName(null));

            if (TryLoadFile(mainFile, out var mainData) && TryDeserializeUniversal(mainData, gameData, mod)) return true;
            if (TryLoadFile(backupFile, out var bakData) && TryDeserializeUniversal(bakData, gameData, mod)) return true;

            string prefsData = PlayerPrefs.GetString("savegame");
            if (!string.IsNullOrEmpty(prefsData))
            {
                try
                {
                    byte[] decoded = System.Convert.FromBase64String(prefsData);
                    return TryDeserializeUniversal(decoded, gameData, mod);
                }
                catch { }
            }

            return false;
        }

        private bool TryLoadFile(string filename, out byte[] data)
        {
            if (!File.Exists(filename)) { data = null; return false; }
            try { data = File.ReadAllBytes(filename); return true; }
            catch { data = null; return false; }
        }

        private bool TrySaveFile(string filename, byte[] data)
        {
            try { File.WriteAllBytes(filename, data); return true; }
            catch { return false; }
        }

        private bool TryCopyFile(string source, string destination)
        {
            if (!File.Exists(source)) return false;
            try { File.Copy(source, destination, true); return true; }
            catch { return false; }
        }

        private bool TryDeserializeUniversal(byte[] serializedData, ISerializableGameData gameData, string mod)
        {
            byte[] processData = serializedData;

            // Decrypt XOR obfuscation if present
            if (TryDecrypt(serializedData, out var decryptedData))
            {
                processData = decryptedData;
            }

            // Try Zlib compressed format first
            if (TryDeserializeNewFormat(processData, gameData, mod)) return true;

            // Fallback to raw uncompressed format
            if (TryDeserializeOldFormat(processData, gameData, mod)) return true;

            return false;
        }

        // --- Crypto Methods ---
        private static uint RandomCrypto(ref uint w, ref uint z)
        {
            z = 36969 * (z & 65535) + (z >> 16);
            w = 18000 * (w & 65535) + (w >> 16);
            return (z << 16) + w;
        }

        private bool TryDecrypt(byte[] encryptedData, out byte[] decrypted)
        {
            decrypted = null;
            if (encryptedData == null || encryptedData.Length == 0) return false;

            uint size = (uint)(encryptedData.Length - 1);
            byte[] data = new byte[size];
            Array.Copy(encryptedData, data, size);

            uint w = 0x12345678 ^ size;
            uint z = 0x87654321 ^ size;

            byte checksumm = 0;
            for (int i = 0; i < size; ++i)
            {
                data[i] = (byte)(data[i] ^ (byte)RandomCrypto(ref w, ref z));
                checksumm += data[i];
            }

            byte fileChecksumm = (byte)(encryptedData[size] ^ (byte)RandomCrypto(ref w, ref z));
            if (checksumm != fileChecksumm) return false;

            decrypted = data;
            return true;
        }

        private byte[] Encrypt(byte[] rawData)
        {
            uint size = (uint)rawData.Length;
            byte[] data = new byte[size + 1];
            Array.Copy(rawData, data, size);

            byte checksumm = 0;
            uint w = 0x12345678 ^ size;
            uint z = 0x87654321 ^ size;

            for (int i = 0; i < size; ++i)
            {
                checksumm += data[i];
                data[i] = (byte)(data[i] ^ (byte)RandomCrypto(ref w, ref z));
            }
            data[size] = (byte)(checksumm ^ (byte)RandomCrypto(ref w, ref z));

            return data;
        }

        // --- Deserialization Methods ---
        private bool TryDeserializeNewFormat(byte[] data, ISerializableGameData gameData, string mod)
        {
            try
            {
                int index = 0;
                var formatId = Helpers.DeserializeInt(data, ref index);
                var gameId = Helpers.DeserializeLong(data, ref index);
                var time = Helpers.DeserializeLong(data, ref index);
                var version = Helpers.DeserializeLong(data, ref index);
                var gameVersion = Helpers.DeserializeString(data, ref index);

                byte[] uncompressed = ZlibStream.UncompressBuffer(data.Skip(index).ToArray());

                if (gameData.TryDeserialize(gameId, time, version, mod, uncompressed, 0))
                {
                    _currentGameId = gameId; _currentVersion = version;
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        private bool TryDeserializeOldFormat(byte[] data, ISerializableGameData gameData, string mod)
        {
            try
            {
                int index = 0;
                var formatId = Helpers.DeserializeInt(data, ref index);
                var gameId = Helpers.DeserializeLong(data, ref index);
                var time = Helpers.DeserializeLong(data, ref index);

                var version = formatId >= 1 ? Helpers.DeserializeLong(data, ref index) : 0;
                var gameVersion = formatId >= 2 ? Helpers.DeserializeString(data, ref index) : "old";
                var savedMod = formatId >= 3 ? Helpers.DeserializeString(data, ref index) : null;

                if (gameData.TryDeserialize(gameId, time, version, mod, data, index))
                {
                    _currentGameId = gameId; _currentVersion = version;
                    return true;
                }
                return false;
            }
            catch { return false; }
        }
    }
}