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
	public class IPv4Packet {
		private byte _tos;
		private int _id;
		private byte _flags;
		private short _frag_offset;
		private byte _ttl;
		private byte _protocol;

		private byte[] _src = new byte[4];
		private byte[] _dst = new byte[4];
		private byte[] _options;

		private byte[] _payload;

		public IPv4Packet(byte[] data) {
			if (data.Length < 20) {
				throw new Exception("IPv4 packet too small to include a header");
			}

			int version = (data[0] >> 4) & 0x0f;
			byte header_length = (byte) (data[0] & 0x0f);
			_tos = data[1];
			int total_length = (data[2] << 8) | data[3];
			_id = (data[4] << 8) | data[5];
			_flags = (byte) ((data[6] >> 5) & 0x07);
			_frag_offset = (short) (((data[6] & 0x1f) << 8) | data[7]);
			_ttl = data[8];
			_protocol = data[9];
			int checksum = (data[10] << 8) | data[11];
			Array.Copy(data, 12, _src, 0, 4);
			Array.Copy(data, 16, _dst, 0, 4);

			if (version != 4) {
				throw new Exception("IPv4 packet version field not 4");
			}

			if (total_length < header_length * 4 || total_length > data.Length) {
				throw new Exception("Invalid total length: " + total_length);
			}

			if (header_length < 5) {
				throw new Exception("Invalid header length: " + header_length);
			}

			if (checksum != this.calculateChecksum(data)) {
				throw new Exception("Checksum didn't match, corrupted packet");
			}

			_options = new byte[(header_length - 5) * 4];
			Array.Copy(data, 20, _options, 0, _options.Length);

			_payload = new byte[total_length - 20 - _options.Length];
			Array.Copy(data, 20 + _options.Length, _payload, 0, _payload.Length);
		}

		public byte[] Data {
			get {
				int total_length = 20 + _options.Length + _payload.Length;
				byte[] data = new byte[total_length];
				data[0] = (byte) ((4 << 4) | (5 + _options.Length/4));
				data[1] = _tos;
				data[2] = (byte) ((total_length >> 8) & 0xff);
				data[3] = (byte) (total_length & 0xff);
				data[4] = (byte) ((_id >> 8) & 0xff);
				data[5] = (byte) (_id & 0xff);
				data[6] = (byte) (((_flags << 5) & 0xe0) |
				                  ((_frag_offset >> 8) & 0x1f));
				data[7] = (byte) (_frag_offset & 0xff);
				data[8] = _ttl;
				data[9] = _protocol;
				Array.Copy(_src, 0, data, 12, 4);
				Array.Copy(_dst, 0, data, 16, 4);
				Array.Copy(_options, 0, data, 20, _options.Length);
				Array.Copy(_payload, 0, data, 20 + _options.Length, _payload.Length);

				int checksum = this.calculateChecksum(data);
				data[10] = (byte) ((checksum >> 8) & 0xff);
				data[11] = (byte) (checksum & 0xff);

				return data;
			}
		}

		public byte TypeOfService {
			get { return _tos; }
			set { _tos = value; }
		}

		public int ID {
			get { return _id; }
			set {
				if (value > 0xffff)
					throw new Exception("ID value too big");
				_id = value;
			}
		}

		public byte Flags {
			get { return _flags; }
		}

		public short FragmentOffset {
			get { return _frag_offset; }
		}

		public byte TimeToLive {
			get { return _ttl; }
			set { _ttl = value; }
		}

		public byte Protocol {
			get { return _protocol; }
			set { _protocol = value; }
		}

		public IPAddress Source {
			get { return new IPAddress(_src); }
			set {
				if (value.AddressFamily != AddressFamily.InterNetwork)
					throw new Exception("Source address not an IPv4 address");
			}
		}

		public IPAddress Destination {
			get { return new IPAddress(_dst); }
			set {
				if (value.AddressFamily != AddressFamily.InterNetwork)
					throw new Exception("Source address not an IPv4 address");
			}
		}

		public byte[] Options {
			get { return _options; }
			set {
				if (_options.Length % 4 != 0)
					throw new Exception("Options length must be divisible with 4");
				_options = value;
			}
		}

		public byte[] Payload {
			get { return _payload; }
			set { _payload = value; }
		}

		private int calculateChecksum(byte[] header) {
			int ret = 0;
			int words = (header[0] & 0x0f) * 2;

			for (int i=0; i<words; i++) {
				/* Ignore the checksum field */
				if (i == 5) continue;

				ret += (header[i*2] << 8) | header[i*2+1];
			}
			ret = (ret & 0xffff) + (ret >> 16);

			/* Return one's complement of the result */
			return (ret ^ 0xffff);
		}
	}
}
