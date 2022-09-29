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

using System;
using System.Collections.Generic;
using FluentAssertions;
using Nethermind.Network.P2P.Subprotocols.Snap.Messages;
using NUnit.Framework;

namespace Nethermind.Network.Test.P2P.Subprotocols.Snap.Messages
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class ByteCodesMessageSerializerTests
    {
        [Test]
        public void Roundtrip()
        {
            byte[][] data = { new byte[] { 0xde, 0xad, 0xc0, 0xde }, new byte[] { 0xfe, 0xed } };

            ByteCodesMessage message = new(data);

            ByteCodesMessageSerializer serializer = new();

            SerializerTester.TestZero(serializer, message);
        }

        [Test]
        public void DecodeEncodeDecodeEmpty()
        {
            byte[] data = {202, 136, 23, 106, 21, 106, 229, 131, 72, 176, 192};
            ByteCodesMessageSerializer serializer = new();
            ByteCodesMessage decode = serializer.Deserialize(data);
            byte[] messageEncode = serializer.Serialize(decode);
            messageEncode.Should().BeEquivalentTo(data);
        }
    }
}
