using NUnit.Framework;

using StackTraceExplorer.Parsers;

namespace StackTraceExplorer.Tests
{
	[TestFixture]
	public class WatsonCrashParserTests
	{
		private WatsonCrashParser parser = null!;

		[SetUp]
		public void SetUp()
		{
			parser = new WatsonCrashParser();
		}

		[Test]
		public void Parse_ManagedFrame_WithSourceInfo()
		{
			var line = "mscorlib_ni!System.Reflection.Assembly.LoadFile+0x0 [f:\\dd\\ndp\\clr\\src\\BCL\\system\\reflection\\assembly.cs @ 588]";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.AssemblyName, Is.EqualTo("mscorlib"));
			Assert.That(frame.FullTypeName, Is.EqualTo("System.Reflection.Assembly"));
			Assert.That(frame.MethodName, Is.EqualTo("LoadFile"));
			Assert.That(frame.FrameType, Is.EqualTo(StackFrameType.Method));
		}

		[Test]
		public void Parse_ManagedFrame_WithoutSourceInfo()
		{
			var line = "Microsoft_Crm_Core!Microsoft.Crm.Core.Helpers.DynamicBindingRedirector.CurrentDomain_AssemblyResolve+0x0";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.AssemblyName, Is.EqualTo("Microsoft.Crm.Core"));
			Assert.That(frame.FullTypeName, Is.EqualTo("Microsoft.Crm.Core.Helpers.DynamicBindingRedirector"));
			Assert.That(frame.MethodName, Is.EqualTo("CurrentDomain_AssemblyResolve"));
		}

		[Test]
		public void Parse_NativeFrame_ReturnsUnknownType()
		{
			var line = "clr!AppDomain::BindAssemblySpec+0x0 [f:\\dd\\ndp\\clr\\src\\vm\\appdomain.cpp @ 9084]";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FrameType, Is.EqualTo(StackFrameType.Unknown));
			Assert.That(frame.AssemblyName, Is.EqualTo("clr"));
		}

		[Test]
		public void Parse_AsyncStateMachine()
		{
			var line = "unknown!Microsoft.Identity.Client.OAuth2.OAuth2Client+_ExecuteRequestAsync_d__13";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FrameType, Is.EqualTo(StackFrameType.AsyncStateMachine));
			Assert.That(frame.FullTypeName, Is.EqualTo("Microsoft.Identity.Client.OAuth2.OAuth2Client"));
		}

		[Test]
		public void Parse_GenericTypeWithBrackets()
		{
			var line = "mscorlib_ni!System.Threading.Tasks.Task`1[[System.__Canon, mscorlib]].TrySetResult+0x0 [[System.__Canon, mscorlib @ 490]";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FullTypeName, Is.EqualTo("System.Threading.Tasks.Task"));
			Assert.That(frame.MethodName, Is.EqualTo("TrySetResult"));
		}

		[Test]
		public void Parse_NestedClass()
		{
			var line = "System_Net_Http!System.Net.Http.HttpClient+__c__DisplayClass55_0 [f:\\dd\\NDP\\fx\\src\\net\\System\\Net\\Http\\HttpClient.cs @ 450]";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.AssemblyName, Is.EqualTo("System.Net.Http"));
			Assert.That(frame.FullTypeName, Does.Contain("HttpClient"));
		}

		[Test]
		public void CanParse_WatsonFormat_ReturnsTrue()
		{
			var line = "mscorlib_ni!System.Reflection.Assembly.LoadFile+0x0 [f:\\dd\\ndp\\clr\\src\\BCL\\system\\reflection\\assembly.cs @ 588]";

			Assert.That(parser.CanParse(line), Is.True);
		}

		[Test]
		public void CanParse_WinDbgFormat_ReturnsFalse()
		{
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.CrmDataReader.GetString(int i) (IL=0x0000, Native=0x00007FFA6EA9DDF0+0x31)";

			Assert.That(parser.CanParse(line), Is.False);
		}

		[Test]
		public void CanParse_StandardNetFormat_ReturnsFalse()
		{
			var line = "   at System.String.Format(String format, Object[] args)";

			Assert.That(parser.CanParse(line), Is.False);
		}

		[Test]
		public void Parse_KernelBaseFrame_ReturnsUnknown()
		{
			var line = "KERNELBASE!RaiseException+0x0 [minkernel\\kernelbase\\xcpt.c @ 955]";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FrameType, Is.EqualTo(StackFrameType.Unknown));
		}

		[Test]
		public void Parse_VCRuntimeFrame_ReturnsUnknown()
		{
			var line = "VCRUNTIME140_CLR0400!__RethrowException+0x0 [f:\\dd\\vctools\\crt\\vcruntime\\src\\eh\\frame.cpp @ 1274]";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FrameType, Is.EqualTo(StackFrameType.Unknown));
		}

		[Test]
		public void Parse_FrameWithInnerClass()
		{
			var line = "mscorlib_ni!System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner [f:\\dd\\ndp\\clr\\src\\BCL\\system\\runtime\\compilerservices\\AsyncMethodBuilder.cs @ 1070]";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.AssemblyName, Is.EqualTo("mscorlib"));
			// MoveNextRunner is a nested type inside AsyncMethodBuilderCore
			Assert.That(frame.FullTypeName, Does.Contain("AsyncMethodBuilderCore"));
		}
	}
}
