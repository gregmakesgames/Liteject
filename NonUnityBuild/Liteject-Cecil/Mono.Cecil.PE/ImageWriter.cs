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

using Liteject.ReflectionBaking.Mono.Cecil.Cil;
using Liteject.ReflectionBaking.Mono.Cecil.Metadata;

using RVA = System.UInt32;

namespace Liteject.ReflectionBaking.Mono.Cecil.PE {

	sealed class ImageWriter : BinaryStreamWriter {

		readonly ModuleDefinition module;
		readonly MetadataBuilder metadata;
		readonly TextMap text_map;

		ImageDebugDirectory debug_directory;
		byte [] debug_data;

		ByteBuffer win32_resources;

		const uint pe_header_size = 0x98u;
		const uint section_header_size = 0x28u;
		const uint file_alignment = 0x200;
		const uint section_alignment = 0x2000;
		const ulong image_base = 0x00400000;

		internal const RVA text_rva = 0x2000;

		readonly bool pe64;
		readonly bool has_reloc;
		readonly uint time_stamp;

		internal Section text;
		internal Section rsrc;
		internal Section reloc;

		ushort sections;

		ImageWriter (ModuleDefinition module, MetadataBuilder metadata, Stream stream)
			: base (stream)
		{
			this.module = module;
			this.metadata = metadata;
			this.pe64 = module.Architecture == TargetArchitecture.AMD64 || module.Architecture == TargetArchitecture.IA64;
			this.has_reloc = module.Architecture == TargetArchitecture.I386;
			this.GetDebugHeader ();
			this.GetWin32Resources ();
			this.text_map = BuildTextMap ();
			this.sections = (ushort) (has_reloc ? 2 : 1); // text + reloc?
			this.time_stamp = (uint) DateTime.UtcNow.Subtract (new DateTime (1970, 1, 1)).TotalSeconds;
		}

		void GetDebugHeader ()
		{
			var symbol_writer = metadata.symbol_writer;
			if (symbol_writer == null)
				return;

			if (!symbol_writer.GetDebugHeader (out debug_directory, out debug_data))
				debug_data = Empty<byte>.Array;
		}

		void GetWin32Resources ()
		{
			var rsrc = GetImageResourceSection ();
			if (rsrc == null)
				return;

			var raw_resources = new byte [rsrc.Data.Length];
			Buffer.BlockCopy (rsrc.Data, 0, raw_resources, 0, rsrc.Data.Length);
			win32_resources = new ByteBuffer (raw_resources);
		}

		Section GetImageResourceSection ()
		{
			if (!module.HasImage)
				return null;

			const string rsrc_section = ".rsrc";

			return module.Image.GetSection (rsrc_section);
		}

		public static ImageWriter CreateWriter (ModuleDefinition module, MetadataBuilder metadata, Stream stream)
		{
			var writer = new ImageWriter (module, metadata, stream);
			writer.BuildSections ();
			return writer;
		}

		void BuildSections ()
		{
			var has_win32_resources = win32_resources != null;
			if (has_win32_resources)
				sections++;

			text = CreateSection (".text", text_map.GetLength (), null);
			var previous = text;

			if (has_win32_resources) {
				rsrc = CreateSection (".rsrc", (uint) win32_resources.length, previous);

				PatchWin32Resources (win32_resources);
				previous = rsrc;
			}

			if (has_reloc)
				reloc = CreateSection (".reloc", 12u, previous);
		}

		Section CreateSection (string name, uint size, Section previous)
		{
			return new Section {
				Name = name,
				VirtualAddress = previous != null
					? previous.VirtualAddress + Align (previous.VirtualSize, section_alignment)
					: text_rva,
				VirtualSize = size,
				PointerToRawData = previous != null
					? previous.PointerToRawData + previous.SizeOfRawData
					: Align (GetHeaderSize (), file_alignment),
				SizeOfRawData = Align (size, file_alignment)
			};
		}

		static uint Align (uint value, uint align)
		{
			align--;
			return (value + align) & ~align;
		}

