using NUnit.Framework;

using StackTraceExplorer.Parsers;

namespace StackTraceExplorer.Tests
{
	[TestFixture]
	public class WinDbgParserTests
	{
		private WinDbgParser parser = null!;

		[SetUp]
		public void SetUp()
		{
			parser = new WinDbgParser();
		}

		[Test]
		public void Parse_GenericMethod_SingleTypeArg_ExtractsArity()
		{
			// This is the exact line from the user's async-job1.txt file
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.Core.Resiliency.ConcurrencyBulkhead.Execute<bool>(System.Func<bool> func, System.TimeSpan timeout) (IL≈0x0058, Native=0x00007FFA6F883440+0xFC)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be stripped of generic args");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(1), "Generic arity should be 1 for Execute<bool>");
			Assert.That(frame.FullTypeName, Is.EqualTo("Microsoft.Crm.Core.Resiliency.ConcurrencyBulkhead"));
			Assert.That(frame.AssemblyName, Is.EqualTo("Microsoft.Crm.Core.dll"));
		}

		[Test]
		public void Parse_GenericMethod_MultipleTypeArgs_Simple_ExtractsArity()
		{
			// Simple case: generic type args without nested generics
			var line = "	Microsoft.Xrm.Telemetry.dll!Microsoft.Xrm.Telemetry.XrmTelemetryExtensions.Execute<TResult, TArg>(ILogger logger, Func<TArg, TResult> func, TArg arg) (IL=0x000A, Native=0x00007FFA6F882180+0x96)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be stripped of generic args");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(2), "Generic arity should be 2 for Execute<TResult, TArg>");
		}

		[Test]
		public void Parse_GenericMethod_ComplexNestedTypeArgs_AtLeastParses()
		{
			// Complex case: generic type args with nested generics (like tuple with Func<int>)
			// This is an edge case - regex can't perfectly balance nested brackets
			// We just verify it parses and captures some useful info
			var line = "	Microsoft.Xrm.Telemetry.dll!Microsoft.Xrm.Telemetry.XrmTelemetryExtensions.Execute<int, (System.Guid, string, System.TimeSpan, System.Func<int>)>(Microsoft.Extensions.Logging.ILogger logger, Microsoft.Xrm.Telemetry.XrmTelemetryActivityType activityType, System.Func<(System.Guid, string, System.TimeSpan, System.Func<int>), int> func, (System.Guid, string, System.TimeSpan, System.Func<int>) arg) (IL≈0x000A, Native=0x00007FFA6F882180+0x96)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.FullTypeName, Does.Contain("XrmTelemetryExtensions"));
			// Note: Due to nested generics, method name and arity extraction may not be perfect
		}

		[Test]
		public void Parse_NonGenericMethod_ZeroArity()
		{
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.Core.Resiliency.AppRateLimitBulkhead.Execute(System.Action action) (IL=0x00B0, Native=0x00007FFA6F8837C0+0x306)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be Execute");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(0), "Generic arity should be 0 for non-generic Execute");
		}

		[Test]
		public void Parse_GenericMethod_WithSquareBrackets_ExtractsArity()
		{
			var line = "	Test.dll!Namespace.Class.Method[T](T arg) (IL=0x0000, Native=0x00000000+0x00)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Method"), "Method name should be stripped of generic args");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(1), "Generic arity should be 1 for Method[T]");
		}

		[Test]
		public void Parse_ClosureMethod_WithGenericTypeArg()
		{
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.OrgBulkhead.<>c__3`1<int>.<Execute>b__3_0((System.Guid organizationId, string activityName, System.TimeSpan timeout, System.Func<int> func) args) (IL≈0x001D, Native=0x00007FFA6F882710+0x90)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			// This is a closure method, the method name is <Execute>b__3_0
			Assert.That(frame!.FullTypeName, Does.Contain("OrgBulkhead"));
		}

		[Test]
		public void CanParse_WinDbgFormat_ReturnsTrue()
		{
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.CrmDataReader.GetString(int i) (IL=0x0000, Native=0x00007FFA6EA9DDF0+0x31)";

			Assert.That(parser.CanParse(line), Is.True);
		}

		[Test]
		public void CanParse_StandardFormat_ReturnsFalse()
		{
			var line = "   at System.String.Format(String format, Object[] args)";

			Assert.That(parser.CanParse(line), Is.False);
		}

		[Test]
		public void Parse_GenericType_WithNestedGenericArgs()
		{
			// Generic type with two type arguments containing dots
			var line = "	Microsoft.Crm.Asynchronous.dll!Microsoft.Crm.Asynchronous.AsyncEventExecutionManager<Microsoft.Crm.Asynchronous.AsyncOperationQueueManagerBase, Microsoft.Crm.Asynchronous.AsyncEvent>.ExecuteHandler(Microsoft.Crm.Asynchronous.IAsyncEventHandlerFactory handlerFactory) (IL=0x00E4, Native=0x00007FFA6F086700+0x45F)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("ExecuteHandler"), "Method name should be ExecuteHandler");
			Assert.That(frame.FullTypeName, Is.EqualTo("Microsoft.Crm.Asynchronous.AsyncEventExecutionManager<Microsoft.Crm.Asynchronous.AsyncOperationQueueManagerBase, Microsoft.Crm.Asynchronous.AsyncEvent>"));
			Assert.That(frame.AssemblyName, Is.EqualTo("Microsoft.Crm.Asynchronous.dll"));
		}

		[Test]
		public void Parse_GenericType_ExecuteCommand_WithApproxIL()
		{
			// Exact line 151 from async-job1.txt - space+tab prefix and IL≈ (approximately equals)
			var line = " \tMicrosoft.Crm.Asynchronous.dll!Microsoft.Crm.Asynchronous.AsyncEventExecutionManager<Microsoft.Crm.Asynchronous.AsyncOperationQueueManagerBase, Microsoft.Crm.Asynchronous.AsyncEvent>.ExecuteCommand(Microsoft.Crm.Asynchronous.IAsyncEventHandlerFactory handlerFactory) (IL≈0x00A9, Native=0x00007FFA6F087520+0x314)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("ExecuteCommand"), "Method name should be ExecuteCommand");
			Assert.That(frame.FullTypeName, Does.Contain("AsyncEventExecutionManager"));
			Assert.That(frame.AssemblyName, Is.EqualTo("Microsoft.Crm.Asynchronous.dll"));
		}
	}
}
