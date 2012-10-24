﻿using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.Threading;
using System.Net;
using Cassandra.Native;
using System.Numerics;
using System.Globalization;
using Cassandra;
 

namespace MyUTExt
{
    public class CommonBasicTests : IUseFixture<Dev.SettingsFixture>, IDisposable
    {
        bool _compression;
        CassandraCompressionType Compression
        {
            get
            {
                return _compression ? CassandraCompressionType.Snappy : CassandraCompressionType.NoCompression;
            }
        }

        string Keyspace = "test";

        public CommonBasicTests(bool compression)
        {
            this._compression = compression;
        }

        CassandraSession Session;

        public void SetFixture(Dev.SettingsFixture setFix)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");

            var serverSp = setFix.Settings["CassandraServer"].Split(':');

            string ip = serverSp[0];
            int port = int.Parse(serverSp[1]);

            var serverAddress = new IPEndPoint(IPAddress.Parse(ip), port);

            Session = new CassandraSession(new List<IPEndPoint>() { serverAddress }, this.Keyspace, this.Compression, Timeout.Infinite);
        }

        public void Dispose()
        {
            Session.Dispose();
        }



        //public void ProcessOutput(IOutput ret, object expectedValue = null)
        //{
        //    using (ret)
        //    {
        //        if (ret is OutputError)
        //        {
        //            if (expectedValue is OutputError)
        //                Dev.Assert.True(true, "CQL Error [" + (ret as OutputError).CassandraErrorType.ToString() + "] " + (ret as OutputError).Message);
        //            else
        //                Dev.Assert.True(false, "CQL Error [" + (ret as OutputError).CassandraErrorType.ToString() + "] " + (ret as OutputError).Message);
        //        }
        //        else if (ret is OutputPrepared)
        //        {
        //            Console.WriteLine("CQL> Prepared:\t" + (ret as OutputPrepared).QueryID);
        //        }
        //        else if (ret is OutputSetKeyspace)
        //        {
        //            if (expectedValue != null)
        //                Dev.Assert.Equal((string)expectedValue, (ret as OutputSetKeyspace).Value);
        //            Console.WriteLine("CQL> SetKeyspace:\t" + (ret as OutputSetKeyspace).Value);
        //        }
        //        else if (ret is OutputVoid)
        //        {
        //            if (expectedValue != null)
        //                Dev.Assert.True(false, string.Format("\n Received output:  {0} \n Expected output:  {1}", ret.ToString(), expectedValue.ToString()));
        //            else
        //                Console.WriteLine("CQL> (OK)");
        //        }
        //        else if (ret is OutputRows)
        //        {
        //            if (expectedValue != null)
        //                Dev.Assert.Equal((int)expectedValue, (ret as OutputRows).Rows);
        //            CqlRowSet rowPopulator = new CqlRowSet(ret as OutputRows);
        //            rowPopulator.PrintTo(stream: Console.Out, cellEncoder: CellEncoder);
        //            Console.WriteLine("CQL> Done.");
        //        }
        //        else
        //        {
        //            Dev.Assert.True(false, "Unexpected IOutput: " + ret.GetType().FullName);
        //        }
        //    }
        //}

        private static string CellEncoder(object col)
        {
            if (col is VarintBuffer)
            {
                Array.Reverse(((VarintBuffer)col).BigIntegerBytes);
                return ((VarintBuffer)col).ToBigInteger().ToString();
            }
            else if (col is DecimalBuffer)
            {
                return ((DecimalBuffer)col).ToDecimal().ToString();
            }
            else
                return col.ToString();
        }

        public void ExecuteSyncQuery(CassandraSession session, string query, CqlConsistencyLevel consistency = CqlConsistencyLevel.DEFAULT, string messageInstead = null)
        {            
            if (messageInstead != null)
                Console.WriteLine("CQL<\t" + messageInstead);
            else
                Console.WriteLine("CQL< Query:\t" + query);
            using (var ret = session.Query(query,consistency))
            {                
                ret.PrintTo(stream: Console.Out, cellEncoder: CellEncoder);                
                Console.WriteLine("CQL> Done.");
            }
        }

