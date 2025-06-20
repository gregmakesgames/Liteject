using System;
using System.IO;
using System.Reflection;
using Liteject.ReflectionBaking.Mono.Cecil.Cil;
using NUnit.Framework;

using Liteject.ReflectionBaking.Mono.Cecil.PE;

namespace Liteject.ReflectionBaking.Mono.Cecil.Tests {

	public abstract class BaseTestFixture {

		protected static void IgnoreOnMono ()
		{
			if (Platform.OnMono)
				Assert.Ignore ();
		}

		public static string GetResourcePath (string name, Assembly assembly)
		{
			return Path.Combine (FindResourcesDirectory (assembly), name);
		}

		public static string GetAssemblyResourcePath (string name, Assembly assembly)
		{
			return GetResourcePath (Path.Combine ("assemblies", name), assembly);
		}

		public static string GetCSharpResourcePath (string name, Assembly assembly)
		{
			return GetResourcePath (Path.Combine ("cs", name), assembly);
		}

		public static string GetILResourcePath (string name, Assembly assembly)
		{
			return GetResourcePath (Path.Combine ("il", name), assembly);
		}

		public ModuleDefinition GetResourceModule (string name)
		{
			return ModuleDefinition.ReadModule (GetAssemblyResourcePath (name, GetType ().Assembly));
		}

		public ModuleDefinition GetResourceModule (string name, ReaderParameters parameters)
		{
			return ModuleDefinition.ReadModule (GetAssemblyResourcePath (name, GetType ().Assembly), parameters);
		}

		public ModuleDefinition GetResourceModule (string name, ReadingMode mode)
		{
			return ModuleDefinition.ReadModule (GetAssemblyResourcePath (name, GetType ().Assembly), new ReaderParameters (mode));
		}

		internal Image GetResourceImage (string name)
		{
			using (var fs = new FileStream (GetAssemblyResourcePath (name, GetType ().Assembly), FileMode.Open, FileAccess.Read))
				return ImageReader.ReadImageFrom (fs);
		}

		public ModuleDefinition GetCurrentModule ()
		{
			return ModuleDefinition.ReadModule (GetType ().Module.FullyQualifiedName);
		}

		public ModuleDefinition GetCurrentModule (ReaderParameters parameters)
		{
			return ModuleDefinition.ReadModule (GetType ().Module.FullyQualifiedName, parameters);
		}

		public static string FindResourcesDirectory (Assembly assembly)
		{
			var path = Path.GetDirectoryName (new Uri (assembly.CodeBase).LocalPath);
			while (!Directory.Exists (Path.Combine (path, "Resources"))) {
				var old = path;
				path = Path.GetDirectoryName (path);
				Assert.AreNotEqual (old, path);
			}

			return Path.Combine (path, "Resources");
		}

		public static void TestModule (string file, Action<ModuleDefinition> test, bool verify = true, bool readOnly = false, Type symbolReaderProvider = null, Type symbolWriterProvider = null)
		{
			Run (new ModuleTestCase (file, test, verify, readOnly, symbolReaderProvider, symbolWriterProvider));
		}

		public static void TestCSharp (string file, Action<ModuleDefinition> test, bool verify = true, bool readOnly = false, Type symbolReaderProvider = null, Type symbolWriterProvider = null)
		{
			Run (new CSharpTestCase (file, test, verify, readOnly, symbolReaderProvider, symbolWriterProvider));
		}

		public static void TestIL (string file, Action<ModuleDefinition> test, bool verify = true, bool readOnly = false, Type symbolReaderProvider = null, Type symbolWriterProvider = null)
		{
			Run (new ILTestCase (file, test, verify, readOnly, symbolReaderProvider, symbolWriterProvider));
		}

		private static void Run (TestCase testCase)
		{
			var runner = new TestRunner (testCase, TestCaseType.ReadDeferred);
			runner.RunTest ();

			runner = new TestRunner (testCase, TestCaseType.ReadImmediate);
			runner.RunTest ();

			if (testCase.ReadOnly)
				return;

			runner = new TestRunner (testCase, TestCaseType.WriteFromDeferred);
			runner.RunTest();

			runner = new TestRunner (testCase, TestCaseType.WriteFromImmediate);
			runner.RunTest();
		}
	}

	abstract class TestCase {

		public readonly bool Verify;
		public readonly bool ReadOnly;
		public readonly Type SymbolReaderProvider;
		public readonly Type SymbolWriterProvider;
		public readonly Action<ModuleDefinition> Test;

		public abstract string ModuleLocation { get; }

		protected Assembly Assembly { get { return Test.Method.Module.Assembly; } }

