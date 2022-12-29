using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace QuickAzTables
{
    public class TableStore
    {
        private readonly string tableName;
        private readonly string? connectionString;
        private readonly string? sasToken;
        private readonly string? accountName;
        private readonly string invalidKeyCharReplacement;

        /// <summary>
        /// Creates an instance that allows to store and query a given 
        /// Table Storage table. If you would like to manage the data 
        /// using a typed model, consider <see cref="TypedTableStore{T}"/>
        /// </summary>
        /// <param name="tableName">Name of the table this object should use</param>
        /// <param name="connectionString">connection string to the table storage account</param>
        /// <param name="createTableIfNotExists">
        /// Whether to create the table if not exists. If set to <see langword="false"/> 
        /// the table can be created using <see cref="CreateTableIfNotExistsAsync"/> at a 
        /// later time, but before other operations. If set to <see langword="true"/> 
        /// (default) you're expected to make sure either there's only one instance for 
        /// a given table is used, or constructors are called with enough time in between,
        /// as the createIfNotExists operation can return a <code>Conflict</code> error
        /// if multiple creations are requested within a small duration.
        /// </param>
        /// <param name="invalidKeyCharReplacement">
        /// If an operation was invoked with keys that found to have invalid characters, 
        /// what character to be used to replace them so the key is valid for the operaton. 
        /// Default is <code>""</code> which removed the invalid characters. 
        /// See methods on <see cref="TableKey"/> class
        /// </param>
        public TableStore(string tableName,
                          string connectionString,
                          bool createTableIfNotExists = true,
                          string invalidKeyCharReplacement = "")
        {
            if (!string.IsNullOrWhiteSpace(tableName))
                throw new InvalidOperationException($"{nameof(tableName)} cannot be null or empty");
            
            if (!string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException($"{nameof(connectionString)} cannot be null or empty");

            ArgumentNullException.ThrowIfNull(invalidKeyCharReplacement);

            this.tableName = tableName;
            this.connectionString = connectionString;
            this.invalidKeyCharReplacement = invalidKeyCharReplacement;
            if (createTableIfNotExists)
            {
                GetTable().CreateIfNotExists();
            }
        }

        /// <summary>
        /// Creates an instance that allows to store and query a given 
        /// Table Storage table. If you would like to manage the data 
        /// using a typed model, consider <see cref="TypedTableStore{T}"/>
        /// </summary>
        /// <param name="tableName">Name of the table this object should use</param>
        /// <param name="sasToken">SAS Token to the table storage account</param>
        /// <param name="accountName">table storage account name</param>
        /// <param name="createTableIfNotExists">
        /// Whether to create the table if not exists. If set to <see langword="false"/> 
        /// the table can be created using <see cref="CreateTableIfNotExistsAsync"/> at a 
        /// later time, but before other operations. If set to <see langword="true"/> 
        /// (default) you're expected to make sure either there's only one instance for 
        /// a given table is used, or constructors are called with enough time in between,
        /// as the createIfNotExists operation can return a <code>Conflict</code> error
        /// if multiple creations are requested within a small duration.
        /// </param>
        /// <param name="invalidKeyCharReplacement">
        /// If an operation was invoked with keys that found to have invalid characters, 
        /// what character to be used to replace them so the key is valid for the operaton. 
        /// Default is <code>""</code> which removed the invalid characters. 
        /// See methods on <see cref="TableKey"/> class
        /// </param>

        public TableStore(string tableName,
                          string sasToken,
                          string accountName,
                          bool createTableIfNotExists = true,
                          string invalidKeyCharReplacement = "")
        {
            if (!string.IsNullOrWhiteSpace(sasToken))
                throw new InvalidOperationException($"{nameof(sasToken)} cannot be null or empty");

            if (!string.IsNullOrWhiteSpace(accountName))
                throw new InvalidOperationException($"{nameof(accountName)} cannot be null or empty");

            ArgumentNullException.ThrowIfNull(invalidKeyCharReplacement);

            this.tableName = tableName;
            this.sasToken = sasToken;
            this.accountName = accountName;
            this.invalidKeyCharReplacement = invalidKeyCharReplacement;
            if (createTableIfNotExists)
            {
                GetTable().CreateIfNotExists();
            }
        }

        /// <summary>
        /// Creates this table if it does not exist.
        /// Note that running this operation on the same table at the same time will cause Table Storage 
        /// to return a <code>Conflict</code> error. You're expected to synchronize calls to avoid this.
        /// </summary>
        /// <returns></returns>
        public Task CreateTableIfNotExistsAsync() => GetTable().CreateIfNotExistsAsync();

        /// <summary>
        /// Queries table storage with the given <paramref name="partitionKey"/> and
        /// <paramref name="rowKey"/> and returns the matching record if exists.
        /// <see langword="null"/> otherwise. 
        /// If you want multiple rows in the result see <see cref="QueryAsync(string?, string?)"/>
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async Task<TableEntity?> QuerySingleAsync(string partitionKey, string rowKey)
        {
            if (string.IsNullOrWhiteSpace(partitionKey) || string.IsNullOrWhiteSpace(rowKey))
                throw new ArgumentException(
                    $"Both {nameof(partitionKey)} and {nameof(rowKey)} must be specified. " +
                    $"To match multiple rows by only one of them, use {nameof(QueryAsync)} instead. " +
                    $"To retrieve the full table, use {nameof(RetrieveFullTable)} instead.");

            await foreach (var item in QueryAsync(partitionKey, rowKey))
            {
                return item;
            }
            return null;
        }

        /// <summary>
        /// Queries table storage with the given <paramref name="partitionKey"/> or 
        /// <paramref name="rowKey"/>. If only <paramref name="partitionKey"/> was 
        /// specified this will result in a partition scan. If only <paramref name="rowKey"/>
        /// was specified this will result in a table scan. If you're looking for a single
        /// row, you can use <see cref="QuerySingleAsync(string, string)"/> instead.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public IAsyncEnumerable<TableEntity> QueryAsync(string? partitionKey = null,
                                                               string? rowKey = null)
        {
            partitionKey = TableKey.Sanitize(partitionKey, invalidKeyCharReplacement);
            rowKey = TableKey.Sanitize(rowKey, invalidKeyCharReplacement);
            if (string.IsNullOrWhiteSpace(partitionKey) && string.IsNullOrWhiteSpace(rowKey))
                throw new InvalidOperationException($"Both '{nameof(partitionKey)}' and {nameof(rowKey)} cannot be null or whitespace. " +
                    $"Atleast one must be provided. " +
                    $"To retrive the entire table, explicitly invoke {nameof(RetrieveFullTable)}()");

            return ExecuteQuerySegmented(partitionKey, rowKey);
        }

        /// <summary>
        /// Returns all rows in the table. While the data is paged/segmented, use this 
        /// sparingly as this can result in a large dataset if your table is large.
        /// </summary>
        public IAsyncEnumerable<TableEntity> RetrieveFullTable()
        {
            return ExecuteQuerySegmented(partitionKey: null, rowKey: null);
        }

        private async IAsyncEnumerable<TableEntity> ExecuteQuerySegmented(string? partitionKey, string? rowKey)
        {
            var querySegments = new[]
            {
                string.IsNullOrWhiteSpace(partitionKey) ? null : $"PartitionKey eq '{partitionKey.Replace("'","''")}'",
                string.IsNullOrWhiteSpace(rowKey) ? null : $"RowKey eq '{rowKey.Replace("'","''")}'",
            };
            var filter = string.Join(" and ", querySegments.Where(s => s is not null));
            // tried the LINQ version of QueryAsync. Kept returning empty sets.
            // Remember the same/similar experince in last 2 versions of table SDKs.
            // Ain't got time or the interest to figure out why the API doesn't work
            // as expected, so using odata string version
            var queryResults = GetTable().QueryAsync<TableEntity>(filter);
            await foreach (var item in queryResults)
            {
                yield return item;
            }
        }

        /// <summary>
        /// Stores the given item in the storage against the specified keys.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async Task StoreSingleAsync(TableEntity item, string partitionKey, string rowKey)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            partitionKey = TableKey.Sanitize(partitionKey, invalidKeyCharReplacement);
            rowKey = TableKey.Sanitize(rowKey, invalidKeyCharReplacement);

            if (string.IsNullOrWhiteSpace(partitionKey)) throw new ArgumentException($"'{nameof(partitionKey)}' cannot be null or whitespace", nameof(partitionKey));
            if (string.IsNullOrWhiteSpace(rowKey)) throw new ArgumentException($"'{nameof(rowKey)}' cannot be null or whitespace", nameof(rowKey));

            item.PartitionKey = partitionKey;
            item.RowKey = rowKey;

            var response = await GetTable().UpsertEntityAsync(item);

            ThrowIfErrorResponse(response,
                                 partitionKey: partitionKey,
                                 rowKey: rowKey);
        }

        /// <summary>
        /// Stores all given items in the specified table partition.
        /// If a matching row exists, it will be merged with the new item.
        /// Note that all items must be in the same partition as this will be 
        /// executed as a batched Entity Group Transaction.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async Task StoreMultipleInPartitionAsync(List<TableEntity> items, string partitionKey)
        {
            ArgumentNullException.ThrowIfNull(items);
            if (string.IsNullOrWhiteSpace(partitionKey)) throw new ArgumentException($"'{nameof(partitionKey)}' cannot be null or whitespace", nameof(partitionKey));
            if (!items.Any()) return;

            var batch = items
                .Select(i =>
                {
                    i.PartitionKey = partitionKey;
                    i.RowKey = TableKey.Sanitize(i.RowKey, invalidKeyCharReplacement);
                    return i;
                })
                .Select(entity => new TableTransactionAction(TableTransactionActionType.UpdateMerge, // UpdateMerge likely keeps existing unmatched props. UpsertMerge probably doesn't
                                                             entity))
                .ToList();

            var batchResult = await GetTable().SubmitTransactionAsync(batch);

            ThrowIfErrorResponse(batchResult.GetRawResponse(), partitionKey: partitionKey);
        }

        private static void ThrowIfErrorResponse(Response response,
                                          string partitionKey,
                                          string? rowKey = null,
                                          [CallerMemberName] string? operationName = null)
        {
            if (!response.IsError) return;
            throw new Exception($"{operationName} for {nameof(partitionKey)} '{partitionKey}'{(rowKey is null ? "" : $" {nameof(rowKey)} '{rowKey}'")} failed with {response.Status} - {response.ReasonPhrase}");
        }

        /// <summary>
        /// Deletes the row identified by provided keys.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async Task DeleteSingleAsync(string partitionKey, string rowKey)
        {
            partitionKey = TableKey.Sanitize(partitionKey, invalidKeyCharReplacement);
            rowKey = TableKey.Sanitize(rowKey, invalidKeyCharReplacement);
            if (string.IsNullOrWhiteSpace(partitionKey)) throw new ArgumentException($"'{nameof(partitionKey)}' cannot be null or whitespace", nameof(partitionKey));
            if (string.IsNullOrWhiteSpace(rowKey)) throw new ArgumentException($"'{nameof(rowKey)}' cannot be null or whitespace", nameof(rowKey));

            var response = await GetTable().DeleteEntityAsync(partitionKey, rowKey);
            
            ThrowIfErrorResponse(response,
                                 partitionKey: partitionKey,
                                 rowKey: rowKey);
        }

        /// <summary>
        /// Deletes all rows identified in the specified table partition.
        /// Note that all items must be in the same partition as this will be 
        /// executed as a batched Entity Group Transaction.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async Task DeleteMultipleInPartitionAsync(string partitionKey, List<string> rowKeys)
        {
            partitionKey = TableKey.Sanitize(partitionKey, invalidKeyCharReplacement);
            ArgumentNullException.ThrowIfNull(rowKeys);
            if (string.IsNullOrWhiteSpace(partitionKey)) throw new ArgumentException($"'{nameof(partitionKey)}' cannot be null or whitespace", nameof(partitionKey));
            rowKeys = rowKeys.Select(k => TableKey.Sanitize(k, invalidKeyCharReplacement)).ToList();
            if (rowKeys.Any(k => string.IsNullOrWhiteSpace(k))) throw new ArgumentException($"'{nameof(rowKeys)}' cannot be null or whitespace", nameof(rowKeys));

            if (!rowKeys.Any()) return;

            var batch = rowKeys
              .Select(rowKey => new TableTransactionAction(TableTransactionActionType.Delete, new TableEntity(partitionKey, rowKey)))
              .ToList();

            var batchResult = await GetTable().SubmitTransactionAsync(batch);

            ThrowIfErrorResponse(batchResult.GetRawResponse(),
                                 partitionKey: partitionKey);
        }

        private TableClient GetTable()
        {
            var service = !string.IsNullOrWhiteSpace(connectionString)
                ? new TableServiceClient(connectionString)
                : new TableServiceClient(new Uri($"https://{accountName}.table.core.windows.net"), new AzureSasCredential(sasToken!));

            return service.GetTableClient(tableName);
        }

        /// <summary>
        /// Lists the names of tables in the specified table storage account
        /// </summary>
        public static IList<string> ListTables(string connectionString,
                                               Expression<Func<TableItem, bool>>? filter = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException($"{nameof(connectionString)} must be specified and cannot be empty.");

            var service = new TableServiceClient(connectionString);
            filter ??= t => true;

            return service.Query(filter).Select(t => t.Name).ToList();
        }

        /// <summary>
        /// Lists the names of tables in the specified table storage account
        /// </summary>
        public static IList<string> ListTables(string sasToken,
                                               string accountName,
                                               Expression<Func<TableItem, bool>>? filter = null)
        {
            if(string.IsNullOrWhiteSpace(sasToken)) throw new ArgumentException($"{nameof(sasToken)} must be specified and cannot be empty.");
            if (string.IsNullOrWhiteSpace(accountName)) throw new ArgumentException($"{nameof(accountName)} must be specified and cannot be empty.");

            var service = new TableServiceClient(new Uri($"https://{accountName}.table.core.windows.net"), new AzureSasCredential(sasToken!));
            filter ??= t => true;

            return service.Query(filter).Select(t => t.Name).ToList();
        }
    }
}