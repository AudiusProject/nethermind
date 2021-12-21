﻿//  Copyright (c) 2021 Demerzel Solutions Limited
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

using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Network.Enr;

/// <summary>
/// Represents an Ethereum Node Record (ENR) as defined in https://eips.ethereum.org/EIPS/eip-778
/// </summary>
public class NodeRecord
{
    private long _enrSequence;

    private string? _enrString;

    private Keccak? _contentHash;

    private SortedDictionary<string, EnrContentEntry> Entries { get; } = new();

    /// <summary>
    /// This field is used when this <see cref="NodeRecord"/> is deserialized and an unknown entry is encountered.
    /// In such cases we do not know the RLP serialization format of such an entry and we store the original RLP
    /// in order to be able to verify the signature. I think that we may replace it by Keccak(OriginalContentRlp).
    /// </summary>
    public byte[]? OriginalContentRlp { get; set; }

    /// <summary>
    /// Represents the version / id / sequence of the node record data. It should be increased by one with each
    /// update to the node data. Setting sequence on this class wipes out <see cref="EnrString"/> and
    /// <see cref="ContentHash"/>.
    /// </summary>
    public long EnrSequence
    {
        get => _enrSequence;
        set
        {
            if (_enrSequence != value)
            {
                _enrSequence = value;
                _enrString = null;
                _contentHash = null;
                Signature = null;
            }
        }
    }

    /// <summary>
    /// A base64 string representing a node record with the 'enr:' prefix
    /// enr:-IS4QHCYrYZbAK(...)WM0xOIN1ZHCCdl8
    /// </summary>
    public string EnrString
    {
        get
        {
            return _enrString ??= CreateEnrString();
        }
    }

    /// <summary>
    /// Hash of the content, i.e. Keccak([seq, k, v, ...]) as defined in https://eips.ethereum.org/EIPS/eip-778
    /// </summary>
    public Keccak ContentHash
    {
        get
        {
            return _contentHash ??= CalculateContentHash();
        }
    }

    private Keccak CalculateContentHash()
    {
        KeccakRlpStream rlpStream = new();
        EncodeContent(rlpStream);
        return rlpStream.GetHash();
    }

    /// <summary>
    /// A signature resulting from a secp256k1 signing of the [seq, k, v, ...] content.
    /// </summary>
    public Signature? Signature { get; set; }

    public NodeRecord()
    {
        SetEntry(IdEntry.Instance);
    }

    /// <summary>
    /// Sets one of the record entries. Entries are then automatically sorted by keys.
    /// </summary>
    /// <param name="entry"></param>
    public void SetEntry(EnrContentEntry entry)
    {
        if (Entries.ContainsKey(entry.Key))
        {
            EnrSequence++;
        }

        Entries[entry.Key] = entry;
    }

    /// <summary>
    /// Gets a record entry value (in case of the value types). Use <see cref="GetObj{TValue}"/> for reference types./>
    /// </summary>
    /// <param name="entryKey">Key of the entry to retrieve.</param>
    /// <typeparam name="TValue">Type of the entry value.</typeparam>
    /// <returns>Value of the entry or <value>null</value> if the entry is missing.</returns>
    public TValue? GetValue<TValue>(string entryKey) where TValue : struct
    {
        if (Entries.ContainsKey(entryKey))
        {
            EnrContentEntry<TValue> entry = (EnrContentEntry<TValue>)Entries[entryKey];
            return entry.Value;
        }

        return null;
    }

    /// <summary>
    /// Gets a record entry value (in case of the ref types). Use <see cref="GetValue{TValue}"/> for value types./>
    /// </summary>
    /// <param name="entryKey">Key of the entry to retrieve.</param>
    /// <typeparam name="TValue">Type of the entry value.</typeparam>
    /// <returns>Value of the entry or <value>null</value> if the entry is missing.</returns>
    public TValue? GetObj<TValue>(string entryKey) where TValue : class
    {
        if (Entries.ContainsKey(entryKey))
        {
            EnrContentEntry<TValue> entry = (EnrContentEntry<TValue>)Entries[entryKey];
            return entry.Value;
        }

        return null;
    }

