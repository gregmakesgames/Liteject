//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System;
using System.IO;

#if !READ_ONLY

namespace Liteject.ReflectionBaking.Mono.Cecil.PE {

	class BinaryStreamWriter : BinaryWriter {

		public BinaryStreamWriter (Stream stream)
			: base (stream)
		{
		}

		public void WriteByte (byte value)
		{
			Write (value);
		}

		public void WriteUInt16 (ushort value)
		{
			Write (value);
		}

		public void WriteInt16 (short value)
		{
			Write (value);
		}

		public void WriteUInt32 (uint value)
		{
			Write (value);
		}

		public void WriteInt32 (int value)
		{
			Write (value);
		}

		public void WriteUInt64 (ulong value)
		{
			Write (value);
		}

		public void WriteBytes (byte [] bytes)
		{
			Write (bytes);
		}

		public void WriteDataDirectory (DataDirectory directory)
		{
			Write (directory.VirtualAddress);
			Write (directory.Size);
		}

		public void WriteBuffer (ByteBuffer buffer)
		{
			Write (buffer.buffer, 0, buffer.length);
		}

		protected void Advance (int bytes)
		{
			BaseStream.Seek (bytes, SeekOrigin.Current);
		}
	}
}

#endif