		void WriteDOSHeader ()
		{
			Write (new byte [] {
				// dos header start
				0x4d, 0x5a, 0x90, 0x00, 0x03, 0x00, 0x00,
				0x00, 0x04, 0x00, 0x00, 0x00, 0xff, 0xff,
				0x00, 0x00, 0xb8, 0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x00, 0x00, 0x00, 0x00,
				// lfanew
				0x80, 0x00, 0x00, 0x00,
				// dos header end
				0x0e, 0x1f, 0xba, 0x0e, 0x00, 0xb4, 0x09,
				0xcd, 0x21, 0xb8, 0x01, 0x4c, 0xcd, 0x21,
				0x54, 0x68, 0x69, 0x73, 0x20, 0x70, 0x72,
				0x6f, 0x67, 0x72, 0x61, 0x6d, 0x20, 0x63,
				0x61, 0x6e, 0x6e, 0x6f, 0x74, 0x20, 0x62,
				0x65, 0x20, 0x72, 0x75, 0x6e, 0x20, 0x69,
				0x6e, 0x20, 0x44, 0x4f, 0x53, 0x20, 0x6d,
				0x6f, 0x64, 0x65, 0x2e, 0x0d, 0x0d, 0x0a,
				0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x00
			});
		}

		ushort SizeOfOptionalHeader ()
		{
			return (ushort) (!pe64 ? 0xe0 : 0xf0);
		}

		void WritePEFileHeader ()
		{
			WriteUInt32 (0x00004550);		// Magic
			WriteUInt16 (GetMachine ());	// Machine
			WriteUInt16 (sections);			// NumberOfSections
			WriteUInt32 (time_stamp);
			WriteUInt32 (0);	// PointerToSymbolTable
			WriteUInt32 (0);	// NumberOfSymbols
			WriteUInt16 (SizeOfOptionalHeader ());	// SizeOfOptionalHeader

			// ExecutableImage | (pe64 ? 32BitsMachine : LargeAddressAware)
			var characteristics = (ushort) (0x0002 | (!pe64 ? 0x0100 : 0x0020));
			if (module.Kind == ModuleKind.Dll || module.Kind == ModuleKind.NetModule)
				characteristics |= 0x2000;
			WriteUInt16 (characteristics);	// Characteristics
		}

		ushort GetMachine ()
		{
			switch (module.Architecture) {
			case TargetArchitecture.I386:
				return 0x014c;
			case TargetArchitecture.AMD64:
				return 0x8664;
			case TargetArchitecture.IA64:
				return 0x0200;
			case TargetArchitecture.ARMv7:
				return 0x01c4;
			}

			throw new NotSupportedException ();
		}

		Section LastSection ()
		{
			if (reloc != null)
				return reloc;

			if (rsrc != null)
				return rsrc;

			return text;
		}

