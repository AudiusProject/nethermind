// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Crypto;
using Nethermind.Logging;
using Nethermind.Network.Rlpx.Handshake;
using NUnit.Framework;

namespace Nethermind.Network.Test.Rlpx.Handshake
{
    [Parallelizable(ParallelScope.Self)]
    [TestFixture]
    public class AuthMessageSerializerTests
    {
        private const string TestPrivateKeyHex = "0x3a1076bf45ab87712ad64ccb3b10217737f7faacbf2872e88fdd9a537d8fe266";

        private readonly Random _random = new(1);

        private readonly PrivateKey _privateKey = new(TestPrivateKeyHex);

        private readonly AuthMessageSerializer _serializer = new();

        private void TestEncodeDecode(IEthereumEcdsa ecdsa)
        {
            AuthMessage authMessage = new();
            authMessage.EphemeralPublicHash = new Keccak(new byte[AuthMessageSerializer.EphemeralHashLength]);
            authMessage.Nonce = new byte[AuthMessageSerializer.NonceLength];
            authMessage.Signature = ecdsa.Sign(_privateKey, Keccak.Compute("anything"));
            _random.NextBytes(authMessage.EphemeralPublicHash.Bytes);
            authMessage.PublicKey = _privateKey.PublicKey;
            _random.NextBytes(authMessage.Nonce);
            authMessage.IsTokenUsed = true;
            byte[] bytes = _serializer.Serialize(authMessage);
            AuthMessage after = _serializer.Deserialize(bytes);

            Assert.AreEqual(authMessage.Signature, after.Signature);
            Assert.AreEqual(authMessage.EphemeralPublicHash, after.EphemeralPublicHash);
            Assert.AreEqual(authMessage.PublicKey, after.PublicKey);
            Assert.True(Bytes.AreEqual(authMessage.Nonce, after.Nonce));
            Assert.AreEqual(authMessage.IsTokenUsed, after.IsTokenUsed);
        }

        [TestCase(ChainId.Mainnet)]
        [TestCase(ChainId.Morden)]
        [TestCase(ChainId.RootstockMainnet)]
        [TestCase(ChainId.DefaultGethPrivateChain)]
        [TestCase(ChainId.EthereumClassicMainnet)]
        [TestCase(ChainId.EthereumClassicTestnet)]
        public void Encode_decode_before_eip155(int chainId)
        {
            EthereumEcdsa ecdsa = new(ChainId.Olympic, LimboLogs.Instance);
            TestEncodeDecode(ecdsa);
        }

        [TestCase(ChainId.Mainnet)]
        [TestCase(ChainId.Ropsten)]
        [TestCase(ChainId.Kovan)]
        public void Encode_decode_with_eip155(int chainId)
        {
            EthereumEcdsa ecdsa = new(ChainId.Olympic, LimboLogs.Instance);
            TestEncodeDecode(ecdsa);
        }
    }
}
