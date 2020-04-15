using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using dFakto.States.Workers.FileStores;
using dFakto.States.Workers.Interfaces;
using dFakto.States.Workers.Sql.Common;
using dFakto.States.Workers.Sql.Exceptions;
using Microsoft.Extensions.Logging;

namespace dFakto.States.Workers.Sql
{
    public class SqlQueryInput : SqlQuery
    {
        public string ConnectionName { get; set; }
    }
    
    public class SqlQueryWorker : BaseWorker<SqlQueryInput,SqlQueryOutput>
    {
        private readonly ILogger<SqlQueryWorker> _logger;
        private readonly IEnumerable<BaseDatabase> _databases;
        private readonly FileStoreFactory _fileStoreFactory;

        public SqlQueryWorker(ILogger<SqlQueryWorker> logger, IEnumerable<BaseDatabase> databases, FileStoreFactory fileStoreFactory) : base("SQLQuery")
        {
            _logger = logger;
            _databases = databases;
            _fileStoreFactory = fileStoreFactory;
        }

        public override async Task<SqlQueryOutput> DoWorkAsync(SqlQueryInput input, CancellationToken token)
        {
            var database = _databases.FirstOrDefault(x => x.Name == input.ConnectionName);
            if (database == null)
            {
                throw new ArgumentException("Invalid ConnectionName");
            }

            using DbConnection connection = database.CreateConnection();
            
            try
            {
                _logger.LogDebug("Opening connection...");
                await connection.OpenAsync(token);
            }
            catch (Exception e)
            {
                throw new SqlConnectionException(e);
            }

            if (input.QueryFileToken != null || input.QueryFileName != null)
            {
                using (var fileStore = _fileStoreFactory.GetFileStoreFromFileToken(input.QueryFileToken))
                {
                    if (input.QueryFileName != null)
                    {
                        input.QueryFileToken = await fileStore.CreateFileToken(input.QueryFileName);
                    }
                
                    using(Stream stream = await fileStore.OpenRead(input.QueryFileToken))
                    using(StreamReader reader = new StreamReader(stream))
                    {
                        input.Query = reader.ReadToEnd();
                    }
                }
                
            }
            
            _logger.LogDebug("Creating command...");
            using var cmd = connection.CreateCommand(input);
            
            
            var output = new SqlQueryOutput();

            try
            {
                switch (input.Type)
                {
                    case SqlQueryType.Scalar:
                        _logger.LogDebug("Execute scalar...");
                        output.Scalar = await cmd.ExecuteScalarAsync(token);
                        break;
                    case SqlQueryType.NonQuery:
                        _logger.LogDebug("Execute non query...");
                        await cmd.ExecuteNonQueryAsync(token);
                        break;
                    case SqlQueryType.Reader:
                        _logger.LogDebug("Execute Reader...");
                        using (var reader = await cmd.ExecuteReaderAsync(token))
                        {
                            output.Result = new List<Dictionary<string, object>>();

                            while(reader.Read())
                            {
                                Dictionary<string,object> line = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    line.Add(reader.GetName(i), reader.GetValue(i));
                                }
                                output.Result.Add(line);

                                if (output.Result.Count >= input.MaxResults)
                                    break;
                            }
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,"Error while executing query");
                throw new SqlQueryException(e);
            }

            return output;
        }
    }
}