    /// <summary>
    /// Needed for an optimized content serialization. We serialize content to calculate signatures.
    /// </summary>
    /// <returns>Length of the Rlp([seq, k, v, ...]) when calculated without the RLP sequence prefix.</returns>
    private int GetContentLengthWithoutSignature()
    {
        int contentLength =
            Rlp.LengthOf(EnrSequence); // this is a different meaning of a sequence than the RLP sequence
        foreach ((_, EnrContentEntry enrContentEntry) in Entries)
        {
            contentLength += enrContentEntry.GetRlpLength();
        }

        return contentLength;
    }

    /// <summary>
    /// Needed for optimized RLP serialization.
    /// </summary>
    /// <returns>Length of the Rlp([signature, seq, k, v, ...]) when calculated without the RLP sequence prefix.</returns>
    private int GetContentLengthWithSignature()
    {
        return GetContentLengthWithoutSignature() + 64 + 2;
    }

    /// <summary>
    /// Needed for optimized RLP serialization when a proper length byte array has to be allocated upfront.
    /// </summary>
    /// <returns>Length of the Rlp([signature, seq, k, v, ...])</returns>
    public int GetRlpLengthWithSignature()
    {
        return Rlp.LengthOfSequence(
            GetContentLengthWithSignature());
    }

    /// <summary>
    /// Applies Rlp([seq, k, v, ...]]).
    /// </summary>
    /// <param name="rlpStream">An RLP stream to encode the content to.</param>
    private void EncodeContent(RlpStream rlpStream)
    {
        int contentLength = GetContentLengthWithoutSignature();
        rlpStream.StartSequence(contentLength);
        rlpStream.Encode(EnrSequence);
        foreach ((_, EnrContentEntry contentEntry) in Entries.OrderBy(e => e.Key))
        {
            contentEntry.Encode(rlpStream);
        }
    }

    /// <summary>
    /// Added here for diagnostic purposes - hes is easier to read and compare.
    /// </summary>
    /// <returns>Rlp([signature, seq, k, v, ...]) as a hex string</returns>
    public string GetHex()
    {
        int contentLength = GetContentLengthWithSignature();
        int totalLength = Rlp.LengthOfSequence(contentLength);
        RlpStream rlpStream = new(totalLength);
        Encode(rlpStream);
        return rlpStream.Data!.ToHexString();
    }

    /// <summary>
    /// Applies Rlp([signature, seq, k, v, ...]]).
    /// </summary>
    /// <param name="rlpStream">An RLP stream to encode the content to.</param>
    public void Encode(RlpStream rlpStream)
    {
        RequireSignature();

        int contentLength = GetContentLengthWithSignature();
        rlpStream.StartSequence(contentLength);
        rlpStream.Encode(Signature!.Bytes);
        rlpStream.Encode(EnrSequence); // a different sequence here (not RLP sequence)
        foreach ((_, EnrContentEntry contentEntry) in Entries.OrderBy(e => e.Key))
        {
            contentEntry.Encode(rlpStream);
        }
    }

    private string CreateEnrString()
    {
        RequireSignature();

        int rlpLength = GetRlpLengthWithSignature();
        RlpStream rlpStream = new(rlpLength);
        Encode(rlpStream);
        byte[] rlpData = rlpStream.Data!;
        
        // https://tools.ietf.org/html/rfc4648#section-5
        // Base64Url must be used, hence the replace calls.
        // Convert.ToBase64String uses '+'. '/' signs and padding.
        return string.Concat("enr:",
            Convert.ToBase64String(rlpData).Replace("+", "-").Replace("/", "_").Replace("=", ""));
    }

    private void RequireSignature()
    {
        if (Signature is null)
        {
            throw new Exception("Cannot encode a node record with an empty signature.");
        }
    }
}