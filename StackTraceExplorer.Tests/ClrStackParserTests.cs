using NUnit.Framework;

using StackTraceExplorer.Parsers;

namespace StackTraceExplorer.Tests
{
	[TestFixture]
	public class ClrStackParserTests
	{
		private ClrStackParser parser = null!;

		[SetUp]
		public void SetUp()
		{
			parser = new ClrStackParser();
		}

		[Test]
		public void Parse_SimpleMethod()
		{
			var line = "00000027A658D6E0 00007ff8c35869ad System.Reflection.Assembly.LoadFile(System.String)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FullTypeName, Is.EqualTo("System.Reflection.Assembly"));
			Assert.That(frame.MethodName, Is.EqualTo("LoadFile"));
			Assert.That(frame.Parameters, Is.EqualTo("System.String"));
			Assert.That(frame.FrameType, Is.EqualTo(StackFrameType.Method));
		}

		[Test]
		public void Parse_AsyncStateMachine()
		{
			var line = "00000027A667B2A0 00007ff865ea06de Microsoft.Identity.Client.Internal.Requests.RequestBase+d__11.MoveNext()";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FrameType, Is.EqualTo(StackFrameType.AsyncStateMachine));
			Assert.That(frame.FullTypeName, Does.Contain("RequestBase"));
		}

		[Test]
		public void Parse_GenericType()
		{
			var line = "00000027A667B150 00007ff8c2d82c43 System.Lazy`1[[System.__Canon, mscorlib]].CreateValue()";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FullTypeName, Is.EqualTo("System.Lazy"));
			Assert.That(frame.MethodName, Is.EqualTo("CreateValue"));
		}

		[Test]
		public void Parse_NestedClass()
		{
			var line = "00000027A667B480 00007ff8c2dd025f System.Runtime.CompilerServices.AsyncMethodBuilderCore+MoveNextRunner.Run()";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FullTypeName, Does.Contain("AsyncMethodBuilderCore"));
			Assert.That(frame.MethodName, Is.EqualTo("Run"));
		}

		[Test]
		public void Parse_LambdaDisplayClass()
		{
			var line = "00000027A667DBC0 00007ff865df23c2 System.Net.Http.HttpClient+c__DisplayClass59_0.b__0(System.Threading.Tasks.Task)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FrameType, Is.EqualTo(StackFrameType.Lambda));
		}

		[Test]
		public void Parse_GenericTaskResult()
		{
			var line = "00000027A667B560 00007ff8666895ef System.Threading.Tasks.Task`1[[Microsoft.Identity.Client.Utils.MeasureDurationResult, Microsoft.Identity.Client]].TrySetResult(Microsoft.Identity.Client.Utils.MeasureDurationResult)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FullTypeName, Is.EqualTo("System.Threading.Tasks.Task"));
			Assert.That(frame.MethodName, Is.EqualTo("TrySetResult"));
		}

		[Test]
		public void CanParse_ClrStackFormat_ReturnsTrue()
		{
			var line = "00000027A658D6E0 00007ff8c35869ad System.Reflection.Assembly.LoadFile(System.String)";

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
		public void CanParse_RuntimeFrame_ReturnsFalse()
		{
			var line = "00000027A658DA48 00007ff8c4276893 [GCFrame: 00000027a658da48]";

			Assert.That(parser.CanParse(line), Is.False);
		}

		[Test]
		public void Parse_MethodWithMultipleParams()
		{
			var line = "00000027A667E2D0 00007ff8c2d60f25 System.Threading.ExecutionContext.Run(System.Threading.ExecutionContext, System.Threading.ContextCallback, System.Object, Boolean)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null);
			Assert.That(frame!.FullTypeName, Is.EqualTo("System.Threading.ExecutionContext"));
			Assert.That(frame.MethodName, Is.EqualTo("Run"));
			Assert.That(frame.Parameters, Does.Contain("ExecutionContext"));
			Assert.That(frame.Parameters, Does.Contain("Boolean"));
		}
	}
}
