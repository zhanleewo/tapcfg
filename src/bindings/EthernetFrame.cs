/**
 *  tapcfg - A cross-platform configuration utility for TAP driver
 *  Copyright (C) 2008  Juho Vähä-Herttua
 *
 *  This library is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU Lesser General Public
 *  License as published by the Free Software Foundation; either
 *  version 2.1 of the License, or (at your option) any later version.
 *
 *  This library is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 *  Lesser General Public License for more details.
 */

using System;

namespace TAPCfg {
	public enum EtherType : int {
		InterNetwork   = 0x0800,
		ARP            = 0x0806,
		RARP           = 0x8035,
		AppleTalk      = 0x809b,
		AARP           = 0x80f3,
		InterNetworkV6 = 0x86dd,
		CobraNet       = 0x8819,
	}

	public class EthernetFrame {
		private byte[] data;
		private byte[] src = new byte[6];
		private byte[] dst = new byte[6];
		private int etherType;

		public EthernetFrame(byte[] data) {
			this.data = data;
			Array.Copy(data, 0, dst, 0, 6);
			Array.Copy(data, 6, src, 0, 6);
			etherType = (data[12] << 8) | data[13];
		}

		public byte[] Data {
			get { return data; }
		}

		public int Length {
			get { return data.Length; }
		}

		public byte[] SourceAddress {
			get { return src; }
		}

		public byte[] DestinationAddress {
			get { return dst; }
		}

		public int EtherType {
			get { return etherType; }
		}

		public byte[] Payload {
			get {
				byte[] ret = new byte[data.Length - 14];
				Array.Copy(data, 14, ret, 0, ret.Length);
				return ret;
			}
		}
	}
}
