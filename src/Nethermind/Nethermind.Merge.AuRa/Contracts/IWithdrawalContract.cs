// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Merge.AuRa.Contracts;

public interface IWithdrawalContract
{
    void ExecuteWithdrawals(BlockHeader blockHeader, ulong[] amounts, Address[] addresses);
}