        public void ExecuteSyncScalar(CassandraSession session, string query, string messageInstead = null)
        {
            if (messageInstead != null)
                Console.WriteLine("CQL<\t" + messageInstead);
            else
                Console.WriteLine("CQL< Query:\t" + query);
            var ret = session.Scalar(query);
            Console.Write("CQL> ");
            Console.WriteLine(ret);
            Console.WriteLine("CQL> Done.");
        }

        public void ExecuteSyncNonQuery(CassandraSession session, string query, string messageInstead = null, CqlConsistencyLevel consistency= CqlConsistencyLevel.DEFAULT)
        {
            if (messageInstead != null)
                Console.WriteLine("CQL<\t" + messageInstead);
            else
                Console.WriteLine("CQL< Query:\t" + query);
            session.NonQuery(query, consistency);
            Console.WriteLine("CQL> (OK).");
        }


        public void PrepareQuery(CassandraSession session, string query, out byte[] preparedID, out Metadata metadata, string messageInstead = null)
        {
            if (messageInstead != null)
                Console.WriteLine("CQL<\t" + messageInstead);
            else
                Console.WriteLine("CQL< Prepared Query:\t" + query);
            Metadata md;
            preparedID = session.PrepareQuery(query, out md);
            metadata = md;
            Console.WriteLine("CQL> (OK).");             
        }

        public void ExecutePreparedQuery(CassandraSession session, byte[] preparedID, Metadata metadata, object[] values, CqlConsistencyLevel consistency = CqlConsistencyLevel.DEFAULT, string messageInstead = null)
        {
            if (messageInstead != null)
                Console.WriteLine("CQL<\t" + messageInstead);
            else
                Console.WriteLine("CQL< Executing Prepared Query:\t");
            session.ExecuteQuery(preparedID, metadata, values, consistency);
            Console.WriteLine("CQL> (OK).");
        }

        public CassandraSession ConnectToTestServer()
        {
            return Session;
        }

        public void Test()
        {
            var conn = ConnectToTestServer();
            string keyspaceName = "keyspace" + Guid.NewGuid().ToString("N").ToLower();

            ExecuteSyncNonQuery(conn, string.Format(@"CREATE KEYSPACE {0} 
         WITH replication = {{ 'class' : 'SimpleStrategy', 'replication_factor' : 1 }};"
                , keyspaceName));

            conn.ChangeKeyspace(keyspaceName);
            string tableName = "table" + Guid.NewGuid().ToString("N").ToLower();
            ExecuteSyncNonQuery(conn, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid,
         author text,
         body text,
         isok boolean,
		 fval float,
		 dval double,
         PRIMARY KEY(tweet_id))", tableName));

            Randomm rndm = new Randomm();
            StringBuilder longQ = new StringBuilder();
            longQ.AppendLine("BEGIN BATCH ");

            int RowsNo = 5000;
            for (int i = 0; i < RowsNo; i++)
            {
                longQ.AppendFormat(@"INSERT INTO {0} (
         tweet_id,
         author,
         isok,
         body,
		 fval,
		 dval)
VALUES ({1},'test{2}','{3}','body{2}','{4}','{5}');", tableName, Guid.NewGuid().ToString(), i, i % 2 == 0 ? "false" : "true", rndm.NextSingle(), rndm.NextDouble());
            }
            longQ.AppendLine("APPLY BATCH;");
            ExecuteSyncNonQuery(conn, longQ.ToString(), "Inserting...");
            ExecuteSyncQuery(conn, string.Format(@"SELECT * from {0} LIMIT 5000;", tableName));
            ExecuteSyncNonQuery(conn, string.Format(@"DROP TABLE {0};", tableName));

            ExecuteSyncNonQuery(conn, string.Format(@"DROP KEYSPACE {0};", keyspaceName));
        }


