using Ionic.Zlib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using UnityEngine;
using GameModel.Serialization;

namespace Services.Storage
{
    public class FileStorage : IDataStorage
    {
        private long _currentGameId;
        private long _currentVersion;
        private const int _formatId = 3;
        private readonly string _savesDir;

        public FileStorage()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _savesDir = Application.persistentDataPath + "/Saves/";
#else
            _savesDir = Application.dataPath + "/../SavesDir/";
#endif

            if (!Directory.Exists(_savesDir))
            {
                Directory.CreateDirectory(_savesDir);
            }

            UnityEngine.Debug.Log("FileStorage initialized. Path: " + _savesDir);
        }

        private string GetFilePath(string mod)
        {
            string fileName = string.IsNullOrEmpty(mod) ? "savegame.dat" : $"savegame_{mod}.dat";
            return Path.Combine(_savesDir, fileName);
        }

        public bool TryLoad(ISerializableGameData gameData, string mod)
        {
            return TryLoadFromFile(mod, out var data) && TryDeserialize(data, gameData, mod);
        }

        public void Save(ISerializableGameData gameData)
        {
            try
            {
                if (_currentGameId == gameData.GameId && _currentVersion == gameData.DataVersion)
                {
                    return;
                }

                var data = new List<byte>();

                // Serialize header
                data.AddRange(Helpers.Serialize(_formatId));
                data.AddRange(Helpers.Serialize(gameData.GameId));
                data.AddRange(Helpers.Serialize(gameData.TimePlayed));
                data.AddRange(Helpers.Serialize(gameData.DataVersion));
                data.AddRange(Helpers.Serialize(AppConfig.version));

                // Compress and append game data
                data.AddRange(ZlibStream.CompressBuffer(gameData.Serialize().ToArray()));

                File.WriteAllBytes(GetFilePath(gameData.ModId), data.ToArray());

                _currentGameId = gameData.GameId;
                _currentVersion = gameData.DataVersion;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("FileStorage Save Error: " + e.Message);
            }
        }

        public bool TryImportOriginalSave(ISerializableGameData gameData, string mod)
        {
            if (string.IsNullOrEmpty(mod)) return false;
            return TryLoadFromFile(null, out var data) && TryDeserialize(data, gameData, mod);
        }

        private bool TryLoadFromFile(string mod, out byte[] data)
        {
            try
            {
                string path = GetFilePath(mod);
                if (!File.Exists(path))
                {
                    data = null;
                    return false;
                }

                data = File.ReadAllBytes(path);
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("FileStorage Load Error: " + e.Message);
                data = null;
                return false;
            }
        }

        private bool TryDeserialize(byte[] serializedData, ISerializableGameData gameData, string mod)
        {
            try
            {
                _currentGameId = -1;
                int index = 0;

                var formatId = Helpers.DeserializeInt(serializedData, ref index);
                var gameId = Helpers.DeserializeLong(serializedData, ref index);
                var time = Helpers.DeserializeLong(serializedData, ref index);
                var version = Helpers.DeserializeLong(serializedData, ref index);
                var gameVersion = Helpers.DeserializeString(serializedData, ref index);

                // Decompress Zlib data
                byte[] compressedData = serializedData.Skip(index).ToArray();
                if (!gameData.TryDeserialize(gameId, time, version, mod, ZlibStream.UncompressBuffer(compressedData), 0))
                {
                    return false;
                }

                _currentGameId = gameId;
                _currentVersion = version;
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("Deserialization Error: " + e.Message);
                return false;
            }
        }
    }
}