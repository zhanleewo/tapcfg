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
using System.Net;
using System.Net.Sockets;

namespace TAP {
	public class NDPrefixInfo {
		private byte _prefix_len;
		private byte _flags_reserved = 0;

		private int _valid_time;
		private int _preferred_time;

		private byte[] _prefix;

		public NDPrefixInfo(IPAddress prefix, byte length) {
			this.AdvOnLink     = true;
			this.AdvAutonomous = true;
			this.AdvRouterAddr = false;
			_valid_time        = 2592000;
			_preferred_time    = 604800;

			this.Prefix = prefix;
			this.PrefixLength = length;
		}

		public byte[] Data {
			get {
				byte[] ret = new byte[32];
				ret[0]  = 3;               /* ND_OPT_PREFIX_INFORMATION  */
				ret[1]  = 4;               /* len */
				ret[2]  = _prefix_len;
				ret[3]  = _flags_reserved;
				ret[4]  = (byte) ((_valid_time >> 24) & 0xff);
				ret[5]  = (byte) ((_valid_time >> 16) & 0xff);
				ret[6]  = (byte) ((_valid_time >>  8) & 0xff);
				ret[7]  = (byte) (_valid_time & 0xff);
				ret[8]  = (byte) ((_preferred_time >> 24) & 0xff);
				ret[9]  = (byte) ((_preferred_time >> 16) & 0xff);
				ret[10] = (byte) ((_preferred_time >>  8) & 0xff);
				ret[11] = (byte) (_preferred_time & 0xff);
				ret[12] = ret[13] = ret[14] = ret[15] = 0;
				Array.Copy(_prefix, 0, ret, 16, 16);
				return ret;
			}
		}

		public int Length {
			get { return 32; }
		}

		public bool AdvOnLink {
			get { return (_flags_reserved & 0x80) != 0; }
			set { setFlag(0x80, value); }
		}

		public bool AdvAutonomous {
			get { return (_flags_reserved & 0x40) != 0; }
			set { setFlag(0x40, value); }
		}

		public bool AdvRouterAddr {
			get { return (_flags_reserved & 0x20) != 0; }
			set { setFlag(0x20, value); }
		}

		public int AdvValidLifetime {
			get { return _valid_time; }
			set { _valid_time = value; }
		}

		public int AdvPreferredLifetime {
			get { return _preferred_time; }
			set { _preferred_time = value; }
		}

		public IPAddress Prefix {
			get { return new IPAddress(_prefix); }
			set {
				if (value.AddressFamily != AddressFamily.InterNetworkV6)
					throw new Exception("Prefix address not an IPv6 address");
				_prefix = value.GetAddressBytes();
			}
		}

		public byte PrefixLength {
			get { return _prefix_len; }
			set { _prefix_len = value; }
		}

		private void setFlag(byte mask, bool value) {
			if (value) {
				_flags_reserved |= mask;
			} else {
				_flags_reserved &= (byte) (~mask);
			}
		}
	}

	public class NDRouterAdvPacket {
		private byte _curhoplimit;
		private byte _flags_reserved;
		private short _router_lifetime;
		private int _reachable;
		private int _retransmit;

		private NDPrefixInfo _prefix;

		public NDRouterAdvPacket(IPAddress addr) {
			_curhoplimit = 64;
			this.AdvManagedFlag = false;
			this.AdvOtherConfigFlag = false;
			this.AdvHomeAgentFlag = false;
			_router_lifetime = 0;
			_reachable = 0;
			_retransmit = 0;

			_prefix = new NDPrefixInfo(addr, 64);
		}

		public byte[] Data {
			get {
				byte[] data = new byte[16 + _prefix.Length];
				data[0]  = (byte) ICMPv6Type.RouterAdvertisement;
				data[1]  = 0; /* code */
				data[2]  = 0; /* checksum-hi */
				data[3]  = 0; /* checksum.lo */
				data[4]  = _curhoplimit;
				data[5]  = _flags_reserved;
				data[6]  = (byte) ((_router_lifetime >> 8) & 0xff);
				data[7]  = (byte) (_router_lifetime & 0xff);
				data[8]  = (byte) ((_reachable >> 24) & 0xff);
				data[9]  = (byte) ((_reachable >> 16) & 0xff);
				data[10] = (byte) ((_reachable >>  8) & 0xff);
				data[11] = (byte) (_reachable & 0xff);
				data[12] = (byte) ((_retransmit >> 24) & 0xff);
				data[13] = (byte) ((_retransmit >> 16) & 0xff);
				data[14] = (byte) ((_retransmit >>  8) & 0xff);
				data[15] = (byte) (_retransmit & 0xff);

				byte[] prefix = _prefix.Data;
				Array.Copy(prefix, 0, data, 16, prefix.Length);
				return data;
			}
		}

		public byte AdvCurHopLimit {
			get { return _curhoplimit; }
			set { _curhoplimit = value; }
		}

		public bool AdvManagedFlag {
			get { return (_flags_reserved & 0x80) != 0; }
			set { setFlag(0x80, value); }
		}

		public bool AdvOtherConfigFlag {
			get { return (_flags_reserved & 0x40) != 0; }
			set { setFlag(0x40, value); }
		}

		public bool AdvHomeAgentFlag {
			get { return (_flags_reserved & 0x20) != 0; }
			set { setFlag(0x20, value); }
		}

		public short AdvDefaultLifetime {
			get { return _router_lifetime; }
			set { _router_lifetime = value; }
		}

		public int AdvReachableTime {
			get { return _reachable; }
			set { _reachable = value; }
		}

		public int AdvRetransTime {
			get { return _retransmit; }
			set { _retransmit = value; }
		}

		private void setFlag(byte mask, bool value) {
			if (value) {
				_flags_reserved |= mask;
			} else {
				_flags_reserved &= (byte) (~mask);
			}
		}
	}
}