        public void ExceedingCassandraType(Type toExceed, Type toExceedWith, bool shouldPass = true)
        {
            string cassandraDataTypeName = convertTypeNameToCassandraEquivalent(toExceed);
            var conn = ConnectToTestServer();
            conn.ChangeKeyspace("test"); 
            string tableName = "table" + Guid.NewGuid().ToString("N");
            ExecuteSyncNonQuery(conn, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         label text,
         number {1}
         );", tableName, cassandraDataTypeName));

            var Minimum = toExceedWith.GetField("MinValue").GetValue(this);
            var Maximum = toExceedWith.GetField("MaxValue").GetValue(this);

            if (toExceedWith == typeof(Double) || toExceedWith == typeof(Single))
            {
                Minimum = Minimum.GetType().GetMethod("ToString", new Type[] { typeof(String) }).Invoke(Minimum, new object[1] { "r" });
                Maximum = Maximum.GetType().GetMethod("ToString", new Type[] { typeof(String) }).Invoke(Maximum, new object[1] { "r" });
            }

            try
            {
                ExecuteSyncNonQuery(conn, string.Format("INSERT INTO {0}(tweet_id, label, number) VALUES ({1}, '{2}', '{3}');", tableName, Guid.NewGuid().ToString(), "Minimum", Minimum), null);
                ExecuteSyncNonQuery(conn, string.Format("INSERT INTO {0}(tweet_id, label, number) VALUES ({1}, '{2}', '{3}');", tableName, Guid.NewGuid().ToString(), "Maximum", Maximum), null);
            }
            catch (CassandraInvalidException) { }
            
            ExecuteSyncQuery(conn, string.Format("SELECT * FROM {0};", tableName));
            ExecuteSyncNonQuery(conn, string.Format("DROP TABLE {0};", tableName));
        }


        private string convertTypeNameToCassandraEquivalent(Type t)
        {
            switch (t.Name)
            {
                case "Int32":
                    return "int";

                case "Int64":
                    return "bigint";

                case "Single":
                    return "float";

                case "Double":
                    return "double";

                case "Decimal":
                    return "decimal";

                case "BigInteger":
                    return "varint";

                case "Char":
                    return "ascii";

                case "String":
                    return "text";
                
                case "DateTimeOffset":
                    return "timestamp";

                default:
                    throw new InvalidOperationException();                    
            }
        }

        
        public void insertingSingleValue(Type tp)
        {
            string cassandraDataTypeName = convertTypeNameToCassandraEquivalent(tp);
            CassandraSession conn = ConnectToTestServer();
            conn.ChangeKeyspace("test");
            string tableName = "table" + Guid.NewGuid().ToString("N");
            ExecuteSyncNonQuery(conn, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         number {1}
         );", tableName, cassandraDataTypeName));
            ExecuteSyncNonQuery(conn, string.Format("INSERT INTO {0}(tweet_id,number) VALUES ({1}, '{2}');", tableName, Guid.NewGuid().ToString(), RandomVal(tp)), null); // rndm.GetType().GetMethod("Next" + tp.Name).Invoke(rndm, new object[] { })
            ExecuteSyncQuery(conn, string.Format("SELECT * FROM {0};", tableName));
            ExecuteSyncNonQuery(conn, string.Format("DROP TABLE {0};", tableName));
        }


        public void prepareTest()
        {
            string tableName = "table" + Guid.NewGuid().ToString("N");
            ExecuteSyncNonQuery(this.Session, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         label text,
         number {1}
         );", tableName, "int"));

            byte[] preparedID;
            Metadata md;
            object[] values = new object[2];
            values[0] = (string)"LSD";
            values[1] = (int)1410;
            PrepareQuery(this.Session, string.Format("INSERT INTO {0}(tweet_id, label, number) VALUES ({1}, ?, ?);", tableName, Guid.NewGuid()), out preparedID, out md);
            ExecutePreparedQuery(this.Session, preparedID, md, values);                
        }

