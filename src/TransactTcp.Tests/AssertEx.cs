using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp.Tests
{
    internal static class AssertEx
    {
        public static void IsTrue(Func<bool> testFunc, int timeoutMilliseconds = 20000)
        {
            while (!testFunc() && timeoutMilliseconds > 0)
            {
                timeoutMilliseconds -= 10;
                Thread.Sleep(10);
            }

            if (timeoutMilliseconds <= 0)
            {
                throw new AssertFailedException();
            }
        }

        public static void IsFalse(Func<bool> testFunc, int timeoutMilliseconds = 20000)
        {
            while (testFunc() && timeoutMilliseconds > 0)
            {
                timeoutMilliseconds -= 10;
                Thread.Sleep(10);
            }

            if (timeoutMilliseconds <= 0)
            {
                throw new AssertFailedException();
            }
        }

        public static void IsNull(Func<object> testFunc, int timeoutMilliseconds = 20000)
        {
            while (testFunc() != null && timeoutMilliseconds > 0)
            {
                timeoutMilliseconds -= 10;
                Thread.Sleep(10);
            }

            if (timeoutMilliseconds <= 0)
            {
                throw new AssertFailedException();
            }
        }

        public static void IsNotNull(Func<object> testFunc, int timeoutMilliseconds = 20000)
        {
            while (testFunc() == null && timeoutMilliseconds > 0)
            {
                timeoutMilliseconds -= 10;
                Thread.Sleep(10);
            }

            if (timeoutMilliseconds <= 0)
            {
                throw new AssertFailedException();
            }
        }

        public static async Task IsTrue(Func<Task<bool>> testFunc, int timeoutMilliseconds = 20000)
        {
            while (!await testFunc() && timeoutMilliseconds > 0)
            {
                timeoutMilliseconds -= 10;
                Thread.Sleep(10);
            }

            if (timeoutMilliseconds <= 0)
            {
                throw new AssertFailedException();
            }
        }

        public static async Task IsFalse(Func<Task<bool>> testFunc, int timeoutMilliseconds = 20000)
        {
            while (await testFunc() && timeoutMilliseconds > 0)
            {
                timeoutMilliseconds -= 10;
                Thread.Sleep(10);
            }

            if (timeoutMilliseconds <= 0)
            {
                throw new AssertFailedException();
            }
        }

        public static async Task IsNull(Func<Task<object>> testFunc, int timeoutMilliseconds = 20000)
        {
            while (await testFunc() != null && timeoutMilliseconds > 0)
            {
                timeoutMilliseconds -= 10;
                Thread.Sleep(10);
            }

            if (timeoutMilliseconds <= 0)
            {
                throw new AssertFailedException();
            }
        }

        public static async Task IsNotNull(Func<Task<object>> testFunc, int timeoutMilliseconds = 20000)
        {
            while (await testFunc() == null && timeoutMilliseconds > 0)
            {
                timeoutMilliseconds -= 10;
                Thread.Sleep(10);
            }

            if (timeoutMilliseconds <= 0)
            {
                throw new AssertFailedException();
            }
        }



    }
}
