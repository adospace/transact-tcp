using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp.Tests
{
    [TestClass]
    public class SetupAssemblyInitializer
    {
        //static readonly ServiceActorCallMonitorTracer _monitor 
        //    = new ServiceActorCallMonitorTracer(false);

        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
                .Enrich.FromLogContext()
                .WriteTo.Trace(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            //ServiceActor.ActionQueue.BeginMonitor(_monitor);

            //ServiceActor.ServiceRef.ClearCache();
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            //ServiceActor.ActionQueue.BeginMonitor(_monitor);
        }

    }

}