        public void checkingOrderOfCollection(string CassandraCollectionType, Type TypeOfDataToBeInputed, Type TypeOfKeyForMap = null, string pendingMode="")
        {                       
            string cassandraDataTypeName = convertTypeNameToCassandraEquivalent(TypeOfDataToBeInputed);
            string cassandraKeyDataTypeName = "";

            string openBracket = CassandraCollectionType == "list" ? "['" : "{'";
            string closeBracket = CassandraCollectionType == "list" ? "']" : "'}";
            string mapSyntax = "";

            string randomKeyValue = String.Empty;

            if (TypeOfKeyForMap != null)
            {
                cassandraKeyDataTypeName = convertTypeNameToCassandraEquivalent(TypeOfKeyForMap);
                mapSyntax = cassandraKeyDataTypeName + ",";

                if (TypeOfKeyForMap == typeof(DateTimeOffset))
                    randomKeyValue = (string)(RandomVal(typeof(DateTimeOffset)).GetType().GetMethod("ToString", new Type[] { typeof(String) }).Invoke(RandomVal(typeof(DateTimeOffset)), new object[1] { "yyyy-MM-dd H:mm:sszz00" }) + "' : '");
                else
                    randomKeyValue = RandomVal(TypeOfDataToBeInputed) + "' : '";
            }

            CassandraSession conn = ConnectToTestServer();            
            string tableName = "table" + Guid.NewGuid().ToString("N");
            ExecuteSyncNonQuery(conn, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         some_collection {1}<{2}{3}>
         );", tableName, CassandraCollectionType, mapSyntax, cassandraDataTypeName));

            Guid tweet_id = Guid.NewGuid();            

            StringBuilder longQ = new StringBuilder();
            longQ.AppendLine("BEGIN BATCH ");

            int CollectionElementsNo = 1000;
            List<Int32> orderedAsInputed = new List<Int32>(CollectionElementsNo);

            string inputSide = "some_collection + {1}";
            if (CassandraCollectionType == "list" && pendingMode == "prepending")
                inputSide = "{1} + some_collection";
            
            for (int i = 0; i < CollectionElementsNo; i++)
            {
                var data = i*(i%2);                
                longQ.AppendFormat(@"UPDATE {0} SET some_collection = "+inputSide+" WHERE tweet_id = {2};"
                    , tableName, openBracket + randomKeyValue + data + closeBracket, tweet_id.ToString());
                orderedAsInputed.Add(data);
            }

            longQ.AppendLine("APPLY BATCH;");
            ExecuteSyncNonQuery(conn, longQ.ToString(), "Inserting...");

            if (CassandraCollectionType == "set")
            {
                orderedAsInputed.Sort();
                orderedAsInputed.RemoveRange(0, orderedAsInputed.LastIndexOf(0));
            }
            else if (CassandraCollectionType == "list" && pendingMode == "prepending")
                orderedAsInputed.Reverse();
                
            CqlRowSet rs = Session.Query(string.Format("SELECT * FROM {0};", tableName),CqlConsistencyLevel.DEFAULT);

            using (rs)
            {
                int ind = 0;
                foreach (var row in rs.GetRows())
                    foreach (var value in row[1] as System.Collections.IEnumerable)
                    {
                        Assert.True(orderedAsInputed[ind] == (int)value);
                        ind++;
                    }
            }

            ExecuteSyncQuery(conn, string.Format("SELECT * FROM {0};", tableName));
            ExecuteSyncNonQuery(conn, string.Format("DROP TABLE {0};", tableName));
        }