		void WriteOptionalHeaders ()
		{
			WriteUInt16 ((ushort) (!pe64 ? 0x10b : 0x20b));	// Magic
			WriteByte (8);	// LMajor
			WriteByte (0);	// LMinor
			WriteUInt32 (text.SizeOfRawData);	// CodeSize
			WriteUInt32 ((reloc != null ? reloc.SizeOfRawData : 0)
				+ (rsrc != null ? rsrc.SizeOfRawData : 0));	// InitializedDataSize
			WriteUInt32 (0);	// UninitializedDataSize

			var startub_stub = text_map.GetRange (TextSegment.StartupStub);
			WriteUInt32 (startub_stub.Length > 0 ? startub_stub.Start : 0);  // EntryPointRVA
			WriteUInt32 (text_rva);	// BaseOfCode

			if (!pe64) {
				WriteUInt32 (0);	// BaseOfData
				WriteUInt32 ((uint) image_base);	// ImageBase
			} else {
				WriteUInt64 (image_base);	// ImageBase
			}

			WriteUInt32 (section_alignment);	// SectionAlignment
			WriteUInt32 (file_alignment);		// FileAlignment

			WriteUInt16 (4);	// OSMajor
			WriteUInt16 (0);	// OSMinor
			WriteUInt16 (0);	// UserMajor
			WriteUInt16 (0);	// UserMinor
			WriteUInt16 (4);	// SubSysMajor
			WriteUInt16 (0);	// SubSysMinor
			WriteUInt32 (0);	// Reserved

			var last_section = LastSection();
			WriteUInt32 (last_section.VirtualAddress + Align (last_section.VirtualSize, section_alignment));	// ImageSize
			WriteUInt32 (text.PointerToRawData);	// HeaderSize

			WriteUInt32 (0);	// Checksum
			WriteUInt16 (GetSubSystem ());	// SubSystem
			WriteUInt16 ((ushort) module.Characteristics);	// DLLFlags

			const ulong stack_reserve = 0x100000;
			const ulong stack_commit = 0x1000;
			const ulong heap_reserve = 0x100000;
			const ulong heap_commit = 0x1000;

			if (!pe64) {
				WriteUInt32 ((uint) stack_reserve);
				WriteUInt32 ((uint) stack_commit);
				WriteUInt32 ((uint) heap_reserve);
				WriteUInt32 ((uint) heap_commit);
			} else {
				WriteUInt64 (stack_reserve);
				WriteUInt64 (stack_commit);
				WriteUInt64 (heap_reserve);
				WriteUInt64 (heap_commit);
			}

			WriteUInt32 (0);	// LoaderFlags
			WriteUInt32 (16);	// NumberOfDataDir

			WriteZeroDataDirectory ();	// ExportTable
			WriteDataDirectory (text_map.GetDataDirectory (TextSegment.ImportDirectory));	// ImportTable
			if (rsrc != null) {							// ResourceTable
				WriteUInt32 (rsrc.VirtualAddress);
				WriteUInt32 (rsrc.VirtualSize);
			} else
				WriteZeroDataDirectory ();

			WriteZeroDataDirectory ();	// ExceptionTable
			WriteZeroDataDirectory ();	// CertificateTable
			WriteUInt32 (reloc != null ? reloc.VirtualAddress : 0);			// BaseRelocationTable
			WriteUInt32 (reloc != null ? reloc.VirtualSize : 0);

			if (text_map.GetLength (TextSegment.DebugDirectory) > 0) {
				WriteUInt32 (text_map.GetRVA (TextSegment.DebugDirectory));
				WriteUInt32 (28u);
			} else
				WriteZeroDataDirectory ();

			WriteZeroDataDirectory ();	// Copyright
			WriteZeroDataDirectory ();	// GlobalPtr
			WriteZeroDataDirectory ();	// TLSTable
			WriteZeroDataDirectory ();	// LoadConfigTable
			WriteZeroDataDirectory ();	// BoundImport
			WriteDataDirectory (text_map.GetDataDirectory (TextSegment.ImportAddressTable));	// IAT
			WriteZeroDataDirectory ();	// DelayImportDesc
			WriteDataDirectory (text_map.GetDataDirectory (TextSegment.CLIHeader));	// CLIHeader
			WriteZeroDataDirectory ();	// Reserved
		}

		void WriteZeroDataDirectory ()
		{
			WriteUInt32 (0);
			WriteUInt32 (0);
		}

		ushort GetSubSystem ()
		{
			switch (module.Kind) {
			case ModuleKind.Console:
			case ModuleKind.Dll:
			case ModuleKind.NetModule:
				return 0x3;
			case ModuleKind.Windows:
				return 0x2;
			default:
				throw new ArgumentOutOfRangeException ();
			}
		}

		void WriteSectionHeaders ()
		{
			WriteSection (text, 0x60000020);

			if (rsrc != null)
				WriteSection (rsrc, 0x40000040);

			if (reloc != null)
				WriteSection (reloc, 0x42000040);
		}

