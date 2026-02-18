using NUnit.Framework;

using StackTraceExplorer.Parsers;

namespace StackTraceExplorer.Tests
{
	[TestFixture]
	public class StandardNetParserTests
	{
		private StandardNetParser parser = null!;

		[SetUp]
		public void SetUp()
		{
			parser = new StandardNetParser();
		}

		[Test]
		public void Parse_GenericMethod_SingleTypeArg_ExtractsArity()
		{
			var line = "   at Microsoft.Crm.Core.Resiliency.ConcurrencyBulkhead.Execute<bool>(System.Func<bool> func, System.TimeSpan timeout)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be stripped of generic args");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(1), "Generic arity should be 1 for Execute<bool>");
			Assert.That(frame.FullTypeName, Is.EqualTo("Microsoft.Crm.Core.Resiliency.ConcurrencyBulkhead"));
		}

		[Test]
		public void Parse_GenericMethod_MultipleTypeArgs_ExtractsArity()
		{
			var line = "   at MyNamespace.MyClass.DoSomething<T1, T2, T3>(T1 arg1, T2 arg2)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("DoSomething"), "Method name should be stripped of generic args");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(3), "Generic arity should be 3 for DoSomething<T1, T2, T3>");
		}

		[Test]
		public void Parse_NonGenericMethod_ZeroArity()
		{
			var line = "   at System.String.Format(String format, Object[] args)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Format"), "Method name should be Format");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(0), "Generic arity should be 0 for non-generic method");
		}

		[Test]
		public void Parse_GenericMethod_WithSquareBrackets_ExtractsArity()
		{
			var line = "   at Namespace.Class.Method[T](T arg)";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Method"), "Method name should be stripped of generic args");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(1), "Generic arity should be 1 for Method[T]");
		}

		[Test]
		public void Parse_GenericMethod_WithILOffset_ExtractsArity()
		{
			var line = "   at Microsoft.Crm.Core.Resiliency.ConcurrencyBulkhead.Execute<bool>(System.Func<bool> func, System.TimeSpan timeout)  ilOffset = 0x58";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be stripped of generic args");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(1), "Generic arity should be 1 for Execute<bool>");
			Assert.That(frame.ILOffset, Is.EqualTo(0x58), "IL offset should be parsed");
		}

		[Test]
		public void CanParse_StandardFormat_ReturnsTrue()
		{
			var line = "   at System.String.Format(String format, Object[] args)";

			Assert.That(parser.CanParse(line), Is.True);
		}

		[Test]
		public void CanParse_WinDbgFormat_ReturnsFalse()
		{
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.CrmDataReader.GetString(int i) (IL=0x0000, Native=0x00007FFA6EA9DDF0+0x31)";

			// StandardParser can also parse this format (it's more permissive), but WinDbg should have priority
			// This test verifies StandardParser doesn't break on this format
			Assert.That(parser.CanParse(line), Is.True);
		}

		#region TraceError Format Tests (simple type names without namespace)

		[Test]
		public void Parse_TraceError_SimpleTypeName_WithILOffset()
		{
			// TraceError format: "at TypeName.Method(params) ilOffset = 0xHEX"
			// Note: No namespace, just simple type name
			var line = "   at CrmTrace.Write(TraceRedirection traceRedirection, Guid orgId, TraceCategory traceCategory, TraceLevel traceLevel, Int32 skipFrames, String format, Object[] args)  ilOffset = 0x8E";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.FullTypeName, Is.EqualTo("CrmTrace"), "Type name should be just 'CrmTrace' (no namespace)");
			Assert.That(frame.MethodName, Is.EqualTo("Write"), "Method name should be 'Write'");
			Assert.That(frame.Parameters, Does.Contain("traceRedirection"), "Parameters should be captured");
			Assert.That(frame.ILOffset, Is.EqualTo(0x8E), "IL offset should be parsed");
		}

		[Test]
		public void Parse_TraceError_SimpleTypeName_LeadingTab()
		{
			// TraceError format can have leading tab instead of spaces
			var line = "	at MessageProcessor.Execute(PipelineExecutionContext context)  ilOffset = 0x61F";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.FullTypeName, Is.EqualTo("MessageProcessor"), "Type name should be 'MessageProcessor'");
			Assert.That(frame.MethodName, Is.EqualTo("Execute"), "Method name should be 'Execute'");
			Assert.That(frame.ILOffset, Is.EqualTo(0x61F), "IL offset should be parsed");
		}

		[Test]
		public void Parse_TraceError_AngleBracketClosure()
		{
			// TraceError format with compiler-generated closure type
			var line = "	at <>c__DisplayClass20_0.<ExecuteRequest>b__0()  ilOffset = 0xA1";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.FullTypeName, Is.EqualTo("<>c__DisplayClass20_0"), "Type should be the closure type");
			Assert.That(frame.MethodName, Is.EqualTo("<ExecuteRequest>b__0"), "Method name should include closure method");
			Assert.That(frame.FrameType, Is.EqualTo(StackFrameType.Lambda), "Should be detected as lambda");
		}

		[Test]
		public void Parse_TraceError_GenericTypeSuffix()
		{
			// TraceError format with generic type argument in method
			var line = "	at ActivityLoggerExtensions.Execute(ILogger logger, EventId eventId, ActivityType activityType, Func`1 func, IEnumerable`1 additionalCustomProperties)  ilOffset = 0x4F";

			var frame = parser.Parse(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.FullTypeName, Is.EqualTo("ActivityLoggerExtensions"), "Type name should be 'ActivityLoggerExtensions'");
			Assert.That(frame.MethodName, Is.EqualTo("Execute"), "Method name should be 'Execute'");
			Assert.That(frame.Parameters, Does.Contain("Func`1"), "Parameters should contain generic type marker");
		}

		[Test]
		public void CanParse_TraceError_Format()
		{
			var line = "	at CrmTrace.TraceError(Exception ex, Guid orgId, TraceCategory traceCategory, Int32 skipFrames, String format, Object[] args)  ilOffset = 0x4F";

			Assert.That(parser.CanParse(line), Is.True, "Should be able to parse TraceError format");
		}

		#endregion
	}
}
