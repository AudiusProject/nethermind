//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
//
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System;
using Nethermind.Config;

namespace Nethermind.Blockchain.Synchronization
{
    [ConfigCategory(Description = "Configuration of the synchronization modes.")]
    public class SyncConfig : ISyncConfig
    {
        private bool _synchronizationEnabled = true;
        private bool? _fastSync;
        private bool? _snapSync;
        private StateSyncMode? _syncMode;

        public StateSyncMode SyncMode
        {
            get
            {
                if (_snapSync.HasValue && _snapSync.Value)
                {
                    return StateSyncMode.SnapSync;
                }

                if (_fastSync.HasValue)
                {
                    return _fastSync.Value ? StateSyncMode.FastSync : StateSyncMode.FullSync;
                }

                return _syncMode ?? StateSyncMode.FullSync;
            }
            set => _syncMode = value;
        }

        public static ISyncConfig Default { get; } = new SyncConfig();

        public static ISyncConfig WithFullSyncOnly { get; } =
            new SyncConfig { FastBlocks = false, SyncMode = StateSyncMode.FullSync };

        public static ISyncConfig WithFastSync { get; } = new SyncConfig { SyncMode = StateSyncMode.FastSync };

        public static ISyncConfig WithFastBlocks { get; } =
            new SyncConfig { FastBlocks = true, SyncMode = StateSyncMode.FastSync };

        public static ISyncConfig WithEth2Merge { get; } =
            new SyncConfig { FastBlocks = false, BlockGossipEnabled = false };

        public bool NetworkingEnabled { get; set; } = true;

        public bool SynchronizationEnabled
        {
            get => NetworkingEnabled && _synchronizationEnabled;
            set => _synchronizationEnabled = value;
        }

        public long? FastSyncCatchUpHeightDelta { get; set; } = 8192;
        public bool FastBlocks { get; set; }
        public bool UseGethLimitsInFastBlocks { get; set; } = true;

        [Obsolete("FastSync flag will be deprecated in the future, consider using SyncMode.", false)]
        public bool FastSync { set => _fastSync = value; }

        public bool DownloadHeadersInFastSync { get; set; } = true;
        public bool DownloadBodiesInFastSync { get; set; } = true;
        public bool DownloadReceiptsInFastSync { get; set; } = true;
        public long AncientBodiesBarrier { get; set; }
        public long AncientReceiptsBarrier { get; set; }
        public string PivotTotalDifficulty { get; set; }
        public string PivotNumber { get; set; }
        public string PivotHash { get; set; }
        public bool WitnessProtocolEnabled { get; set; } = false;

        [Obsolete("SnapSync flag will be deprecated in the future, consider using SyncMode.", false)]
        public bool SnapSync { set => _snapSync = value; }
        public bool FixReceipts { get; set; } = false;
        public bool BlockGossipEnabled { get; set; } = true;
    }
}