		void WriteSection (Section section, uint characteristics)
		{
			var name = new byte [8];
			var sect_name = section.Name;
			for (int i = 0; i < sect_name.Length; i++)
				name [i] = (byte) sect_name [i];

			WriteBytes (name);
			WriteUInt32 (section.VirtualSize);
			WriteUInt32 (section.VirtualAddress);
			WriteUInt32 (section.SizeOfRawData);
			WriteUInt32 (section.PointerToRawData);
			WriteUInt32 (0);	// PointerToRelocations
			WriteUInt32 (0);	// PointerToLineNumbers
			WriteUInt16 (0);	// NumberOfRelocations
			WriteUInt16 (0);	// NumberOfLineNumbers
			WriteUInt32 (characteristics);
		}

		void MoveTo (uint pointer)
		{
			BaseStream.Seek (pointer, SeekOrigin.Begin);
		}

		void MoveToRVA (Section section, RVA rva)
		{
			BaseStream.Seek (section.PointerToRawData + rva - section.VirtualAddress, SeekOrigin.Begin);
		}

		void MoveToRVA (TextSegment segment)
		{
			MoveToRVA (text, text_map.GetRVA (segment));
		}

		void WriteRVA (RVA rva)
		{
			if (!pe64)
				WriteUInt32 (rva);
			else
				WriteUInt64 (rva);
		}

		void PrepareSection (Section section)
		{
			MoveTo (section.PointerToRawData);

			const int buffer_size = 4096;

			if (section.SizeOfRawData <= buffer_size) {
				Write (new byte [section.SizeOfRawData]);
				MoveTo (section.PointerToRawData);
				return;
			}

			var written = 0;
			var buffer = new byte [buffer_size];
			while (written != section.SizeOfRawData) {
				var write_size = System.Math.Min((int) section.SizeOfRawData - written, buffer_size);
				Write (buffer, 0, write_size);
				written += write_size;
			}

			MoveTo (section.PointerToRawData);
		}

		void WriteText ()
		{
			PrepareSection (text);

			// ImportAddressTable

			if (has_reloc) {
				WriteRVA (text_map.GetRVA (TextSegment.ImportHintNameTable));
				WriteRVA (0);
			}

			// CLIHeader

			WriteUInt32 (0x48);
			WriteUInt16 (2);
			WriteUInt16 ((ushort) ((module.Runtime <= TargetRuntime.Net_1_1) ? 0 : 5));

			WriteUInt32 (text_map.GetRVA (TextSegment.MetadataHeader));
			WriteUInt32 (GetMetadataLength ());
			WriteUInt32 ((uint) module.Attributes);
			WriteUInt32 (metadata.entry_point.ToUInt32 ());
			WriteDataDirectory (text_map.GetDataDirectory (TextSegment.Resources));
			WriteDataDirectory (text_map.GetDataDirectory (TextSegment.StrongNameSignature));
			WriteZeroDataDirectory ();	// CodeManagerTable
			WriteZeroDataDirectory ();	// VTableFixups
			WriteZeroDataDirectory ();	// ExportAddressTableJumps
			WriteZeroDataDirectory ();	// ManagedNativeHeader

			// Code

			MoveToRVA (TextSegment.Code);
			WriteBuffer (metadata.code);

			// Resources

			MoveToRVA (TextSegment.Resources);
			WriteBuffer (metadata.resources);

			// Data

			if (metadata.data.length > 0) {
				MoveToRVA (TextSegment.Data);
				WriteBuffer (metadata.data);
			}

			// StrongNameSignature
			// stays blank

			// MetadataHeader

			MoveToRVA (TextSegment.MetadataHeader);
			WriteMetadataHeader ();

			WriteMetadata ();

			// DebugDirectory
			if (text_map.GetLength (TextSegment.DebugDirectory) > 0) {
				MoveToRVA (TextSegment.DebugDirectory);
				WriteDebugDirectory ();
			}

			if (!has_reloc)
				return;

			// ImportDirectory
			MoveToRVA (TextSegment.ImportDirectory);
			WriteImportDirectory ();

			// StartupStub
			MoveToRVA (TextSegment.StartupStub);
			WriteStartupStub ();
		}

