// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Win32.SafeHandles;

using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Extensions;
using Nethermind.Logging;

namespace Nethermind.Db
{
    public class SimpleFilePublicKeyDb : IFullDb
    {
        public const string DbFileName = "SimpleFileDb.db";

        private ILogger _logger;
        private bool _hasPendingChanges;
        private ConcurrentDictionary<ArraySegment<byte>, byte[]> _cache;

        public string DbPath { get; }
        public string Name { get; }
        public string Description { get; }

        public ICollection<byte[]> Keys => _cache.Keys.Select(key => key.AsSpan().ToArray()).ToArray();
        public ICollection<byte[]> Values => _cache.Values;
        public int Count => _cache.Count;

        public SimpleFilePublicKeyDb(string name, string dbDirectoryPath, ILogManager logManager)
        {
            _logger = logManager.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));
            if (dbDirectoryPath is null) throw new ArgumentNullException(nameof(dbDirectoryPath));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DbPath = Path.Combine(dbDirectoryPath, DbFileName);
            Description = $"{Name}|{DbPath}";

            if (!Directory.Exists(dbDirectoryPath))
            {
                Directory.CreateDirectory(dbDirectoryPath);
            }

            LoadData();
        }

        public byte[] this[ReadOnlySpan<byte> key]
        {
            get
            {
                ArraySegment<byte> asBytes = new(ArrayPool<byte>.Shared.Rent(key.Length), 0, key.Length);
                try
                {
                    key.CopyTo(asBytes);
                    return _cache[asBytes];
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(asBytes.Array);
                }
            }
            set
            {
                if (value is null)
                {
                    _cache.TryRemove(key.ToArray(), out _);
                }
                else
                {
                    _cache.AddOrUpdate(key.ToArray(), newValue => Add(value), (x, oldValue) => Update(oldValue, value));
                }
            }
        }

        public KeyValuePair<byte[], byte[]>[] this[byte[][] keys] => keys.Select(k => new KeyValuePair<byte[], byte[]>(k, _cache.TryGetValue(k, out var value) ? value : null)).ToArray();

        public void Remove(ReadOnlySpan<byte> key)
        {
            _hasPendingChanges = true;

            ArraySegment<byte> asBytes = new(ArrayPool<byte>.Shared.Rent(key.Length), 0, key.Length);
            try
            {
                key.CopyTo(asBytes);
                _cache.TryRemove(asBytes, out _);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(asBytes.Array);
            }
        }

        public bool KeyExists(ReadOnlySpan<byte> key)
        {
            ArraySegment<byte> asBytes = new(ArrayPool<byte>.Shared.Rent(key.Length), 0, key.Length);
            try
            {
                key.CopyTo(asBytes);
                return _cache.ContainsKey(asBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(asBytes.Array);
            }
        }

        public IDb Innermost => this;
        public void Flush() { }
        public void Clear()
        {
            File.Delete(DbPath);
        }

        public IEnumerable<KeyValuePair<byte[], byte[]?>> GetAll(bool ordered = false) =>
            _cache.Select(kv => new KeyValuePair<byte[], byte[]?>(kv.Key.ToArray(), kv.Value));

        public IEnumerable<byte[]> GetAllValues(bool ordered = false) => _cache.Values;

        public IBatch StartBatch()
        {
            return this.LikeABatch(CommitBatch);
        }

        private void CommitBatch()
        {
            if (!_hasPendingChanges)
            {
                if (_logger.IsTrace) _logger.Trace($"Skipping commit ({Name}), no changes");
                return;
            }

            using Backup backup = new(DbPath, _logger);
            _hasPendingChanges = false;
            KeyValuePair<ArraySegment<byte>, byte[]>[] snapshot = _cache.ToArray();

            if (_logger.IsDebug) _logger.Debug($"Saving data in {DbPath} | backup stored in {backup.BackupPath}");
            try
            {
                using StreamWriter streamWriter = new(DbPath);
                foreach ((Span<byte> key, byte[] value) in snapshot)
                {
                    if (value is not null)
                    {
                        key.StreamHex(streamWriter);
                        streamWriter.Write(',');
                        value.StreamHex(streamWriter);
                        streamWriter.WriteLine();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Failed to store data in {DbPath}", e);
            }
        }

        private class Backup : IDisposable
        {
            private readonly string _dbPath;
            private readonly ILogger _logger;

            public string BackupPath { get; }

            public Backup(string dbPath, ILogger logger)
            {
                _dbPath = dbPath;
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));

                try
                {
                    BackupPath = $"{_dbPath}_{Guid.NewGuid().ToString()}";

                    if (File.Exists(_dbPath))
                    {
                        File.Move(_dbPath, BackupPath);
                    }
                }
                catch (Exception e)
                {
                    if (_logger.IsError) _logger.Error($"Error during backup creation for {_dbPath} | backup path {BackupPath}", e);
                }
            }

            public void Dispose()
            {
                try
                {
                    if (BackupPath is not null && File.Exists(BackupPath))
                    {
                        if (File.Exists(_dbPath))
                        {
                            File.Delete(BackupPath);
                        }
                        else
                        {
                            File.Move(BackupPath, _dbPath);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (_logger.IsError) _logger.Error($"Error during backup removal of {_dbPath} | backup path {BackupPath}", e);
                }
            }
        }

        private void LoadData()
        {
            const int maxLineLength = 2048;

            _cache = new ConcurrentDictionary<ArraySegment<byte>, byte[]>(Bytes.ArraySegmentEqualityComparer);

            if (!File.Exists(DbPath))
            {
                return;
            }

            using SafeFileHandle fileHandle = File.OpenHandle(DbPath, FileMode.OpenOrCreate);

            byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(maxLineLength);
            int read = RandomAccess.Read(fileHandle, rentedBuffer, 0);

            long offset = 0L;
            Span<byte> bytes = default;
            while (read > 0)
            {
                offset += read;
                bytes = rentedBuffer.AsSpan(0, read + bytes.Length);
                while (true)
                {
                    // Store the original span incase need to undo the key slicing if end of line not found
                    Span<byte> iterationSpan = bytes;
                    int commaIndex = bytes.IndexOf((byte)',');
                    Span<byte> key = default;
                    if (commaIndex >= 0)
                    {
                        key = bytes[..commaIndex];
                        bytes = bytes[(commaIndex + 1)..];
                    }
                    int lineEndIndex = bytes.IndexOf((byte)'\n');
                    if (lineEndIndex < 0)
                    {
                        // Restore the iteration start span
                        bytes = iterationSpan;
                        break;
                    }

                    Span<byte> value;
                    if (bytes[lineEndIndex - 1] == (byte)'\r')
                    {
                        // Windows \r\n
                        value = bytes[..(lineEndIndex - 1)];
                    }
                    else
                    {
                        // Linux \n
                        value = bytes[..lineEndIndex];
                    }

                    if (commaIndex < 0)
                    {
                        // End of line but no comma
                        RecordError(value);
                    }
                    else if (lineEndIndex >= 0)
                    {
                        _cache[Bytes.FromUtf8HexString(key)] = Bytes.FromUtf8HexString(value);
                    }
                    // Move to after end of line
                    bytes = bytes[(lineEndIndex + 1)..];
                }

                if (bytes.Length > 0)
                {
                    // Move up any remaining to start of buffer
                    bytes.CopyTo(rentedBuffer);
                }

                read = RandomAccess.Read(fileHandle, rentedBuffer.AsSpan(bytes.Length), offset);
            }

            ArrayPool<byte>.Shared.Return(rentedBuffer);
            if (bytes.Length > 0)
            {
                ThrowInvalidDataException();
            }

            void RecordError(Span<byte> data)
            {
                if (_logger.IsError)
                {
                    string line = Encoding.UTF8.GetString(data);
                    _logger.Error($"Error when loading data from {Name} - expected two items separated by a comma and got '{line}')");
                }
            }

            static void ThrowInvalidDataException()
            {
                throw new InvalidDataException("Malformed data");
            }
        }

        private byte[] Update(byte[] oldValue, byte[] newValue)
        {
            if (!Bytes.AreEqual(oldValue, newValue))
            {
                _hasPendingChanges = true;
            }

            return newValue;
        }

        private byte[] Add(byte[] value)
        {
            _hasPendingChanges = true;
            return value;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