		protected TestCase (Action<ModuleDefinition> test, bool verify, bool readOnly, Type symbolReaderProvider, Type symbolWriterProvider)
		{
			Test = test;
			Verify = verify;
			ReadOnly = readOnly;
			SymbolReaderProvider = symbolReaderProvider;
			SymbolWriterProvider = symbolWriterProvider;
		}
	}

	class ModuleTestCase : TestCase {

		public readonly string Module;

		public ModuleTestCase (string module, Action<ModuleDefinition> test, bool verify, bool readOnly, Type symbolReaderProvider, Type symbolWriterProvider)
			: base (test, verify, readOnly, symbolReaderProvider, symbolWriterProvider)
		{
			Module = module;
		}

		public override string ModuleLocation
		{
			get { return BaseTestFixture.GetAssemblyResourcePath (Module, Assembly); }
		}
	}

	class CSharpTestCase : TestCase {

		public readonly string File;

		public CSharpTestCase (string file, Action<ModuleDefinition> test, bool verify, bool readOnly, Type symbolReaderProvider, Type symbolWriterProvider)
			: base (test, verify, readOnly, symbolReaderProvider, symbolWriterProvider)
		{
			File = file;
		}

		public override string ModuleLocation
		{
			get
			{
				return CompilationService.CompileResource (BaseTestFixture.GetCSharpResourcePath (File, Assembly));
			}
		}
	}

	class ILTestCase : TestCase {

		public readonly string File;

		public ILTestCase (string file, Action<ModuleDefinition> test, bool verify, bool readOnly, Type symbolReaderProvider, Type symbolWriterProvider)
			: base (test, verify, readOnly, symbolReaderProvider, symbolWriterProvider)
		{
			File = file;
		}

		public override string ModuleLocation
		{
			get
			{
				return CompilationService.CompileResource (BaseTestFixture.GetILResourcePath (File, Assembly)); ;
			}
		}
	}

	class TestRunner {

		readonly TestCase test_case;
		readonly TestCaseType type;

		public TestRunner (TestCase testCase, TestCaseType type)
		{
			this.test_case = testCase;
			this.type = type;
		}

		ModuleDefinition GetModule ()
		{
			var location = test_case.ModuleLocation;
			var directory = Path.GetDirectoryName (location);

			var resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (directory);

			var parameters = new ReaderParameters {
				SymbolReaderProvider = GetSymbolReaderProvider (),
				AssemblyResolver = resolver,
			};

			switch (type) {
			case TestCaseType.ReadImmediate:
				parameters.ReadingMode = ReadingMode.Immediate;
				return ModuleDefinition.ReadModule (location, parameters);
			case TestCaseType.ReadDeferred:
				parameters.ReadingMode = ReadingMode.Deferred;
				return ModuleDefinition.ReadModule (location, parameters);
			case TestCaseType.WriteFromImmediate:
				parameters.ReadingMode = ReadingMode.Immediate;
				return RoundTrip (location, parameters, "cecil-irt");
			case TestCaseType.WriteFromDeferred:
				parameters.ReadingMode = ReadingMode.Deferred;
				return RoundTrip (location, parameters, "cecil-drt");
			default:
				return null;
			}
		}

		ISymbolReaderProvider GetSymbolReaderProvider ()
		{
			if (test_case.SymbolReaderProvider == null)
				return null;

			return (ISymbolReaderProvider) Activator.CreateInstance (test_case.SymbolReaderProvider);
		}

		ISymbolWriterProvider GetSymbolWriterProvider ()
		{
			if (test_case.SymbolReaderProvider == null)
				return null;

			return (ISymbolWriterProvider) Activator.CreateInstance (test_case.SymbolWriterProvider);
		}

		ModuleDefinition RoundTrip (string location, ReaderParameters reader_parameters, string folder)
		{
			var module = ModuleDefinition.ReadModule (location, reader_parameters);
			var rt_folder = Path.Combine (Path.GetTempPath (), folder);
			if (!Directory.Exists (rt_folder))
				Directory.CreateDirectory (rt_folder);
			var rt_module = Path.Combine (rt_folder, Path.GetFileName (location));

			var writer_parameters = new WriterParameters {
				SymbolWriterProvider = GetSymbolWriterProvider (),
			};

			test_case.Test (module);

			module.Write (rt_module, writer_parameters);

			if (test_case.Verify)
				CompilationService.Verify (rt_module);

			return ModuleDefinition.ReadModule (rt_module, reader_parameters);
		}

		public void RunTest ()
		{
			var module = GetModule ();
			if (module == null)
				return;

			test_case.Test(module);
		}
	}

	enum TestCaseType {
		ReadImmediate,
		ReadDeferred,
		WriteFromImmediate,
		WriteFromDeferred,
	}
}