		uint GetMetadataLength ()
		{
			return text_map.GetRVA (TextSegment.DebugDirectory) - text_map.GetRVA (TextSegment.MetadataHeader);
		}

		void WriteMetadataHeader ()
		{
			WriteUInt32 (0x424a5342);	// Signature
			WriteUInt16 (1);	// MajorVersion
			WriteUInt16 (1);	// MinorVersion
			WriteUInt32 (0);	// Reserved

			var version = GetZeroTerminatedString (module.runtime_version);
			WriteUInt32 ((uint) version.Length);
			WriteBytes (version);
			WriteUInt16 (0);	// Flags
			WriteUInt16 (GetStreamCount ());

			uint offset = text_map.GetRVA (TextSegment.TableHeap) - text_map.GetRVA (TextSegment.MetadataHeader);

			WriteStreamHeader (ref offset, TextSegment.TableHeap, "#~");
			WriteStreamHeader (ref offset, TextSegment.StringHeap, "#Strings");
			WriteStreamHeader (ref offset, TextSegment.UserStringHeap, "#US");
			WriteStreamHeader (ref offset, TextSegment.GuidHeap, "#GUID");
			WriteStreamHeader (ref offset, TextSegment.BlobHeap, "#Blob");
		}

		ushort GetStreamCount ()
		{
			return (ushort) (
				1	// #~
				+ 1	// #Strings
				+ (metadata.user_string_heap.IsEmpty ? 0 : 1)	// #US
				+ 1	// GUID
				+ (metadata.blob_heap.IsEmpty ? 0 : 1));	// #Blob
		}

		void WriteStreamHeader (ref uint offset, TextSegment heap, string name)
		{
			var length = (uint) text_map.GetLength (heap);
			if (length == 0)
				return;

			WriteUInt32 (offset);
			WriteUInt32 (length);
			WriteBytes (GetZeroTerminatedString (name));
			offset += length;
		}

		static byte [] GetZeroTerminatedString (string @string)
		{
			return GetString (@string, (@string.Length + 1 + 3) & ~3);
		}

		static byte [] GetSimpleString (string @string)
		{
			return GetString (@string, @string.Length);
		}

		static byte [] GetString (string @string, int length)
		{
			var bytes = new byte [length];
			for (int i = 0; i < @string.Length; i++)
				bytes [i] = (byte) @string [i];

			return bytes;
		}

		void WriteMetadata ()
		{
			WriteHeap (TextSegment.TableHeap, metadata.table_heap);
			WriteHeap (TextSegment.StringHeap, metadata.string_heap);
			WriteHeap (TextSegment.UserStringHeap, metadata.user_string_heap);
			WriteGuidHeap ();
			WriteHeap (TextSegment.BlobHeap, metadata.blob_heap);
		}

		void WriteHeap (TextSegment heap, HeapBuffer buffer)
		{
			if (buffer.IsEmpty)
				return;

			MoveToRVA (heap);
			WriteBuffer (buffer);
		}

		void WriteGuidHeap ()
		{
			MoveToRVA (TextSegment.GuidHeap);
			WriteBytes (module.Mvid.ToByteArray ());
		}

		void WriteDebugDirectory ()
		{
			WriteInt32 (debug_directory.Characteristics);
			WriteUInt32 (time_stamp);
			WriteInt16 (debug_directory.MajorVersion);
			WriteInt16 (debug_directory.MinorVersion);
			WriteInt32 (debug_directory.Type);
			WriteInt32 (debug_directory.SizeOfData);
			WriteInt32 (debug_directory.AddressOfRawData);
			WriteInt32 ((int) BaseStream.Position + 4);

			WriteBytes (debug_data);
		}

