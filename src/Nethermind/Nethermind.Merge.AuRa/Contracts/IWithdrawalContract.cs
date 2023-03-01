// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Merge.AuRa.Contracts;

public interface IWithdrawalContract
{
    void ExecuteWithdrawals(BlockHeader blockHeader, UInt256 failedMaxCount, ulong[] amounts, Address[] addresses);
}