        public void insertingSingleCollection(string CassandraCollectionType, Type TypeOfDataToBeInputed, Type TypeOfKeyForMap = null)
        {
            string cassandraDataTypeName = convertTypeNameToCassandraEquivalent(TypeOfDataToBeInputed);
            string cassandraKeyDataTypeName = "";

            string openBracket = CassandraCollectionType == "list" ? "['" : "{'";
            string closeBracket = CassandraCollectionType == "list" ? "']" : "'}";
            string mapSyntax = "";

            var randomValue = RandomVal(TypeOfDataToBeInputed);
            string randomKeyValue = String.Empty;

            if (TypeOfKeyForMap != null)
            {
                cassandraKeyDataTypeName = convertTypeNameToCassandraEquivalent(TypeOfKeyForMap);
                mapSyntax = cassandraKeyDataTypeName + ",";

                if (TypeOfKeyForMap == typeof(DateTimeOffset))
                    randomKeyValue = (string)(RandomVal(typeof(DateTimeOffset)).GetType().GetMethod("ToString", new Type[] { typeof(String) }).Invoke(RandomVal(typeof(DateTimeOffset)), new object[1] { "yyyy-MM-dd H:mm:sszz00" }) + "' : '");
                else
                    randomKeyValue = RandomVal(TypeOfDataToBeInputed) + "' : '";
            }

            CassandraSession conn = ConnectToTestServer();
            conn.ChangeKeyspace("test");
            string tableName = "table" + Guid.NewGuid().ToString("N");
            ExecuteSyncNonQuery(conn, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         some_collection {1}<{2}{3}>
         );", tableName, CassandraCollectionType, mapSyntax, cassandraDataTypeName));

            Guid tweet_id = Guid.NewGuid();

            ExecuteSyncNonQuery(conn, string.Format("INSERT INTO {0}(tweet_id,some_collection) VALUES ({1}, {2});", tableName, tweet_id.ToString(), openBracket + randomKeyValue + randomValue + closeBracket), null);

            StringBuilder longQ = new StringBuilder();
            longQ.AppendLine("BEGIN BATCH ");

            int CollectionElementsNo = 1000;
            for (int i = 0; i < CollectionElementsNo; i++)
            {
                longQ.AppendFormat(@"UPDATE {0} SET some_collection = some_collection + {1} WHERE tweet_id = {2};"
                    , tableName, openBracket + randomKeyValue + RandomVal(TypeOfDataToBeInputed) + closeBracket, tweet_id.ToString());
            }
            longQ.AppendLine("APPLY BATCH;");
            ExecuteSyncNonQuery(conn, longQ.ToString(), "Inserting...");

            ExecuteSyncQuery(conn, string.Format("SELECT * FROM {0};", tableName));
            ExecuteSyncNonQuery(conn, string.Format("DROP TABLE {0};", tableName));
        }        

        public void TimestampTest()
        {
            var conn = ConnectToTestServer();
            conn.ChangeKeyspace("test");
            string tableName = "table" + Guid.NewGuid().ToString("N");
            ExecuteSyncNonQuery(conn, string.Format(@"CREATE TABLE {0}(
         tweet_id uuid PRIMARY KEY,
         ts timestamp
         );", tableName));

            ExecuteSyncNonQuery(conn, string.Format("INSERT INTO {0}(tweet_id,ts) VALUES ({1}, '{2}');", tableName, Guid.NewGuid().ToString(), "2011-02-03 04:05+0000"), null);
            ExecuteSyncNonQuery(conn, string.Format("INSERT INTO {0}(tweet_id,ts) VALUES ({1}, '{2}');", tableName, Guid.NewGuid().ToString(), 220898707200000), null);
            ExecuteSyncNonQuery(conn, string.Format("INSERT INTO {0}(tweet_id,ts) VALUES ({1}, '{2}');", tableName, Guid.NewGuid().ToString(), 0), null);

            ExecuteSyncQuery(conn, string.Format("SELECT * FROM {0};", tableName));
            ExecuteSyncNonQuery(conn, string.Format("DROP TABLE {0};", tableName));
        }
        private Randomm rndm = new Randomm();
        private object RandomVal(Type tp)
        {
            if (tp != null)                            
                return rndm.GetType().GetMethod("Next" + tp.Name).Invoke(rndm, new object[] { });            
            else
                return "";
        }
    }
}