		void WriteImportDirectory ()
		{
			WriteUInt32 (text_map.GetRVA (TextSegment.ImportDirectory) + 40);	// ImportLookupTable
			WriteUInt32 (0);	// DateTimeStamp
			WriteUInt32 (0);	// ForwarderChain
			WriteUInt32 (text_map.GetRVA (TextSegment.ImportHintNameTable) + 14);
			WriteUInt32 (text_map.GetRVA (TextSegment.ImportAddressTable));
			Advance (20);

			// ImportLookupTable
			WriteUInt32 (text_map.GetRVA (TextSegment.ImportHintNameTable));

			// ImportHintNameTable
			MoveToRVA (TextSegment.ImportHintNameTable);

			WriteUInt16 (0);	// Hint
			WriteBytes (GetRuntimeMain ());
			WriteByte (0);
			WriteBytes (GetSimpleString ("mscoree.dll"));
			WriteUInt16 (0);
		}

		byte [] GetRuntimeMain ()
		{
			return module.Kind == ModuleKind.Dll || module.Kind == ModuleKind.NetModule
				? GetSimpleString ("_CorDllMain")
				: GetSimpleString ("_CorExeMain");
		}

		void WriteStartupStub ()
		{
			switch (module.Architecture) {
			case TargetArchitecture.I386:
				WriteUInt16 (0x25ff);
				WriteUInt32 ((uint) image_base + text_map.GetRVA (TextSegment.ImportAddressTable));
				return;
			default:
				throw new NotSupportedException ();
			}
		}

		void WriteRsrc ()
		{
			PrepareSection (rsrc);
			WriteBuffer (win32_resources);
		}

		void WriteReloc ()
		{
			PrepareSection (reloc);

			var reloc_rva = text_map.GetRVA (TextSegment.StartupStub);
			reloc_rva += module.Architecture == TargetArchitecture.IA64 ? 0x20u : 2;
			var page_rva = reloc_rva & ~0xfffu;

			WriteUInt32 (page_rva);	// PageRVA
			WriteUInt32 (0x000c);	// Block Size

			switch (module.Architecture) {
			case TargetArchitecture.I386:
				WriteUInt32 (0x3000 + reloc_rva - page_rva);
				break;
			default:
				throw new NotSupportedException();
			}
		}

		public void WriteImage ()
		{
			WriteDOSHeader ();
			WritePEFileHeader ();
			WriteOptionalHeaders ();
			WriteSectionHeaders ();
			WriteText ();
			if (rsrc != null)
				WriteRsrc ();
			if (reloc != null)
				WriteReloc ();
		}

		TextMap BuildTextMap ()
		{
			var map = metadata.text_map;

			map.AddMap (TextSegment.Code, metadata.code.length, !pe64 ? 4 : 16);
			map.AddMap (TextSegment.Resources, metadata.resources.length, 8);
			map.AddMap (TextSegment.Data, metadata.data.length, 4);
			if (metadata.data.length > 0)
				metadata.table_heap.FixupData (map.GetRVA (TextSegment.Data));
			map.AddMap (TextSegment.StrongNameSignature, GetStrongNameLength (), 4);

			map.AddMap (TextSegment.MetadataHeader, GetMetadataHeaderLength ());
			map.AddMap (TextSegment.TableHeap, metadata.table_heap.length, 4);
			map.AddMap (TextSegment.StringHeap, metadata.string_heap.length, 4);
			map.AddMap (TextSegment.UserStringHeap, metadata.user_string_heap.IsEmpty ? 0 : metadata.user_string_heap.length, 4);
			map.AddMap (TextSegment.GuidHeap, 16);
			map.AddMap (TextSegment.BlobHeap, metadata.blob_heap.IsEmpty ? 0 : metadata.blob_heap.length, 4);

			int debug_dir_len = 0;
			if (!debug_data.IsNullOrEmpty ()) {
				const int debug_dir_header_len = 28;

				debug_directory.AddressOfRawData = (int) map.GetNextRVA (TextSegment.BlobHeap) + debug_dir_header_len;
				debug_dir_len = debug_data.Length + debug_dir_header_len;
			}

			map.AddMap (TextSegment.DebugDirectory, debug_dir_len, 4);

			if (!has_reloc) {
				var start = map.GetNextRVA (TextSegment.DebugDirectory);
				map.AddMap (TextSegment.ImportDirectory, new Range (start, 0));
				map.AddMap (TextSegment.ImportHintNameTable, new Range (start, 0));
				map.AddMap (TextSegment.StartupStub, new Range (start, 0));
				return map;
			}

			RVA import_dir_rva = map.GetNextRVA (TextSegment.DebugDirectory);
			RVA import_hnt_rva = import_dir_rva + 48u;
			import_hnt_rva = (import_hnt_rva + 15u) & ~15u;
			uint import_dir_len = (import_hnt_rva - import_dir_rva) + 27u;

			RVA startup_stub_rva = import_dir_rva + import_dir_len;
			startup_stub_rva = module.Architecture == TargetArchitecture.IA64
				? (startup_stub_rva + 15u) & ~15u
				: 2 + ((startup_stub_rva + 3u) & ~3u);

			map.AddMap (TextSegment.ImportDirectory, new Range (import_dir_rva, import_dir_len));
			map.AddMap (TextSegment.ImportHintNameTable, new Range (import_hnt_rva, 0));
			map.AddMap (TextSegment.StartupStub, new Range (startup_stub_rva, GetStartupStubLength ()));

			return map;
		}

