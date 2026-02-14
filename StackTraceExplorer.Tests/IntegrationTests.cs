using NUnit.Framework;

using StackTraceExplorer.Parsers;

namespace StackTraceExplorer.Tests
{
	/// <summary>
	/// Integration tests using real stack trace samples.
	/// </summary>
	[TestFixture]
	public class IntegrationTests
	{
		private CompositeParser parser = null!;

		[SetUp]
		public void SetUp()
		{
			parser = new CompositeParser();
		}

		/// <summary>
		/// Test parsing of the Execute&lt;bool&gt; line from async-job1.txt
		/// This is the key test case for generic method overload selection.
		/// </summary>
		[Test]
		public void AsyncJob1_ExecuteBool_ParsesWithGenericArity()
		{
			// Line 21 from async-job1.txt
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.Core.Resiliency.ConcurrencyBulkhead.Execute<bool>(System.Func<bool> func, System.TimeSpan timeout) (IL≈0x0058, Native=0x00007FFA6F883440+0xFC)";

			var frame = parser.ParseLine(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be stripped of <bool>");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(1), "Generic arity should be 1 for Execute<bool>");
			Assert.That(frame.FullTypeName, Is.EqualTo("Microsoft.Crm.Core.Resiliency.ConcurrencyBulkhead"));
			Assert.That(frame.AssemblyName, Is.EqualTo("Microsoft.Crm.Core.dll"));
		}

		/// <summary>
		/// Test parsing of non-generic Execute line from async-job1.txt
		/// This should have generic arity 0 to ensure the resolver picks the right overload.
		/// </summary>
		[Test]
		public void AsyncJob1_ExecuteAction_ParsesWithZeroArity()
		{
			// Line 18 from async-job1.txt
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.Core.Resiliency.AppRateLimitBulkhead.Execute(System.Action action) (IL=0x00B0, Native=0x00007FFA6F8837C0+0x306)";

			var frame = parser.ParseLine(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be Execute");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(0), "Generic arity should be 0 for non-generic Execute");
		}

		/// <summary>
		/// Test parsing of Execute&lt;int&gt; overload from async-job1.txt
		/// </summary>
		[Test]
		public void AsyncJob1_ExecuteInt_ParsesWithGenericArity()
		{
			// Line 27 from async-job1.txt
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.OrgBulkhead.Execute<int>(System.Guid organizationId, string activityName, System.TimeSpan timeout, System.Func<int> func) (IL≈0x0033, Native=0x00007FFA6F881EE0+0x1B1)";

			var frame = parser.ParseLine(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be stripped of <int>");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(1), "Generic arity should be 1 for Execute<int>");
		}

		/// <summary>
		/// Test parsing of Execute&lt;System.Guid&gt; overload from async-job1.txt
		/// </summary>
		[Test]
		public void AsyncJob1_ExecuteGuid_ParsesWithGenericArity()
		{
			// Line 68 from async-job1.txt
			var line = "	Microsoft.Xrm.Telemetry.dll!Microsoft.Xrm.Telemetry.XrmTelemetryExtensions.Execute<System.Guid>(Microsoft.Extensions.Logging.ILogger logger, Microsoft.Xrm.Telemetry.XrmTelemetryActivityType activityType, System.Func<System.Guid> func) (IL=0x0014, Native=0x00007FFA6F8CD120+0xBB)";

			var frame = parser.ParseLine(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.MethodName, Is.EqualTo("Execute"), "Method name should be stripped of <System.Guid>");
			Assert.That(frame.GenericMethodArity, Is.EqualTo(1), "Generic arity should be 1 for Execute<System.Guid>");
		}

		/// <summary>
		/// Verify that WinDbg parser has higher priority than Standard parser.
		/// </summary>
		[Test]
		public void WinDbgFormat_UsesWinDbgParser()
		{
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.Test.Method() (IL=0x0000, Native=0x00000000+0x00)";

			var frame = parser.ParseLine(line);

			Assert.That(frame, Is.Not.Null);
			// WinDbg parser extracts assembly name, Standard parser doesn't
			Assert.That(frame!.AssemblyName, Is.EqualTo("Microsoft.Crm.Core.dll"));
		}

		/// <summary>
		/// Verify lambda/closure methods are detected.
		/// </summary>
		[Test]
		public void AsyncJob1_LambdaMethod_DetectedAsLambda()
		{
			// Line 20 from async-job1.txt
			var line = "	Microsoft.Crm.Core.dll!Microsoft.Crm.Core.Resiliency.ConcurrencyBulkhead.<>c__DisplayClass4_0.<Execute>b__0() (IL=0x000C, Native=0x00007FFA6F883670+0x44)";

			var frame = parser.ParseLine(line);

			Assert.That(frame, Is.Not.Null, "Frame should be parsed");
			Assert.That(frame!.FrameType, Is.EqualTo(StackFrameType.Lambda), "Should be detected as lambda");
		}
	}
}
