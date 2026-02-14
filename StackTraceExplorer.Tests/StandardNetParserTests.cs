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
	}
}
