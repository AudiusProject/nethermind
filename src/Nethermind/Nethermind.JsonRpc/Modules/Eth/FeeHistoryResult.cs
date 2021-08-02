
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
// 

using Nethermind.Int256;

namespace Nethermind.JsonRpc.Modules.Eth
{
    public class FeeHistoryResult
    {
        private long? _oldestBlock;
        public UInt256[]?[]? _reward;
        public UInt256[]? _baseFee;
        public float[]? _gasUsedRatio;
        public const int MaxHistory = 1;

        public FeeHistoryResult(long? oldestBlock = null, UInt256[][]? reward = null, UInt256[]? baseFee = null, float[]? gasUsedRatio = null)
        {
            _oldestBlock = oldestBlock;
            _reward = reward;
            _baseFee = baseFee;
            _gasUsedRatio = gasUsedRatio;
        }
    }
}