		uint GetStartupStubLength ()
		{
			switch (module.Architecture) {
			case TargetArchitecture.I386:
				return 6;
			default:
				throw new NotSupportedException ();
			}
		}

		int GetMetadataHeaderLength ()
		{
			return
				// MetadataHeader
				40
				// #~ header
				+ 12
				// #Strings header
				+ 20
				// #US header
				+ (metadata.user_string_heap.IsEmpty ? 0 : 12)
				// #GUID header
				+ 16
				// #Blob header
				+ (metadata.blob_heap.IsEmpty ? 0 : 16);
		}

		int GetStrongNameLength ()
		{
			if (module.Assembly == null)
				return 0;

			var public_key = module.Assembly.Name.PublicKey;
			if (public_key.IsNullOrEmpty ())
				return 0;

			// in fx 2.0 the key may be from 384 to 16384 bits
			// so we must calculate the signature size based on
			// the size of the public key (minus the 32 byte header)
			int size = public_key.Length;
			if (size > 32)
				return size - 32;

			// note: size == 16 for the ECMA "key" which is replaced
			// by the runtime with a 1024 bits key (128 bytes)

			return 128; // default strongname signature size
		}

		public DataDirectory GetStrongNameSignatureDirectory ()
		{
			return text_map.GetDataDirectory (TextSegment.StrongNameSignature);
		}

		public uint GetHeaderSize ()
		{
			return pe_header_size + SizeOfOptionalHeader () + (sections * section_header_size);
		}

		void PatchWin32Resources (ByteBuffer resources)
		{
			PatchResourceDirectoryTable (resources);
		}

		void PatchResourceDirectoryTable (ByteBuffer resources)
		{
			resources.Advance (12);

			var entries = resources.ReadUInt16 () + resources.ReadUInt16 ();

			for (int i = 0; i < entries; i++)
				PatchResourceDirectoryEntry (resources);
		}

		void PatchResourceDirectoryEntry (ByteBuffer resources)
		{
			resources.Advance (4);
			var child = resources.ReadUInt32 ();

			var position = resources.position;
			resources.position = (int) child & 0x7fffffff;

			if ((child & 0x80000000) != 0)
				PatchResourceDirectoryTable (resources);
			else
				PatchResourceDataEntry (resources);

			resources.position = position;
		}

		void PatchResourceDataEntry (ByteBuffer resources)
		{
			var old_rsrc = GetImageResourceSection ();
			var rva = resources.ReadUInt32 ();
			resources.position -= 4;
			resources.WriteUInt32 (rva - old_rsrc.VirtualAddress + rsrc.VirtualAddress);
		}
	}
}

#endif
