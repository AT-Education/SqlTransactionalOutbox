﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutboxHelpers;
using SqlTransactionalOutboxHelpers.IntegrationTests;
using SqlTransactionalOutboxHelpers.SqlServer.SystemDataNS;
using SqlTransactionalOutboxHelpers.Tests;
using SystemData = System.Data.SqlClient;
//using MicrosoftData = Microsoft.Data.SqlClient;

namespace SqlTransactionalOutboxHelpers.Tests
{
    public class SqlConnectionHelper
    {
        public static async Task<SystemData.SqlConnection> CreateSystemDataSqlConnectionAsync()
        {
            var sqlConnection = new SystemData.SqlConnection(TestConfiguration.SqlConnectionString);
            await sqlConnection.OpenAsync();
            return sqlConnection;
        }

        //public static MicrosoftData.SqlConnection CreateMicrosoftDataSqlConnectionAsync()
        //{
        //    return new MicrosoftData.SqlConnection(TestConfiguration.SqlConnectionString);
        //}
    }

    public static class SqlCommands
    {
        public static readonly string TruncateTransactionalOutbox =
            $"TRUNCATE TABLE [{DefaultOutboxTableConfig.DefaultTransactionalOutboxSchemaName}].[{DefaultOutboxTableConfig.DefaultTransactionalOutboxTableName}]";

    }

    public static class SqlConnectionCustomExtensionsForSystemData
    {
        public static async Task TruncateTransactionalOutboxTableAsync(this SystemData.SqlConnection sqlConnection)
        {
            //CLEAR the Table for Integration Tests to validate:
            await using var sqlCmd = new SystemData.SqlCommand(
                SqlCommands.TruncateTransactionalOutbox,
                sqlConnection
            );
            await sqlCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public static class SystemDataSqlTestHelpers
    {
        public static async Task<List<ISqlTransactionalOutboxItem<Guid>>> ClearAndPopulateTransactionalOutboxTestDataAsync(int testDataSize, TestHarnessSqlTransactionalOutboxPublisher testPublisher = null)
        {
            //Organize
            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();

            //*****************************************************************************************
            //* STEP 1 - Prepare/Clear the Queue Table
            //*****************************************************************************************
            //Clear the Table data for the test...
            await sqlConnection.TruncateTransactionalOutboxTableAsync();

            //*****************************************************************************************
            //* STEP 2 - Insert New Outbox Items to process with TestHarness for Publishing...
            //*****************************************************************************************
            //Initialize Transaction and Outbox Processor
            await using var sqlTransaction = (SystemData.SqlTransaction)await sqlConnection.BeginTransactionAsync().ConfigureAwait(false);

            //Initialize the Test Harness for Publish tracking...
            var testHarnessPublisher = testPublisher ?? new TestHarnessSqlTransactionalOutboxPublisher();
            var outboxProcessor = new SqlServerGuidTransactionalOutboxProcessor<string>(sqlTransaction, testHarnessPublisher);

            var outboxTestItems = TestHelper.CreateTestStringOutboxItemData(testDataSize);

            //Execute Insert of New Items!
            var insertedResults = await outboxProcessor.InsertNewPendingOutboxItemsAsync(
                outboxTestItems
            ).ConfigureAwait(false);

            await sqlTransaction.CommitAsync();

            return insertedResults;
        }
    }
}
