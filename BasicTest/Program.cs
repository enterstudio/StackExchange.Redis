﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using System.IO;

[assembly: AssemblyVersion("1.0.0")]

namespace BasicTest
{
    static class YourPreferredSerializer
    {
        public static T Deserialize<T>(Stream s) { return default(T); }
    }
    static class Program
    {
        public static RedisValue JsonGet(this IDatabase db, RedisKey key,
            string path = ".", CommandFlags flags = CommandFlags.None)
        {
            return (RedisValue)db.Execute("JSON.GET",
                new object[] { key, path }, flags);
        }

        public static T JsonGet<T>(this IDatabase db, RedisKey key,
            string path = ".", CommandFlags flags = CommandFlags.None)
        {
            byte[] bytes = (byte[])db.Execute("JSON.GET",
                new object[] { key, path }, flags);
            using (var ms = new MemoryStream(bytes))
            {
                return YourPreferredSerializer.Deserialize<T>(ms);
            }
        }
        static void Main(string[] args)
        {
            using (var conn = ConnectionMultiplexer.Connect("127.0.0.1:6379"))
            {
                var db = conn.GetDatabase();

                // needs explicit RedisKey type for key-based
                // sharding to work; will still work with strings,
                // but no key-based sharding support
                RedisKey key = "some_key";

                // note: if command renames are configured in
                // the API, they will still work automatically 
                db.Execute("del", key);
                db.Execute("set", key, "12");
                db.Execute("incrby", key, 4);
                int i = (int)db.Execute("get", key);

                Console.WriteLine(i); // 16;

            }



            //int AsyncOpsQty = 500000;
            //if(args.Length == 1)
            //{
            //    int tmp;
            //    if(int.TryParse(args[0], out tmp))
            //        AsyncOpsQty = tmp;
            //}
            //MassiveBulkOpsAsync(AsyncOpsQty, true, true);
            //MassiveBulkOpsAsync(AsyncOpsQty, true, false);
            //MassiveBulkOpsAsync(AsyncOpsQty, false, true);
            //MassiveBulkOpsAsync(AsyncOpsQty, false, false);
        }
        static void MassiveBulkOpsAsync(int AsyncOpsQty, bool preserveOrder, bool withContinuation)
        {            
            using (var muxer = ConnectionMultiplexer.Connect("localhost,resolvedns=1"))
            {
                muxer.PreserveAsyncOrder = preserveOrder;
                RedisKey key = "MBOA";
                var conn = muxer.GetDatabase();
                muxer.Wait(conn.PingAsync());

#if CORE_CLR
                int number = 0;
#endif
                Action<Task> nonTrivial = delegate
                {
#if !CORE_CLR
                    Thread.SpinWait(5);
#else
                    for (int i = 0; i < 50; i++)
                    {
                        number++;
                    }
#endif
                };
                var watch = Stopwatch.StartNew();
                for (int i = 0; i <= AsyncOpsQty; i++)
                {
                    var t = conn.StringSetAsync(key, i);
                    if (withContinuation) t.ContinueWith(nonTrivial);
                }
                int val = (int)muxer.Wait(conn.StringGetAsync(key));
                watch.Stop();

                Console.WriteLine("After {0}: {1}", AsyncOpsQty, val);
                Console.WriteLine("({3}, {4})\r\n{2}: Time for {0} ops: {1}ms; ops/s: {5}", AsyncOpsQty, watch.ElapsedMilliseconds, Me(),
                    withContinuation ? "with continuation" : "no continuation", preserveOrder ? "preserve order" : "any order",
                    AsyncOpsQty / watch.Elapsed.TotalSeconds);
            }
        }
        internal static string Me([CallerMemberName] string caller = null)
        {
            return caller;
        }
    }
}
